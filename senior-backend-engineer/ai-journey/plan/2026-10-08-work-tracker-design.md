# WorkTracker — Design Spec

Date: 2026-10-08
Status: Approved by the candidate after a written review (see `../decisions.md` for the raw log).

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
├── requests.http                              all calls, error cases, manual UAT checklist
├── src/
│   ├── WorkTracker.Host/                      Program.cs, exception → ProblemDetails, Idempotency/
│   ├── WorkTracker.Shared/                    the 3 exception types only
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
| Host | Users, WorkItems, Shared, FastEndpoints, FastEndpoints.Swagger |
| Shared | — |
| Users | Users.Contracts, Shared, FastEndpoints |
| Users.Contracts | — |
| WorkItems | **Users.Contracts only** (never Users), Shared, FastEndpoints |

### 2.2 Boundary rules

- The boundary **between modules** is the project-reference graph: WorkItems has no reference to
  Users, so it cannot touch Users' types at all.
- Everything inside a module project is `internal` (the C# default — we simply never write `public`).
  This guards the projects that *do* reference a module (the Host and tests): `Program.cs` cannot
  construct a repository or an aggregate directly. A module's public surface is exactly its
  `Add<Module>Module(this IServiceCollection)` extension and, for Users, its Contracts project.
- Modules never share domain types. Only primitives (`Guid`, `string`) cross a boundary.
- Each module owns its own data store; no module can see another's.
- `Shared` holds exception types only — never business concepts.
- Each module declares `InternalsVisibleTo` for its own test project.

## 3. Domain model

Value objects exist **only where a rule lives**. IDs are plain `Guid` (a strongly-typed ID per
aggregate was judged ceremony for two aggregates). Aggregates have private constructors and factory
methods, so they are never in an invalid state.

### 3.1 Users

- `Username` — value object. Trimmed; required; 3–32 characters; only letters, digits, `.`, `_`, `-`.
  Equality is case-insensitive. Violations throw `DomainException` with a specific message
  (e.g. `"Username is required."`, `"Username must be between 3 and 32 characters."`).
- `User` — aggregate root. Private constructor; `User.Create(Username)`; exposes `Guid Id`,
  `Username Username`. No setters, no mutating methods.
- `IUserRepository` (in `Domain/`):
  - `Task<bool> TryAddAsync(User user, CancellationToken ct)` — atomic; returns `false` if the
    username (case-insensitive) is already taken.
  - `Task<User?> GetByIdAsync(Guid id, CancellationToken ct)`
  - `Task<bool> ExistsAsync(Guid id, CancellationToken ct)`

### 3.2 WorkItems

- `WorkItemName` — value object. Trimmed; required; 1–200 characters. Violations throw `DomainException`.
- `WorkItem` — aggregate root. `WorkItem.Create(WorkItemName name, Guid assigneeId)`; throws
  `DomainException("Assignee id is required.")` for `Guid.Empty`. Exposes `Guid Id`,
  `WorkItemName Name`, `Guid AssigneeId`. No status, due date, etc. — not asked for.
- `IWorkItemRepository` (in `Domain/`):
  - `Task AddAsync(WorkItem item, CancellationToken ct)`
  - `Task<IReadOnlyList<WorkItem>> GetByAssigneeAsync(Guid assigneeId, CancellationToken ct)`

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

### 3.4 Persistence portability (documented, not built)

The domain model does not change when the store changes; only `Infrastructure/` does.

- **SQL via EF Core:** one `DbContext` and one schema per module (`users.Users`,
  `workitems.WorkItems`). `Username` maps to a single `nvarchar(32)` column with
  `HasConversion(u => u.Value, s => new Username(s))` and a unique, case-insensitive index;
  `TryAddAsync` catches the unique-violation `DbUpdateException` and returns `false`. EF Core handles
  the private constructor and private setters. **No foreign key** from `WorkItems.AssigneeId` to
  `Users.Id` — that would couple the modules at the database level.
- **IDs:** `Guid` suits in-memory storage (generated app-side, no round-trip). With SQL the candidate
  would prefer `int`/`long` identity (or sequential UUIDv7); the repository would then return the
  generated ID.
- **Document store:** one document per aggregate; the value object serialises as a plain field.

## 4. API

| Endpoint | Success | Errors |
|---|---|---|
| `POST /users` `{ "username" }` | 201 `{ id, username }` + `Location: /users/{id}` | 400 invalid, 409 username taken |
| `GET /users/{id}` | 200 `{ id, username }` | 404 not found |
| `POST /work-items` `{ "name", "assigneeId" }` | 201 `{ id, name, assigneeId }` | 400 invalid, 422 unknown assignee |
| `GET /work-items?assigneeId={id}` | 200 `[ { id, name, assigneeId } ]` (empty if none) | 400 missing/empty id |

- Both POSTs require an `Idempotency-Key` header (§8).
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
             ├─ usersApi.UserExistsAsync(...)         → false: BusinessRuleViolationException (422)
             ├─ WorkItem.Create(name, cmd.AssigneeId) → DomainException if empty (400)
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
  | Value objects / aggregate factory | single-object invariants (required, length, characters, empty assignee) | 400 |
  | Handlers | rules needing data (username taken, assignee exists) | 409 / 422 |

  No FastEndpoints validators — that would duplicate the domain rules.

## 6. Error handling

**Exceptions for rule violations, `null` for not-found.** Each exception is named for the kind of
failure, not the layer that handles it. `Shared` defines:

| Exception | Thrown from | Meaning | HTTP |
|---|---|---|---|
| `DomainException` | `Domain/` | an invariant of a single object is broken | 400 |
| `ConflictException` | handlers | the thing already exists (username taken) | 409 |
| `BusinessRuleViolationException` | handlers | a rule involving other data fails (assignee unknown) | 422 |

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

- **xUnit + Shouldly 4.3.0** (BSD-3-Clause). FluentAssertions was avoided because it went commercial;
  AwesomeAssertions was considered but keeps the `FluentAssertions` namespace, which misleads readers.
- **Test plan first.** `ai-journey/plan/test-plan.md` lists numbered cases (`TC-U01`, …) in
  Given/When/Then form. The candidate approves it before implementation. Test method names carry the
  case ID so plan → test is traceable.
- **TDD** during implementation: write the test, see it fail, make it pass.
- **Manual UAT** as the final gate: a checklist at the end of `requests.http` that the candidate runs
  against the live service.

| Project | Covers | Demonstrates |
|---|---|---|
| `Users.Tests` | `Username` valid/invalid cases; `CreateUserHandler` throws `ConflictException` on duplicate (real in-memory repo) | invariants in the domain; handlers testable without mocking frameworks |
| `WorkItems.Tests` | `WorkItemName` rules; `WorkItem.Create` rejects empty assignee; `CreateWorkItemHandler` with a stub `IUsersApi` returning `false` → `BusinessRuleViolationException` | the contract lets WorkItems be tested without the Users module |
| `Api.Tests` | `WebApplicationFactory<Program>`: happy path for all 4 endpoints; 409, 422, 404 over HTTP; idempotent replay; different-body 422; `IdempotencyStore` expiry with `FakeTimeProvider` | the system end to end over real HTTP |

Handlers are tested by instantiating them directly with their dependencies (no command bus needed).

## 10. Deliverables

- `senior-backend-engineer/README.md` — about half a page of design (module boundaries, command/query
  flow, trade-offs: folder-level layering, FastEndpoints coupling, atomic uniqueness, idempotency
  window vs Redis, update concurrency, `Guid` vs `int` IDs, EF Core mapping per §3.4, eventual
  consistency between modules via `UserCreated` events as the "more time" direction), followed by
  build/run/test instructions and how to call the endpoints.
- `senior-backend-engineer/requests.http` — every call, the error cases, and the manual UAT checklist.
- `senior-backend-engineer/ai-journey/`:
  - `plan/` — this spec, the test plan and the implementation plan, as produced.
  - `decisions.md` — raw chronological log: the candidate's messages verbatim, what the AI proposed,
    what was chosen.
  - `transcript.md` — the full session, exported with Claude Code's `/export` by the candidate.
  - `prompts.md` — curated key prompts.
  - `toolchain.md` — Claude Code; Opus 5.5 for brainstorming and the first spec draft, Fable 5.1 for
    the written spec review, then Opus 5.5 again (the candidate switched models via `/model`); superpowers skills (brainstorming, writing-plans,
    test-driven-development); context7 MCP for FastEndpoints docs; NuGet API and web search for the
    assertion-library check.
  - `judgment.md` — where the candidate overrode, corrected or pushed back on the AI, extracted from
    `decisions.md` and extended during code review.
- Small commits per step on branch `feature/backend-take-home`.

## 11. Decision log

See `../decisions.md` — kept raw on purpose, in the candidate's own words.
