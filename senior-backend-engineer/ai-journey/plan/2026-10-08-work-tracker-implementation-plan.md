# WorkTracker implementation plan

Ten tasks, built test-first, from an empty folder to a reviewed branch. Each task names its files, the exact code, the command that should fail, and the command that should then pass.

- **Date:** 2026-10-08
- **Status:** Approved (Q22)
- **Spec:** [design spec](2026-10-08-work-tracker-design.html)
- **Tests:** [test plan](test-plan.html)

## Contents

- [Overview](#overview)
- [Global constraints](#constraints)
- [Where the plan refines the spec](#deviations)
- [Review focus](#review-focus)
- [File map](#files)
- [Task 1: Solution skeleton, Shared exceptions and the Username value object](#task-1)
- [Task 2: User aggregate, repository, Users contract and handlers](#task-2)
- [Task 3: WorkItems module: domain, repository and handlers](#task-3)
- [Task 4: Host, problem details and the Users endpoints](#task-4)
- [Task 5: WorkItems endpoints](#task-5)
- [Task 6: IdempotencyStore](#task-6)
- [Task 7: Idempotency pre/post processors on every POST](#task-7)
- [Task 8: requests.http and README](#task-8)
- [Task 9: AI-journey pages: prompts, toolchain, judgment](#task-9)
- [Task 10: Final review and manual UAT (the human gate)](#task-10)

<a id="overview"></a>

## Overview

> **For agentic workers**
>
> REQUIRED SUB-SKILL: use superpowers:subagent-driven-development or superpowers:executing-plans to carry this out task by task. Each step is a checkbox; tick it in your notes as you go. Read the [design spec](2026-10-08-work-tracker-design.html) and the [test plan](test-plan.html) alongside this plan.

- **Goal:** A .NET 8 modular monolith with Users (create, get by id) and WorkItems (create, list by assignee), meeting every case in the approved test plan.
- **Architecture:** One project per module with vertical slices (`Domain/`, `Features/<UseCase>/`, `Infrastructure/`). WorkItems reaches Users only through `WorkTracker.Users.Contracts`. A thin Host wires the modules, problem details and POST idempotency.
- **Tech stack:** .NET 8, FastEndpoints 8.3.0 (endpoints and command bus), FastEndpoints.Swagger, xUnit 2.9.3, Shouldly 4.3.0, Microsoft.AspNetCore.Mvc.Testing 8.0.31, Microsoft.Extensions.TimeProvider.Testing 10.10.0.
- **Spec:** [2026-10-08-work-tracker-design.html](2026-10-08-work-tracker-design.html) and [test-plan.html](test-plan.html) (approved, Q21).
- **Working dir:** Every command runs from `senior-backend-engineer/`. Paths in this plan are relative to it.

<a id="constraints"></a>

## Global constraints

- Target `net8.0`; SDK pinned by `global.json` to `8.0.425` with `rollForward: latestFeature`.
- Exact package versions: FastEndpoints 8.3.0, FastEndpoints.Swagger 8.3.0, xunit 2.9.3, xunit.runner.visualstudio 3.1.5, Microsoft.NET.Test.Sdk 18.10.1, Shouldly 4.3.0, Microsoft.AspNetCore.Mvc.Testing 8.0.31, Microsoft.Extensions.TimeProvider.Testing 10.10.0.
- Not allowed: MediatR, FluentAssertions, mocking frameworks, FastEndpoints validators, auth, logging setup, containers, CI.
- Every project name starts with `WorkTracker.`. Module types are `internal`; the public surface is `Add<Module>Module()` and `WorkTracker.Users.Contracts`.
- `WorkTracker.WorkItems` references `WorkTracker.Users.Contracts`, never `WorkTracker.Users`.
- Every endpoint calls `AllowAnonymous()`. Endpoints only map, execute and send.
- Error messages are exactly those in [test plan §02](test-plan.html#messages).
- Test method names start with their case ID: `TC_U01_…`.
- Commits: one per task, conventional prefix, explicit paths only, delete any `.omc/` folder first, end with the `Co-Authored-By` trailer. Never push to or open a PR against the JTL template repo.

<a id="deviations"></a>

## Where the plan refines the spec

Small changes found while writing real code. None changes behaviour the test plan promises.

| Spec says | Plan does | Why |
|---|---|---|
| Handler checks the assignee exists, then calls `WorkItem.Create` | `WorkItem.Create` first, then the existence check | An empty assignee must be a 400, not a 422 "does not exist". It also saves a pointless cross-module call. |
| A second dictionary keyed by the lower-cased username | A dictionary keyed by `Username` itself | `Username` equality already ignores case, so the rule lives in one place. |
| Missing `assigneeId` on the list endpoint is a 400 | The list query handler throws `DomainException("Assignee id is required.")` for an empty GUID | A missing query value binds to `Guid.Empty` without error, so something has to check it. No FastEndpoints validator, per the spec. |
| Fingerprint = route + SHA-256 of the request DTO | Request path + SHA-256 of the DTO serialized as JSON | Same idea; the path is what the pre-processor has to hand. |

<a id="review-focus"></a>

## Review focus

Inputs the spec implies but the approved cases don't cover, most likely to bite first. Each now has a test in the task that owns the code.

| # | Input or condition | Expected | Pinned in |
|---|---|---|---|
| 1 | `username` missing or `null` in the JSON body | 400 "Username is required.", not a 500 | Task 1, TC-U02 `null` row |
| 2 | Malformed JSON body | 400 problem details, never a 500 or stack trace | Task 4, second TC-A04 test |
| 3 | Non-ASCII letters, e.g. `José` | 400 character rule (ASCII on purpose, prevents look-alike usernames) | Task 1, TC-U02 `José` row |
| 4 | `POST /work-items` with no `assigneeId` | 400 "Assignee id is required.", not 422 | Task 3, second TC-W03 test |
| 5 | `GET /work-items?assigneeId=00000000-…` | 400, not an empty list | Task 5, TC-A09 third row |

<a id="files"></a>

## File map

```
senior-backend-engineer/
├── global.json · Directory.Build.props · WorkTracker.sln · README.md · requests.http
├── src/
│   ├── WorkTracker.Shared/            DomainException, ConflictException, BusinessRuleViolationException   (T1)
│   ├── WorkTracker.Host/              Program, ProblemDetailsExceptionHandler (T4) · Idempotency/ (T6, T7)
│   └── Modules/
│       ├── Users/
│       │   ├── WorkTracker.Users.Contracts/   IUsersApi                                                 (T2)
│       │   └── WorkTracker.Users/
│       │       ├── Domain/            Username (T1) · User, IUserRepository (T2)
│       │       ├── Infrastructure/    InMemoryUserRepository                                            (T2)
│       │       ├── Features/          UserResponse · CreateUser/ · GetUser/        handlers (T2), endpoints (T4)
│       │       └── UsersApi, UsersModule                                                                (T2)
│       └── WorkItems/
│           └── WorkTracker.WorkItems/
│               ├── Domain/            WorkItemName, WorkItem, IWorkItemRepository                       (T3)
│               ├── Infrastructure/    InMemoryWorkItemRepository                                        (T3)
│               ├── Features/          WorkItemResponse · CreateWorkItem/ · ListWorkItemsByAssignee/   (T3, T5)
│               └── WorkItemsModule                                                                      (T3)
└── tests/
    ├── Directory.Build.props          shared test packages                                              (T1)
    ├── WorkTracker.Users.Tests/       TC-U01…U05                                                        (T1, T2)
    ├── WorkTracker.WorkItems.Tests/   TC-W01…W06                                                        (T3)
    └── WorkTracker.Api.Tests/         TC-A01…A09 (T4, T5) · TC-I01…I06 (T6, T7)
```

Test totals as the tasks land: 11 → 14 → 24 → 30 → 36 → 39 → 46.

<a id="task-1"></a>

## Task 1: Solution skeleton, Shared exceptions and the Username value object

Creates the solution with only the projects this task needs, then builds `Username` test-first.

- **Test cases:** TC-U01, TC-U02
- **Consumes:** nothing
- **Produces:** `WorkTracker.Shared.{DomainException, ConflictException, BusinessRuleViolationException}(string message)`; `internal sealed record WorkTracker.Users.Domain.Username(string? value)` with `string Value`, case-insensitive equality

#### Files

- Create: `global.json`, `Directory.Build.props`, `tests/Directory.Build.props`, `WorkTracker.sln`
- Create: `src/WorkTracker.Shared/` (`WorkTracker.Shared.csproj`, `DomainException.cs`, `ConflictException.cs`, `BusinessRuleViolationException.cs`)
- Create: `src/Modules/Users/WorkTracker.Users/WorkTracker.Users.csproj`, `Domain/Username.cs`
- Test: `tests/WorkTracker.Users.Tests/WorkTracker.Users.Tests.csproj`, `Domain/UsernameTests.cs`

#### Steps

- [ ] **Step 1: Pin the SDK and shared build settings**

  Both the .NET 8 and .NET 10 SDKs are installed. `global.json` pins 8 so `dotnet new sln` makes a `.sln` and everything targets `net8.0`.

  `global.json`

  ```json
  {
    "sdk": { "version": "8.0.425", "rollForward": "latestFeature" }
  }
  ```

  `Directory.Build.props`

  ```xml
  <Project>
    <PropertyGroup>
      <TargetFramework>net8.0</TargetFramework>
      <Nullable>enable</Nullable>
      <ImplicitUsings>enable</ImplicitUsings>
      <LangVersion>12</LangVersion>
    </PropertyGroup>
  </Project>
  ```

  `tests/Directory.Build.props`

  ```xml
  <Project>
    <Import Project="$([MSBuild]::GetPathOfFileAbove('Directory.Build.props', '$(MSBuildThisFileDirectory)../'))" />

    <PropertyGroup>
      <IsPackable>false</IsPackable>
      <IsTestProject>true</IsTestProject>
    </PropertyGroup>

    <ItemGroup>
      <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.10.1" />
      <PackageReference Include="xunit" Version="2.9.3" />
      <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
      <PackageReference Include="Shouldly" Version="4.3.0" />
    </ItemGroup>

    <ItemGroup>
      <Using Include="Xunit" />
      <Using Include="Shouldly" />
    </ItemGroup>
  </Project>
  ```

- [ ] **Step 2: Create the Shared project and its three exceptions**

  `src/WorkTracker.Shared/WorkTracker.Shared.csproj`

  ```xml
  <Project Sdk="Microsoft.NET.Sdk" />
  ```

  `src/WorkTracker.Shared/DomainException.cs`

  ```csharp
  namespace WorkTracker.Shared;

  // A rule of a single object is broken (e.g. a username that is too short). Maps to 400.
  public sealed class DomainException(string message) : Exception(message);
  ```

  `src/WorkTracker.Shared/ConflictException.cs`

  ```csharp
  namespace WorkTracker.Shared;

  // The thing being created already exists (e.g. a taken username). Maps to 409.
  public sealed class ConflictException(string message) : Exception(message);
  ```

  `src/WorkTracker.Shared/BusinessRuleViolationException.cs`

  ```csharp
  namespace WorkTracker.Shared;

  // A rule that needs other data fails (e.g. the assignee does not exist). Maps to 422.
  public sealed class BusinessRuleViolationException(string message) : Exception(message);
  ```

- [ ] **Step 3: Create the Users module and its test project, and add them to a new solution**

  `src/Modules/Users/WorkTracker.Users/WorkTracker.Users.csproj`

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <ItemGroup>
      <FrameworkReference Include="Microsoft.AspNetCore.App" />
      <PackageReference Include="FastEndpoints" Version="8.3.0" />
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include="../../../WorkTracker.Shared/WorkTracker.Shared.csproj" />
    </ItemGroup>

    <ItemGroup>
      <InternalsVisibleTo Include="WorkTracker.Users.Tests" />
    </ItemGroup>
  </Project>
  ```

  `tests/WorkTracker.Users.Tests/WorkTracker.Users.Tests.csproj`

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <ItemGroup>
      <ProjectReference Include="../../src/Modules/Users/WorkTracker.Users/WorkTracker.Users.csproj" />
    </ItemGroup>
  </Project>
  ```

  ```bash
  dotnet new sln -n WorkTracker
  dotnet sln add src/WorkTracker.Shared/WorkTracker.Shared.csproj src/Modules/Users/WorkTracker.Users/WorkTracker.Users.csproj tests/WorkTracker.Users.Tests/WorkTracker.Users.Tests.csproj
  ```

  *Expected: three projects added to `WorkTracker.sln`.*

- [ ] **Step 4: Write the failing tests (TC-U01, TC-U02)**

  The `null` and `José` rows come from the Review Focus list.

  `tests/WorkTracker.Users.Tests/Domain/UsernameTests.cs`

  ```csharp
  using WorkTracker.Shared;
  using WorkTracker.Users.Domain;

  namespace WorkTracker.Users.Tests.Domain;

  public class UsernameTests
  {
      [Theory]
      [InlineData("  alice  ", "alice")]
      [InlineData("bob.smith", "bob.smith")]
      [InlineData("a_b-c", "a_b-c")]
      public void TC_U01_Valid_username_is_accepted_and_trimmed(string input, string expected)
      {
          var username = new Username(input);

          username.Value.ShouldBe(expected);
      }

      [Theory]
      [InlineData(null, "Username is required.")]
      [InlineData("", "Username is required.")]
      [InlineData("   ", "Username is required.")]
      [InlineData("ab", "Username must be between 3 and 32 characters.")]
      [InlineData("abcdefghijabcdefghijabcdefghijabc", "Username must be between 3 and 32 characters.")]
      [InlineData("alice smith", "Username can only contain letters, digits, '.', '_' and '-'.")]
      [InlineData("al!ce", "Username can only contain letters, digits, '.', '_' and '-'.")]
      [InlineData("José", "Username can only contain letters, digits, '.', '_' and '-'.")]
      public void TC_U02_Invalid_username_throws_DomainException_with_specific_message(string? input, string expectedMessage)
      {
          var exception = Should.Throw<DomainException>(() => new Username(input));

          exception.Message.ShouldBe(expectedMessage);
      }
  }
  ```

- [ ] **Step 5: Run the tests and watch them fail**

  ```bash
  dotnet test --filter "FullyQualifiedName~TC_U01|FullyQualifiedName~TC_U02"
  ```

  *Expected: build error `CS0246: The type or namespace name 'Username' could not be found`.*

- [ ] **Step 6: Implement Username**

  Equality ignores case, so the repository in Task 2 can use `Username` itself as the uniqueness key. Letters are ASCII only, on purpose: it rules out look-alike usernames.

  `src/Modules/Users/WorkTracker.Users/Domain/Username.cs`

  ```csharp
  using System.Text.RegularExpressions;
  using WorkTracker.Shared;

  namespace WorkTracker.Users.Domain;

  // 3-32 ASCII letters, digits, '.', '_' or '-', trimmed. Two usernames are equal ignoring case.
  internal sealed partial record Username
  {
      private const int MinLength = 3;
      private const int MaxLength = 32;

      public Username(string? value)
      {
          var trimmed = value?.Trim() ?? string.Empty;

          if (trimmed.Length == 0)
              throw new DomainException("Username is required.");
          if (trimmed.Length is < MinLength or > MaxLength)
              throw new DomainException($"Username must be between {MinLength} and {MaxLength} characters.");
          if (!AllowedCharacters().IsMatch(trimmed))
              throw new DomainException("Username can only contain letters, digits, '.', '_' and '-'.");

          Value = trimmed;
      }

      public string Value { get; }

      public bool Equals(Username? other) =>
          other is not null && string.Equals(Value, other.Value, StringComparison.OrdinalIgnoreCase);

      public override int GetHashCode() => StringComparer.OrdinalIgnoreCase.GetHashCode(Value);

      public override string ToString() => Value;

      [GeneratedRegex("^[A-Za-z0-9._-]+$")]
      private static partial Regex AllowedCharacters();
  }
  ```

- [ ] **Step 7: Run the tests and watch them pass**

  ```bash
  dotnet test --filter "FullyQualifiedName~TC_U01|FullyQualifiedName~TC_U02"
  ```

  *Expected: 11 passed (3 + 8 theory rows).*

- [ ] **Step 8: Commit**

  Delete any `.omc/` folder first, stage only the paths listed, and end the message with the `Co-Authored-By` trailer.

  ```bash
  git add global.json Directory.Build.props tests/Directory.Build.props WorkTracker.sln src/WorkTracker.Shared src/Modules/Users/WorkTracker.Users tests/WorkTracker.Users.Tests
  git commit -m "feat(users): add solution skeleton and Username value object"
  ```

  *Expected: one commit, clean `git status`.*

<a id="task-2"></a>

## Task 2: User aggregate, repository, Users contract and handlers

Adds the rest of the Users domain, the in-memory repository with atomic uniqueness, the create/get handlers and the cross-module contract.

- **Test cases:** TC-U03, TC-U04, TC-U05
- **Consumes:** `Username`, `ConflictException` (Task 1)
- **Produces:** `public interface IUsersApi { Task<bool> UserExistsAsync(Guid userId, CancellationToken ct); }`; `public static IServiceCollection AddUsersModule(this IServiceCollection)`; `internal sealed record UserResponse(Guid Id, string Username)`; `CreateUserCommand(string? Username) : ICommand<UserResponse>`; `GetUserQuery(Guid Id) : ICommand<UserResponse?>`

#### Files

- Create: `src/Modules/Users/WorkTracker.Users.Contracts/WorkTracker.Users.Contracts.csproj`, `IUsersApi.cs`
- Create in `src/Modules/Users/WorkTracker.Users/`: `Domain/User.cs`, `Domain/IUserRepository.cs`, `Infrastructure/InMemoryUserRepository.cs`, `Features/UserResponse.cs`, `Features/CreateUser/CreateUserCommand.cs`, `Features/CreateUser/CreateUserHandler.cs`, `Features/GetUser/GetUserQuery.cs`, `Features/GetUser/GetUserHandler.cs`, `UsersApi.cs`, `UsersModule.cs`
- Modify: `src/Modules/Users/WorkTracker.Users/WorkTracker.Users.csproj` (reference Contracts)
- Test: `tests/WorkTracker.Users.Tests/Features/CreateUserHandlerTests.cs`, `tests/WorkTracker.Users.Tests/Infrastructure/InMemoryUserRepositoryTests.cs`

#### Steps

- [ ] **Step 1: Create the Contracts project**

  `src/Modules/Users/WorkTracker.Users.Contracts/WorkTracker.Users.Contracts.csproj`

  ```xml
  <Project Sdk="Microsoft.NET.Sdk" />
  ```

  `src/Modules/Users/WorkTracker.Users.Contracts/IUsersApi.cs`

  ```csharp
  namespace WorkTracker.Users.Contracts;

  // The only thing other modules may ask the Users module.
  public interface IUsersApi
  {
      Task<bool> UserExistsAsync(Guid userId, CancellationToken ct);
  }
  ```

  Add to the `ProjectReference` group of `WorkTracker.Users.csproj`:

  ```
  <ProjectReference Include="../WorkTracker.Users.Contracts/WorkTracker.Users.Contracts.csproj" />
  ```

  ```bash
  dotnet sln add src/Modules/Users/WorkTracker.Users.Contracts/WorkTracker.Users.Contracts.csproj
  ```

  *Expected: project added.*

- [ ] **Step 2: Write the failing tests (TC-U03, TC-U04, TC-U05)**

  `tests/WorkTracker.Users.Tests/Features/CreateUserHandlerTests.cs`

  ```csharp
  using WorkTracker.Shared;
  using WorkTracker.Users.Features.CreateUser;
  using WorkTracker.Users.Infrastructure;

  namespace WorkTracker.Users.Tests.Features;

  public class CreateUserHandlerTests
  {
      private readonly InMemoryUserRepository _repository = new();
      private readonly CreateUserHandler _handler;

      public CreateUserHandlerTests() => _handler = new CreateUserHandler(_repository);

      [Fact]
      public async Task TC_U03_Create_user_returns_response_and_saves_the_user()
      {
          var response = await _handler.ExecuteAsync(new CreateUserCommand("alice"), CancellationToken.None);

          response.Id.ShouldNotBe(Guid.Empty);
          response.Username.ShouldBe("alice");
          var saved = await _repository.GetByIdAsync(response.Id, CancellationToken.None);
          saved.ShouldNotBeNull().Username.Value.ShouldBe("alice");
      }

      [Fact]
      public async Task TC_U04_Duplicate_username_ignoring_case_throws_ConflictException()
      {
          await _handler.ExecuteAsync(new CreateUserCommand("alice"), CancellationToken.None);

          var exception = await Should.ThrowAsync<ConflictException>(
              () => _handler.ExecuteAsync(new CreateUserCommand("ALICE"), CancellationToken.None));

          exception.Message.ShouldBe("Username 'ALICE' already exists.");
      }
  }
  ```

  `tests/WorkTracker.Users.Tests/Infrastructure/InMemoryUserRepositoryTests.cs`

  ```csharp
  using WorkTracker.Users.Domain;
  using WorkTracker.Users.Infrastructure;

  namespace WorkTracker.Users.Tests.Infrastructure;

  public class InMemoryUserRepositoryTests
  {
      [Fact]
      public async Task TC_U05_Concurrent_adds_of_the_same_username_let_exactly_one_win()
      {
          var repository = new InMemoryUserRepository();
          string[] spellings = ["alice", "Alice", "ALICE", "aLiCe"];

          var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(i => Task.Run(() =>
              repository.TryAddAsync(User.Create(new Username(spellings[i % 4])), CancellationToken.None))));

          results.Count(added => added).ShouldBe(1);
      }
  }
  ```

- [ ] **Step 3: Run the tests and watch them fail**

  ```bash
  dotnet test --filter "FullyQualifiedName~TC_U03|FullyQualifiedName~TC_U04|FullyQualifiedName~TC_U05"
  ```

  *Expected: build errors: `InMemoryUserRepository`, `CreateUserHandler`, `User` not found.*

- [ ] **Step 4: Implement the domain and the repository**

  `src/Modules/Users/WorkTracker.Users/Domain/User.cs`

  ```csharp
  namespace WorkTracker.Users.Domain;

  internal sealed class User
  {
      private User(Guid id, Username username)
      {
          Id = id;
          Username = username;
      }

      public Guid Id { get; }
      public Username Username { get; }

      public static User Create(Username username) => new(Guid.NewGuid(), username);
  }
  ```

  `src/Modules/Users/WorkTracker.Users/Domain/IUserRepository.cs`

  ```csharp
  namespace WorkTracker.Users.Domain;

  internal interface IUserRepository
  {
      // Atomic: returns false when the username is already taken (ignoring case).
      Task<bool> TryAddAsync(User user, CancellationToken ct);
      Task<User?> GetByIdAsync(Guid id, CancellationToken ct);
      Task<bool> ExistsAsync(Guid id, CancellationToken ct);
  }
  ```

  `src/Modules/Users/WorkTracker.Users/Infrastructure/InMemoryUserRepository.cs`

  ```csharp
  using System.Collections.Concurrent;
  using WorkTracker.Users.Domain;

  namespace WorkTracker.Users.Infrastructure;

  internal sealed class InMemoryUserRepository : IUserRepository
  {
      private readonly ConcurrentDictionary<Guid, User> _usersById = new();

      // Keyed by Username, whose equality ignores case. TryAdd lets exactly one caller win,
      // like a unique index would in SQL.
      private readonly ConcurrentDictionary<Username, Guid> _takenUsernames = new();

      public Task<bool> TryAddAsync(User user, CancellationToken ct)
      {
          if (!_takenUsernames.TryAdd(user.Username, user.Id))
              return Task.FromResult(false);

          _usersById[user.Id] = user;
          return Task.FromResult(true);
      }

      public Task<User?> GetByIdAsync(Guid id, CancellationToken ct) =>
          Task.FromResult(_usersById.GetValueOrDefault(id));

      public Task<bool> ExistsAsync(Guid id, CancellationToken ct) =>
          Task.FromResult(_usersById.ContainsKey(id));
  }
  ```

- [ ] **Step 5: Implement the response DTO, the command and the query**

  `src/Modules/Users/WorkTracker.Users/Features/UserResponse.cs`

  ```csharp
  using WorkTracker.Users.Domain;

  namespace WorkTracker.Users.Features;

  internal sealed record UserResponse(Guid Id, string Username)
  {
      public static UserResponse From(User user) => new(user.Id, user.Username.Value);
  }
  ```

  `src/Modules/Users/WorkTracker.Users/Features/CreateUser/CreateUserCommand.cs`

  ```csharp
  using FastEndpoints;

  namespace WorkTracker.Users.Features.CreateUser;

  internal sealed record CreateUserCommand(string? Username) : ICommand<UserResponse>;
  ```

  `src/Modules/Users/WorkTracker.Users/Features/CreateUser/CreateUserHandler.cs`

  ```csharp
  using FastEndpoints;
  using WorkTracker.Shared;
  using WorkTracker.Users.Domain;

  namespace WorkTracker.Users.Features.CreateUser;

  internal sealed class CreateUserHandler(IUserRepository users) : ICommandHandler<CreateUserCommand, UserResponse>
  {
      public async Task<UserResponse> ExecuteAsync(CreateUserCommand command, CancellationToken ct)
      {
          var user = User.Create(new Username(command.Username));

          if (!await users.TryAddAsync(user, ct))
              throw new ConflictException($"Username '{user.Username.Value}' already exists.");

          return UserResponse.From(user);
      }
  }
  ```

  `src/Modules/Users/WorkTracker.Users/Features/GetUser/GetUserQuery.cs`

  ```csharp
  using FastEndpoints;

  namespace WorkTracker.Users.Features.GetUser;

  internal sealed record GetUserQuery(Guid Id) : ICommand<UserResponse?>;
  ```

  `src/Modules/Users/WorkTracker.Users/Features/GetUser/GetUserHandler.cs`

  ```csharp
  using FastEndpoints;
  using WorkTracker.Users.Domain;

  namespace WorkTracker.Users.Features.GetUser;

  // Read only: returns null when the user does not exist; the endpoint turns that into 404.
  internal sealed class GetUserHandler(IUserRepository users) : ICommandHandler<GetUserQuery, UserResponse?>
  {
      public async Task<UserResponse?> ExecuteAsync(GetUserQuery query, CancellationToken ct)
      {
          var user = await users.GetByIdAsync(query.Id, ct);
          return user is null ? null : UserResponse.From(user);
      }
  }
  ```

- [ ] **Step 6: Implement the contract and the module registration**

  Both registrations are singletons: the repository holds the data, and FastEndpoints creates command handlers outside a request scope when they are not resolved per request.

  `src/Modules/Users/WorkTracker.Users/UsersApi.cs`

  ```csharp
  using WorkTracker.Users.Contracts;
  using WorkTracker.Users.Domain;

  namespace WorkTracker.Users;

  internal sealed class UsersApi(IUserRepository users) : IUsersApi
  {
      public Task<bool> UserExistsAsync(Guid userId, CancellationToken ct) => users.ExistsAsync(userId, ct);
  }
  ```

  `src/Modules/Users/WorkTracker.Users/UsersModule.cs`

  ```csharp
  using Microsoft.Extensions.DependencyInjection;
  using WorkTracker.Users.Contracts;
  using WorkTracker.Users.Domain;
  using WorkTracker.Users.Infrastructure;

  namespace WorkTracker.Users;

  // The module's only public entry point besides WorkTracker.Users.Contracts.
  public static class UsersModule
  {
      public static IServiceCollection AddUsersModule(this IServiceCollection services) =>
          services
              .AddSingleton<IUserRepository, InMemoryUserRepository>()
              .AddSingleton<IUsersApi, UsersApi>();
  }
  ```

- [ ] **Step 7: Run all Users tests and watch them pass**

  ```bash
  dotnet test tests/WorkTracker.Users.Tests
  ```

  *Expected: 14 passed.*

- [ ] **Step 8: Commit**

  Delete any `.omc/` folder first, stage only the paths listed, and end the message with the `Co-Authored-By` trailer.

  ```bash
  git add WorkTracker.sln src/Modules/Users tests/WorkTracker.Users.Tests
  git commit -m "feat(users): add User aggregate, repository, handlers and IUsersApi contract"
  ```

  *Expected: one commit.*

<a id="task-3"></a>

## Task 3: WorkItems module: domain, repository and handlers

Builds the WorkItems module against `IUsersApi` only. Its tests use a hand-written stub, so the Users module is never loaded.

- **Test cases:** TC-W01 to TC-W06
- **Consumes:** `IUsersApi` (Task 2), `DomainException`, `BusinessRuleViolationException` (Task 1)
- **Produces:** `public static IServiceCollection AddWorkItemsModule(this IServiceCollection)`; `internal sealed record WorkItemResponse(Guid Id, string Name, Guid AssigneeId)`; `CreateWorkItemCommand(string? Name, Guid AssigneeId) : ICommand<WorkItemResponse>`; `ListWorkItemsByAssigneeQuery(Guid AssigneeId) : ICommand<IReadOnlyList<WorkItemResponse>>`

#### Files

- Create in `src/Modules/WorkItems/WorkTracker.WorkItems/`: `WorkTracker.WorkItems.csproj`, `Domain/WorkItemName.cs`, `Domain/WorkItem.cs`, `Domain/IWorkItemRepository.cs`, `Infrastructure/InMemoryWorkItemRepository.cs`, `Features/WorkItemResponse.cs`, `Features/CreateWorkItem/CreateWorkItemCommand.cs`, `Features/CreateWorkItem/CreateWorkItemHandler.cs`, `Features/ListWorkItemsByAssignee/ListWorkItemsByAssigneeQuery.cs`, `Features/ListWorkItemsByAssignee/ListWorkItemsByAssigneeHandler.cs`, `WorkItemsModule.cs`
- Test in `tests/WorkTracker.WorkItems.Tests/`: `WorkTracker.WorkItems.Tests.csproj`, `Domain/WorkItemNameTests.cs`, `Domain/WorkItemTests.cs`, `Features/StubUsersApi.cs`, `Features/CreateWorkItemHandlerTests.cs`, `Features/ListWorkItemsByAssigneeHandlerTests.cs`

#### Steps

- [ ] **Step 1: Create the projects**

  WorkItems references **Users.Contracts only**, never `WorkTracker.Users`.

  `src/Modules/WorkItems/WorkTracker.WorkItems/WorkTracker.WorkItems.csproj`

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <ItemGroup>
      <FrameworkReference Include="Microsoft.AspNetCore.App" />
      <PackageReference Include="FastEndpoints" Version="8.3.0" />
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include="../../Users/WorkTracker.Users.Contracts/WorkTracker.Users.Contracts.csproj" />
      <ProjectReference Include="../../../WorkTracker.Shared/WorkTracker.Shared.csproj" />
    </ItemGroup>

    <ItemGroup>
      <InternalsVisibleTo Include="WorkTracker.WorkItems.Tests" />
    </ItemGroup>
  </Project>
  ```

  `tests/WorkTracker.WorkItems.Tests/WorkTracker.WorkItems.Tests.csproj`

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <ItemGroup>
      <ProjectReference Include="../../src/Modules/WorkItems/WorkTracker.WorkItems/WorkTracker.WorkItems.csproj" />
    </ItemGroup>
  </Project>
  ```

  ```bash
  dotnet sln add src/Modules/WorkItems/WorkTracker.WorkItems/WorkTracker.WorkItems.csproj tests/WorkTracker.WorkItems.Tests/WorkTracker.WorkItems.Tests.csproj
  ```

  *Expected: two projects added.*

- [ ] **Step 2: Write the failing domain tests (TC-W01, TC-W02, TC-W03)**

  `tests/WorkTracker.WorkItems.Tests/Domain/WorkItemNameTests.cs`

  ```csharp
  using WorkTracker.Shared;
  using WorkTracker.WorkItems.Domain;

  namespace WorkTracker.WorkItems.Tests.Domain;

  public class WorkItemNameTests
  {
      [Fact]
      public void TC_W01_Valid_name_is_accepted_and_trimmed()
      {
          new WorkItemName("  Write README  ").Value.ShouldBe("Write README");
          new WorkItemName(new string('x', 200)).Value.Length.ShouldBe(200);
      }

      [Theory]
      [InlineData(null)]
      [InlineData("")]
      [InlineData("   ")]
      public void TC_W02_Missing_name_throws_DomainException(string? input)
      {
          var exception = Should.Throw<DomainException>(() => new WorkItemName(input));

          exception.Message.ShouldBe("Work item name is required.");
      }

      [Fact]
      public void TC_W02_Name_over_200_characters_throws_DomainException()
      {
          var exception = Should.Throw<DomainException>(() => new WorkItemName(new string('x', 201)));

          exception.Message.ShouldBe("Work item name must be at most 200 characters.");
      }
  }
  ```

  `tests/WorkTracker.WorkItems.Tests/Domain/WorkItemTests.cs`

  ```csharp
  using WorkTracker.Shared;
  using WorkTracker.WorkItems.Domain;

  namespace WorkTracker.WorkItems.Tests.Domain;

  public class WorkItemTests
  {
      [Fact]
      public void TC_W03_Empty_assignee_throws_DomainException()
      {
          var exception = Should.Throw<DomainException>(
              () => WorkItem.Create(new WorkItemName("Write README"), Guid.Empty));

          exception.Message.ShouldBe("Assignee id is required.");
      }
  }
  ```

- [ ] **Step 3: Run them and watch them fail**

  ```bash
  dotnet test tests/WorkTracker.WorkItems.Tests
  ```

  *Expected: build errors: `WorkItemName`, `WorkItem` not found.*

- [ ] **Step 4: Implement the domain**

  `src/Modules/WorkItems/WorkTracker.WorkItems/Domain/WorkItemName.cs`

  ```csharp
  using WorkTracker.Shared;

  namespace WorkTracker.WorkItems.Domain;

  // 1-200 characters, trimmed.
  internal sealed record WorkItemName
  {
      private const int MaxLength = 200;

      public WorkItemName(string? value)
      {
          var trimmed = value?.Trim() ?? string.Empty;

          if (trimmed.Length == 0)
              throw new DomainException("Work item name is required.");
          if (trimmed.Length > MaxLength)
              throw new DomainException($"Work item name must be at most {MaxLength} characters.");

          Value = trimmed;
      }

      public string Value { get; }

      public override string ToString() => Value;
  }
  ```

  `src/Modules/WorkItems/WorkTracker.WorkItems/Domain/WorkItem.cs`

  ```csharp
  using WorkTracker.Shared;

  namespace WorkTracker.WorkItems.Domain;

  internal sealed class WorkItem
  {
      private WorkItem(Guid id, WorkItemName name, Guid assigneeId)
      {
          Id = id;
          Name = name;
          AssigneeId = assigneeId;
      }

      public Guid Id { get; }
      public WorkItemName Name { get; }

      // A user id owned by the Users module. Only the id crosses the boundary.
      public Guid AssigneeId { get; }

      public static WorkItem Create(WorkItemName name, Guid assigneeId)
      {
          if (assigneeId == Guid.Empty)
              throw new DomainException("Assignee id is required.");

          return new WorkItem(Guid.NewGuid(), name, assigneeId);
      }
  }
  ```

  `src/Modules/WorkItems/WorkTracker.WorkItems/Domain/IWorkItemRepository.cs`

  ```csharp
  namespace WorkTracker.WorkItems.Domain;

  internal interface IWorkItemRepository
  {
      Task AddAsync(WorkItem workItem, CancellationToken ct);
      Task<IReadOnlyList<WorkItem>> GetByAssigneeAsync(Guid assigneeId, CancellationToken ct);
  }
  ```

- [ ] **Step 5: Run the domain tests and watch them pass**

  ```bash
  dotnet test tests/WorkTracker.WorkItems.Tests
  ```

  *Expected: 6 passed.*

- [ ] **Step 6: Write the failing handler tests (TC-W04, TC-W05, TC-W06)**

  The second TC-W03 test comes from the Review Focus list: an empty assignee must be a 400, not a 422, so the handler checks it before asking Users.

  `tests/WorkTracker.WorkItems.Tests/Features/StubUsersApi.cs`

  ```csharp
  using WorkTracker.Users.Contracts;

  namespace WorkTracker.WorkItems.Tests.Features;

  // Stands in for the Users module: always gives the same answer.
  internal sealed class StubUsersApi(bool userExists) : IUsersApi
  {
      public Task<bool> UserExistsAsync(Guid userId, CancellationToken ct) => Task.FromResult(userExists);
  }
  ```

  `tests/WorkTracker.WorkItems.Tests/Features/CreateWorkItemHandlerTests.cs`

  ```csharp
  using WorkTracker.Shared;
  using WorkTracker.WorkItems.Features.CreateWorkItem;
  using WorkTracker.WorkItems.Infrastructure;

  namespace WorkTracker.WorkItems.Tests.Features;

  public class CreateWorkItemHandlerTests
  {
      private static readonly Guid AssigneeId = Guid.NewGuid();
      private readonly InMemoryWorkItemRepository _repository = new();

      [Fact]
      public async Task TC_W03_Handler_rejects_an_empty_assignee_before_the_user_lookup()
      {
          var handler = new CreateWorkItemHandler(_repository, new StubUsersApi(userExists: false));

          var exception = await Should.ThrowAsync<DomainException>(() =>
              handler.ExecuteAsync(new CreateWorkItemCommand("Write README", Guid.Empty), CancellationToken.None));

          exception.Message.ShouldBe("Assignee id is required.");
      }

      [Fact]
      public async Task TC_W04_Unknown_assignee_throws_BusinessRuleViolationException_and_saves_nothing()
      {
          var handler = new CreateWorkItemHandler(_repository, new StubUsersApi(userExists: false));

          var exception = await Should.ThrowAsync<BusinessRuleViolationException>(() =>
              handler.ExecuteAsync(new CreateWorkItemCommand("Write README", AssigneeId), CancellationToken.None));

          exception.Message.ShouldBe($"User '{AssigneeId}' does not exist.");
          (await _repository.GetByAssigneeAsync(AssigneeId, CancellationToken.None)).ShouldBeEmpty();
      }

      [Fact]
      public async Task TC_W05_Known_assignee_creates_and_saves_the_work_item()
      {
          var handler = new CreateWorkItemHandler(_repository, new StubUsersApi(userExists: true));

          var response = await handler.ExecuteAsync(
              new CreateWorkItemCommand("  Write README  ", AssigneeId), CancellationToken.None);

          response.Id.ShouldNotBe(Guid.Empty);
          response.Name.ShouldBe("Write README");
          response.AssigneeId.ShouldBe(AssigneeId);
          var saved = await _repository.GetByAssigneeAsync(AssigneeId, CancellationToken.None);
          saved.ShouldHaveSingleItem().Id.ShouldBe(response.Id);
      }
  }
  ```

  `tests/WorkTracker.WorkItems.Tests/Features/ListWorkItemsByAssigneeHandlerTests.cs`

  ```csharp
  using WorkTracker.WorkItems.Domain;
  using WorkTracker.WorkItems.Features.ListWorkItemsByAssignee;
  using WorkTracker.WorkItems.Infrastructure;

  namespace WorkTracker.WorkItems.Tests.Features;

  public class ListWorkItemsByAssigneeHandlerTests
  {
      [Fact]
      public async Task TC_W06_Lists_only_the_given_assignees_work_items()
      {
          var repository = new InMemoryWorkItemRepository();
          var userA = Guid.NewGuid();
          var userB = Guid.NewGuid();
          await repository.AddAsync(WorkItem.Create(new WorkItemName("A1"), userA), CancellationToken.None);
          await repository.AddAsync(WorkItem.Create(new WorkItemName("A2"), userA), CancellationToken.None);
          await repository.AddAsync(WorkItem.Create(new WorkItemName("B1"), userB), CancellationToken.None);
          var handler = new ListWorkItemsByAssigneeHandler(repository);

          var forA = await handler.ExecuteAsync(new ListWorkItemsByAssigneeQuery(userA), CancellationToken.None);
          var forUnknown = await handler.ExecuteAsync(new ListWorkItemsByAssigneeQuery(Guid.NewGuid()), CancellationToken.None);

          forA.Select(item => item.Name).ShouldBe(new[] { "A1", "A2" }, ignoreOrder: true);
          forUnknown.ShouldBeEmpty();
      }
  }
  ```

- [ ] **Step 7: Run them and watch them fail**

  ```bash
  dotnet test tests/WorkTracker.WorkItems.Tests
  ```

  *Expected: build errors: `InMemoryWorkItemRepository`, `CreateWorkItemHandler`, `ListWorkItemsByAssigneeHandler` not found.*

- [ ] **Step 8: Implement the repository, DTO, command, query and module registration**

  `src/Modules/WorkItems/WorkTracker.WorkItems/Infrastructure/InMemoryWorkItemRepository.cs`

  ```csharp
  using System.Collections.Concurrent;
  using WorkTracker.WorkItems.Domain;

  namespace WorkTracker.WorkItems.Infrastructure;

  internal sealed class InMemoryWorkItemRepository : IWorkItemRepository
  {
      private readonly ConcurrentDictionary<Guid, WorkItem> _workItems = new();

      public Task AddAsync(WorkItem workItem, CancellationToken ct)
      {
          _workItems[workItem.Id] = workItem;
          return Task.CompletedTask;
      }

      public Task<IReadOnlyList<WorkItem>> GetByAssigneeAsync(Guid assigneeId, CancellationToken ct) =>
          Task.FromResult<IReadOnlyList<WorkItem>>(
              _workItems.Values.Where(item => item.AssigneeId == assigneeId).ToList());
  }
  ```

  `src/Modules/WorkItems/WorkTracker.WorkItems/Features/WorkItemResponse.cs`

  ```csharp
  using WorkTracker.WorkItems.Domain;

  namespace WorkTracker.WorkItems.Features;

  internal sealed record WorkItemResponse(Guid Id, string Name, Guid AssigneeId)
  {
      public static WorkItemResponse From(WorkItem workItem) =>
          new(workItem.Id, workItem.Name.Value, workItem.AssigneeId);
  }
  ```

  `src/Modules/WorkItems/WorkTracker.WorkItems/Features/CreateWorkItem/CreateWorkItemCommand.cs`

  ```csharp
  using FastEndpoints;

  namespace WorkTracker.WorkItems.Features.CreateWorkItem;

  internal sealed record CreateWorkItemCommand(string? Name, Guid AssigneeId) : ICommand<WorkItemResponse>;
  ```

  `src/Modules/WorkItems/WorkTracker.WorkItems/Features/CreateWorkItem/CreateWorkItemHandler.cs`

  ```csharp
  using FastEndpoints;
  using WorkTracker.Shared;
  using WorkTracker.Users.Contracts;
  using WorkTracker.WorkItems.Domain;

  namespace WorkTracker.WorkItems.Features.CreateWorkItem;

  internal sealed class CreateWorkItemHandler(IWorkItemRepository workItems, IUsersApi users)
      : ICommandHandler<CreateWorkItemCommand, WorkItemResponse>
  {
      public async Task<WorkItemResponse> ExecuteAsync(CreateWorkItemCommand command, CancellationToken ct)
      {
          // Single-object rules first (400), then the rule that needs another module (422).
          var workItem = WorkItem.Create(new WorkItemName(command.Name), command.AssigneeId);

          if (!await users.UserExistsAsync(command.AssigneeId, ct))
              throw new BusinessRuleViolationException($"User '{command.AssigneeId}' does not exist.");

          await workItems.AddAsync(workItem, ct);
          return WorkItemResponse.From(workItem);
      }
  }
  ```

  `src/Modules/WorkItems/WorkTracker.WorkItems/Features/ListWorkItemsByAssignee/ListWorkItemsByAssigneeQuery.cs`

  ```csharp
  using FastEndpoints;

  namespace WorkTracker.WorkItems.Features.ListWorkItemsByAssignee;

  internal sealed record ListWorkItemsByAssigneeQuery(Guid AssigneeId) : ICommand<IReadOnlyList<WorkItemResponse>>;
  ```

  `src/Modules/WorkItems/WorkTracker.WorkItems/Features/ListWorkItemsByAssignee/ListWorkItemsByAssigneeHandler.cs`

  ```csharp
  using FastEndpoints;
  using WorkTracker.Shared;
  using WorkTracker.WorkItems.Domain;

  namespace WorkTracker.WorkItems.Features.ListWorkItemsByAssignee;

  // Read only. An unknown assignee simply has no work items; reads never call another module.
  internal sealed class ListWorkItemsByAssigneeHandler(IWorkItemRepository workItems)
      : ICommandHandler<ListWorkItemsByAssigneeQuery, IReadOnlyList<WorkItemResponse>>
  {
      public async Task<IReadOnlyList<WorkItemResponse>> ExecuteAsync(ListWorkItemsByAssigneeQuery query, CancellationToken ct)
      {
          if (query.AssigneeId == Guid.Empty)
              throw new DomainException("Assignee id is required.");

          var items = await workItems.GetByAssigneeAsync(query.AssigneeId, ct);
          return items.Select(WorkItemResponse.From).ToList();
      }
  }
  ```

  `src/Modules/WorkItems/WorkTracker.WorkItems/WorkItemsModule.cs`

  ```csharp
  using Microsoft.Extensions.DependencyInjection;
  using WorkTracker.WorkItems.Domain;
  using WorkTracker.WorkItems.Infrastructure;

  namespace WorkTracker.WorkItems;

  // The module's only public entry point. It expects IUsersApi to be registered by the host.
  public static class WorkItemsModule
  {
      public static IServiceCollection AddWorkItemsModule(this IServiceCollection services) =>
          services.AddSingleton<IWorkItemRepository, InMemoryWorkItemRepository>();
  }
  ```

- [ ] **Step 9: Run all WorkItems tests and watch them pass**

  ```bash
  dotnet test tests/WorkTracker.WorkItems.Tests
  ```

  *Expected: 10 passed.*

- [ ] **Step 10: Commit**

  Delete any `.omc/` folder first, stage only the paths listed, and end the message with the `Co-Authored-By` trailer.

  ```bash
  git add WorkTracker.sln src/Modules/WorkItems tests/WorkTracker.WorkItems.Tests
  git commit -m "feat(workitems): add WorkItems domain, repository and handlers"
  ```

  *Expected: one commit.*

<a id="task-4"></a>

## Task 4: Host, problem details and the Users endpoints

Creates the runnable app and the two Users endpoints, then proves them over real HTTP.

- **Test cases:** TC-A01 to TC-A05
- **Consumes:** `AddUsersModule()`, `CreateUserCommand`, `GetUserQuery`, `UserResponse` (Task 2)
- **Produces:** `public partial class Program`; test helpers `PostWithKeyAsync`, `CreateUserAsync`, `NewUsername`, `ShouldBeProblemAsync`, records `UserDto`, `WorkItemDto`

#### Files

- Create: `src/WorkTracker.Host/WorkTracker.Host.csproj`, `Program.cs`, `ProblemDetailsExceptionHandler.cs`, `Properties/launchSettings.json`
- Create in `src/Modules/Users/WorkTracker.Users/Features/`: `CreateUser/CreateUserRequest.cs`, `CreateUser/CreateUserEndpoint.cs`, `GetUser/GetUserRequest.cs`, `GetUser/GetUserEndpoint.cs`
- Test: `tests/WorkTracker.Api.Tests/WorkTracker.Api.Tests.csproj`, `TestSupport.cs`, `UsersEndpointsTests.cs`

#### Steps

- [ ] **Step 1: Create the Host project**

  `src/WorkTracker.Host/WorkTracker.Host.csproj`

  ```xml
  <Project Sdk="Microsoft.NET.Sdk.Web">
    <ItemGroup>
      <PackageReference Include="FastEndpoints.Swagger" Version="8.3.0" />
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include="../WorkTracker.Shared/WorkTracker.Shared.csproj" />
      <ProjectReference Include="../Modules/Users/WorkTracker.Users/WorkTracker.Users.csproj" />
    </ItemGroup>

    <ItemGroup>
      <InternalsVisibleTo Include="WorkTracker.Api.Tests" />
    </ItemGroup>
  </Project>
  ```

  `src/WorkTracker.Host/Properties/launchSettings.json`

  ```json
  {
    "profiles": {
      "http": {
        "commandName": "Project",
        "launchBrowser": true,
        "launchUrl": "swagger",
        "applicationUrl": "http://localhost:5080",
        "environmentVariables": { "ASPNETCORE_ENVIRONMENT": "Development" }
      }
    }
  }
  ```

  `src/WorkTracker.Host/ProblemDetailsExceptionHandler.cs`

  ```csharp
  using Microsoft.AspNetCore.Diagnostics;
  using Microsoft.AspNetCore.Mvc;
  using WorkTracker.Shared;

  namespace WorkTracker.Host;

  // Turns the Shared exceptions into RFC 9457 problem details with the rule's own message.
  // Anything else is a generic 500 that exposes nothing internal.
  internal sealed class ProblemDetailsExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
  {
      public ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
      {
          var (status, detail) = exception switch
          {
              DomainException => (StatusCodes.Status400BadRequest, exception.Message),
              ConflictException => (StatusCodes.Status409Conflict, exception.Message),
              BusinessRuleViolationException => (StatusCodes.Status422UnprocessableEntity, exception.Message),
              _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
          };

          httpContext.Response.StatusCode = status;
          return problemDetails.TryWriteAsync(new ProblemDetailsContext
          {
              HttpContext = httpContext,
              ProblemDetails = new ProblemDetails { Status = status, Detail = detail }
          });
      }
  }
  ```

  `src/WorkTracker.Host/Program.cs`

  ```csharp
  using FastEndpoints;
  using FastEndpoints.Swagger;
  using WorkTracker.Host;
  using WorkTracker.Users;

  var builder = WebApplication.CreateBuilder(args);

  builder.Services
      .AddProblemDetails()
      .AddExceptionHandler<ProblemDetailsExceptionHandler>()
      .AddUsersModule()
      .AddFastEndpoints(o => o.Assemblies = [typeof(UsersModule).Assembly])
      .SwaggerDocument();

  var app = builder.Build();

  app.UseExceptionHandler();
  app.UseFastEndpoints(c => c.Errors.UseProblemDetails());
  app.UseSwaggerGen();

  app.Run();

  // Lets WorkTracker.Api.Tests start the app with WebApplicationFactory<Program>.
  public partial class Program { }
  ```

  ```bash
  dotnet sln add src/WorkTracker.Host/WorkTracker.Host.csproj
  ```

  *Expected: project added.*

- [ ] **Step 2: Create the API test project and its helpers**

  `tests/WorkTracker.Api.Tests/WorkTracker.Api.Tests.csproj`

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">
    <ItemGroup>
      <PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="8.0.31" />
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include="../../src/WorkTracker.Host/WorkTracker.Host.csproj" />
    </ItemGroup>
  </Project>
  ```

  `tests/WorkTracker.Api.Tests/TestSupport.cs`

  ```csharp
  using System.Net;
  using System.Net.Http.Json;
  using Microsoft.AspNetCore.Mvc;

  namespace WorkTracker.Api.Tests;

  internal sealed record UserDto(Guid Id, string Username);
  internal sealed record WorkItemDto(Guid Id, string Name, Guid AssigneeId);

  internal static class TestSupport
  {
      public const string KeyHeader = "Idempotency-Key";
      public const string ReplayedHeader = "Idempotent-Replayed";

      // A unique, valid username so tests never collide.
      public static string NewUsername() => "user" + Guid.NewGuid().ToString("N")[..8];

      // POSTs JSON with an Idempotency-Key: a fresh one unless the test passes its own.
      public static Task<HttpResponseMessage> PostWithKeyAsync(
          this HttpClient client, string url, object body, string? key = null)
      {
          var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = JsonContent.Create(body) };
          request.Headers.Add(KeyHeader, key ?? Guid.NewGuid().ToString());
          return client.SendAsync(request);
      }

      public static async Task<UserDto> CreateUserAsync(this HttpClient client)
      {
          var response = await client.PostWithKeyAsync("/users", new { username = NewUsername() });
          response.StatusCode.ShouldBe(HttpStatusCode.Created);
          return (await response.Content.ReadFromJsonAsync<UserDto>())!;
      }

      public static async Task<ProblemDetails> ShouldBeProblemAsync(
          this HttpResponseMessage response, HttpStatusCode expectedStatus)
      {
          response.StatusCode.ShouldBe(expectedStatus);
          response.Content.Headers.ContentType.ShouldNotBeNull().MediaType.ShouldBe("application/problem+json");
          return (await response.Content.ReadFromJsonAsync<ProblemDetails>())!;
      }
  }
  ```

  ```bash
  dotnet sln add tests/WorkTracker.Api.Tests/WorkTracker.Api.Tests.csproj
  ```

  *Expected: project added.*

- [ ] **Step 3: Write the failing tests (TC-A01 to TC-A05)**

  The second TC-A04 test (malformed JSON) comes from the Review Focus list.

  `tests/WorkTracker.Api.Tests/UsersEndpointsTests.cs`

  ```csharp
  using System.Net;
  using System.Net.Http.Json;
  using System.Text;
  using Microsoft.AspNetCore.Mvc.Testing;
  using static WorkTracker.Api.Tests.TestSupport;

  namespace WorkTracker.Api.Tests;

  public class UsersEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
  {
      private readonly HttpClient _client = factory.CreateClient();

      [Fact]
      public async Task TC_A01_Create_user_returns_201_with_body_and_location()
      {
          var username = NewUsername();

          var response = await _client.PostWithKeyAsync("/users", new { username });

          response.StatusCode.ShouldBe(HttpStatusCode.Created);
          var user = (await response.Content.ReadFromJsonAsync<UserDto>())!;
          user.Id.ShouldNotBe(Guid.Empty);
          user.Username.ShouldBe(username);
          response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe($"/users/{user.Id}");
      }

      [Fact]
      public async Task TC_A02_Get_user_at_location_returns_200_with_the_same_user()
      {
          var created = await _client.PostWithKeyAsync("/users", new { username = NewUsername() });

          var response = await _client.GetAsync(created.Headers.Location);

          response.StatusCode.ShouldBe(HttpStatusCode.OK);
          (await response.Content.ReadFromJsonAsync<UserDto>())
              .ShouldBe(await created.Content.ReadFromJsonAsync<UserDto>());
      }

      [Fact]
      public async Task TC_A03_Get_unknown_user_returns_404_problem()
      {
          var id = Guid.NewGuid();

          var problem = await (await _client.GetAsync($"/users/{id}")).ShouldBeProblemAsync(HttpStatusCode.NotFound);

          problem.Detail.ShouldBe($"User '{id}' was not found.");
      }

      [Fact]
      public async Task TC_A04_Invalid_username_returns_400_problem()
      {
          var response = await _client.PostWithKeyAsync("/users", new { username = "a" });

          var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest);
          problem.Detail.ShouldBe("Username must be between 3 and 32 characters.");
      }

      [Fact]
      public async Task TC_A04_Malformed_json_returns_400_problem_not_500()
      {
          var request = new HttpRequestMessage(HttpMethod.Post, "/users")
          {
              Content = new StringContent("{ not json", Encoding.UTF8, "application/json")
          };
          request.Headers.Add(KeyHeader, Guid.NewGuid().ToString());

          await (await _client.SendAsync(request)).ShouldBeProblemAsync(HttpStatusCode.BadRequest);
      }

      [Fact]
      public async Task TC_A05_Duplicate_username_ignoring_case_returns_409_problem()
      {
          var username = NewUsername();
          await _client.PostWithKeyAsync("/users", new { username });

          var response = await _client.PostWithKeyAsync("/users", new { username = username.ToUpperInvariant() });

          var problem = await response.ShouldBeProblemAsync(HttpStatusCode.Conflict);
          problem.Detail.ShouldBe($"Username '{username.ToUpperInvariant()}' already exists.");
      }
  }
  ```

- [ ] **Step 4: Run them and watch them fail**

  ```bash
  dotnet test tests/WorkTracker.Api.Tests
  ```

  *Expected: all 6 fail with 404 Not Found: the endpoints do not exist yet.*

- [ ] **Step 5: Implement the Users endpoints**

  Endpoints map the request to a command or query, run it, and send the result. `AllowAnonymous()` is required because FastEndpoints secures endpoints by default and there is no auth.

  `src/Modules/Users/WorkTracker.Users/Features/CreateUser/CreateUserRequest.cs`

  ```csharp
  namespace WorkTracker.Users.Features.CreateUser;

  internal sealed class CreateUserRequest
  {
      public string? Username { get; set; }
  }
  ```

  `src/Modules/Users/WorkTracker.Users/Features/CreateUser/CreateUserEndpoint.cs`

  ```csharp
  using FastEndpoints;
  using WorkTracker.Users.Features.GetUser;

  namespace WorkTracker.Users.Features.CreateUser;

  internal sealed class CreateUserEndpoint : Endpoint<CreateUserRequest, UserResponse>
  {
      public override void Configure()
      {
          Post("/users");
          AllowAnonymous();
      }

      public override async Task HandleAsync(CreateUserRequest req, CancellationToken ct)
      {
          var user = await new CreateUserCommand(req.Username).ExecuteAsync(ct);
          await Send.CreatedAtAsync<GetUserEndpoint>(new { id = user.Id }, user, cancellation: ct);
      }
  }
  ```

  `src/Modules/Users/WorkTracker.Users/Features/GetUser/GetUserRequest.cs`

  ```csharp
  namespace WorkTracker.Users.Features.GetUser;

  internal sealed class GetUserRequest
  {
      public Guid Id { get; set; }
  }
  ```

  `src/Modules/Users/WorkTracker.Users/Features/GetUser/GetUserEndpoint.cs`

  ```csharp
  using FastEndpoints;
  using Microsoft.AspNetCore.Http;

  namespace WorkTracker.Users.Features.GetUser;

  internal sealed class GetUserEndpoint : Endpoint<GetUserRequest, UserResponse>
  {
      public override void Configure()
      {
          Get("/users/{id}");
          AllowAnonymous();
      }

      public override async Task HandleAsync(GetUserRequest req, CancellationToken ct)
      {
          var user = await new GetUserQuery(req.Id).ExecuteAsync(ct);

          if (user is null)
          {
              await Send.ResultAsync(TypedResults.Problem(
                  detail: $"User '{req.Id}' was not found.", statusCode: StatusCodes.Status404NotFound));
              return;
          }

          await Send.OkAsync(user, ct);
      }
  }
  ```

- [ ] **Step 6: Run the API tests and watch them pass**

  ```bash
  dotnet test tests/WorkTracker.Api.Tests
  ```

  *Expected: 6 passed. If TC-A01 fails on `Location`, check that `GetUserEndpoint` is discovered and that the route value is named `id`.*

- [ ] **Step 7: Run the whole suite, then commit**

  ```bash
  dotnet test
  ```

  *Expected: 30 passed (14 Users + 10 WorkItems + 6 API).*

  Delete any `.omc/` folder first, stage only the paths listed, and end the message with the `Co-Authored-By` trailer.

  ```bash
  git add WorkTracker.sln src/WorkTracker.Host src/Modules/Users/WorkTracker.Users/Features tests/WorkTracker.Api.Tests
  git commit -m "feat(host): add host, problem details and Users endpoints"
  ```

  *Expected: one commit.*

<a id="task-5"></a>

## Task 5: WorkItems endpoints

Adds the two WorkItems endpoints and registers the module in the host.

- **Test cases:** TC-A06 to TC-A09
- **Consumes:** `AddWorkItemsModule()`, `CreateWorkItemCommand`, `ListWorkItemsByAssigneeQuery`, `WorkItemResponse` (Task 3); test helpers (Task 4)
- **Produces:** test helper `CreateWorkItemAsync(this HttpClient, Guid assigneeId, string name)`

#### Files

- Create in `src/Modules/WorkItems/WorkTracker.WorkItems/Features/`: `CreateWorkItem/CreateWorkItemRequest.cs`, `CreateWorkItem/CreateWorkItemEndpoint.cs`, `ListWorkItemsByAssignee/ListWorkItemsByAssigneeRequest.cs`, `ListWorkItemsByAssignee/ListWorkItemsByAssigneeEndpoint.cs`
- Modify: `src/WorkTracker.Host/WorkTracker.Host.csproj` (reference WorkItems), `src/WorkTracker.Host/Program.cs` (register the module and its assembly)
- Modify: `tests/WorkTracker.Api.Tests/TestSupport.cs` (add `CreateWorkItemAsync`)
- Test: `tests/WorkTracker.Api.Tests/WorkItemsEndpointsTests.cs`

#### Steps

- [ ] **Step 1: Add the test helper**

  Add to `TestSupport`:

  `tests/WorkTracker.Api.Tests/TestSupport.cs`

  ```csharp
  public static async Task<WorkItemDto> CreateWorkItemAsync(this HttpClient client, Guid assigneeId, string name)
  {
      var response = await client.PostWithKeyAsync("/work-items", new { name, assigneeId });
      response.StatusCode.ShouldBe(HttpStatusCode.Created);
      return (await response.Content.ReadFromJsonAsync<WorkItemDto>())!;
  }
  ```

- [ ] **Step 2: Write the failing tests (TC-A06 to TC-A09)**

  The all-zero GUID row of TC-A09 comes from the Review Focus list.

  `tests/WorkTracker.Api.Tests/WorkItemsEndpointsTests.cs`

  ```csharp
  using System.Net;
  using System.Net.Http.Json;
  using Microsoft.AspNetCore.Mvc.Testing;
  using static WorkTracker.Api.Tests.TestSupport;

  namespace WorkTracker.Api.Tests;

  public class WorkItemsEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
  {
      private readonly HttpClient _client = factory.CreateClient();

      [Fact]
      public async Task TC_A06_Create_work_item_returns_201_with_body_and_no_location()
      {
          var user = await _client.CreateUserAsync();

          var response = await _client.PostWithKeyAsync("/work-items", new { name = "Write README", assigneeId = user.Id });

          response.StatusCode.ShouldBe(HttpStatusCode.Created);
          response.Headers.Location.ShouldBeNull();
          var item = (await response.Content.ReadFromJsonAsync<WorkItemDto>())!;
          item.Id.ShouldNotBe(Guid.Empty);
          item.Name.ShouldBe("Write README");
          item.AssigneeId.ShouldBe(user.Id);
      }

      [Fact]
      public async Task TC_A07_Unknown_assignee_returns_422_problem()
      {
          var assigneeId = Guid.NewGuid();

          var response = await _client.PostWithKeyAsync("/work-items", new { name = "Write README", assigneeId });

          var problem = await response.ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity);
          problem.Detail.ShouldBe($"User '{assigneeId}' does not exist.");
      }

      [Fact]
      public async Task TC_A08_List_returns_only_the_assignees_work_items()
      {
          var userA = await _client.CreateUserAsync();
          var userB = await _client.CreateUserAsync();
          await _client.CreateWorkItemAsync(userA.Id, "A1");
          await _client.CreateWorkItemAsync(userA.Id, "A2");
          await _client.CreateWorkItemAsync(userB.Id, "B1");

          var forA = await _client.GetFromJsonAsync<WorkItemDto[]>($"/work-items?assigneeId={userA.Id}");
          var forUnknown = await _client.GetFromJsonAsync<WorkItemDto[]>($"/work-items?assigneeId={Guid.NewGuid()}");

          forA.ShouldNotBeNull().Select(item => item.Name).ShouldBe(new[] { "A1", "A2" }, ignoreOrder: true);
          forUnknown.ShouldNotBeNull().ShouldBeEmpty();
      }

      [Theory]
      [InlineData("/work-items")]
      [InlineData("/work-items?assigneeId=not-a-guid")]
      [InlineData("/work-items?assigneeId=00000000-0000-0000-0000-000000000000")]
      public async Task TC_A09_List_without_a_valid_assignee_id_returns_400_problem(string url)
      {
          await (await _client.GetAsync(url)).ShouldBeProblemAsync(HttpStatusCode.BadRequest);
      }
  }
  ```

- [ ] **Step 3: Run them and watch them fail**

  ```bash
  dotnet test tests/WorkTracker.Api.Tests --filter "FullyQualifiedName~WorkItemsEndpointsTests"
  ```

  *Expected: 6 fail with 404: no WorkItems endpoints yet.*

- [ ] **Step 4: Implement the endpoints**

  `src/Modules/WorkItems/WorkTracker.WorkItems/Features/CreateWorkItem/CreateWorkItemRequest.cs`

  ```csharp
  namespace WorkTracker.WorkItems.Features.CreateWorkItem;

  internal sealed class CreateWorkItemRequest
  {
      public string? Name { get; set; }
      public Guid AssigneeId { get; set; }
  }
  ```

  `src/Modules/WorkItems/WorkTracker.WorkItems/Features/CreateWorkItem/CreateWorkItemEndpoint.cs`

  ```csharp
  using FastEndpoints;
  using Microsoft.AspNetCore.Http;

  namespace WorkTracker.WorkItems.Features.CreateWorkItem;

  // 201 without a Location header: there is no get-one-work-item endpoint to point at.
  internal sealed class CreateWorkItemEndpoint : Endpoint<CreateWorkItemRequest, WorkItemResponse>
  {
      public override void Configure()
      {
          Post("/work-items");
          AllowAnonymous();
      }

      public override async Task HandleAsync(CreateWorkItemRequest req, CancellationToken ct)
      {
          var workItem = await new CreateWorkItemCommand(req.Name, req.AssigneeId).ExecuteAsync(ct);
          await Send.ResponseAsync(workItem, StatusCodes.Status201Created, ct);
      }
  }
  ```

  `src/Modules/WorkItems/WorkTracker.WorkItems/Features/ListWorkItemsByAssignee/ListWorkItemsByAssigneeRequest.cs`

  ```csharp
  namespace WorkTracker.WorkItems.Features.ListWorkItemsByAssignee;

  internal sealed class ListWorkItemsByAssigneeRequest
  {
      // Bound from ?assigneeId=. A value that is not a GUID fails binding with a 400.
      public Guid AssigneeId { get; set; }
  }
  ```

  `src/Modules/WorkItems/WorkTracker.WorkItems/Features/ListWorkItemsByAssignee/ListWorkItemsByAssigneeEndpoint.cs`

  ```csharp
  using FastEndpoints;

  namespace WorkTracker.WorkItems.Features.ListWorkItemsByAssignee;

  internal sealed class ListWorkItemsByAssigneeEndpoint
      : Endpoint<ListWorkItemsByAssigneeRequest, IReadOnlyList<WorkItemResponse>>
  {
      public override void Configure()
      {
          Get("/work-items");
          AllowAnonymous();
      }

      public override async Task HandleAsync(ListWorkItemsByAssigneeRequest req, CancellationToken ct)
      {
          var workItems = await new ListWorkItemsByAssigneeQuery(req.AssigneeId).ExecuteAsync(ct);
          await Send.OkAsync(workItems, ct);
      }
  }
  ```

- [ ] **Step 5: Register the module in the host**

  Add to the `ProjectReference` group of `WorkTracker.Host.csproj`:

  ```
  <ProjectReference Include="../Modules/WorkItems/WorkTracker.WorkItems/WorkTracker.WorkItems.csproj" />
  ```

  In `Program.cs`, add `using WorkTracker.WorkItems;` and change the service registration to:

  `src/WorkTracker.Host/Program.cs`

  ```csharp
  builder.Services
      .AddProblemDetails()
      .AddExceptionHandler<ProblemDetailsExceptionHandler>()
      .AddUsersModule()
      .AddWorkItemsModule()
      .AddFastEndpoints(o => o.Assemblies = [typeof(UsersModule).Assembly, typeof(WorkItemsModule).Assembly])
      .SwaggerDocument();
  ```

- [ ] **Step 6: Run the whole suite and watch it pass**

  ```bash
  dotnet test
  ```

  *Expected: 36 passed.*

- [ ] **Step 7: Commit**

  Delete any `.omc/` folder first, stage only the paths listed, and end the message with the `Co-Authored-By` trailer.

  ```bash
  git add src/Modules/WorkItems/WorkTracker.WorkItems/Features src/WorkTracker.Host tests/WorkTracker.Api.Tests
  git commit -m "feat(workitems): add WorkItems endpoints and register the module"
  ```

  *Expected: one commit.*

<a id="task-6"></a>

## Task 6: IdempotencyStore

The store on its own, tested directly with a fake clock: atomic reservation, replay inside the window, expiry and purging after it.

- **Test cases:** TC-I05, TC-I06
- **Consumes:** `InternalsVisibleTo WorkTracker.Api.Tests` on the Host (Task 4)
- **Produces:** `IdempotencyStore(TimeProvider time, TimeSpan window)` with `Reservation Reserve(string key, string fingerprint)`, `void Complete(string key, StoredResponse response)`, `void Release(string key)`, `int Count`; `record StoredResponse(int StatusCode, string? Location, string Body)`; `record Reservation(ReservationStatus Status, StoredResponse? Response = null)`; `enum ReservationStatus { Reserved, InProgress, Completed, KeyReusedForDifferentRequest }`

#### Files

- Create: `src/WorkTracker.Host/Idempotency/IdempotencyStore.cs`
- Modify: `tests/WorkTracker.Api.Tests/WorkTracker.Api.Tests.csproj` (add `Microsoft.Extensions.TimeProvider.Testing`)
- Test: `tests/WorkTracker.Api.Tests/Idempotency/IdempotencyStoreTests.cs`

#### Steps

- [ ] **Step 1: Add the fake clock package**

  Add to the `PackageReference` group of `WorkTracker.Api.Tests.csproj`:

  ```
  <PackageReference Include="Microsoft.Extensions.TimeProvider.Testing" Version="10.10.0" />
  ```

- [ ] **Step 2: Write the failing tests (TC-I05, TC-I06)**

  `tests/WorkTracker.Api.Tests/Idempotency/IdempotencyStoreTests.cs`

  ```csharp
  using Microsoft.Extensions.Time.Testing;
  using WorkTracker.Host.Idempotency;

  namespace WorkTracker.Api.Tests.Idempotency;

  public class IdempotencyStoreTests
  {
      private readonly FakeTimeProvider _time = new();
      private readonly IdempotencyStore _store;

      public IdempotencyStoreTests() => _store = new IdempotencyStore(_time, TimeSpan.FromSeconds(10));

      [Fact]
      public void TC_I05_A_reserved_key_that_is_not_completed_is_in_progress()
      {
          _store.Reserve("K", "fingerprint").Status.ShouldBe(ReservationStatus.Reserved);

          _store.Reserve("K", "fingerprint").Status.ShouldBe(ReservationStatus.InProgress);
      }

      [Fact]
      public async Task TC_I05_Concurrent_reservations_of_one_key_let_exactly_one_through()
      {
          var reservations = await Task.WhenAll(Enumerable.Range(0, 20)
              .Select(_ => Task.Run(() => _store.Reserve("K", "fingerprint"))));

          reservations.Count(r => r.Status == ReservationStatus.Reserved).ShouldBe(1);
          reservations.Count(r => r.Status == ReservationStatus.InProgress).ShouldBe(19);
      }

      [Fact]
      public void TC_I06_Completed_key_replays_inside_the_window_and_is_forgotten_after_it()
      {
          var stored = new StoredResponse(201, "/users/1", "{}");
          _store.Reserve("K", "fingerprint");
          _store.Complete("K", stored);
          _store.Reserve("other", "fingerprint");
          _store.Complete("other", stored);

          _time.Advance(TimeSpan.FromSeconds(9));
          _store.Reserve("K", "fingerprint").ShouldBe(new Reservation(ReservationStatus.Completed, stored));

          _time.Advance(TimeSpan.FromSeconds(2));
          _store.Reserve("K", "fingerprint").Status.ShouldBe(ReservationStatus.Reserved);
          _store.Count.ShouldBe(1); // "other" expired and was purged
      }
  }
  ```

- [ ] **Step 3: Run them and watch them fail**

  ```bash
  dotnet test tests/WorkTracker.Api.Tests --filter "FullyQualifiedName~IdempotencyStoreTests"
  ```

  *Expected: build error: namespace `WorkTracker.Host.Idempotency` not found.*

- [ ] **Step 4: Implement the store**

  Expired entries are purged on every reservation, so `GetOrAdd` never meets a stale entry and the reservation is a single atomic call.

  `src/WorkTracker.Host/Idempotency/IdempotencyStore.cs`

  ```csharp
  using System.Collections.Concurrent;

  namespace WorkTracker.Host.Idempotency;

  internal sealed record StoredResponse(int StatusCode, string? Location, string Body);

  internal enum ReservationStatus { Reserved, InProgress, Completed, KeyReusedForDifferentRequest }

  internal sealed record Reservation(ReservationStatus Status, StoredResponse? Response = null);

  // Remembers each Idempotency-Key for a time window, in process memory.
  // In production this would be Redis SET key NX EX <window>, shared by every instance.
  internal sealed class IdempotencyStore(TimeProvider time, TimeSpan window)
  {
      private sealed record Entry(string Fingerprint, DateTimeOffset ExpiresAt, StoredResponse? Response);

      private readonly ConcurrentDictionary<string, Entry> _entries = new();

      public int Count => _entries.Count;

      public Reservation Reserve(string key, string fingerprint)
      {
          var now = time.GetUtcNow();
          RemoveExpired(now);

          var reserved = new Entry(fingerprint, now + window, Response: null);
          var entry = _entries.GetOrAdd(key, reserved);   // atomic: exactly one caller adds

          if (ReferenceEquals(entry, reserved))
              return new Reservation(ReservationStatus.Reserved);
          if (entry.Fingerprint != fingerprint)
              return new Reservation(ReservationStatus.KeyReusedForDifferentRequest);
          return entry.Response is null
              ? new Reservation(ReservationStatus.InProgress)
              : new Reservation(ReservationStatus.Completed, entry.Response);
      }

      // Stores the successful response; the window restarts from now.
      public void Complete(string key, StoredResponse response)
      {
          if (_entries.TryGetValue(key, out var entry))
              _entries.TryUpdate(key, entry with { Response = response, ExpiresAt = time.GetUtcNow() + window }, entry);
      }

      // Forgets the key after a failed request, so the client can retry with it.
      public void Release(string key) => _entries.TryRemove(key, out _);

      private void RemoveExpired(DateTimeOffset now)
      {
          foreach (var entry in _entries)
          {
              if (entry.Value.ExpiresAt <= now)
                  _entries.TryRemove(entry);
          }
      }
  }
  ```

- [ ] **Step 5: Run them and watch them pass**

  ```bash
  dotnet test tests/WorkTracker.Api.Tests --filter "FullyQualifiedName~IdempotencyStoreTests"
  ```

  *Expected: 3 passed.*

- [ ] **Step 6: Commit**

  Delete any `.omc/` folder first, stage only the paths listed, and end the message with the `Co-Authored-By` trailer.

  ```bash
  git add src/WorkTracker.Host/Idempotency tests/WorkTracker.Api.Tests
  git commit -m "feat(host): add IdempotencyStore with atomic reservation and expiry"
  ```

  *Expected: one commit.*

<a id="task-7"></a>

## Task 7: Idempotency pre/post processors on every POST

Wires the store into FastEndpoints so both POST endpoints require a key, replay stored responses and release keys on failure.

- **Test cases:** TC-I01 to TC-I04
- **Consumes:** `IdempotencyStore` and its records (Task 6); both POST endpoints (Tasks 4, 5); `TestSupport` helpers
- **Produces:** Response header `Idempotent-Replayed: true` on replays; config key `Idempotency:WindowSeconds` (default 10)

#### Files

- Create in `src/WorkTracker.Host/Idempotency/`: `IdempotencyState.cs`, `IdempotencyPreProcessor.cs`, `IdempotencyPostProcessor.cs`, `IdempotencyRegistration.cs`
- Create: `src/WorkTracker.Host/appsettings.json`
- Modify: `src/WorkTracker.Host/Program.cs`
- Test: `tests/WorkTracker.Api.Tests/Idempotency/IdempotencyEndpointsTests.cs`

#### Steps

- [ ] **Step 1: Write the failing tests (TC-I01 to TC-I04)**

  The second TC-I02 test pins the spec's promise that a replay keeps the `Location` header.

  `tests/WorkTracker.Api.Tests/Idempotency/IdempotencyEndpointsTests.cs`

  ```csharp
  using System.Net;
  using System.Net.Http.Json;
  using Microsoft.AspNetCore.Mvc.Testing;
  using static WorkTracker.Api.Tests.TestSupport;

  namespace WorkTracker.Api.Tests.Idempotency;

  public class IdempotencyEndpointsTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
  {
      private const string KeyUsedForDifferentRequest = "Idempotency-Key was already used with a different request.";
      private readonly HttpClient _client = factory.CreateClient();

      [Theory]
      [InlineData(null)]
      [InlineData("")]
      public async Task TC_I01_User_post_without_a_key_returns_400_and_creates_nothing(string? key)
      {
          var username = NewUsername();
          var request = new HttpRequestMessage(HttpMethod.Post, "/users") { Content = JsonContent.Create(new { username }) };
          if (key is not null)
              request.Headers.TryAddWithoutValidation(KeyHeader, key);

          var problem = await (await _client.SendAsync(request)).ShouldBeProblemAsync(HttpStatusCode.BadRequest);

          problem.Detail.ShouldBe("Idempotency-Key header is required.");
          (await _client.PostWithKeyAsync("/users", new { username })).StatusCode.ShouldBe(HttpStatusCode.Created);
      }

      [Fact]
      public async Task TC_I01_Work_item_post_without_a_key_returns_400()
      {
          var user = await _client.CreateUserAsync();

          var response = await _client.PostAsJsonAsync("/work-items", new { name = "Write README", assigneeId = user.Id });

          var problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest);
          problem.Detail.ShouldBe("Idempotency-Key header is required.");
      }

      [Fact]
      public async Task TC_I02_Replayed_work_item_post_returns_the_stored_response_and_creates_nothing()
      {
          var user = await _client.CreateUserAsync();
          var key = Guid.NewGuid().ToString();
          var body = new { name = "Write README", assigneeId = user.Id };

          var first = await _client.PostWithKeyAsync("/work-items", body, key);
          var second = await _client.PostWithKeyAsync("/work-items", body, key);

          first.StatusCode.ShouldBe(HttpStatusCode.Created);
          first.Headers.Contains(ReplayedHeader).ShouldBeFalse();
          second.StatusCode.ShouldBe(HttpStatusCode.Created);
          second.Headers.GetValues(ReplayedHeader).ShouldBe(new[] { "true" });
          (await second.Content.ReadFromJsonAsync<WorkItemDto>())
              .ShouldBe(await first.Content.ReadFromJsonAsync<WorkItemDto>());
          var items = await _client.GetFromJsonAsync<WorkItemDto[]>($"/work-items?assigneeId={user.Id}");
          items.ShouldNotBeNull().Length.ShouldBe(1);
      }

      [Fact]
      public async Task TC_I02_Replayed_user_post_keeps_the_location_header()
      {
          var key = Guid.NewGuid().ToString();
          var body = new { username = NewUsername() };

          var first = await _client.PostWithKeyAsync("/users", body, key);
          var second = await _client.PostWithKeyAsync("/users", body, key);

          second.StatusCode.ShouldBe(HttpStatusCode.Created);
          second.Headers.Location.ShouldBe(first.Headers.Location);
          (await second.Content.ReadFromJsonAsync<UserDto>())
              .ShouldBe(await first.Content.ReadFromJsonAsync<UserDto>());
      }

      [Fact]
      public async Task TC_I03_Key_reused_with_a_different_body_or_endpoint_returns_422()
      {
          var key = Guid.NewGuid().ToString();
          (await _client.PostWithKeyAsync("/users", new { username = NewUsername() }, key))
              .StatusCode.ShouldBe(HttpStatusCode.Created);

          var differentBody = await (await _client.PostWithKeyAsync("/users", new { username = NewUsername() }, key))
              .ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity);
          var differentEndpoint = await (await _client.PostWithKeyAsync("/work-items", new { name = "x", assigneeId = Guid.NewGuid() }, key))
              .ShouldBeProblemAsync(HttpStatusCode.UnprocessableEntity);

          differentBody.Detail.ShouldBe(KeyUsedForDifferentRequest);
          differentEndpoint.Detail.ShouldBe(KeyUsedForDifferentRequest);
      }

      [Fact]
      public async Task TC_I04_A_failed_request_releases_its_key()
      {
          var key = Guid.NewGuid().ToString();

          (await _client.PostWithKeyAsync("/users", new { username = "a" }, key)).StatusCode.ShouldBe(HttpStatusCode.BadRequest);

          (await _client.PostWithKeyAsync("/users", new { username = NewUsername() }, key)).StatusCode.ShouldBe(HttpStatusCode.Created);
      }
  }
  ```

- [ ] **Step 2: Run them and watch them fail**

  ```bash
  dotnet test tests/WorkTracker.Api.Tests --filter "FullyQualifiedName~IdempotencyEndpointsTests"
  ```

  *Expected: TC-I01, TC-I02 and TC-I03 fail (no key check, no replay); TC-I04 passes by accident, which is fine.*

- [ ] **Step 3: Implement the per-request state and the pre-processor**

  `src/WorkTracker.Host/Idempotency/IdempotencyState.cs`

  ```csharp
  namespace WorkTracker.Host.Idempotency;

  // Shared by the pre- and post-processor of one request (FastEndpoints ProcessorState).
  internal sealed class IdempotencyState
  {
      // Set only when this request reserved the key, so only the owner completes or releases it.
      public string? ReservedKey { get; set; }
  }
  ```

  `src/WorkTracker.Host/Idempotency/IdempotencyPreProcessor.cs`

  ```csharp
  using System.Security.Cryptography;
  using System.Text.Json;
  using FastEndpoints;

  namespace WorkTracker.Host.Idempotency;

  // Before a POST handler runs: require a key, then reserve it, replay the stored response,
  // or reject the request. Sending a response here means the handler does not run.
  internal sealed class IdempotencyPreProcessor(IdempotencyStore store) : IGlobalPreProcessor
  {
      public const string KeyHeader = "Idempotency-Key";
      public const string ReplayedHeader = "Idempotent-Replayed";

      public async Task PreProcessAsync(IPreProcessorContext ctx, CancellationToken ct)
      {
          var http = ctx.HttpContext;
          var key = http.Request.Headers[KeyHeader].ToString();

          if (string.IsNullOrWhiteSpace(key))
          {
              await SendProblemAsync(http, StatusCodes.Status400BadRequest, "Idempotency-Key header is required.");
              return;
          }

          var reservation = store.Reserve(key, Fingerprint(ctx));
          switch (reservation.Status)
          {
              case ReservationStatus.Reserved:
                  http.ProcessorState<IdempotencyState>().ReservedKey = key;
                  break;
              case ReservationStatus.Completed:
                  await ReplayAsync(http, reservation.Response!, ct);
                  break;
              case ReservationStatus.InProgress:
                  await SendProblemAsync(http, StatusCodes.Status409Conflict,
                      "A request with this Idempotency-Key is already being processed.");
                  break;
              case ReservationStatus.KeyReusedForDifferentRequest:
                  await SendProblemAsync(http, StatusCodes.Status422UnprocessableEntity,
                      "Idempotency-Key was already used with a different request.");
                  break;
          }
      }

      // Same route + same request body = same request.
      private static string Fingerprint(IPreProcessorContext ctx)
      {
          var body = JsonSerializer.SerializeToUtf8Bytes(ctx.Request, ctx.Request?.GetType() ?? typeof(object));
          return $"{ctx.HttpContext.Request.Path}:{Convert.ToHexString(SHA256.HashData(body))}";
      }

      private static async Task ReplayAsync(HttpContext http, StoredResponse stored, CancellationToken ct)
      {
          http.MarkResponseStart();
          http.Response.StatusCode = stored.StatusCode;
          http.Response.ContentType = "application/json; charset=utf-8";
          http.Response.Headers[ReplayedHeader] = "true";
          if (stored.Location is not null)
              http.Response.Headers.Location = stored.Location;
          await http.Response.WriteAsync(stored.Body, ct);
      }

      private static async Task SendProblemAsync(HttpContext http, int status, string detail)
      {
          http.MarkResponseStart();
          await TypedResults.Problem(detail: detail, statusCode: status).ExecuteAsync(http);
      }
  }
  ```

- [ ] **Step 4: Implement the post-processor**

  FastEndpoints runs post-processors even when the handler throws (`HasExceptionOccurred`), then rethrows the exception to the problem-details handler because we don't mark it handled.

  `src/WorkTracker.Host/Idempotency/IdempotencyPostProcessor.cs`

  ```csharp
  using System.Text.Json;
  using FastEndpoints;

  namespace WorkTracker.Host.Idempotency;

  // After a POST handler: store a successful response for replay, or release the key on failure.
  internal sealed class IdempotencyPostProcessor(IdempotencyStore store) : IGlobalPostProcessor
  {
      private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

      public Task PostProcessAsync(IPostProcessorContext ctx, CancellationToken ct)
      {
          var key = ctx.HttpContext.ProcessorState<IdempotencyState>().ReservedKey;
          if (key is null)
              return Task.CompletedTask; // this request did not reserve a key (missing, replayed or rejected)

          var response = ctx.HttpContext.Response;
          if (ctx.HasExceptionOccurred || response.StatusCode >= StatusCodes.Status400BadRequest)
          {
              store.Release(key);
              return Task.CompletedTask;
          }

          var body = JsonSerializer.Serialize(ctx.Response, ctx.Response?.GetType() ?? typeof(object), JsonOptions);
          store.Complete(key, new StoredResponse(response.StatusCode, response.Headers.Location.FirstOrDefault(), body));
          return Task.CompletedTask;
      }
  }
  ```

- [ ] **Step 5: Register it and attach it to POST endpoints only**

  `src/WorkTracker.Host/Idempotency/IdempotencyRegistration.cs`

  ```csharp
  using FastEndpoints;

  namespace WorkTracker.Host.Idempotency;

  internal static class IdempotencyRegistration
  {
      public static IServiceCollection AddIdempotency(this IServiceCollection services, IConfiguration configuration)
      {
          var window = TimeSpan.FromSeconds(configuration.GetValue("Idempotency:WindowSeconds", 10));
          return services
              .AddSingleton(TimeProvider.System)
              .AddSingleton(sp => new IdempotencyStore(sp.GetRequiredService<TimeProvider>(), window));
      }

      // Only POSTs create things; GETs are safe to repeat.
      public static void UseIdempotencyOnPosts(this EndpointDefinition endpoint)
      {
          if (!endpoint.Verbs.Contains("POST"))
              return;

          endpoint.PreProcessor<IdempotencyPreProcessor>(Order.Before);
          endpoint.PostProcessor<IdempotencyPostProcessor>(Order.After);
      }
  }
  ```

  `src/WorkTracker.Host/appsettings.json`

  ```json
  {
    "Idempotency": {
      "WindowSeconds": 10
    }
  }
  ```

  Final `Program.cs`:

  `src/WorkTracker.Host/Program.cs`

  ```csharp
  using FastEndpoints;
  using FastEndpoints.Swagger;
  using WorkTracker.Host;
  using WorkTracker.Host.Idempotency;
  using WorkTracker.Users;
  using WorkTracker.WorkItems;

  var builder = WebApplication.CreateBuilder(args);

  builder.Services
      .AddProblemDetails()
      .AddExceptionHandler<ProblemDetailsExceptionHandler>()
      .AddIdempotency(builder.Configuration)
      .AddUsersModule()
      .AddWorkItemsModule()
      .AddFastEndpoints(o => o.Assemblies = [typeof(UsersModule).Assembly, typeof(WorkItemsModule).Assembly])
      .SwaggerDocument();

  var app = builder.Build();

  app.UseExceptionHandler();
  app.UseFastEndpoints(c =>
  {
      c.Errors.UseProblemDetails();
      c.Endpoints.Configurator = endpoint => endpoint.UseIdempotencyOnPosts();
  });
  app.UseSwaggerGen();

  app.Run();

  // Lets WorkTracker.Api.Tests start the app with WebApplicationFactory<Program>.
  public partial class Program { }
  ```

- [ ] **Step 6: Run the whole suite and watch it pass**

  ```bash
  dotnet test
  ```

  *Expected: 46 passed. If TC-I02 replays an empty body, FastEndpoints did not fill `ctx.Response` for that send method: set `Response = workItem;` (or `user`) in the endpoint before calling `Send`.*

- [ ] **Step 7: Commit**

  Delete any `.omc/` folder first, stage only the paths listed, and end the message with the `Co-Authored-By` trailer.

  ```bash
  git add src/WorkTracker.Host tests/WorkTracker.Api.Tests
  git commit -m "feat(host): require Idempotency-Key on POSTs and replay stored responses"
  ```

  *Expected: one commit.*

<a id="task-8"></a>

## Task 8: requests.http and README

The two hand-off documents: every call plus the manual UAT checklist, and the half-page README.

- **Test cases:** smoke check only; UAT in Task 10
- **Consumes:** the running app (Tasks 4-7); port 5080 from `launchSettings.json`
- **Produces:** the UAT checklist (UAT-01 to UAT-10) used in Task 10

#### Files

- Create: `requests.http`, `README.md`

#### Steps

- [ ] **Step 1: Write requests.http**

  Works in VS Code (REST Client), Rider and Visual Studio 17.12+. Request 10 uses a fixed key on purpose, so UAT-07 and UAT-08 can resend it.

  `requests.http`

  ```http
  ### WorkTracker: every call, the error cases, and the manual UAT checklist at the bottom.
  ### Start the app first:  dotnet run --project src/WorkTracker.Host
  @baseUrl = http://localhost:5080

  ### 1. Create a user -> 201 + Location
  # @name createUser
  POST {{baseUrl}}/users
  Content-Type: application/json
  Idempotency-Key: {{$guid}}

  { "username": "alice" }

  ### 2. Get that user -> 200
  GET {{baseUrl}}/users/{{createUser.response.body.$.id}}

  ### 3. Unknown user -> 404
  GET {{baseUrl}}/users/7c9e6679-7425-40de-944b-e07fc1f90ae7

  ### 4. Same username, different case -> 409
  POST {{baseUrl}}/users
  Content-Type: application/json
  Idempotency-Key: {{$guid}}

  { "username": "ALICE" }

  ### 5a. Too short -> 400
  POST {{baseUrl}}/users
  Content-Type: application/json
  Idempotency-Key: {{$guid}}

  { "username": "a" }

  ### 5b. Bad characters -> 400
  POST {{baseUrl}}/users
  Content-Type: application/json
  Idempotency-Key: {{$guid}}

  { "username": "al!ce" }

  ### 6. Create a work item for alice -> 201
  POST {{baseUrl}}/work-items
  Content-Type: application/json
  Idempotency-Key: {{$guid}}

  { "name": "Write README", "assigneeId": "{{createUser.response.body.$.id}}" }

  ### 7. Unknown assignee -> 422
  POST {{baseUrl}}/work-items
  Content-Type: application/json
  Idempotency-Key: {{$guid}}

  { "name": "Write README", "assigneeId": "7c9e6679-7425-40de-944b-e07fc1f90ae7" }

  ### 8. List alice's work items -> 200
  GET {{baseUrl}}/work-items?assigneeId={{createUser.response.body.$.id}}

  ### 9a. List without assigneeId -> 400
  GET {{baseUrl}}/work-items

  ### 9b. List with an invalid assigneeId -> 400
  GET {{baseUrl}}/work-items?assigneeId=not-a-guid

  ### 10. Double click: send this twice -> the second reply has Idempotent-Replayed: true
  POST {{baseUrl}}/work-items
  Content-Type: application/json
  Idempotency-Key: double-click-demo

  { "name": "Clicked twice", "assigneeId": "{{createUser.response.body.$.id}}" }

  ### 11. Same key, different body -> 422
  POST {{baseUrl}}/work-items
  Content-Type: application/json
  Idempotency-Key: double-click-demo

  { "name": "Something else", "assigneeId": "{{createUser.response.body.$.id}}" }

  ### 12. No Idempotency-Key -> 400
  POST {{baseUrl}}/users
  Content-Type: application/json

  { "username": "bob" }

  ### 13. Malformed JSON -> 400
  POST {{baseUrl}}/users
  Content-Type: application/json
  Idempotency-Key: {{$guid}}

  { not json

  ###
  # MANUAL UAT CHECKLIST (the human gate; see ai-journey/plan/test-plan.html section 07)
  # Restart the app before starting so the data is empty.
  # [ ] UAT-01  Open http://localhost:5080/swagger: the 4 endpoints are listed and readable.
  # [ ] UAT-02  Run 1, then 2: 201 with a Location header, then 200 with the same user.
  # [ ] UAT-03  Run 4: 409, "Username 'ALICE' already exists."
  # [ ] UAT-04  Run 5a and 5b: 400 each, the message names the broken rule.
  # [ ] UAT-05  Run 6 twice, then 8: 201 each; the list holds both.
  # [ ] UAT-06  Run 7: 422, "User '...' does not exist."
  # [ ] UAT-07  Run 10 twice quickly: the second reply has Idempotent-Replayed: true; 8 grows by 1.
  # [ ] UAT-08  Wait more than 10 seconds, run 10 again: a new item is created (documented trade-off).
  # [ ] UAT-09  Run 12: 400, "Idempotency-Key header is required."
  # [ ] UAT-10  Run 13 and GET /users/not-a-guid: 400 or 404 problem details, never a stack trace.
  ```

- [ ] **Step 2: Write README.md**

  About half a page of design, then how to build, run, test and call it. Markdown, so GitHub renders it.

  `README.md`

  ````markdown
  # WorkTracker

  A .NET 8 modular monolith with two modules, **Users** and **WorkItems**, built with FastEndpoints, CQRS and DDD. Data is kept in memory.

  ## Design

  **Modules.** Each module is one project with vertical slices inside: `Domain/` for the model, `Features/<UseCase>/` for the endpoint, command or query and handler, and `Infrastructure/` for the in-memory repository. Everything in a module is `internal`; its only public surface is an `Add<Module>Module()` method. WorkItems never references Users: it checks that an assignee exists through `IUsersApi` in `WorkTracker.Users.Contracts`, so only primitives cross the boundary.

  **CQRS.** An endpoint maps the request to a command or query and runs it on the FastEndpoints command bus. `*Command` handlers change state, `*Query` handlers only read, and both return DTOs, never domain objects.

  **Domain.** `Username` and `WorkItemName` are value objects because they hold rules (length, characters, trimming). `User` and `WorkItem` have private constructors and `Create` factories, so they can't exist in an invalid state. A broken rule throws `DomainException` (400), a taken username `ConflictException` (409), an unknown assignee `BusinessRuleViolationException` (422). One exception handler turns them into RFC 9457 problem details with a specific message.

  **Trade-offs**
  - Layers are folders, not projects: one use case lives in one folder. The compiler doesn't stop a handler from reaching into `Infrastructure/`; code review does.
  - Handlers implement FastEndpoints' `ICommandHandler` instead of MediatR, which is now commercial. The cost is that handlers depend on FastEndpoints.
  - Username uniqueness is atomic (`ConcurrentDictionary.TryAdd`). With SQL it would be a unique index plus catching the violation.
  - Both POSTs require an `Idempotency-Key`, remembered for 10 seconds (configurable) in process memory. That stops double clicks, not slow retries, and doesn't work across instances. Production would use Redis `SET NX EX` and a longer window.
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
  ````

- [ ] **Step 3: Smoke-check the requests against the running app**

  ```bash
  dotnet run --project src/WorkTracker.Host
  ```

  *Expected: (run it in the background) the app listens on http://localhost:5080. Send requests 1, 2, 6 and 8 once each and see 201, 200, 201, 200. Stop the app.*

  This is only a smoke check that the file works. The real UAT is the candidate's, in Task 10.

- [ ] **Step 4: Commit**

  Delete any `.omc/` folder first, stage only the paths listed, and end the message with the `Co-Authored-By` trailer.

  ```bash
  git add requests.http README.md
  git commit -m "docs: add requests.http with UAT checklist and README"
  ```

  *Expected: one commit.*

<a id="task-9"></a>

## Task 9: AI-journey pages: prompts, toolchain, judgment

The three remaining ai-journey pages, in the same HTML style. They are drawn from `decisions.html` and the transcript, quoting the candidate verbatim.

- **Test cases:** none (docs)
- **Consumes:** `assets/style.css`; `decisions.html` Q1 to Q21 and later entries
- **Produces:** pages linked from the README's `ai-journey/` link

#### Files

- Create in `ai-journey/`: `prompts.html`, `toolchain.html`, `judgment.html`
- Modify: `ai-journey/decisions.html` (link the three pages in the header)

#### Steps

- [ ] **Step 1: prompts.html**

  The key prompts, quoted verbatim with typos kept, each with one line on what it caused. In order:

  - The opening prompt (`/superpowers:brainstorming` … "if there is any question, please ask"), which set the ask-first process.
  - "simple is the best, easy to read, easy to maintenance", which set the principle used for every later choice (Q2).
  - The FastEndpoints CQRS override (Q3).
  - The specific error messages push-back (Q6).
  - The validation and concurrency question that exposed the username race (Q7).
  - The double-click / 10s window idea and its correction (Q9, Q10).
  - The 8 written review comments on the spec (Q12 to Q19).
  - The HTML docs rule (Q20) and the test plan approval (Q21).
  - Any new key prompts from implementation and review (added in Task 10).

- [ ] **Step 2: toolchain.html**

  - Claude Code CLI on Windows. Models: Opus 5.5 for brainstorming and the first spec draft, Fable 5.1 for the written spec review, Opus 5.5 again from Q20 on (switched with `/model`).
  - superpowers skills: brainstorming, writing-plans, test-driven-development, plus the execution skill the candidate picks for this plan.
  - context7 MCP server: FastEndpoints docs (processors, exception handling in post-processors, `ProcessorState`, send methods).
  - NuGet API: package versions and .NET 8 support checks; web search for the assertion-library comparison (Q17).
  - Claude Code `/export` and `/compact`: how the transcript was kept across the context limit.
  - Hooks that ran in the session (impeccable design checks on the HTML docs), and what was fixed because of them.

- [ ] **Step 3: judgment.html**

  A table with one row per time the candidate overrode, corrected or pushed back on the AI: question, what the AI proposed, what the candidate decided, why it mattered. Rows come straight from `decisions.html`: Q3, Q6, Q7, Q9, Q10, Q13, Q15, Q18, Q19, Q20. A second section, "Code review", is filled in during Task 10 with what the candidate rejected or changed in the AI's code.

- [ ] **Step 4: Link the pages and commit**

  Add chips for the three pages to the header of `decisions.html`. Open each page in a browser in light and dark mode. Delete any `.omc/` folder first, stage only the paths listed, and end the message with the `Co-Authored-By` trailer.

  ```bash
  git add ai-journey/prompts.html ai-journey/toolchain.html ai-journey/judgment.html ai-journey/decisions.html
  git commit -m "docs: add ai-journey prompts, toolchain and judgment pages"
  ```

  *Expected: one commit.*

<a id="task-10"></a>

## Task 10: Final review and manual UAT (the human gate)

Nothing is done until the candidate has run the UAT checklist and is happy with it.

- **Test cases:** UAT-01 to UAT-10
- **Consumes:** everything above
- **Produces:** a branch ready to hand in

#### Files

- Modify: `ai-journey/decisions.html`, `ai-journey/judgment.html`, `ai-journey/prompts.html` (new entries)
- Create: `ai-journey/transcript-part2.md` (exported by the candidate)

#### Steps

- [ ] **Step 1: Run the whole suite from a clean build**

  ```bash
  dotnet clean
  dotnet build
  dotnet test
  ```

  *Expected: build succeeds; 46 passed, 0 failed.*

- [ ] **Step 2: Whole-branch code review**

  Review the branch against the spec and this plan: module boundaries (WorkItems references only Users.Contracts), thin endpoints, no domain objects in responses, exact error messages, no leftover TODOs. The candidate decides what to change. Each accepted or rejected finding becomes a row in `judgment.html` ("Code review").

  ```bash
  git diff main --stat
  ```

  *Expected: only `senior-backend-engineer/` files changed.*

- [ ] **Step 3: Manual UAT (candidate)**

  The candidate restarts the app and works through UAT-01 to UAT-10 at the bottom of `requests.http`. Any failure goes back to the task that owns it, fixed test-first.

- [ ] **Step 4: Record the outcome**

  Add the UAT verdict and any review decisions to `decisions.html` as new Q&A entries (Q22 onwards), quoting the candidate verbatim. The candidate runs `/export` to `ai-journey/transcript-part2.md`, **not** `transcript.md`, which holds the full first part.

- [ ] **Step 5: Final commit**

  Delete any `.omc/` folder first, stage only the paths listed, and end the message with the `Co-Authored-By` trailer.

  ```bash
  git add ai-journey/decisions.html ai-journey/judgment.html ai-journey/prompts.html ai-journey/transcript-part2.md
  git commit -m "docs: record code review, UAT result and transcript part 2"
  ```

  *Expected: clean `git status`; the branch is ready.*

---

*WorkTracker implementation plan · Built from the [design spec](2026-10-08-work-tracker-design.html) and the [test plan](test-plan.html)*
