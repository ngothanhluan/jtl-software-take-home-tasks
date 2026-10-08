# WorkTracker — Design Spec

Date: 2026-10-08
Status: Approved in conversation section by section; awaiting final review of this document.

## 1. Goal

A small .NET 8 service, organized as a **modular monolith**, using **FastEndpoints**, **CQRS** and
**DDD**, with two modules:

- **Users** — create a user by username; get a user by ID.
- **WorkItems** — create a work item with a name and an assignee (user ID); list work items by assignee.

Evaluation focus (from the brief): module boundaries, a deliberate domain model, a clean
command/query split, thin endpoints, testability, clarity, and the AI journey. Time box 2–4 hours;
no gold-plating.

**Guiding principle (set by the candidate):** simple, easy to read, easy to maintain. Build what the
brief asks for; document — not build — what it does not, unless deliberately chosen (see §8).

## 2. Architecture

**Vertical slices inside each module, with the domain model in its own folder.** Chosen over Clean
Architecture project-per-layer, which would mean ~8 projects for 4 endpoints and reading one use case
across 4 projects.

### 2.1 Projects

```
senior-backend-engineer/
├── WorkTracker.sln
├── requests.http
├── src/
│   ├── WorkTracker.Host/                      Program.cs, exception → ProblemDetails, Idempotency/
│   ├── WorkTracker.SharedKernel/              the 3 exception types only
│   └── Modules/
│       ├── Users/
│       │   ├── WorkTracker.Users/             Domain/, Features/, Infrastructure/, UsersModule.cs
│       │   └── WorkTracker.Users.Contracts/   IUsersApi
│       └── WorkItems/
│           └── WorkTracker.WorkItems/         Domain/, Features/, Infrastructure/, WorkItemsModule.cs
└── tests/
    ├── WorkTracker.Users.Tests/
    ├── WorkTracker.WorkItems.Tests/
    └── WorkTracker.Api.Tests/
```

Names are prefixed `WorkTracker.` partly for clarity and partly because FastEndpoints excludes some
assembly names from endpoint scanning.

| Project | References |
|---|---|
| Host | Users, WorkItems, SharedKernel, FastEndpoints, FastEndpoints.Swagger |
| SharedKernel | — |
| Users | Users.Contracts, SharedKernel, FastEndpoints |
| Users.Contracts | — |
| WorkItems | **Users.Contracts only** (never Users), SharedKernel, FastEndpoints |

### 2.2 Boundary rules

- Everything inside a module project is `internal`. A module's public surface is exactly its
  `Add<Module>Module(this IServiceCollection)` extension and, for Users, its Contracts project.
- Modules never share domain types. Only primitives (`Guid`, `string`) cross a boundary.
- Each module owns its own data store; no module can see another's.
- `SharedKernel` holds exception types only — never business concepts.
- Test projects get access to internals via `InternalsVisibleTo`, declared by each module.

## 3. Domain model

### 3.1 Users

- `UserId` — value object wrapping `Guid`; `UserId.New()`.
- `Username` — value object. Trimmed; required; 3–32 characters; only letters, digits, `.`, `_`, `-`.
  Equality is case-insensitive. Violations throw `DomainException` with a specific message
  (e.g. `"Username is required."`, `"Username must be between 3 and 32 characters."`).
- `User` — aggregate root. Private constructor; `User.Create(Username)`; exposes `Id`, `Username`.
  No setters, no mutating methods.
- `IUserRepository` (in `Domain/`):
  - `Task<bool> TryAddAsync(User user, CancellationToken ct)` — atomic; returns `false` if the
    username (case-insensitive) is already taken.
  - `Task<User?> GetByIdAsync(UserId id, CancellationToken ct)`
  - `Task<bool> ExistsAsync(UserId id, CancellationToken ct)`

### 3.2 WorkItems

- `WorkItemId` — value object wrapping `Guid`.
- `WorkItemName` — value object. Trimmed; required; 1–200 characters. Violations throw `DomainException`.
- `AssigneeId` — WorkItems' **own** value object wrapping `Guid`; rejects `Guid.Empty`
  (`"Assignee id is required."`). Deliberately not Users' `UserId`.
- `WorkItem` — aggregate root. `WorkItem.Create(WorkItemName, AssigneeId)`; exposes `Id`, `Name`,
  `AssigneeId`. No status, due date, etc. — not asked for.
- `IWorkItemRepository` (in `Domain/`):
  - `Task AddAsync(WorkItem item, CancellationToken ct)`
  - `Task<IReadOnlyList<WorkItem>> GetByAssigneeAsync(AssigneeId assigneeId, CancellationToken ct)`

### 3.3 Cross-module contract

`WorkTracker.Users.Contracts`:

```csharp
public interface IUsersApi
{
    Task<bool> UserExistsAsync(Guid userId, CancellationToken ct);
}
```

Implemented by an internal `UsersApi` class in the Users module (backed by `IUserRepository`) and
registered in `AddUsersModule()`. WorkItems validates assignees **synchronously** through it.

## 4. API

| Endpoint | Success | Errors |
|---|---|---|
| `POST /users` `{ "username" }` | 201 `{ id, username }` + `Location: /users/{id}` | 400 invalid, 409 username taken |
| `GET /users/{id}` | 200 `{ id, username }` | 404 not found |
| `POST /work-items` `{ "name", "assigneeId" }` | 201 `{ id, name, assigneeId }` | 400 invalid, 422 unknown assignee |
| `GET /work-items?assigneeId={id}` | 200 `[ { id, name, assigneeId } ]` (empty if none) | 400 missing/empty id |

- Both POSTs require an `Idempotency-Key` header (§6).
- `POST /work-items` returns 201 without a `Location` header: there is no get-single-work-item
  endpoint and we do not add one.
- `GET /work-items` does **not** check that the user exists — an unknown assignee returns `[]`.
  Reads never call another module.
- Each module owns its URL space (`/users`, `/work-items`).
- Swagger UI via `FastEndpoints.Swagger`, plus `requests.http` with every call and error case.

## 5. Request flow (CQRS)

**CQRS library: FastEndpoints' built-in command bus.** Both commands and queries implement
`ICommand<TResult>`; the split is expressed by naming (`*Command` / `*Query`), by the rule that query
handlers never change state, and by commands and queries never sharing a handler. MediatR was rejected
because it went commercial; the cost accepted is that handlers depend on FastEndpoints (which the
modules already reference for endpoints).

One feature folder per use case, e.g. `Features/CreateWorkItem/`:
`CreateWorkItemEndpoint.cs`, `CreateWorkItemCommand.cs`, `CreateWorkItemHandler.cs`,
`CreateWorkItemRequest.cs`. Response DTOs (`UserResponse`, `WorkItemResponse`) are shared within
their module.

```
HTTP POST /work-items
 └─ CreateWorkItemEndpoint            maps Request → Command (one line), no logic
     └─ command.ExecuteAsync(ct)       FastEndpoints command bus
         └─ CreateWorkItemHandler
             ├─ new WorkItemName(cmd.Name)            → DomainException (400)
             ├─ new AssigneeId(cmd.AssigneeId)        → DomainException (400)
             ├─ usersApi.UserExistsAsync(...)         → false: BusinessRuleViolationException (422)
             ├─ WorkItem.Create(name, assigneeId)
             ├─ repository.AddAsync(workItem)
             └─ return WorkItemResponse (DTO)
     └─ Send.ResponseAsync(response, 201)
```

Rules:

- **Endpoints** map request → command/query, execute, send. Nothing else.
- **Handlers** orchestrate: build value objects, call the aggregate, persist. They return **DTOs,
  never domain objects**.
- **Query handlers** only read and map; they never mutate state.
- **Validation** lives in exactly one place per rule:

  | Layer | Checks | Result |
  |---|---|---|
  | FastEndpoints binding | malformed JSON, non-GUID IDs | 400 |
  | Value objects | single-value invariants (required, length, characters, empty GUID) | 400 |
  | Handlers | rules needing data (username taken, assignee exists) | 409 / 422 |

  No FastEndpoints validators — that would duplicate the value-object rules.

## 6. Error handling

**Exceptions for rule violations, `null` for not-found.** `SharedKernel` defines:

| Exception | HTTP |
|---|---|
| `DomainException` | 400 |
| `ConflictException` | 409 |
| `BusinessRuleViolationException` | 422 |

- Every exception carries a specific human-readable message written where the rule lives
  (e.g. `"Username 'alice' already exists."`, `"User '3f2a…' does not exist."`).
- One `IExceptionHandler` in the Host maps these to **ProblemDetails** (RFC 9457), with the message in
  `detail`. Unknown exceptions → generic 500 with no internals leaked.
- Query handlers return `null` for not-found; the endpoint sends 404 with
  `"User '{id}' was not found."`.
- FastEndpoints is configured with `Errors.UseProblemDetails()` so its own binding errors use the same
  format.

## 7. Concurrency

- **Username uniqueness** is enforced atomically: `InMemoryUserRepository` keeps a second
  `ConcurrentDictionary` keyed by lower-cased username; `TryAdd` succeeds for exactly one caller. The
  handler throws `ConflictException` when `TryAddAsync` returns `false`. There is no separate
  check-then-add. Production equivalent: a unique index plus catching the violation.
- **Work items** have no uniqueness rule; a concurrent add is already safe.
- **Aggregates are immutable after creation**, so readers never observe partial state.
- **Update concurrency is documented, not built** (no update use cases exist): `Version` on
  aggregates, compare-and-swap in the repository, 409 on mismatch; over HTTP, ETag + `If-Match` → 412.

## 8. Idempotency (deliberate addition)

Protects both POST endpoints against double submission (e.g. a double click). The client sends an
`Idempotency-Key` header and **reuses the same key when retrying**. The server remembers each key
for a configurable window (`Idempotency:WindowSeconds`, default **10**).

| Situation | Response |
|---|---|
| Header missing or empty | 400 `"Idempotency-Key header is required."` |
| First request with key | processed; the 2xx response (status, body, `Location`) is stored |
| Same key + same request within window | stored response replayed, header `Idempotent-Replayed: true`; nothing created |
| Same key, first request still in flight | 409 `"A request with this Idempotency-Key is already being processed."` |
| Same key, different endpoint or body | 422 `"Idempotency-Key was already used with a different request."` |
| First request failed (4xx/5xx) | key released; the client may retry with the same key |
| After the window | key forgotten; processed as new |

Design:

- Lives entirely in `WorkTracker.Host/Idempotency/` — an HTTP concern; the modules and domain do not
  know it exists.
- `IdempotencyPreProcessor` (`IGlobalPreProcessor`) and `IdempotencyPostProcessor`
  (`IGlobalPostProcessor`), attached via `c.Endpoints.Configurator` to POST endpoints only.
- The request fingerprint is the endpoint route plus a SHA-256 hash of the bound request DTO
  serialized as JSON.
- `IdempotencyStore` (singleton) — `ConcurrentDictionary` keyed by the idempotency key.
  **Reservation is atomic** (`TryAdd`, or `TryUpdate` over an expired entry). Each reservation also
  purges expired entries, so memory is bounded by the keys seen within the window. Time comes from an
  injected `TimeProvider`.
- On a handler exception the post-processor releases the key and lets the exception propagate to the
  ProblemDetails handler.

Known trade-off: a 10s window covers double clicks, not slow automatic retries (for example a client
that times out at 30s and retries with the same key gets a duplicate). The window is configurable;
payment APIs such as Stripe keep keys for 24h. Production equivalent: Redis `SET key NX EX <window>`,
which also works across multiple instances.

## 9. Testing

xUnit with plain `Assert` (FluentAssertions avoided — commercial license since v8).

| Project | Tests | Demonstrates |
|---|---|---|
| `Users.Tests` | `Username` valid/invalid cases (theory); `CreateUserHandler` throws `ConflictException` on duplicate (real in-memory repo) | invariants in the domain; handlers testable without mocking frameworks |
| `WorkItems.Tests` | `WorkItemName` / `AssigneeId` rules; `CreateWorkItemHandler` with a stub `IUsersApi` returning `false` → `BusinessRuleViolationException` | the contract lets WorkItems be tested without the Users module |
| `Api.Tests` | `WebApplicationFactory<Program>`: happy path for all 4 endpoints; 409, 422, 404 over HTTP; idempotent replay; different-body 422; `IdempotencyStore` expiry with `FakeTimeProvider` | the system end to end over real HTTP |

Handlers are tested by instantiating them directly with their dependencies (no command bus needed).

## 10. Deliverables

- `senior-backend-engineer/README.md` — about half a page of design (module boundaries, command/query
  flow, trade-offs: folder-level layering, FastEndpoints coupling, atomic uniqueness, idempotency
  window vs Redis, update concurrency, eventual consistency between modules via `UserCreated` events
  as the "more time" direction), followed by build/run/test instructions and how to call the endpoints.
- `senior-backend-engineer/requests.http`.
- `senior-backend-engineer/ai-journey/`:
  - `plan/` — this spec and the implementation plan, as produced.
  - `prompts.md` — curated key prompts from the session.
  - `toolchain.md` — Claude Code (Opus 5.5); superpowers skills (brainstorming, writing-plans,
    test-driven-development); context7 MCP for FastEndpoints docs.
  - `judgment.md` — extracted from the conversation and extended during code review.
- Small commits per step on branch `feature/backend-take-home`.

## 11. Decision log

| # | Decision | Who | Notes |
|---|---|---|---|
| 1 | Assignee must exist; checked synchronously via `Users.Contracts` | Candidate (agreed with AI's recommendation) | Eventual consistency documented as the evolution path |
| 2 | Vertical slices + `Domain/` folder, not Clean Architecture | Candidate asked; AI recommended | "Simple is the best, easy to read, easy to maintain" |
| 3 | FastEndpoints command bus for CQRS | **Candidate overrode** AI's hand-rolled-interfaces recommendation | MediatR commercial; CQRS is about how it's used, not interface names |
| 4 | In-memory repositories | Candidate | |
| 5 | Exceptions + central ProblemDetails mapping; `null` for not-found | Candidate (agreed) | |
| 6 | Error responses carry specific messages | **Candidate pushed back** to make this explicit | e.g. "Username 'alice' already exists." |
| 7 | Atomic username uniqueness | **Candidate raised** concurrency | AI's design had a check-then-add race |
| 8 | Update concurrency: document, don't build | Candidate | Not in requirements |
| 9 | Idempotency-Key required on POSTs, 10s window | **Candidate overrode** AI's "document only" recommendation and designed the key + window combination | AI first read the window as content-based dedupe and argued against it (race, rule in the wrong layer); candidate clarified it was a client key with a short server-side window, which avoids both problems |
| 10 | Retries reuse the key | **Candidate corrected** AI's wording | |
| 11 | Expired-key purge on reserve | AI self-correction | Earlier claim that lazy replacement bounded memory was wrong |
