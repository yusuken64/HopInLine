Task ID:
CARD-001

Model:
haiku

Title:
Extract participant card CSS to scoped stylesheets and make the cards responsive and accessible

Purpose:
`ParticipantCard.razor` and `RemovedParticipantCard.razor` each embed a `<style>` block in their markup. Because they render once per participant, a 20-person queue emits 20 duplicate `<style>` elements, each globally redefining `.participant-card`. The two CSS blocks are byte-identical. The fixed `height: 85px` clips long names, and the absolutely-positioned button row plus `margin-bottom: 50px` overlaps the page title on a narrow screen — a phone is the likely primary device for a queue app. The repeated buttons also announce as the same three unlabelled controls N times to a screen reader.

Dependencies:
FACTORY-001

Consumes:
FACTORY-001: `ParticipantFactory.GetForegroundColor(string backgroundHex)`

Produces:
`wwwroot/css/participant-card.css` — the shared card stylesheet. Nothing else consumes it programmatically.

Files:
- `HopInLine/wwwroot/css/participant-card.css` (new)
- `HopInLine/Pages/Line/ParticipantCard.razor` (edit)
- `HopInLine/Pages/Line/RemovedParticipantCard.razor` (edit)
- `HopInLine/Pages/_Layout.cshtml` (edit — one `<link>` tag)

Requirements:
1. Create `wwwroot/css/participant-card.css` containing the union of the two existing `<style>` blocks, deduplicated. Both files define the same rules, so copy from `ParticipantCard.razor` and then add only the rules unique to `RemovedParticipantCard.razor`.
   The project already uses scoped `.razor.css` for `MainLayout` and `NavMenu`, but scoped CSS cannot be shared between two components, which is the whole point here — so use one plain stylesheet linked once instead.
2. Delete the `<style>` block from **both** `.razor` files entirely.
3. Add `<link rel="stylesheet" href="css/participant-card.css" />` to `Pages/_Layout.cshtml` in the existing `<head>`, immediately after the current `site.css` link.
4. In the shared stylesheet, replace the fixed sizing with a flexible layout:
   - remove `height: 85px` — use `min-height: 85px` so long names grow the card instead of being clipped
   - remove `margin-bottom: 50px`
   - make `.participant-card` a flex container with `flex-wrap: wrap`, `gap: 0.5rem`, and `align-items: center`
   - change the button row from `position: absolute` to a normal flex child with `margin-left: auto`, so it sits at the end of the row and wraps below on a narrow viewport
   - add `word-break: break-word` to the name element so a long unbroken name cannot overflow
5. In `ParticipantCard.razor`, set the text colour from the background instead of leaving it at the browser default. The generated background colours are random, so black text on a dark card is currently unreadable. Apply both in the inline style attribute:
   `style="background-color:@Participant.Color; color:@ParticipantFactory.GetForegroundColor(Participant.Color)"`
   Do the same in `RemovedParticipantCard.razor`.
6. Add an `aria-label` to every repeated button in both components, naming the participant so a screen reader distinguishes them:
   - up → `aria-label="Move @Participant.Name up"`
   - down → `aria-label="Move @Participant.Name down"`
   - remove → `aria-label="Remove @Participant.Name from the line"`
   - re-add → `aria-label="Add @Participant.Name back to the line"`
   - delete → `aria-label="Permanently delete @Participant.Name"`
   Apply the labels that match the buttons each component actually has; do not add buttons that are not already there.
7. If either component uses an `<h3>` or `<h4>` for the participant name, change it to a `<span>` or `<div>`. A repeated card is not a document section, and the current markup produces N sibling headings at the same level with no parent section, which breaks the heading outline.

Constraints:
- Do NOT change any `@onclick` handler, parameter, or event callback. This task is presentation only — the components' public API and behavior must be identical.
- Do NOT add an `<h1>`; each page's own task owns its `<h1>`.
- Do NOT convert these to scoped `.razor.css` files. Two components must share one stylesheet.
- Do NOT edit `site.css`, `MainLayout.razor.css`, or `NavMenu.razor.css`.
- Do not touch `ViewLine.razor`; VIEW-001 owns it.
- Do not remove the colour randomisation.

Acceptance Criteria:
- Neither `.razor` file contains a `<style>` element.
- Rendering a line with 20 participants produces exactly one `participant-card.css` request and zero inline `<style>` blocks.
- A participant named with 60 characters renders fully without clipping, and the card grows in height.
- At a 375px-wide viewport the button row wraps below the name and does not overlap the page heading.
- Every repeated button has a unique `aria-label` containing the participant's name.
- A card with a dark random background renders white text; a light background renders black text.
- `dotnet build` succeeds with 0 errors.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
Report the final `participant-card.css` contents and confirm both `<style>` blocks were removed. Note any CSS rule that existed in only one of the two original blocks.
