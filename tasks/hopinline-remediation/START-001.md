Task ID:
START-001

Model:
haiku

Title:
Show the creator both the join link and the manage link, and move EditLine behind the token

Purpose:
The manage token exists but nothing hands it to the person who created the line. Without this task the creator has no way to reach their own management controls, and the whole access-control design is unusable. This task closes the loop: `StartLine` creates the line, then shows two clearly distinguished links — one to share, one to keep.

Dependencies:
SERVICE-002, VIEW-001, EDIT-001

Consumes:
SERVICE-002: `ILineService.CreateLineAsync(...)` returning a `LineDto` with a populated `ManageToken`
VIEW-001: the `/manage/{LineId}/{ManageToken}` route
EDIT-001: the repaired edit page

Produces:
(nothing downstream consumes this)

Files:
- `HopInLine/Pages/Line/StartLine.razor` (edit)
- `HopInLine/Pages/Line/EditLine.razor` (edit — route and token only)

Requirements:
1. In `StartLine.razor`, after a successful create, do **not** navigate immediately. Render a confirmation panel showing both links so the creator can copy the manage link before leaving the page:
   - **Share this link:** the absolute URL for `/view-line/{id}` — labelled as the link for people joining the queue.
   - **Keep this link:** the absolute URL for `/manage/{id}/{token}` — labelled with an explicit warning that anyone holding it can manage the line, and that it cannot be recovered if lost.
   Build absolute URLs with `NavigationManager.ToAbsoluteUri(...)` so they are copyable.
2. Add a "Go to my line" button on that panel navigating to the manage URL. Do not auto-navigate — an automatic redirect loses the manage link for anyone who does not think to copy it from the address bar.
3. Delete the unused `autoAdvanceInterval` field (CS0169 — assigned nowhere, read nowhere).
4. Add bounds to the interval input: `min="1"`, `max="1440"`, `step="1"`. It currently allows `max="999"` with no lower bound, and SERVICE-002 now throws `ArgumentOutOfRangeException` outside 1 minute..24 hours — so an out-of-range value would surface as an unhandled exception. Validate before calling the service and show an inline message instead.
5. Wrap the create call in `try/catch` for `ArgumentException` and `InvalidOperationException` and render the message inline (`<div class="alert alert-danger" role="alert">`). `InvalidOperationException` is what SERVICE-002 throws when id allocation is exhausted.
6. Add an `<h1>Start a line</h1>` as the first element, and give every input a `<label for>`. `App.razor`'s `<FocusOnNavigate Selector="h1">` matches nothing on this page today.
7. In `EditLine.razor`, change the route from `/edit-line/{lineId}` to:
   `@page "/manage/{LineId}/{ManageToken}/edit"`
   Add `[Parameter] public string? ManageToken { get; set; }` and pass it to every `ILineService` call, replacing the `string.Empty` placeholders SERVICE-002 left with a `// TODO(START-001)` comment. Remove those comments as you clear them.
8. In `EditLine.razor`, if `ValidateManageTokenAsync` returns false in `OnInitializedAsync`, render an "you do not have permission to edit this line" message and no form. Do not navigate.
9. Update every link that pointed at `/edit-line/{id}` to the new route including the token. Search the repository for `edit-line` and fix every hit. `ViewLine.razor` has one — update it, since VIEW-001 has already run and the file is stable.

Constraints:
- Do NOT display the manage token anywhere except on `StartLine`'s post-create panel and inside the URL bar of a manage page. Never render it on `/view-line/{id}`, and never put it in a page `<title>`.
- Do NOT email, log, or persist the token anywhere beyond the `Lines` table.
- Do NOT add a "recover my manage link" feature. There is no account to authenticate a recovery against; offering one would be a bypass, not a feature.
- Do NOT change `ILineService` or `LineService.cs`; SERVICE-002 owns them.
- Do NOT edit `ViewLine.razor` beyond the `edit-line` link fix in requirement 9.
- Do not auto-copy to the clipboard without a user gesture — browsers block it and it fails silently.

Acceptance Criteria:
- Creating a line shows both links, visibly distinguished, with the manage link carrying a warning.
- The manage link opens the line with all controls; the share link opens it with none.
- `/manage/{id}/{token}/edit` opens the edit form; the same URL with a wrong token shows a permission message and no form.
- No route matching `/edit-line/` remains anywhere in the repository.
- An interval of 0 or 5000 shows an inline message and makes no service call.
- CS0169 for `autoAdvanceInterval` no longer appears in the build output.
- `StartLine.razor` has exactly one `<h1>`, first, and every input has a working `<label for>`.
- No `// TODO(START-001)` or `string.Empty` placeholder token remains in `EditLine.razor`.
- `dotnet build` succeeds with 0 errors and two fewer warnings than before.

Validation:
`dotnet build` completes with 0 errors. Confirm CS0169 is gone from the warning list.

Handoff:
State the two exact URL shapes shown to the creator, confirm every `edit-line` reference was updated, and report the warning count before and after.
