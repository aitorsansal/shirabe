# Shirabe roadmap (post 1.0.1)

Shirabe is a lookup tool, not a study app. Practice lives in NihongoNoPractise (NNP); the long-term plan is to link the two (send flashcard-style practice from Shirabe lists to NNP). No accounts, no sync, no INTERNET permission unless the privacy policy and Data Safety form are updated first.

Order: 1.1a, 1.1b, 1.1c, then 1.2. Cut 1.2 only after closed-test feedback is in.

## Pending verification

- **Anki import check.** The list export uses Anki's file headers (`#columns`, `#tags column`, `#guid column`) per the manual, but hasn't been imported into a real Anki yet. Before relying on it: import one exported list into Anki desktop and AnkiDroid with the Basic note type and check that no header card appears, Front/Back are filled, tags arrive, Japanese text isn't garbled (the file is UTF-8 without a BOM), and re-importing after adding a word updates instead of duplicating. Not a blocker for the next release.

## Open from the 1.0.2 comparison with Takoboto

Done since the report: ranking by core meaning and JLPT level, matched meaning in result previews, one line per sense, handwriting Append/Replace switch, stroke-order autoplay, "Show more results", Spanish/French/German meaning search, Anki-ready list export, grade chip and JMdict id badge removed, polite passive/causative/causative-passive forms in the Conj. tab, wildcards (`?`/`*` on kanji forms and readings, romaji too; a leading `*` scans every form, 200-850 ms on the Mi 10T, so it gets a 500 ms debounce instead of 200 ms; a reversed-text column in the 1.2 rebuild would make suffix patterns an index lookup), word of the day (any JLPT-listed word, picked from the date, on the empty search page). Still open:

- **Recent-word chips clip in landscape.** With タイムリー first in the row, the 取り扱う chip rendered as 取り扱 (the う cut off). Seen 2026-11-05 on the Mi 10T; not investigated yet, likely FlexLayout child measuring.
- **Kanji search by components (radicals):** see 1.2; needs KRADFILE and a rebuild. Handwriting already covers most "I can't type this kanji" cases, so it stays last.
- **English ranking gaps.** "station" doesn't put 駅 first and "evening" doesn't put 夕方 first: both need a head-noun tier (the query as the head of a longer gloss). "come" puts 来る third behind honorific verbs; accepted as is.

## 1.1a: layout fixes, no data changes (done)

- **Landscape audit** on the Mi 10T (840x344 dp). Search: the header is one row (wordmark, search bar, icons) and the search field opts out of the full-screen landscape keyboard (`Controls/KeyboardOptions`). Handwriting: square canvas with Undo/Clear/Strokes in a column beside it. Conj. tab: plain and polite side by side once a row is at least 560 dp wide (`Controls/ConjugationRowGrid`). Lists, Settings, About, kanji words and the other word tabs needed nothing.
- **Conj. tab wrapping.** The kana split (たべなかっ|た) didn't reproduce on the current layout, even at font scale 1.5. What did split was the fixed 64 dp label column ("INFORM|AL", "CORTÉ|S" from 1.3 up); it now sizes to its labels.

## 1.1b: uses data already in the DB (done)

- **Search finds compounds.** A 1-2 kanji query (行, 食堂) also lists headwords that contain it (銀行), after the exact and prefix matches; `SearchMatchKind.Contains`, `DictionaryRepository.FindCompoundsByKanji`.
- **Chained kanji search.** Tapping a kanji tile on the word page's Kanji tab opens `KanjiWordsPage` (the search for that kanji as a pushed page); Back returns to the word.
- **Long-press to copy** the headword, the reading and the kanji tile character (`Controls/LongPress`, `Services/ClipboardCopy`).
- **Part-of-speech chips** are colored by family (verb green, noun blue, adjective yellow, adverb purple, other lime; `PartOfSpeech.GroupOf`) instead of by position, and tapping one explains its code (`TagInfo`, `Pos_*` strings in en/es/ca). Same chips replace the plain-text headers on the Meaning tab. Inside list rows the row's own tap swallows the chip's, so `EntryRowView` routes taps by position.
- **Share button on the word page: dropped** (little real use without entry links; long-press copy covers the common case).

- **JLPT N5-N1 badges** on words (search rows, lists, word page) and kanji, from Waller's lists via yomitan-jlpt-vocab and JLPT_Vocabulary (`WordEntryRow.JlptLevel`, `KanjiRow.JlptN`), labelled as unofficial estimates. KANJIDIC2's old 4-level `jlpt` field is not shown.

(No kanji filters or sorting: the JLPT chip on the kanji tab is enough. Grade and frequency chips already exist.)

- **Frequency order for prefix matches, compounds and related words.** `FindByPrefix`, `FindCompoundsByKanji` and `FindRelatedByKanji` rank by `FormPriority.EffectiveBand` like the rest of search (prefix: JLPT only counts when the matched form is the headword or primary reading). `FindByPrefix` also became an index range lookup instead of a LIKE scan.

## 1.1c: NNP link (undecided, blocked on NNP)

Per-list CSV export for Anki (Word, Back, Reading, Meanings, Tags and a `shirabe-{JMdict id}` GUID column, via `WordListCsv`) already exists. NNP has no import and no personal-card model; the options are analysed in `../NihongoNoPractise/reports/SHIRABE_INTEGRATION_OPTIONS.md`. Nothing to build in Shirabe until NNP decides which of those it wants. Likely Shirabe-side pieces, cheapest first:

- "Look up in Shirabe" from NNP needs nothing here: `ProcessTextLookupActivity` already accepts it.
- A "Practise in NNP" button (word page and list page) that launches NNP, with a Play Store fallback. Replaces the Settings promo that opens the store listing even when NNP is installed.
- A versioned companion JSON export (JMdict `entryId`, selected form, reading, gloss, language, attribution) next to the CSV, only once NNP's inbox/personal-card model exists. The CSV only carries the JMdict id inside the Anki GUID column (`shirabe-{id}`), with no selected form, language or attribution fields, so it isn't a proper machine-readable interchange format.

## 1.2: one dictionary rebuild, batch everything

- **Language model change (Spanish content).** Settings language only changes the UI. Dictionary content always shows English and Spanish side by side (Takoboto-style); when there is no Spanish translation, show English only, never an empty section. Needs: es glosses from full JMdict, es Tatoeba translations, es kanji meanings (accent-insensitive search of the es/fr/de meanings already exists, see "Faster translated-meaning search" below). Measured coverage: es glosses on 70% of common JMdict words, 19% of kanji, 17% of used Tatoeba sentences.
- **Faster translated-meaning search.** It scans `TranslatedSenseRow` unindexed with a LIKE + GLOB pre-filter: up to ~410 ms per search on a Mi 10T with es/fr/de all on (German's 166k senses are most of it). An accent-folded, per-word indexed column (or FTS5 table) built by DataBuilder would make it an index lookup.
- **Loanword origins.** Parse JMdict's `lsource` in DataBuilder and show it once on the word page ("from English: timely", language names in en/es/ca). Today the only trace is the Spanish translators' inline "(eng: timely)" notes (~7% of Spanish senses, almost all katakana words), which `GlossNotes.WithoutOrigin` hides in the popup and search previews but the word page still shows as written; once the origin has its own line they can be hidden there too.
- **Kanji search by components.** KRADFILE (EDRDG licence, add to notices). Multi-select grid of components grouped by stroke count; results are kanji containing all selected components; components that would yield zero results are disabled.

## Parked / only if requested

- **Share-intent search** (`ACTION_SEND` text/plain). Shared links carry only a URL and the app cannot fetch pages, so it only helps apps that share text but block selection; `PROCESS_TEXT` Look Up already covers the common case.
- **Opt-in Sentry crash reporting** (needs INTERNET permission, Settings toggle, privacy policy in en/es/ca, Data Safety update). Only if Play vitals are not enough.
- **Tablet two-pane layout.**

## Not planned

- Accounts or cloud sync (breaks the privacy claim).
- JMnedict names (about 100MB, no demand).
- Built-in SRS (NNP does this).
