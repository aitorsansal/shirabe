# Handoff: Japanese Dictionary — Search, Kanji Drawing & Word Detail

## Overview
Phone UI for a Japanese dictionary app on top of the existing `JapaneseDictionary.Core` layer (JMdict-backed `DictionaryRepository`, `DictionaryEntry`, `KanjiInfo`, `ExampleSentence`, `WordClass`). Three screens: search (text search across JP/kana/rōmaji/English, with a de-inflection fallback), a handwriting-input sheet that returns candidate kanji, and a word detail page (senses, example sentences, kanji breakdown with stroke order, conjugation table, related compounds).

## About the Design Files
The bundled file (`Japanese Dictionary.dc.html`) is a **design reference built in HTML**, not production code. It is a self-contained prototype (uses a small in-house templating runtime, not React/Vue) built only to communicate layout, states, and interactions. The target app is **.NET MAUI (C#/XAML)** — the task is to **recreate this design as MAUI XAML pages/controls** bound to the real `JapaneseDictionary.Core` data layer, following MAUI idioms (DataTemplates, CollectionView, Shell navigation, MVVM) rather than porting any HTML/CSS/JS.

## Fidelity
**High-fidelity.** Colors, type, spacing, and copy in the file are final; recreate them pixel-for-pixel using MAUI's styling system (Styles/ResourceDictionaries), not ad hoc literals.

## Design tokens
- Background: `#f3f2f2` (screens), `#eae9e9` (hover/pressed fill), white `#ffffff` (input fields, kanji tile, candidate chips)
- Ink: `#201e1d` (primary text/borders), `#605d5d` (secondary/labels), `#7d7979` / `#9b9797` (tertiary/meta), `#444141` (gloss body text)
- Accent: `#ec3013` (primary action, active tab, common-word badge, highlighted example text), pressed state `#dd2b0f`, deep accent for reading annotations `#ae1800`
- Rules: 2px solid `rgba(32,30,29,.4)` for major dividers (below header, above tab bar); 1px `rgba(32,30,29,.18)` for row separators
- Type: **Archivo** (400/500/600/700/800) for all UI text and labels; **Noto Serif JP** (400/600/700) for every Japanese-language string (headword, kana, kanji, example sentences) — this pairing is what gives Japanese text visual weight against the UI chrome. No radius anywhere (square corners throughout, matching the Modernist design system this was built against).
- Spacing: 16px screen gutters, 12–14px row padding, 44px minimum tap target on every button/row

## Screens

### 1. Search (default screen)
- **Header**: "辞書 Shirabe" wordmark (Noto Serif JP 22px + Archivo uppercase label) left, "Offline · JMdict" meta right; 2px rule below.
- **Search bar**: bordered input (search icon, placeholder swaps by mode — `食べる · たべる · taberu` vs `to eat, book, school…`), a clear (×) button that appears once text is entered, and a dark square button (pencil/draw icon) that opens the handwriting sheet.
- **Mode toggle**: two-segment control — "日本語 · かな · rōmaji" / "English" — switches placeholder + which field the query matches against.
- **Recents** (shown when query is empty): last 6 opened words as pill buttons in Noto Serif JP, plus a short "Try" hint block.
- **Result rows** (shown once there's a query): reading in small accent Archivo above the headword; headword large in Noto Serif JP 27px with an optional red "common" badge; part-of-speech tag chips; a 2–3-sense gloss line; optional "de-inflected from …" note when the match came through the conjugation-stripping fallback; chevron affordance; row opens the Detail screen.
- **Empty state**: "No matches" message + a "Draw the kanji" CTA button that opens the same handwriting sheet.

### 2. Handwriting sheet (modal, slides up from bottom over Search)
- Backdrop tap or "Done" button closes it.
- Header: "Handwriting" kicker + "Draw the character" title + Done button.
- **Canvas**: 300×300 logical square, white, thin dashed center crosshair guides, freehand strokes rendered as smooth paths (stroke width configurable, default 9px, in `#201e1d`); placeholder copy "Draw here — stroke by stroke" shown when empty.
- **Controls row**: Undo (removes last stroke), Clear (removes all), and a stroke counter tile.
- **Candidates strip**: horizontally scrolling row of matched kanji tiles (character + stroke count), ranked "by stroke count and drawing extent" against the note that production ranks via a stroke-similarity scorer over KanjiVG paths (not yet implemented — see Known Gaps). Tapping a candidate inserts it into the search query and the user still has to submit/read results.

### 3. Word detail (pushed screen, back-navigates to Search)
- Header: back chevron + "Results" label, 2px rule.
- **Entry header**: reading (accent, small) → headword (Noto Serif JP 46px) → badge row (`common`, JLPT level, word-class tags, entry id).
- **Sticky tab bar**: Meaning / Sentences / Kanji / Conjugation (last tab only shown for conjugable word classes — verbs/i-adjectives/suru-nouns). Active tab is solid accent fill with white label; inactive tabs are plain text, all separated by 1px rules.
- **Meaning tab**: senses grouped by part-of-speech heading (accent, uppercase); each sense numbered, gloss text, optional italic-weight usage note. Below: "Other forms" (alternate spellings/kana-only forms) as bordered chips, then "Related & compounds" as a tappable list (word + reading + right-aligned gloss) that navigates to that word's detail page (or falls back to a new search if not in the sample set).
- **Sentences tab**: one card per Tatoeba example — Japanese sentence with the matched inflected form underlined/colored in accent, English translation below, small "Tatoeba #id" meta.
- **Kanji tab**: one card per kanji in the headword — large character tile, meanings, ON/KUN readings (Noto Serif JP), meta chips (stroke count / grade / frequency rank), and a stroke-order control: a play button plus a progress bar that steps through strokes on tap (placeholder — real playback needs KanjiVG path data, see Known Gaps).
- **Conjugation tab**: short note on the word's class, then a 3-column table (Form / Plain / Polite) covering non-past, past, negative, past-negative, te-form, potential, passive, causative, volitional, conditional, imperative as applicable to the word's class (generated in the prototype from a small conjugation-rule table).

## Interactions & behavior
- Search matches on: substring of headword, substring of reading, rōmaji prefix (rōmaji-to-kana converted for kana matching), and English gloss substring; if nothing matches, tries stripping a set of common inflectional endings (deinflection) and re-matching the stem — this should be replaced by/reconciled with `DictionaryRepository`'s real search + deinflection logic.
- Opening a result or a compound updates a small "recents" list (max 6, de-duplicated, most-recent-first).
- Drawing capture: pointer-down starts a new stroke, pointer-move appends points, pointer-up/leave ends it; Undo pops the last stroke array, Clear empties all.
- All interactive rows/buttons keep a visible hover/pressed tint and a 44px minimum height for touch targets.

## Known gaps to fill when implementing
1. **Real search** should call `DictionaryRepository` (JMdict-backed) instead of the prototype's tiny in-memory word list — the prototype's matching rules (substring, rōmaji, deinflection) are a *behavioral spec*, not the data source.
2. **Stroke-order rendering** needs the actual KanjiVG stroke paths (see Core's kanji stroke data) drawn/animated in sequence — the prototype only shows a stepper placeholder.
3. **Handwriting recognition** needs a real stroke-similarity scorer comparing drawn strokes against KanjiVG paths; the prototype ranks candidates by a naive stroke-count heuristic only.
4. **Example sentences** should pull from the linked Tatoeba corpus via `ExampleSentence`, matched to the entry.
5. Conjugation table generation should be driven by `WordClass` rather than the prototype's local rule table, if `WordClass` already encodes this.

## Files
- `Japanese Dictionary.dc.html` — the full interactive prototype (search, drawing sheet, detail page with all four tabs). Open it in a browser to see all states; view source for exact copy, layout order, and structure.
