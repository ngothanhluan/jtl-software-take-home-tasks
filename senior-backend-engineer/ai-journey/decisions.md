# Decisions — raw log

Chronological. Each entry: what the AI put on the table, the candidate's reply (quoted as typed,
typos included), and the outcome. Kept raw on purpose; see `transcript.md` for the full session.

---

**2026-10-08 — 01. Must an assignee exist?**
AI offered: (A) synchronous check via a public Users contract, (B) eventual consistency via events,
(C) no check. Recommended A.
Candidate: "I think A, but which architecture we're following? clean architecture or vertical slice?
remember simple is the best, easy to read, easy to maintenance"
Outcome: A. "Simple is the best" became the guiding principle for every later choice.

**02. Clean Architecture vs vertical slice**
AI proposed vertical slices per module with a `Domain/` folder; Clean Architecture rejected as ~8
projects for 4 endpoints.
Candidate: "I think this structure is good to go"
Outcome: vertical slices.

**03. CQRS library**
AI recommended hand-written `ICommand`/`IQuery` interfaces (no library); listed FastEndpoints bus,
MediatR, Mediator as alternatives.
Candidate: "as Mediatr goes commercial I think I will go with fastendpoint. as CQRS pattern is how we
control, use it, not just because its name. the implementation class can be name *Query while
implemting the ICommand interface. I think it does not matter"
Outcome: **candidate overrode the AI.** FastEndpoints command bus; `*Command` / `*Query` naming.

**04. Persistence**
AI recommended plain in-memory repositories over EF InMemory or SQLite.
Candidate: "we no doubt, A, in-memory should be good option for a test repo."
Outcome: in-memory.

**05. Error handling**
AI recommended exceptions + central ProblemDetails mapping, `null` for not-found; alternatives were a
`Result<T>` type or ErrorOr/FluentResults.
Candidate: "A, keep it simple"
Outcome: exceptions.

**06. Section 1 (domain + API) review**
Candidate: "agree on option1. Also just want to push back the error handling. is that error return
with specific error message? I expect that it would be something "Username is already existed" when
checking username uniqueness"
AI confirmed every error carries a specific `detail` message and showed example bodies.
Candidate: "agree, lets go next"
Outcome: **candidate pushed back** to make specific messages an explicit requirement.

**07. Section 2 (request flow) review — validation and concurrency**
Candidate: "it looks right, but I just want to follow up with some questions, there is the
validation? also we should handle the concurrency issue with adding/updating data."
AI: validation already lives in value objects/handlers (explained the three layers). Admitted the
username check-then-add had a race and changed it to an atomic `TryAddAsync`. Asked whether to build
update concurrency (no update use cases exist).
Outcome: **candidate caught a real race** in the AI's design. Atomic add adopted.

**08. Update concurrency / idempotency**
Candidate: "no, the requirement does not mention it. but should we handle idempotency, when somehow
user double click? and create 2 same workitem?"
AI recommended documenting idempotency, not building it.
Candidate: "how about idempotency check for a certain window? like 10s? because there is very low
chance of a human create 2 identical workitem on purpose in 5-10s."
AI misread this as content-based dedupe and argued against it (race, rule in the wrong layer).
Candidate: "no I mean we still need idempotency key require from the client and use that key for 10s
window. how that sound?"
AI designed it: required `Idempotency-Key` header, 10s configurable window, replay of the original
response, in-memory store.
Outcome: **candidate overrode "document only" and supplied the design** (client key + short
server-side window). Update concurrency: documented, not built.

**09. Retries and the key**
AI wrote that a slow retry "would create a duplicate" in a way that read as if the retry used a new key.
Candidate: "oh, good to know that trade off. but retrying should not change the idempotency key, I
think. other stuffs are good for me."
AI corrected itself: retries reuse the key; the real limit is the 10s server-side window. AI also
corrected an earlier wrong claim that lazy replacement bounds the store's memory — expired keys are now
purged on each reservation.
Outcome: **candidate corrected the AI's wording.**

**10. Section 3 (tests + deliverables)**
Candidate: "I think the section 3 looks good for the tests. for the judgment, I think it's all good
from my feedback. just extracting from the conversation. there will me more during doing code review
as well."
Outcome: approved. Spec written and committed.

**11. Written spec review (8 comments)**
Candidate comments, as typed:
1. "hrm, I don't think we should use internal here. as we only have 1 project. internal should be
   same as public. Ah I see now, we need it internal as Test project should only ref to the
   Contracts, right?" — AI: not quite; the inter-module boundary is the reference graph, `internal`
   guards Host/tests. Candidate: "agree." → keep `internal`.
2. "I don't think the name SharedKernel is too formal, why not just Shared? make it simple."
   → renamed to `Shared`.
3. "Well, for sql database, I prefer int or long, but it's acceptable for GUID as we're using
   in-memory data storage." → `Guid` kept; preference recorded in README trade-offs.
4. "Tell me why we need value object here? any tradeoff, why not just POCO? looks like we over
   complicated? if so explain why?" — AI: the brief grades value objects/invariants explicitly;
   offered (A) VOs for everything incl. IDs, (B) VOs only where a rule lives, plain `Guid` IDs.
   Candidate: "well I think that's a good catch for option B, by let says how this is adaptable for
   different database? like sql or no sql with ef core? ... what columns looks like?" — AI showed
   the EF Core column mapping (`HasConversion`, unique index, no cross-module FK).
   → **B adopted**; strongly-typed IDs dropped; §3.4 added to the spec.
5. "I prefer the name ApiException. there is any meaning of using the name DomainException?" —
   AI: it names where the rule broke; the domain must not describe itself in API terms.
   Candidate: "agree, then for what exception comming from other places/ business it should be
   different name, you says?" → kept `DomainException`, `ConflictException`,
   `BusinessRuleViolationException`, each named for the kind of failure.
6. "Hrm, there is any alternatives fror FluentAssertion? please research. and provide 2-3 best
   options with your recommendation. and explain why you choose it." — AI researched Shouldly
   4.3.0 (BSD-3), AwesomeAssertions 9.6.0 (Apache-2.0, keeps `FluentAssertions` namespace), plain
   xUnit Assert; recommended Shouldly. Candidate: "agree with shouldy. this is just testing package.
   we will go with whatever popular and prod ready." → Shouldly.
7. "Instead of this beautified decision log. can we make it raw, because I think the interviewer
   would like to see how is my thought and chain of thinking." → this file, plus `/export`
   transcript. Candidate: "ok lets do bowth."
8. "I think this is good, but it's still unit tests and integration tests. There is any skills you
   have to plan a test plan with test cases? so that human should be the final gate when implementing
   a feature request like this." — AI proposed `test-plan.md` with numbered cases approved before
   code, TDD during, manual UAT checklist run by the candidate at the end; mentioned `gsd-verify-work`.
   Candidate: "well gsd is plugins for solo development workflow. but I think tdd is good enough for
   this case. human might need to test manually to verify their intent." → test plan + TDD +
   manual UAT; no GSD.

**Model note:** Opus 5.5 up to and including the first spec draft; Fable 5.1 for the written spec
review (entry 11) and the resulting spec update; then back to Opus 5.5. The candidate switched with
`/model` each time.
