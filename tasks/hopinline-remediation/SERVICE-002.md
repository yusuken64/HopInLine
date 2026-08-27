Task ID:
SERVICE-002

Model:
haiku

Title:
Implement ILineService with manage-token authorization and abuse limits

Purpose:
There is currently no authorization of any kind. Every mutation is reachable by anyone who knows a line's 4-character id, and `/list-lines` publishes all of them. This task makes `LineService` the single enforcement point: the line id stays the shareable **join** code, and the `ManageToken` gates every mutation. It is also the natural place for abuse limits, because ASP.NET's `AddRateLimiter` middleware only covers page requests — it cannot see Blazor circuit invocations, so throttling has to live in the service.

Dependencies:
SERVICE-001, CONTRACT-001

Consumes:
CONTRACT-001: the `ILineService` interface — implement it exactly as written
SERVICE-001: the repaired `LineService` with its single-`NotifyAsync`-per-mutation discipline
IDGEN-001: `IdGenerator.NormalizeId`, `IdGenerator.GenerateManageToken`

Produces:
`LineService : ILineService` — the authorization contract consumed by VIEW-001, EDIT-001, START-001.

Files:
- `HopInLine/Data/Line/LineService.cs` (edit)

Requirements:
1. Declare `public class LineService : ILineService` and make the class satisfy the interface exactly as CONTRACT-001 published it. Add the `manageToken` parameter to each mutating method in the position the interface specifies.
2. Add a private guard called at the top of **every** mutating method, before any other work:
   ```csharp
   private async Task<Line> AuthorizeAsync(string lineId, string manageToken)
   ```
   It normalizes the id, loads the line, throws `KeyNotFoundException` if the line does not exist, compares the token, throws `UnauthorizedAccessException` on mismatch, and returns the line.
3. Compare tokens in **constant time**:
   ```csharp
   CryptographicOperations.FixedTimeEquals(
       Encoding.UTF8.GetBytes(stored), Encoding.UTF8.GetBytes(provided))
   ```
   Guard first against a null or empty stored token and against a null or empty provided token — both must fail closed, returning false rather than matching. Do not use `==`, `string.Equals`, or `SequenceEqual` for this comparison.
4. Implement `ValidateManageTokenAsync(string lineId, string manageToken)` returning `bool` using the same comparison. It must never throw for a wrong token or a missing line — it returns `false`. Pages call it to decide whether to render management controls.
5. `AddParticipantAsync(string lineId, string participantName)` takes **no** token — joining is public — and must build the `Participant` server-side via `ParticipantFactory.Create(...)`. It must never accept a caller-supplied `Participant`. The current `LineHub.AddParticipant` takes a whole entity including `Position`, `TurnCount`, `Removed`, `LineId`, and the `Line` navigation property, which lets a caller insert itself at the front of the queue or corrupt the graph. Reject a null, whitespace-only, or over-60-character name with `ArgumentException`.
6. Abuse limits, enforced here because middleware cannot see circuit calls:
   - **Participant cap:** in `AddParticipantAsync`, if the line already has 200 participants with `Removed == false`, throw `InvalidOperationException("This line is full.")`.
   - **Advance throttle:** in `AdvanceLineAsync`, reject a manual advance made less than 1 second after this line's `CountDownStart` by returning without advancing. This blocks a click-spam loop from racing the queue without adding shared mutable state.
   - **Server-side length enforcement:** in `CreateLineAsync` and `UpdateLineAsync`, reject a `name` longer than 100 or a `description` longer than 500 with `ArgumentException`. The `MaxLength` mappings protect the database; these protect the user from a silent truncation error.
   - **Interval bounds:** reject `autoAdvanceInterval` outside 1 minute..24 hours with `ArgumentOutOfRangeException`.
7. `GetLineAsync` takes no token and returns a `LineDto` with `ManageToken` set to `string.Empty`. **The read path must never leak the token to a page that did not supply it** — otherwise the join view hands every visitor the keys. `CreateLineAsync` is the sole exception: it returns the real token to its creator.
8. Preserve SERVICE-001's rule that every mutating method ends with exactly one `NotifyAsync`. The authorization guard runs before the mutation, so a rejected call must notify nobody.
9. Update every caller so the build stays green. `ViewLine.razor`, `EditLine.razor`, `StartLine.razor`, and `ListLines.razor` all call these methods and will not compile against the new signatures. Pass `string.Empty` as the token from any call site whose own task has not yet been run — VIEW-001, EDIT-001, START-001, and LIST-001 replace those placeholders with the real token. Add a `// TODO(VIEW-001)`-style comment on each placeholder naming the task that owns it.

Constraints:
- Do NOT edit `SQLLineRepository.cs` or `ILineRepository.cs`. REPO-001 and REPO-002 own them. Authorization belongs in the service, never the repository — the background scheduler calls the repository directly and has no token.
- Do NOT add ASP.NET Identity, cookies, claims, `[Authorize]`, or any authentication middleware. The design is deliberately accountless.
- Do NOT add `AddRateLimiter` to `Program.cs`. It cannot intercept Blazor circuit invocations, so it would give a false sense of coverage.
- Do NOT log the manage token, and do not include it in an exception message. It is a credential.
- Do NOT change the `LineHub` signature or delete it; HUB-001 does that.
- Beyond the placeholder token arguments and the TODO comments, make no other change to any `.razor` file.
- Do not weaken the guard to a "token is optional when empty" mode. An empty supplied token must fail.

Acceptance Criteria:
- `LineService` implements `ILineService` with no missing members.
- Every mutating method rejects an empty, null, or wrong token with `UnauthorizedAccessException` and performs no database write and no notification.
- A wrong token and a nonexistent line are distinguishable only as `UnauthorizedAccessException` vs `KeyNotFoundException`; neither response reveals the correct token.
- `GetLineAsync` returns `ManageToken == string.Empty`; `CreateLineAsync` returns a populated one.
- Adding a 201st active participant throws `InvalidOperationException`.
- A 101-character name is rejected before reaching the database.
- No occurrence of `==` or `string.Equals` is used to compare manage tokens.
- `dotnet build` succeeds with 0 errors.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
Paste the `AuthorizeAsync` guard and the constant-time comparison verbatim. List every `.razor` call site where you left a placeholder token and the task that owns each — VIEW-001, EDIT-001, START-001, and LIST-001 must clear all of them.
