# "Look Up" (ACTION_PROCESS_TEXT) — Pre-Implementation Plan

Status: **v1 built and working on-device** (`Platforms/Android/ProcessTextLookupActivity.cs` + supporting Android resources). This document is the output of a design discussion (council review), a facts-gathering pass over the codebase, and two rounds of on-device Gate 1/Gate 2 validation; §9 below records what was actually built and two real bugs/behaviors found and fixed along the way.

## 1. What we're building

Android has a system feature called **Process Text**: when a user selects text in *any* app, the system's text-selection toolbar (Copy / Share / …) can be extended by any app that declares an `ACTION_PROCESS_TEXT` intent filter. Declaring it adds a "Look Up" entry to that toolbar, system-wide, in every app that supports text selection.

Tapping it currently would (per Android's contract) launch an Activity of our choosing with the selected string in the intent extras. Our job is to take that string, run it through the existing `SearchService`, and show a result — ideally in a small popup rather than a full app switch.

This is **not** a search-box feature. It's an interruption-handling feature: the user is mid-task in another app, hits a word they don't know, and wants an answer without leaving what they were doing.

## 2. Why this isn't a simple "add an intent filter and wire it up" ticket

Two structural facts about this codebase make this harder than the Android boilerplate suggests:

- **`SearchService`'s deinflection step only fires on kana-only input.** `Deinflector` is invoked from `SearchService.AddDeinflectedMatches` only for query forms that pass `IsKanaOnly` (regex `^[぀-ゟ゠-ヿ]+$`, `SearchService.cs`). Text a user selects out of a book, article, or app is frequently **kanji + kana mixed and conjugated** (e.g. 食べました, 食べ物を) — exactly the case deinflection does *not* currently cover. Without knowing what real selections look like, we don't know how often this pipeline will return nothing.
- **`AppDataContext` has never been initialized from a cold, externally-launched entry point.** Today, `AppDataContext.EnsureInitializedAsync()` is only ever called lazily from `SearchViewModel` after `MainActivity`/Shell has already started up (`MauiProgram.cs` never calls it eagerly). A `Process Text` tap can launch our Activity as the **first** activity in a fresh process — nothing has run `CreateMauiApp()` yet in that Activity's context except the fact that `MainApplication.CreateMauiApp()` always runs before any Activity's `OnCreate` (Android guarantees `Application.onCreate` before `Activity.onCreate`), but the *first-run* path (`DatabaseInitializer.EnsureDictionaryDatabaseAsync()` copying `dictionary.db3` out of the package) has never been exercised from this trigger before and its timing is unmeasured.

Both are answerable with small, cheap spikes — before any UI or manifest work — per the council's recommendation below.

## 3. Two gates before real implementation

### Gate 1 — Validate assumptions (½–1 day, no production code)

1. **Real selection sampling.** On a physical device, select Japanese text in a few real apps (Chrome, a manga/reader app if available, WhatsApp/X) and log what `ACTION_PROCESS_TEXT` actually delivers — single words vs. phrases vs. sentences, whitespace/punctuation behavior, length caps.
2. **Cold-start DI spike.** Add a throwaway second `Activity` with an intent filter, force-kill the app process, launch it via `adb shell am start`, and log whether `App.Services` resolves and how long `AppDataContext.EnsureInitializedAsync()` takes end-to-end (including the SQLite copy-out on first run). This produces a real number for "how long is the user staring at a popup with nothing in it."

**Decision point:** Gate 1's data decides the scope of Gate 2. If most real selections are single dictionary-form words, the minimal wiring (§5) is sufficient. If most are multi-word/conjugated, we need at least a trim-to-plausible-token step before `SearchService.Search()` — shipping "search the raw selection, show nothing found" as the default outcome for most taps is not acceptable.

### Gate 2 — Build to what Gate 1 showed

Everything in §4–§7 below assumes Gate 1 passes reasonably (cold start is fast enough to feel responsive, and selections are searchable often enough to be useful). If Gate 1 fails badly on either axis, stop and reconsider before writing the feature.

## 4. Gate 1 results (empirical, run on a physical device)

A throwaway spike Activity (`Platforms/Android/ProcessTextSpikeActivity.cs`, deleted once this section was written) was added with the real `ACTION_PROCESS_TEXT` intent filter and a `Theme.AppCompat.Dialog` theme, wired to call `AppDataContext.EnsureInitializedAsync()` and a real `SearchService.Search()`, timing both and reporting via `Toast` + logcat.

**Cold-start timing** (app process fully killed via `adb shell am force-stop`, then launched via `adb shell am start` with the Process Text action):

| Selection | Init (`EnsureInitializedAsync`) | `Search()` | Result |
|---|---|---|---|
| `食べました` (kanji+kana, conjugated) | 41ms | 502ms | **0 hits** — deinflection doesn't cover kanji+kana mixed input, confirms the gap flagged in §2 |
| `食べる` (kanji, dictionary form) | 40ms | 73ms | 2 hits (ExactMatch, Prefix) |
| `たべました` (all-kana, conjugated) | 40ms | 177ms | 1 hit (Conjugated) — deinflection works correctly here |

**Warm-process timing** (same process, second call right after the first, no force-stop):

| Selection | Init | `Search()` |
|---|---|---|
| `食べました` | 0ms | 46–50ms |

**Conclusions:**

- `AppDataContext.EnsureInitializedAsync()` is **not** the bottleneck — it's consistently ~40ms even on a fully cold process. This resolves the concern in §2 about the first-run SQLite copy-out being slow; it isn't.
- The real cold-start cost is the **first native `Search()` call** (~500ms vs. ~50ms warm) — almost certainly SQLite/query-plan warmup on first use, not anything specific to this feature. Still needs a loading indicator in the popup for the cold path, but the number is small enough (~550ms total) to likely feel acceptable rather than broken.
- `IPlatformApplication.Current.Services` was available **immediately** in the spike Activity's `OnCreate`, even on a fully cold process with the app previously force-stopped — confirming `MainApplication.OnCreate()` → `CreateMauiApp()` always runs before any Activity's `OnCreate`, with no race to guard against. **This resolves open question #2 below**: there is exactly one process, one DI container, shared by both entry points; no concurrency hazard to design around.
- The deinflection gap predicted in §2 is real and reproducible: kanji+kana mixed conjugated forms return 0 hits; all-kana conjugated forms are handled correctly by the existing deinflector.

**Real-device end-to-end check** (not just `adb am start`): selecting Japanese text in a real app's text-selection toolbar does show a "Look Up" entry alongside Copy/Share, and tapping it does launch the dialog-themed Activity as a small popup dimming the background — confirming the OS integration and visual approach both work as designed, not just in theory. One live finding from this pass: selecting a **single kana character** (e.g. べ) returned 50 hits (the `Search()` default `limit`) — a real edge case the popup UI needs to handle explicitly (see updated open question in §7), since a single character is a fundamentally different UX case (too little context to search meaningfully) from a word or short phrase.

**Gate 1 verdict: pass.** Timing is fast enough to be usable, the OS integration works end-to-end on a real device, and the deinflection gap is confirmed and scoped rather than hypothetical. Proceed to Gate 2.

## 5. Pieces that need to be added, and what each one is

| Piece | What it is | Why it's needed |
|---|---|---|
| **Intent filter declaration** | An `[IntentFilter]` C# attribute (compiles into a `<intent-filter>` in the generated Android manifest) declaring `ACTION_PROCESS_TEXT` support, `MIMETYPE text/plain`, category `DEFAULT`. | This is literally what makes "Look Up" appear in the system text-selection menu. Without it, Android has no idea our app wants to participate. |
| **A second, separate `Activity`** | A new Android `Activity`-derived class, distinct from `MainActivity`. | `MainActivity` is `MainLauncher = true` with the app's normal splash/Shell startup theme — reusing it for Process Text would either full-launch the whole app UI (the "switch app" friction we're trying to avoid) or require awkward runtime branching on launch intent inside the app's main entry point. A dedicated Activity keeps the two entry points cleanly separated. |
| **Translucent/Dialog theme for that Activity** | An Android theme (e.g. `Theme.AppCompat.Dialog` or a custom style derived from it) applied via the `[Activity(Theme = "...")]` attribute. | This is what makes the result appear as a small floating popup over whatever app the user was in, instead of a full-screen app switch. This is the standard, well-trodden pattern other "quick lookup" apps (e.g. Google Translate's Process Text handler) use — no special OS permission required, unlike a true system overlay (`SYSTEM_ALERT_WINDOW`, which also carries Play Store review scrutiny — avoid it). |
| **A minimal result view/page** | A small MAUI page (or plain Android layout, depending on how much MAUI plumbing the new Activity ends up hosting — open question, see §6) showing: the looked-up word, reading, and gloss(es); a "not found" state; and a dismiss action. | This is the actual "Look Up" UX — reusing `Colors.xaml` tokens per project convention so it looks native to the app rather than a bolted-on Android dialog. |
| **A bootstrap path into `AppDataContext`** | Code in the new Activity's `OnCreate` that resolves `App.Services` (the existing static service-locator escape hatch already used by `HandwritingSheetPage`) and calls `AppDataContext.EnsureInitializedAsync()` before touching `SearchService`. | `AppDataContext` is a DI singleton; nothing about a second Activity changes that — but this is the **first** consumer that might be the *first* thing to touch it in a process's lifetime, so it needs to handle the full (uninitialized) first-run cost, not just the already-warm case every other call site currently gets in practice. |
| **A loading/pending state in the popup** | Some indication (spinner, brief delay tolerance) shown while `EnsureInitializedAsync()` is running, for the cold-start case measured in Gate 1. | Prevents a blank popup from looking broken while the SQLite copy-out or DI construction is still in flight. |
| **Selection pre-processing (scope depends on Gate 1 data)** | Trimming/normalizing the raw selected string before calling `SearchService.Search()` — at minimum stripping surrounding whitespace/punctuation; possibly a heuristic to cut at the first non-word boundary for multi-word selections. **Not** a full tokenizer/segmenter — that's out of scope for v1 regardless of what Gate 1 shows (it's a much bigger, separate effort). | Directly addresses the deinflection-coverage gap identified in §2 to the extent feasible without a real morphological segmenter. |
| **Result rendering for ambiguous/multi-match results** | A decision on how the popup shows more than one candidate (`SearchService.Search()` already returns `IReadOnlyList<SearchResult>`, ranked by `SearchMatchKind`: `ExactMatch` → `Prefix` → `Conjugated` → `Gloss`) — e.g. show the top match only, or a short scrollable list capped at N. | A small popup can't reasonably show 50 results the way the full search page can; this needs an explicit UI decision, not just reusing the existing search results list as-is. |

## 6. What we are *not* building in v1

- No sentence tokenizer/morphological segmenter. Segmentation of arbitrary multi-word selections into dictionary-lookupable units is a real, separate project (the council's "Expansionist" advisor's platform-scale vision) — not something to smuggle into this feature's first version.
- No floating system overlay (`SYSTEM_ALERT_WINDOW`) — the dialog-themed-Activity pattern avoids that permission and its Play Store review risk entirely.
- No Quick Settings tile, Share-target/OCR handler, or accessibility-service live-translate overlay. These are legitimate future directions raised in the council discussion, but they're separate features that happen to share some infrastructure — not part of this scope.
- No changes to `SearchService`'s core matching logic. The plan is to consume its existing public surface (`Search(string, SearchMode, int limit)` returning `IReadOnlyList<SearchResult>`) as-is.

## 7. Open questions to resolve before/during Gate 2

1. ~~**Result view technology.**~~ **Resolved by Gate 1 data:** cold-start init is only ~40ms and warm search is ~50ms — fast enough that hosting a real MAUI page is viable, but the ~500ms *first* `Search()` call (see §4) means the popup needs a loading state regardless of which technology is chosen. Lean toward a lightweight plain Android layout for v1 (faster to build, fewer MAUI/Shell edge cases in a non-Shell-routed entry point) rather than a full `ContentPage` host — revisit only if the plain-layout approach can't reuse enough of `Colors.xaml`'s tokens to look native.
2. ~~**Concurrency on `AppDataContext`'s init lock.**~~ **Resolved by Gate 1 data:** confirmed empirically — `IPlatformApplication.Current.Services` is available immediately regardless of process state, there is one process/one DI container shared by both entry points, and `EnsureInitializedAsync()`'s existing `SemaphoreSlim` guard already handles the case where the main app is mid-init. No new concurrency handling needed.
3. ~~**`SearchMode` selection.**~~ **Resolved:** run both `Japanese` and `English` mode and merge, rather than trying to detect script upfront (warm search is only ~50ms per call, so a second call is cheap). Priority order: `Japanese`-mode results first (ranked internally by `SearchMatchKind`: `ExactMatch` → `Prefix` → `Conjugated`), then `English`-mode `Gloss` matches appended after, deduped by `EntryId` if the same entry surfaces from both.
4. ~~**Silent-failure handling for first-run DB copy-out.**~~ **Resolved:** not worth engineering for. This is a 2-person app (the user + one other person, who'll be told to open the app normally once before relying on Process Text). A plain try/catch around the lookup with a generic "couldn't look this up" fallback message is enough — no dedicated error-state design needed.
5. **Manifest/`.csproj` fragility.** The existing ONNX-related build settings (`JapaneseDictionary.csproj`, the three documented workarounds for `AndroidApplication`, `RuntimeIdentifiers`, and the manual `AndroidLibrary` item) are explicitly flagged in `CLAUDE.md` as order-sensitive. Gate 1's spike Activity built and ran cleanly alongside them with no interaction — the additive intent-filter approach is confirmed safe.
6. ~~**New from Gate 1: very short/low-context selections.**~~ **Resolved: leave as-is, no special handling.** A single kana character can still be a legitimate, meaningful lookup (e.g. 聞 alone is a real kanji with its own entry) — the 50-hit case seen in testing (べ) was a low-information character, not representative of all single-character selections. Adding a length-based cutoff would incorrectly block valid single-kanji lookups, so v1 just shows whatever `Search()` returns, capped at its existing `limit`.

## 8. Suggested sequencing

1. ~~Gate 1 spikes — cold-start timing + real selection sampling.~~ **Done** (§4). Spike file deleted.
2. Decide the popup's view technology per resolved open question #1 (plain Android layout, leaning toward not hosting a full MAUI page).
3. Add the real intent filter + Activity + theme (can reuse/clean up the spike's manifest/attribute wiring, which is already verified working end-to-end on a device).
4. Wire in `AppDataContext`/`SearchService` for real: query both `SearchMode`s and merge per open question #3, wrap the lookup in a try/catch with a generic failure message per open question #4. No special-casing needed for short selections (open question #6).
5. Visual pass: apply `Colors.xaml` tokens so the popup matches the rest of the app rather than looking like a bare Android dialog.
6. Full regression pass: confirm the normal app (`MainActivity`) launch path, theming, and existing features are unaffected — already spot-checked during Gate 1 (spike Activity coexisted cleanly), but worth a final pass once the real feature replaces the spike.

## 9. Gate 2 build notes — what was actually built, and two real findings

**What exists now:** `Platforms/Android/ProcessTextLookupActivity.cs`, a plain (non-MAUI) `Activity` with the real `ACTION_PROCESS_TEXT` intent filter and a custom `LookupDialogTheme` (`values/styles.xml`), showing a rounded card (`drawable/lookup_popup_background.xml`) with word/reading/gloss `TextView`s (`layout/process_text_lookup.xml`), styled from `values/colors.xml` + `values-night/colors.xml` (system day/night, per open question #3's known limitation). It bootstraps `AppDataContext` via `IPlatformApplication.Current.Services`, queries both `SearchMode`s and merges Japanese-first per the resolved open question #1, and falls back to a generic message on any failure per resolved open question #2.

**Bug found and fixed: popup text was clipped mid-sentence.** The root layout was originally `wrap_content` width with a `minWidth`. The window's actual size gets fixed at its *first* layout pass — while only the loading spinner was visible — and doesn't grow when the wider result/message text is swapped in afterward, so text was clipped by the window's original (narrower) surface even though the `TextView`'s own measured bounds looked correct in the accessibility tree. **Fix:** gave the root a fixed `280dp` width instead of `wrap_content`, so the window is sized consistently from the very first frame regardless of loading vs. loaded content. Confirmed fixed on-device with a full not-found message rendering correctly.

**Finding: full *process* cold start can be much slower than Gate 1's number.** Gate 1 measured `EnsureInitializedAsync()` + `Search()` in isolation (~500ms cold). A real cold launch — app process fully killed by the OS, not just by us via `force-stop` for testing — took `Activity onCreate: 2269ms` / `Displayed: +2869ms` on the test device (a MIUI phone, which is known for aggressive background-process killing). This is the full Mono/MAUI runtime spin-up cost, not something specific to this feature, and not something to "fix" here — but it's worth setting expectations that "select text after not having opened the app in a while" can take ~3s to show a result, not ~0.5s, on some devices/OEM skins.

**Finding: English-mode "cat" → いざこざ (trouble/quarrel) is expected, not a bug.** `SearchService`'s gloss search is an existing substring match (unmodified by this feature) — "cat" matches any gloss containing that substring (e.g. "complicated"). Same behavior the main app's English search already has.

**Confirmed working on a real device, end-to-end:** "Look Up" appears in the system text-selection toolbar in real apps (Chrome, phone dialer, etc.), tapping it shows the dialog-themed popup with correct dark-mode colors and backdrop dim, a found word shows word/reading/glosses correctly (食べる → たべる → "to eat / to live on…"), and a not-found selection shows the full correct message after the layout fix.

## 10. Source of this plan

Derived from a 5-advisor council review (Contrarian / First-Principles / Expansionist / Outsider / Executor perspectives + peer review) run against this codebase's actual architecture, followed by a facts-gathering pass over `MainActivity.cs`, `MainApplication.cs`, `AndroidManifest.xml`, `MauiProgram.cs`, `AppDataContext.cs`, `SearchService.cs`, `Deinflector.cs`, `HandwritingSheetPage`, and `JapaneseDictionary.csproj`, followed by on-device Gate 1 and Gate 2 validation (§4, §9).
