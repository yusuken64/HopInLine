Task ID:
IDGEN-001

Model:
haiku

Title:
Cryptographic line ID and manage token generation

Purpose:
Replace a 4-character line ID drawn from a non-thread-safe shared `Random` (456,976 possible values, brute-forceable) with crypto-random identifiers, and add the manage-token generator the access-control work depends on.

Dependencies:
(none)

Consumes:
(nothing)

Produces:
ID/token generation contract consumed by SERVICE-001 and SERVICE-002:
- `IdGenerator.GenerateUniqueId()` → 8-char unambiguous Base32 join code
- `IdGenerator.GenerateManageToken()` → 128-bit URL-safe secret
- `IdGenerator.NormalizeId(string?)` → trimmed, upper-cased, ambiguity-folded join code

Files:
- `HopInLine/Data/Line/IdGenerator.cs` (rewrite)

Requirements:
1. Replace the static `Random` field with `System.Security.Cryptography.RandomNumberGenerator`. The current shared `Random` instance is documented to return corrupted results when used concurrently, which this app does on every request.
2. `GenerateUniqueId()` returns 8 characters from the unambiguous alphabet `ABCDEFGHJKMNPQRSTUVWXYZ23456789` (no `0`/`O`, no `1`/`I`/`L`). Use `RandomNumberGenerator.GetInt32` or rejection sampling — do not use plain modulo over raw bytes, which biases the distribution.
3. `GenerateManageToken()` returns a URL-safe Base64 encoding of 16 crypto-random bytes (128 bits), with `+`→`-`, `/`→`_`, and `=` padding stripped.
4. `NormalizeId(string? input)`: return `string.Empty` for null or whitespace; otherwise return `input.Trim().ToUpperInvariant()`. Nothing else — do not attempt to fold look-alike characters.
5. Make the class `static`.

Constraints:
- Do not change any call site. `GenerateUniqueId()` keeps its name and signature so `LineService` continues to compile.
- Do not add a uniqueness/database check here — that stays in `LineService`.
- No `Random` or `Guid.NewGuid()` for either identifier.

Acceptance Criteria:
- `IdGenerator` is a static class with no `Random` field.
- `GenerateUniqueId()` returns 8 characters, all drawn from the 31-character alphabet, and never returns the same value twice across 10,000 successive calls.
- `GenerateManageToken()` returns a 22-character URL-safe string containing no `+`, `/`, or `=`.
- `NormalizeId("  abcd1234 ")` returns `"ABCD1234"`; `NormalizeId(null)` returns `""`.
- Calling all three concurrently from 100 tasks produces no duplicates and no exceptions.

Validation:
`dotnet build` completes with 0 errors.

Handoff:
Report the exact alphabet used, the generated ID length, and the three public method signatures.
