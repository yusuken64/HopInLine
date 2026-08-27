Task ID:
FACTORY-001

Model:
haiku

Title:
Make ParticipantFactory static and thread-safe; add contrast-safe foreground colour helper

Purpose:
`ParticipantFactory` is registered as a DI singleton but `LineService.cs:14` does `new ParticipantFactory()` — the registration is dead. It also shares a static `Random` across threads and allocates a fresh `new Random()` on every colour call. Additionally, participant cards render a random pastel background with no defined text colour, so contrast is unverified; CARD-001 needs a helper to fix that.

Dependencies:
(none)

Consumes:
(nothing)

Produces:
Colour contract consumed by CARD-001:
- `ParticipantFactory.GetForegroundColor(string backgroundHex)` → `"#000000"` or `"#FFFFFF"`

Files:
- `HopInLine/Data/Line/ParticipantFactory.cs` (edit)

Requirements:
1. Convert the instance method `Create(string name = "Waiter")` to a `static` method. Leave the CLASS non-static (do not add the `static` keyword to the class declaration) — `LineService.cs:14` still does `new ParticipantFactory()` and `Program.cs:9` still registers it, and this task is not allowed to edit either file. SERVICE-001 and PROG-002 remove those two lines; the class can be sealed static in WARN-001.
2. Delete the `private static readonly Random _random` field and the `Random random = new Random()` local inside `GenerateUniqueColor()`. Use `Random.Shared` (thread-safe) everywhere instead.
3. Add `public static string GetForegroundColor(string backgroundHex)`:
   - Parse a `#RRGGBB` string (tolerate a missing `#`; return `"#000000"` for any unparseable input).
   - Convert each channel to a 0-1 value, linearise it (`c <= 0.03928 ? c/12.92 : Math.Pow((c+0.055)/1.055, 2.4)`), and compute relative luminance `0.2126*R + 0.7152*G + 0.0722*B`.
   - Return `"#000000"` when luminance > 0.179, otherwise `"#FFFFFF"`. This threshold is the standard WCAG crossover point for choosing black vs white text.

Constraints:
- Keep the existing `NewParticipantID`, `GenerateUniqueColor`, and `HslToRgb` behaviour and signatures — `ViewLine.razor` calls `NewParticipantID()` and `GenerateUniqueColor()` statically today and must keep compiling.
- Do NOT remove the `AddSingleton<ParticipantFactory>()` registration in `Program.cs`; PROG-002 owns that file.
- Do NOT edit `LineService.cs` to remove `new ParticipantFactory()`; SERVICE-001 owns that file. Making the class static would break that line, so instead leave the class non-static ONLY IF required to keep the build green — prefer: make the class static AND leave a note in Handoff that `LineService.cs:14` and `Program.cs:9` must be cleaned up by SERVICE-001 and PROG-002. If making it static breaks the build, keep the class non-static, make all members static, and report that.

Acceptance Criteria:
- No `Random` instance is allocated anywhere in the file; only `Random.Shared` is used.
- `GetForegroundColor("#FFFFFF")` returns `"#000000"`; `GetForegroundColor("#000000")` returns `"#FFFFFF"`; `GetForegroundColor("nonsense")` returns `"#000000"`.
- `GenerateUniqueColor()` still returns a `#RRGGBB` string.
- Called concurrently from 100 tasks, no exceptions and no all-zero colours.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
State whether the class ended up static, and list any file outside this task that still references `new ParticipantFactory()` so the owning task can clean it up.
