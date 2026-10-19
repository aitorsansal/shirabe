# Shirabe roadmap (post 1.0.1)

Shirabe is a lookup tool, not a study app. Practice lives in NihongoNoPractise (NNP); the long-term plan is to link the two (send flashcard-style practice from Shirabe lists to NNP). No accounts, no sync, no INTERNET permission unless the privacy policy and Data Safety form are updated first.

Order: 1.1a, 1.1b, 1.1c, then 1.2. Cut 1.2 only after closed-test feedback is in.

## 1.1a: layout fixes, no data changes

- **Landscape audit.** Check every page with the phone rotated (a wider/higher screen than that is assumed to work if the phone does). Fix clipping and cramped layouts.
- **Kana forms wrapping mid-word in the Conj. tab** on narrow screens (e.g. たべなかっ|た).

## 1.1b: uses data already in the DB (done)

- **Search finds compounds.** A 1-2 kanji query (行, 食堂) also lists headwords that contain it (銀行), after the exact and prefix matches; `SearchMatchKind.Contains`, `DictionaryRepository.FindCompoundsByKanji`.
- **Chained kanji search.** Tapping a kanji tile on the word page's Kanji tab opens `KanjiWordsPage` (the search for that kanji as a pushed page); Back returns to the word.
- **Long-press to copy** the headword, the reading and the kanji tile character (`Controls/LongPress`, `Services/ClipboardCopy`).
- **Share button on the word page: dropped** (little real use without entry links; long-press copy covers the common case).

(No kanji filters or sorting: a JLPT chip on the kanji tab is enough. Grade and frequency chips already exist. The JLPT chip moved to 1.2 because KANJIDIC2's `jlpt` is the old 4-level scale, which does not map cleanly to N5-N1.)

## 1.1c: NNP link (undecided, blocked on NNP)

Per-list CSV export (`Word,Reading,Meanings`, via `WordListCsv`) already exists. NNP has no import and no personal-card model; the options are analysed in `../NihongoNoPractise/reports/SHIRABE_INTEGRATION_OPTIONS.md`. Nothing to build in Shirabe until NNP decides which of those it wants. Likely Shirabe-side pieces, cheapest first:

- "Look up in Shirabe" from NNP needs nothing here: `ProcessTextLookupActivity` already accepts it.
- A "Practise in NNP" button (word page and list page) that launches NNP, with a Play Store fallback. Replaces the Settings promo that opens the store listing even when NNP is installed.
- A versioned companion JSON export (JMdict `entryId`, selected form, reading, gloss, language, attribution) next to the CSV, only once NNP's inbox/personal-card model exists. The CSV has no entry ID, so it can't identify exact entries.

## 1.2: one dictionary rebuild, batch everything

- **Language model change (Spanish content).** Settings language only changes the UI. Dictionary content always shows English and Spanish side by side (Takoboto-style); when there is no Spanish translation, show English only, never an empty section. Needs: es glosses from full JMdict, es Tatoeba translations, es kanji meanings, accent-insensitive Spanish search. Measured coverage: es glosses on 70% of common JMdict words, 19% of kanji, 17% of used Tatoeba sentences.
- **Frequency bands.** JMdict nf01-nf48 for ranking search and related words.
- **JLPT N5-N1 chip** on the kanji tab (and later on words). Source: community per-level lists (e.g. Tanos), since the JLPT has published no official lists since 2010; label as unofficial and add to the notices. KANJIDIC2's old 4-level `jlpt` field is not used.
- **Kanji search by components.** KRADFILE (EDRDG licence, add to notices). Multi-select grid of components grouped by stroke count; results are kanji containing all selected components; components that would yield zero results are disabled.
- **Word of the day** (needs the common/frequency flag): deterministic by date, same for everyone, no storage, shown on the empty search state. No notification or widget at first.

## Parked / only if requested

- **Share-intent search** (`ACTION_SEND` text/plain). Shared links carry only a URL and the app cannot fetch pages, so it only helps apps that share text but block selection; `PROCESS_TEXT` Look Up already covers the common case.
- **Opt-in Sentry crash reporting** (needs INTERNET permission, Settings toggle, privacy policy in en/es/ca, Data Safety update). Only if Play vitals are not enough.
- **Tablet two-pane layout.**

## Not planned

- Accounts or cloud sync (breaks the privacy claim).
- JMnedict names (about 100MB, no demand).
- Built-in SRS (NNP does this).
