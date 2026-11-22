# ADR-0028: Shared .NET building blocks taken from Identity

**Status:** Accepted
**Date:** 2026-11-22
**Requirements addressed:** none directly (internal refactor; supports R-1 by cutting per-service boilerplate)
**Amends:** repo-structure §6 (adds `HiveSpace.BuildingBlocks.Abstractions`)

## Context
Identity, the first rich .NET service, grew its own generic plumbing: `Result<T>`/`Failure`/`Violation`/`Unit`, an `IClock`, an EF-backed `IEventOutbox`, an outbox cleanup job, `OrThrow()` from result to `ApiException`, the authorization result handler that writes the error envelope, and reading the user id from `sub`. Order, Payment and Media will need the same pieces. Copying them per service would drift.

Domain and Application layers must not reference ASP.NET Core or EF (NetArchTest in Catalog and Identity). `HiveSpace.BuildingBlocks.Errors` has an ASP.NET Core framework reference, so Identity handlers could not use its `ErrorCodes` and wrote `"common.*"` string literals instead.

## Decision
- **New lib `HiveSpace.BuildingBlocks.Abstractions`** with no package or framework references. It holds `Result<T>`, `Failure`, `Violation`, `Unit`, `ErrorCodes` and `IEventOutbox` (`Enqueue<T>(eventType, version, correlationId, data)`). A NetArchTest rule in its test project keeps `Microsoft.*`, EF, Azure and Grpc out. Domain and Application projects may reference it.
- **Errors** references Abstractions and adds `ResultExtensions.OrThrow`, `ApiAuthorizationResultHandler`, `ClaimsPrincipal.GetUserId()` and `AddApiErrors()` (registers the handler).
- **Messaging** references Abstractions and adds `EfEventOutbox<TContext>` (producer from `MessagingOptions.Producer`, registered by `AddEventBus`) and an opt-in `OutboxCleanup<TContext>` (`AddOutboxCleanup<TContext>(retention)`, default 24 h).
- **Time** comes from .NET's `TimeProvider` (`TimeProvider.System` in production, `FakeTimeProvider` in tests) instead of a home-made `IClock`.
- The service template references Abstractions from Domain and Application and calls `AddApiErrors()`.
- Out of scope: Catalog keeps its own `IEventOutbox.WriteAsync`; no MediatR; the migrate-on-startup block stays in each `Program.cs`.

## Options considered
- **One Abstractions lib with no references (chosen)** — Domain and Application stay pure, and `ErrorCodes` is usable everywhere. Cons: one more project.
- **Move `Result` and friends into Errors** — fewer projects, but Domain and Application would then pull ASP.NET Core and break the layer rules.
- **Keep everything per service** — no coupling between services, but every service re-implements the same plumbing and the copies drift.

## Consequences
Easier: a new rich service gets result handling, the error envelope for 401/403, the outbox and the clock with a few registrations. Harder: a change to these types touches every service at once, so they must stay small and generic; anything named after a service concept stays out (repo-structure §6).

**What it teaches:** dependency direction at the library level (a reference-free "contracts" assembly that inner layers can use); .NET's built-in `TimeProvider` and `FakeTimeProvider` instead of a custom clock; generic hosted services over a `DbContext` type parameter.
