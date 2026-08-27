Task ID:
EDIT-001

Model:
haiku

Title:
Give EditLine a detached edit model, working Cancel, confirmations, and inline errors

Purpose:
`EditLine.razor` binds its form inputs directly to a tracked EF entity. Because `AddDbContext` is scoped and a Blazor Server scope lasts the whole circuit, the same tracked instance is handed to the next page — so Cancel does not cancel, and the discarded edits show up in `ViewLine`. Delete fires immediately with no confirmation. Both failure paths navigate to `/not-found` and `/error`, neither of which is a Blazor route (`/Error` is the exception-handler Razor Page, and Blazor routing is case-sensitive), so the user gets the framework's generic dead-end. There is also a CS8073 warning at roughly line 82 — `line.AutoAdvanceInterval == null` is always false on a non-nullable `TimeSpan`, making the intended fallback dead code.

Dependencies:
CONTRACT-001, JS-001, UX-001

Consumes:
CONTRACT-001: `ILineService` — the method names and parameter order this page will call
JS-001: `window.hopInLine.confirm(message)`
UX-001: the `/not-found` route

Produces:
(nothing downstream consumes this)

Files:
- `HopInLine/Pages/Line/EditLine.razor` (edit)

Requirements:
1. Add a private `EditModel` class inside the `@code` block with plain settable properties mirroring the editable fields only: `Name`, `Description`, `AutoAdvanceLine`, `AutoReAdd`, `AutoRestartTimerOnAdvance`, `AutoAdvanceIntervalMinutes` (an `int`). Bind every form input to this model, **never to the entity or DTO**. This is what makes Cancel actually discard.
2. In `OnInitializedAsync`, load the line read-only and copy its values into a fresh `EditModel`. Do not retain the loaded entity in a field that the form touches.
3. Cancel navigates away and copies nothing back. Since the form only ever mutated `EditModel`, no explicit revert is needed — do not add one.
4. Save copies `EditModel` back through the service in one call and then navigates. Convert `AutoAdvanceIntervalMinutes` to a `TimeSpan` with `TimeSpan.FromMinutes(...)` at the boundary.
5. Fix the CS8073 at roughly line 82. `TimeSpan` is a non-nullable value type, so `line.AutoAdvanceInterval == null` can never be true. Replace the whole null-check with a real validity check on the minutes field: if `AutoAdvanceIntervalMinutes < 1`, treat it as invalid (see requirement 8) rather than silently substituting a default.
6. Replace both bad navigations:
   - the `/not-found` navigation stays as the string `/not-found` — UX-001 added that page, so it now resolves. Verify the string is exactly lowercase `not-found`.
   - the `/error` navigation is wrong. Replace it with an inline error message rendered on the page (`<div class="alert alert-danger" role="alert">@errorMessage</div>`) and do not navigate. A failed save must leave the user on their form with their input intact.
7. Add a confirmation before Delete (roughly line 55) using `await JS.InvokeAsync<bool>("hopInLine.confirm", "Delete this line permanently? Everyone in it will lose their place.")`. Proceed only when it returns true. Inject `IJSRuntime JS`.
8. Client-side bounds on the interval input: `min="1"` and `max="1440"`. It currently allows `max="999"` minutes with no lower bound. Also add a `step="1"`. If `AutoAdvanceIntervalMinutes` is outside 1..1440 on Save, set `errorMessage` and return without calling the service.
9. Add an `<h1>` as the first element of the page (`Edit line`), and give every input an associated `<label for="...">`. `App.razor`'s `<FocusOnNavigate Selector="h1">` currently matches nothing here.
10. Fix the heading order: if the page uses `<h3>`/`<h4>` under no `<h1>`, promote them so the outline is `h1` then `h2`, with no skipped level.

Constraints:
- Do NOT pass a manage token yet. SERVICE-002 adds the token parameters and START-001 changes the route; adding them here would break the build against the current `LineService` signatures.
- Do NOT change the page's `@page` route. START-001 moves this page off `/edit-line/{lineId}`.
- Do NOT use `EditForm`, `DataAnnotations`, or a validator. A plain form plus the two explicit checks above is sufficient and avoids a new model-validation dependency.
- Do NOT call `AsNoTracking()` from the Razor page directly — go through the existing `LineService` method. Tracking is the repository's concern, and REPO-001 already handles it.
- Do NOT edit `Index.razor`, `ViewLine.razor`, or `StartLine.razor`.
- Do NOT use `IJSRuntime` in `OnInitializedAsync` — with prerendering still enabled at this point in the sequence there is no JS interop available during the first render. Only call it from event handlers.
- Keep the existing `ShowOverlay`/`HideOverlay` `try/finally` blocks EMERG-001 added.

Acceptance Criteria:
- Editing the name, pressing Cancel, then opening the line shows the **original** name.
- Delete shows a browser confirm and aborts cleanly when dismissed.
- A save failure renders a red inline message and leaves the form populated; the URL does not change.
- An interval of 0 or 5000 is rejected with a message and no service call.
- CS8073 no longer appears in the build output.
- The page has exactly one `<h1>`, it is first, and no heading level is skipped.
- Every input has a `<label for>` whose target `id` exists.
- `dotnet build` succeeds with 0 errors and one fewer warning than before.

Validation:
`dotnet build` completes with 0 errors. Confirm CS8073 is gone from the warning list.

Handoff:
Paste the `EditModel` class. Confirm no form input binds to a `Line` or `LineDto`, and report the warning count before and after.
