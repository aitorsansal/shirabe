# Shirabe

Shirabe (調べ, "looking something up") is an offline Japanese-English dictionary for Android, built with .NET MAUI. Everything runs on the device: the dictionary is a bundled SQLite database, so lookups need no network connection.

## Features

- **Search** by Japanese (kanji or kana), romaji or English. Romaji is converted to kana automatically, and conjugated forms are resolved back to their dictionary form (for example, a past-tense verb finds its base entry).
- **Handwriting input**: draw a character and pick it from the recognition candidates.
- **Word detail**: senses and word classes, example sentences, a per-kanji breakdown with readings, meanings and stroke counts, and a conjugation table.
- **Romaji word readings** in results, word details and system-wide lookup, enabled by default and switchable in Settings. Converted offline from dictionary kana readings, with kana-spelled long vowels (for example, `gakkou` and `koohii`). Example sentence readings are not included.
- **System-wide lookup**: select text in any Android app and choose "Look Up" in the selection toolbar to see a quick result popup without leaving the app.
- **Light and dark themes**, switchable in the app.
- Recent searches are stored locally on the device.

## Project layout

| Project | Description |
|---|---|
| `JapaneseDictionary` | The MAUI app (`net10.0-android`) |
| `JapaneseDictionary.Core` | Search, repositories and handwriting recognition. Plain `net10.0`, no MAUI dependency |
| `JapaneseDictionary.DataBuilder` | Console tool that builds `dictionary.db3` from the public source corpora |
| `JapaneseDictionary.Tests` | xUnit tests for Core |

The app is called Shirabe; the solution, project and namespace names keep the original working title, `JapaneseDictionary`. `CLAUDE.md` has a more detailed architecture walkthrough, and `design/` holds the UI design reference.

## Install

Shirabe is free on Google Play (search for "Shirabe", package `com.aitorsansal.japanesedictionary`). There is no sideloadable APK on GitHub: a locally signed APK would not be updatable over the Play version.

## Building

Requirements: .NET 10 SDK with the MAUI Android workload.

```
git clone https://github.com/aitorsansal/shirabe.git
cd shirabe

# the dictionary database is a GitHub Release asset, not in git
powershell -File scripts/fetch-database.ps1   # or: sh scripts/fetch-database.sh

dotnet build JapaneseDictionary/JapaneseDictionary.csproj -f net10.0-android
dotnet test JapaneseDictionary.Tests/JapaneseDictionary.Tests.csproj
```

To build and deploy to a connected device or emulator, add `-t:Run` to the build command.

`dictionary.db3` (about 130 MB) is not tracked in git. `scripts/fetch-database.*` downloads it from the GitHub Release named in `database-release.txt` and verifies its SHA-256; the build fails with that hint if the file is missing.

### Regenerating the dictionary database

The downloaded `dictionary.db3` is ready to use. You only need to rebuild it after changing a parser or the writer, or to refresh the data. Put the source files in `JapaneseDictionary.DataBuilder/RawData/` (this folder is gitignored because the files are large):

| File | Source |
|---|---|
| `JMdict_e` | [JMdict](https://www.edrdg.org/wiki/index.php/JMdict-EDICT_Dictionary_Project) (English edition) |
| `kanjidic2.xml` | [KANJIDIC2](https://www.edrdg.org/wiki/index.php/KANJIDIC_Project) |
| `kanjivg.xml` | [KanjiVG](https://kanjivg.tagaini.net/) (combined XML) |
| `jpn_indices.csv`, `sentences.csv` | [Tatoeba downloads](https://tatoeba.org/en/downloads) |

Then run `dotnet run` from `JapaneseDictionary.DataBuilder/`. It writes `JapaneseDictionary/Resources/Raw/dictionary.db3`.

## Credits and licenses

The source code is MIT licensed (`LICENSE`). The MIT license does not cover the dictionary database, which is CC BY-SA 4.0 (`DATA-LICENSE.md`), or the third-party components in `THIRD-PARTY-NOTICES.md`.

The dictionary data is not mine. It comes from the projects below, which are used under their respective licenses. The bundled `dictionary.db3` is derived from them and stays under their terms.

**Dictionary and kanji data**

- **JMdict** and **KANJIDIC2** are the property of the [Electronic Dictionary Research and Development Group](https://www.edrdg.org/) and are used in conformance with the Group's [licence](https://www.edrdg.org/edrdg/licence.html) ([CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/)).
- **KanjiVG** (stroke order data) is copyright Ulrich Apel and released under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Project page: <https://kanjivg.tagaini.net/>.
- **Example sentences** come from [Tatoeba](https://tatoeba.org/) and are licensed under [CC BY 2.0 FR](https://creativecommons.org/licenses/by/2.0/fr/). Sentences are credited to their contributors on Tatoeba, and each sentence in the app shows its Tatoeba id.

**Handwriting recognition**

- Character recognition uses the model from [DaKanji Single Kanji Recognition](https://github.com/CaptainDario/DaKanji-Single-Kanji-Recognition) (MIT, copyright CaptainDario). Character recognition powered by machine learning from Dariyooo (DaAppLab). The model was trained on the [ETL Character Database](https://etlcdb.db.aist.go.jp/) (AIST) and the KanjiVG dataset.
- Inference runs through [ONNX Runtime](https://github.com/microsoft/onnxruntime) (MIT).

**Fonts**

- [Archivo](https://github.com/Omnibus-Type/Archivo) and [Noto Serif JP](https://fonts.google.com/noto/specimen/Noto+Serif+JP): SIL Open Font License 1.1.

**Libraries**: [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (MIT), [sqlite-net-pcl](https://github.com/praeclarum/sqlite-net) (MIT) and [SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw) (Apache 2.0).

## License

- **Source code**: [MIT](LICENSE). You are free to fork, modify and redistribute it.
- **Dictionary database** (`dictionary.db3`): [CC BY-SA 4.0](DATA-LICENSE.md), because it is derived from JMdict, KANJIDIC2, KanjiVG and Tatoeba. Redistributions must credit those sources and keep the same license.
- **Third-party components** (handwriting model, fonts, libraries): see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
- The application name and icon are not covered by the code license. If you publish a fork, please use your own name and icon.

## Development history

The first day of development happened locally without commits. The initial commits were reconstructed afterwards from file modification times, so they are grouped by when files last changed and do not each build on their own. History from the Android scaffolding commit onward is as it happened.
