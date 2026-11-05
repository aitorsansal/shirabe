# NNP ↔ Shirabe companion contract (v1)

Shared contract between **Nihongo No Practise** (`com.aitorsansal.nihongonopractise`, "NNP") and **Shirabe** (`com.aitorsansal.japanesedictionary`). An identical copy lives in both repos (`NihongoNoPractise/COMPANION_CONTRACT.md`, `JapaneseDictionary/docs/COMPANION_CONTRACT.md`). Change both in the same session, or not at all.

Status: **agreed, not implemented** (2026-11-05). Product plan and NNP-side design: `NihongoNoPractise/SHIRABE_LINK_PLAN.md`. Background analysis: `NihongoNoPractise/reports/SHIRABE_INTEGRATION_OPTIONS.md`.

## Principles

- **Transport:** Android intents targeted at the companion's package (`Intent.SetPackage`). No custom URL schemes, no HTTPS App Links in v1. In-app buttons don't need them.
- **No shared storage.** No shared SQLite, preferences or `sharedUserId`. Batches travel as a file behind a `content://` URI with a temporary read grant.
- **Neither app requires the other.** Each one checks for the companion before showing a companion action and degrades to an install prompt (absent) or an update prompt (too old).
- **Every request is untrusted input.** Any app can send these intents. Validate everything, never execute routes or callbacks from extras, and require a user-visible preview before anything is persisted.
- **Public identifiers only.** Dictionary identity is `source = "jmdict"` + JMdict `ent_seq`. NNP SQLite row IDs never cross the boundary.

## Versioning

- Every request carries the int extra `com.aitorsansal.companion.V` (current: `1`). Missing means `1`.
- A receiver that gets a higher version than it supports shows "Update <app> to open this" and does nothing else.
- Capability check before showing an action: `PackageManager.GetPackageInfo(companionPackage)` → `LongVersionCode` ≥ the minimum version code listed per action below. This needs a `<queries><package android:name="…"/></queries>` entry for the companion package in each manifest. Don't use broad package visibility.
- Minimum version codes are filled in when each side ships (`TBD` until then).

## Actions

### 1. Look up a word (NNP → Shirabe)

Existing Shirabe receiver `ProcessTextLookupActivity`. Shirabe's only change is the optional reading.

| | |
|---|---|
| Action | `android.intent.action.PROCESS_TEXT` |
| Package | `com.aitorsansal.japanesedictionary` |
| MIME | `text/plain` |
| `android.intent.extra.PROCESS_TEXT` | query, plain text, ≤ 64 chars, no markup. Conjugated forms are fine (Shirabe deinflects). |
| `android.intent.extra.PROCESS_TEXT_READONLY` | `true` |
| `com.aitorsansal.companion.READING` | optional, kana reading of the query (from `<rt>`). Shirabe prefers a top-N search result whose reading matches; without one it keeps today's first-result behaviour. |
| Flags | none (the popup stays in the caller's task; Back returns to NNP) |
| Shirabe min version | `TBD` (the reading extra). Older versions ignore the extra, so NNP may still send to them. |

### 2. Open a dictionary entry (NNP → Shirabe)

Existing `MainActivity` extra; no Shirabe change.

| | |
|---|---|
| Component | Shirabe launcher activity, via `PackageManager.GetLaunchIntentForPackage` (don't hard-code the generated Java class name) |
| `com.aitorsansal.japanesedictionary.ENTRY_ID` | int, JMdict `ent_seq` |
| Flags | `NewTask \| ClearTop \| SingleTop` (with `NewTask` alone, a running app is only brought to the front and never receives the intent) |

### 3. Import words (Shirabe → NNP)

One path for a single word and for a whole list.

| | |
|---|---|
| Action | `com.aitorsansal.nihongonopractise.action.IMPORT_WORDS` |
| Package | `com.aitorsansal.nihongonopractise` |
| Data | `content://` URI of a UTF-8 JSON file (Shirabe's `FileProvider`, `FLAG_GRANT_READ_URI_PERMISSION`), set with `SetDataAndType(uri, "application/json")`. NNP's filter declares `DataMimeType = "application/json"`; without the type the intent does not resolve. |
| Flags | `NewTask \| ClearTop \| SingleTop` (with `NewTask` alone, a running app is only brought to the front and never receives the intent) |
| NNP min version | `TBD` |

NNP reads the stream once on receipt (the grant can expire). Rejected without reading further:
- files over 2 MB
- more than 2,000 entries.

Payload:

```json
{
  "v": 1,
  "requestId": "6f1c…-uuid",
  "listName": "Favourites",
  "attribution": "<JMdict credit from DATA-LICENSE.md> <JLPT credit, because an entry has jlpt>",
  "entries": [
    {
      "source": "jmdict",
      "entryId": 1586060,
      "form": "診る",
      "reading": "みる",
      "jlpt": 4,
      "glosses": {
        "en": ["to examine", "to see (a patient)"],
        "es": ["examinar", "revisar"]
      }
    }
  ]
}
```

| Field | Rule |
|---|---|
| `v` | int, required |
| `requestId` | UUID string, required. Same id received twice → NNP shows "already imported" instead of re-importing. |
| `listName` | optional, ≤ 60 chars. `null` for a single word. |
| `attribution` | required. Must credit every data source in the payload: the JMdict/EDRDG acknowledgement always, plus the JLPT credit (Jonathan Waller's JLPT lists, tanos.co.uk, CC BY; JMdict ids via stephenmk/yomitan-jlpt-vocab, CC BY-SA 4.0) whenever any entry has `jlpt`. Shirabe builds the text from its `DATA-LICENSE.md`; NNP stores it with each card and shows it unchanged. |
| `entries[].source` | `"jmdict"` only in v1; other values are skipped and reported. |
| `entries[].entryId` | positive int |
| `entries[].form` | the written form the user saw (kanji or kana), ≤ 32 chars |
| `entries[].reading` | kana, ≤ 32 chars. Must be a reading valid for `form`; Shirabe sends the matching pair. |
| `entries[].jlpt` | optional int 1–5 (Shirabe's JLPT level data). Sending it makes the JLPT credit in `attribution` mandatory. |
| `entries[].glosses` | object keyed by language code (`en`, `es`, `fr`, `de`, `ca`, …). Shirabe sends English plus whatever translation languages the user enabled. Each value ≤ 5 strings of ≤ 80 chars, applied in this order: (1) **drop** every meaning over 80 chars, never truncate one (a cut meaning can say something wrong); (2) keep the first 5 of what is left; (3) leave out a language with no meanings left. `en` is required: if no English meaning survives, Shirabe skips the entry and counts it as skipped. NNP applies the same rules on receipt (dropping, not rejecting the file), so an older or buggy sender can't block an import; an entry left without `en` is shown as skipped in the preview. |

Card identity in NNP is `(source, entryId, form, reading)`. Importing again updates glosses and keeps review history.

### 4. Practise an entry (Shirabe → NNP)

| | |
|---|---|
| Action | `com.aitorsansal.nihongonopractise.action.PRACTISE_ENTRY` |
| Package | `com.aitorsansal.nihongonopractise` |
| `com.aitorsansal.companion.SOURCE` | `"jmdict"` |
| `com.aitorsansal.companion.ENTRY_ID` | int |
| `com.aitorsansal.companion.FORM` | string (shown and used for the fallback match) |
| `com.aitorsansal.companion.READING` | string |
| `com.aitorsansal.companion.GLOSSES_JSON` | optional, same `glosses` object as action 3. Lets "Add to Mis palabras" work without a second round trip. |
| `com.aitorsansal.companion.JLPT` | optional int 1–5, same meaning as `entries[].jlpt` |
| `com.aitorsansal.companion.ATTRIBUTION` | required when `GLOSSES_JSON` or `JLPT` is present; same content rule as action 3's `attribution` |
| Flags | `NewTask \| ClearTop \| SingleTop` (with `NewTask` alone, a running app is only brought to the front and never receives the intent) |
| NNP min version | `TBD` |

`GLOSSES_JSON` follows the same meaning rules as action 3 (drop over 80 chars, first 5, `en` required; without a surviving `en` NNP treats the glosses as absent).

NNP shows its own matching exercises for that entry and offers to add the word to "Mis palabras" (see the plan). It never starts a quiz without a tap.

## Receiver rules (both apps)

- Handle the intent in `OnCreate` (only when `savedInstanceState == null`) and in `OnNewIntent`. A restored activity must not replay a request, and neither may a relaunch from Recents: ignore intents carrying `FLAG_ACTIVITY_LAUNCHED_FROM_HISTORY`.
- Every receiving intent filter includes `CATEGORY_DEFAULT`; implicit `startActivity` calls don't resolve without it, even with a package set.
- Queue the request until the app is ready (database initialized, onboarding finished, Shell up), then drain it once. A request that arrives mid-onboarding waits until onboarding is done.
- Never discard the user's in-progress quiz silently. A request that would start a session asks first.
- Unknown actions, versions, sources or malformed payloads → a short message, no partial import.
- Lookups (action 1) are unavailable in exams and before a question is answered.

## Changelog

- v1 (2026-11-05): actions 1–4. Later the same day, still before any sender exists: attribution must credit JLPT data when `jlpt` is sent; meanings over 80 chars are dropped, not truncated; optional `JLPT` extra on action 4. Earlier the same day, before any implementation: launch flags `NewTask | ClearTop | SingleTop`, explicit JSON MIME type, Recents-replay and `CATEGORY_DEFAULT` rules.
