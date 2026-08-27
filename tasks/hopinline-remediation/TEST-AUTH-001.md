Task ID:
TEST-AUTH-001

Model:
haiku

Title:
Tests proving every mutation rejects a missing or wrong manage token

Purpose:
Authorization is the one area where a silent regression is invisible in normal use — the app works perfectly for everyone, including attackers. These tests assert the negative case for every mutating method, so removing a guard breaks the build rather than quietly opening the app up. They also pin the two leak paths: the read DTO must not carry the token, and an error message must not reveal it.

Dependencies:
TEST-001, SERVICE-002

Consumes:
TEST-001: `SqliteDbFixture` and `TestData`
SERVICE-002: `ILineService` with its `AuthorizeAsync` guard, constant-time comparison, and abuse limits
IDGEN-001: `IdGenerator.GenerateManageToken()` and `GenerateUniqueId()`

Produces:
(nothing downstream consumes this)

Files:
- `HopInLine.Tests/AuthorizationTests.cs` (new)
- `HopInLine.Tests/IdGeneratorTests.cs` (new)

Requirements:
1. `AuthorizationTests` builds a real `LineService` over a `SqliteDbFixture`-backed `SQLLineRepository`, plus a stub `ILineChangeNotifier`. Write the stub as a small private class in the test file that records the line ids it was asked to notify — do not use a mocking library. The recorded list is what proves a rejected call notifies nobody.
2. Write a `[Theory]` driven by a list of mutating operations, so adding a method to `ILineService` without a guard shows up as a gap. For each of these — `UpdateLineAsync`, `DeleteLineAsync`, `AdvanceLineAsync`, `MoveParticipantUpAsync`, `MoveParticipantDownAsync`, `RemoveParticipantAsync`, `ReAddRemovedParticipantAsync`, `DeleteRemovedParticipantAsync`, `PauseTimerAsync`, `ResumeTimerAsync`, `RestartTimerAsync` — assert three things:
   - a **wrong** token throws `UnauthorizedAccessException`
   - an **empty string** token throws `UnauthorizedAccessException` (it must fail closed, not be treated as "no token required")
   - the correct token succeeds
   Express the operations as a `Func<ILineService, string, string, Task>` list so one theory covers all eleven.
3. `RejectedCall_DoesNotMutateOrNotify`: attempt a `DeleteLineAsync` with a wrong token, assert it throws, then assert the line still exists **and** the notifier stub recorded zero notifications. A guard that runs after the mutation, or after the notify, would pass the exception assertion alone — this is what catches that.
4. `GetLineAsync_DoesNotLeakManageToken`: create a line, read it back with `GetLineAsync`, assert the returned `LineDto.ManageToken` is `string.Empty`. Without this the join view hands every visitor the keys.
5. `CreateLineAsync_ReturnsManageToken`: assert the created DTO's token is non-empty and at least 16 characters.
6. `UnauthorizedException_DoesNotContainToken`: catch the `UnauthorizedAccessException` from a wrong-token call and assert its `Message` contains neither the correct token nor the supplied one.
7. `MissingLine_ThrowsKeyNotFound`: a mutation against a nonexistent id throws `KeyNotFoundException`, not `UnauthorizedAccessException` — and `ValidateManageTokenAsync` for a nonexistent id returns `false` without throwing.
8. `AddParticipantAsync_RequiresNoToken`: joining succeeds with no token supplied, confirming the public join path was not accidentally locked down.
9. Abuse limits: `AddParticipant_Beyond200_Throws` (`InvalidOperationException`), `CreateLine_NameOver100_Throws` and `UpdateLine_DescriptionOver500_Throws` (`ArgumentException`), `CreateLine_IntervalOutOfRange_Throws` (`ArgumentOutOfRangeException` for both 30 seconds and 48 hours), and `AddParticipant_BlankName_Throws` (`ArgumentException` for null, `""`, and `"   "`).
10. `IdGeneratorTests`:
    - `GenerateUniqueId_Is8CharsFromAllowedAlphabet`: assert length 8 and that every character is in `ABCDEFGHJKMNPQRSTUVWXYZ23456789`. Explicitly assert the id contains none of `0`, `O`, `1`, `I`, `L`.
    - `GenerateUniqueId_IsDistinctAcross1000Calls`: generate 1000 ids and assert the distinct count is 1000. The original used a shared static `Random`, which is documented to return garbage under concurrent use.
    - `GenerateManageToken_IsUrlSafe`: assert length is at least 22 and that the token contains no `+`, `/`, or `=`, so it survives being placed in a URL path segment.
    - `GenerateManageToken_IsDistinctAcross1000Calls`.
    - `NormalizeId_TrimsAndUppercases`: `"  abcd1234  "` normalizes to `"ABCD1234"`, and normalizing an already-normalized id is a no-op.

Constraints:
- Do NOT weaken any assertion to make a test pass. If a guard is genuinely missing from the product code, report it in Handoff — do not add the guard here and do not skip the test.
- Do NOT modify anything under `HopInLine/`.
- Do NOT edit `SqliteDbFixture.cs` or `TestData.cs`; pass overrides instead.
- Do NOT use Moq, NSubstitute, or FluentAssertions. Hand-written stub plus xunit asserts only.
- Do NOT write position, timer, concurrency, or duration-formatting tests; TEST-REPO-001, TEST-CONC-001, and FMT-001 own those.
- Do not assert on the timing of the constant-time comparison. Timing assertions are inherently flaky; the code review covers that the correct API is used.
- Do not print a real manage token to test output.

Acceptance Criteria:
- `dotnet test` runs green with at least 25 new tests.
- Removing the guard from any single mutating method makes at least one theory row fail.
- Making `GetLineAsync` return the real token makes `GetLineAsync_DoesNotLeakManageToken` fail.
- Changing `AuthorizeAsync` to treat an empty token as valid makes the empty-token rows fail.
- The suite finishes in under 30 seconds and passes three consecutive runs.

Validation:
`dotnet build` completes with 0 errors and `dotnet test` reports 0 failures.

Handoff:
List every mutating method covered and confirm all three assertions pass for each. Report any missing guard or leaked token you found without fixing it, and state the total suite count.
