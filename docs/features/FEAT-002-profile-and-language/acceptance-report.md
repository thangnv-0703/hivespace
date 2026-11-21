# Acceptance Report: FEAT-002 — Profile and preferred language

> Verdict: **Accepted** · Date: 2026-11-21
> Standard: technical-design.md §6 @ cca7e39 (the file has since gained only an uncommitted diagram link line, outside §6) · Sources: srs.md, use-cases.md (UC-027)
> Repo state audited: af6f315 (`FEAT-002 T10, T11: e2e run green, full verification recorded`)

## Verdict summary
All twelve acceptance criteria hold. Every criterion has at least one test that asserts what it says, the independent review of the feature's test diff found no weakened, skipped or deleted test and no coverage-config change, and the suites were run fresh from the repo head (a cold AppHost for the e2e run): .NET 194/194, Gherkin contract 35 scenarios / 147 steps, e2e 17 passed, web 13/13 tasks, Notification 33 passed with mypy and import-linter clean. No rework or design-defect findings; two minor findings are recorded and do not block. RTM Test refs are appended for the six FRs this feature implements.

**Independence caveat.** This audit ran in the same session that finished T10/T11, so the fresh-eyes rule was met by derivation (standard rebuilt from the SRS and UC-027, every green re-observed at `af6f315`), not by a separate session. Two e2e step edits made earlier in this session (`tests/e2e/steps/profile.steps.ts`: assert both `account-name` copies; wait for the language save and session refresh before the reload) are part of `af6f315`. Both make the spec stricter, and the audit read them as part of the test diff.

## Audit table
| AC | Encodes | Test(s) | Audit | Observed |
| :- | :------ | :------ | :---- | :------- |
| AC-1 | FR-PROF-001, FR-PROF-004, UC-027 1 | `ProfileApiTests.Ac1` (all fields, nulls, language, userId = token subject); `ProfileFlowTests.Ac1`; `profile.feature` "The profile of the signed-in user is returned"; web `ProfilePage` "shows the profile" (read-only email, initials); e2e "email field is read-only" | faithful | green |
| AC-2 | FR-PROF-002, UC-027 4 | `ProfileApiTests.Ac2` (PUT, GET returns `+84912345678`, null clears all three); `ProfileFlowTests.Ac2` ×2; `profile.feature` (save as E.164, null clears); e2e saves and reloads, phone `+84 912 345 678` | faithful | green |
| AC-3 | FR-PROF-003, UC-027 4a | `ProfileApiTests.Ac3` (one 400, four `details` in one response, nothing saved); `ProfileFlowTests.Ac3` ×2 (81 chars, 121 years, 80 accepted), `Today_for_the_date_rule_is_the_date_in_Vietnam`; `ProfileDomainTests`; `profile.feature` (80/81 outline, 121 years, unparsable date, all-fields); e2e invalid date shown on the field | faithful | green |
| AC-4 | FR-PROF-004 | `ProfileApiTests.Ac4` (400, `{email, identity.email_read_only}`, email unchanged); `ProfileFlowTests.Ac4`; `profile.feature` "The email cannot be changed" | faithful | green |
| AC-5 | FR-PROF-006, FR-LOC-002 | `ProfileApiTests.Ac5` (en saved, repeat 200, fr rejected, persisted); `ProfileFlowTests.Ac5`; `ProfileFlowTests.Ac8_..._repeat_writes_none`; `profile.feature` (save/repeat, unsupported) | faithful | green |
| AC-6 | FR-LOC-002, UC-027 3, 3a | web "signed in: UI switches at once, then saved and refreshed" (locale, `<html lang>`, storage asserted *before* awaiting the request); "visitor: kept in browser, no request" (`calls` length 0); "failed save keeps language, toast"; e2e: pick Tiếng Việt → reload (the seeded `hs-locale=en` loses to the profile) → fresh context shows Vietnamese | faithful | green |
| AC-7 | FR-PROF-002, FR-LOC-002 | gateway `Ac7_refresh_returns_the_new_name_and_language...` (starts from a token valid for 10 more minutes, so the 60 s window is bypassed; new claims in the body and in the next `/bff/user` with no second exchange; no refresh token leaked); `Two_parallel_refresh_calls_spend_the_refresh_token_once`; web `refresh()` ×2; e2e header name "Lan Tran" in both copies | faithful | green |
| AC-8 | FR-PROF-006 → FR-LOC-005, NFR-REL-005, NFR-LEG-001 | `ProfileFlowTests.Ac8` ×3 (one event for a name change, `changedFields`, schema-valid via JsonSchema, no phone/gender/dateOfBirth; one for a language change, none for a repeat; none for phone-only or unchanged); Notification `test_the_same_profile_event_delivered_twice_updates_the_contact_once`, `test_a_profile_change_updates_the_contact...`, `test_a_later_email_is_sent_in_the_language_saved...`, invalid event leaves nothing | faithful; see minor M-1 | green |
| AC-9 | FR-AUTH-015, FR-AUTH-016, architecture §8.1 | gateway `Ac9_the_profile_endpoints_without_a_session_are_401_at_the_gateway` (theory ×3, plus the direct-call and spoof cases); `ProfileApiTests.Ac9` ×2 (none / tampered / wrong-audience re-signed with the server key / garbage, each with the `hs.idp` cookie riding along; a control with the good token passes; spoofed `X-User-Id` returns the token subject's profile); `profile.feature` "Calls without a valid token are rejected" | faithful | green |
| AC-10 | FR-LOC-004 | `ProfileApiTests.Ac3...ac10` (exact en and vi message for `phone_invalid`); `profile.feature` "Errors come back in the request language"; all seven codes present in `contracts/errors/error-codes.yaml` (checked directly); web vi/en key parity | faithful | green |
| AC-11 | FR-PROF-003, FR-AUTH-004 | `ProfileFlowTests.Ac11` theory (61 and 80 accepted, 81 rejected with `display_name_too_long`); `DomainTests.Ac4` (the pre-existing test moved from 61 to 81 per D-2, plus an 80-char positive case) | faithful | green |
| AC-12 | dev-standards §9.1 | `contracts/features/identity/profile.feature`, 15 scenarios covering all three operations with each designed error; run by cucumber-js (the criterion says Reqnroll; the repo's Gherkin runner is cucumber-js per ADR-0025 and AGENTS.md) | faithful (runner name only) | green |

**Mutation check (Phase 3, third question).** `DisplayName.MaxLength` 80 → 81 in `Identity.Domain/DisplayName.cs`: `Identity.UnitTests` went red (2 of 77 failed). File restored with `git checkout`; the tree is clean of the mutation.

## Corrected tests
None. No test failed the audit, so none was rewritten.

## Independent execution
All at `af6f315`, on a machine with Docker 27.5.1, nothing listening on :5080 before the run.

| Run | Command | Observed |
| :-- | :------ | :------- |
| Build | `dotnet build HiveSpace.slnx` | 36 projects, 0 errors, 0 warnings |
| Whole .NET suite | `dotnet test --solution HiveSpace.slnx` (Testcontainers) | 194 total, 194 passed, 0 failed, 0 skipped; includes `Identity.IntegrationTests`, `MigrationTests` (AddUserProfile up, down, up on a fresh Postgres container), `Gateway.Tests`, `tests/system` |
| Contract (Gherkin) | `cd tests/contract && pnpm test` | 35 scenarios, 147 steps, all passed (15 in `profile.feature`) |
| E2E (cold) | `cd tests/e2e && pnpm test`; Playwright started the AppHost itself | 17 passed, 1 skipped, 0 failed; the UC-027 scenario passed in `journeys-desktop` and `journeys-mobile-360`. The skip is the existing `skeleton.spec.ts:33` (seller sidebar, skipped below 1024 px by design), unrelated to this feature |
| Notification | `uv run pytest` / `mypy` / `lint-imports` | 33 passed / no issues in 16 files / 2 contracts kept |
| Web | `pnpm turbo run lint typecheck test build --force` | 13 of 13 successful, uncached; Sheriff and vi/en parity included |
| Format | `dotnet format HiveSpace.slnx --verify-no-changes` | clean |
| Contracts lint | Spectral 6.17.1, `buf lint` | 0 errors (18 existing description warnings on template sample operations); buf ok |
| Go | `go test -short ./libs/go/... ./services/inventory/... ./services/cart/...` | 11 passed |

No flakiness observed. The AppHost used for the earlier diagnostic run was stopped; the cold e2e run started and stopped its own.

## Direct verification
- **Side-effect FRs.** The `UserProfileChanged` outbox rows are inspected in `ProfileFlowTests` (envelope data, schema validation, absent private fields); Notification's contact row and the language of a later email are asserted in its integration tests. The e2e journey shows the language reaching a fresh browser context through the saved profile alone.
- **Contracts vs design §3.** `identity.yaml` has `getMyProfile`, `updateMyProfile`, `updateMyLanguage` (all PUT or GET, no PATCH, `ProfileUpdate` schema present); `gateway.yaml` has `/bff/session/refresh`; all seven error codes are in `error-codes.yaml`. No divergence beyond those recorded in the design (D-1, D-2).
- **Screen SCR-STF-016.** The manifest entry (`code-native`, locator into `ui-design.md`) and the covered states match the web spec (default, loading with `aria-busy`, load error with retry, validation, save error, 401 to sign-in, vi rendering). No raw colors or pixel values in `features/profile/` (searched for hex, `rgb(`, arbitrary `[..px]`, `text-[`, `bg-[`: no matches). Observed running in the e2e journey on desktop and 360 px.
- **Binding NFRs.** NFR-LEG-001 (private fields stay inside Identity): asserted by the AC-8 tests. NFR-REL-005 (duplicate delivery): asserted. NFR-L10N-002 (Vietnam date for the future check): `Today_for_the_date_rule_is_the_date_in_Vietnam`. No NFR-PERF target binds these endpoints, so nothing was timed.
- **Pending environment:** none.

## Findings
**Rework:** none.

**Design defect:** none.

**Minor (recorded, non-blocking):**
- **M-1 (AC-8 atomicity).** The criterion says the event is written "in the same transaction". The tests prove the event exists after a save and does not exist after a rejected save, but none forces a failure between the profile write and the outbox write to show they commit together. The handler uses one `SaveChangesAsync`, so the design holds; the proof is by structure, not by a test. A future hardening test could fail the second write and assert the first rolled back.
- **M-2 (partial FRs not marked in the plan).** The design defers the avatar half of FR-PROF-001 to FEAT-035 (FR-MEDIA-001) and seller-centre language persistence, part of FR-LOC-002, to FEAT-009; the plan row for FEAT-002 lists both FRs whole and the RTM Plan ref names only FEAT-002. This report verified only what this feature delivers (a placeholder avatar, storefront language persistence), so the RTM Test ref for those two FRs carries `(partial)`. The plan should add FEAT-035 and FEAT-009 as co-owners so the computed full verification is correct.
- **M-3 (traceability notes, no action needed beyond the plan fix).** The plan row still reads `GET/PATCH /v1/me/profile` (the design already records this as its correction request, D-1). The AC-12 text says Reqnroll, the runner is cucumber-js.

## RTM
Test ref appended (Test ref column only, append-only) in `docs/rtm.md`:
- FR-PROF-001: `features/FEAT-002-profile-and-language/acceptance-report.md (partial)`
- FR-PROF-002, FR-PROF-003, FR-PROF-004, FR-PROF-006: `features/FEAT-002-profile-and-language/acceptance-report.md`
- FR-LOC-002: `features/FEAT-002-profile-and-language/acceptance-report.md (partial)`

FR-AUTH-015 and FR-AUTH-016 are cited by AC-9 but are implemented by FEAT-001; their rows are untouched.
