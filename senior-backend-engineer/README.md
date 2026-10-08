# WorkTracker

A .NET 8 modular monolith with two modules, **Users** and **WorkItems**, built with FastEndpoints, CQRS and DDD. Data is kept in memory.

## Design

```
src/
  WorkTracker.Host/          composition root, error handling, idempotency
  WorkTracker.Shared/        the three exception types the host maps to HTTP status codes
  Modules/Users/             WorkTracker.Users + WorkTracker.Users.Contracts (IUsersApi)
  Modules/WorkItems/         WorkTracker.WorkItems
tests/                       one project per module, plus WorkTracker.Api.Tests over HTTP
```

**Modules.** Each module is one project with vertical slices inside: `Domain/` for the model, `Features/<UseCase>/` for the endpoint, command or query and handler, and `Infrastructure/` for the in-memory repository. Everything in a module is `internal`; its only public surface is an `Add<Module>Module()` method. WorkItems never references Users: it checks that an assignee exists through `IUsersApi` in `WorkTracker.Users.Contracts`, so only primitives cross the boundary.

**CQRS.** An endpoint maps the request to a command or query and runs it on the FastEndpoints command bus. `*Command` handlers change state, `*Query` handlers only read, and both return DTOs, never domain objects.

**Domain.** `Username` and `WorkItemName` are value objects because they hold rules (length, characters, trimming). `User` and `WorkItem` have private constructors and `Create` factories, so they can't exist in an invalid state. A broken rule throws `DomainException` (400), a taken username `ConflictException` (409), an unknown assignee `BusinessRuleViolationException` (422). One exception handler turns them into RFC 9457 problem details with a specific message. A request that can't be read (malformed JSON, an id that isn't a GUID) gets the same shape with a plain message, not the parser's text. Only unexpected errors (500) are logged; expected 4xx are normal traffic.

**Trade-offs**
- Layers are folders, not projects: one use case lives in one folder. The compiler doesn't stop a handler from reaching into `Infrastructure/`; code review does.
- Handlers implement FastEndpoints' `ICommandHandler` instead of MediatR, which is now commercial. The cost is that handlers depend on FastEndpoints.
- Username uniqueness is atomic (`ConcurrentDictionary.TryAdd`). With SQL it would be a unique index plus catching the violation.
- Every POST requires an `Idempotency-Key` (a global pre-processor in `Program.cs`), remembered for 10 seconds (configurable) in process memory. The same key and body replays the stored response with `Idempotent-Replayed: true`; a different body gets 422; a request still running gets 409; a failed request frees its key. That stops double clicks, not slow retries, and doesn't work across instances. Production would use Redis `SET NX EX` and a longer window.
- There are no update use cases, so no optimistic concurrency yet. It would be a `Version` per aggregate, exposed as an ETag with `If-Match` and 412.
- IDs are `Guid` because the app generates them. With SQL I'd use `int`/`long` identity columns or UUIDv7. EF Core would map each value object to one column with `HasConversion`, use one schema per module, and have no foreign key between modules.

**With more time:** replace the synchronous `IUsersApi` check with eventual consistency. Users publishes `UserCreated`, and WorkItems keeps its own list of known assignees.

## Build, run, test

Requires the .NET 8 SDK (pinned in `global.json`).

```bash
cd senior-backend-engineer
dotnet build
dotnet test
dotnet run --project src/WorkTracker.Host   # http://localhost:5080, Swagger UI at /swagger
```

## Calling the endpoints

`requests.http` has every call, every error case and the manual UAT checklist (VS Code REST Client, Rider or Visual Studio). With curl:

```bash
curl -i -X POST http://localhost:5080/users -H "Content-Type: application/json" -H "Idempotency-Key: demo-1" -d '{"username":"alice"}'
curl -i http://localhost:5080/users/<user-id>
curl -i -X POST http://localhost:5080/work-items -H "Content-Type: application/json" -H "Idempotency-Key: demo-2" -d '{"name":"Write README","assigneeId":"<user-id>"}'
curl -i "http://localhost:5080/work-items?assigneeId=<user-id>"
```

How this was built with AI (spec, test plan, implementation plan, decision log, transcript) is in [`ai-journey/`](ai-journey/).
