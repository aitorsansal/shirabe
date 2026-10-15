# Japanese Dictionary

An offline Japanese-English dictionary for Android, built with .NET MAUI. Everything runs on the device: the dictionary is a bundled SQLite database, so lookups need no network connection.

## Features

- **Search** by Japanese (kanji or kana), romaji or English. Romaji is converted to kana automatically, and conjugated forms are resolved back to their dictionary form (for example, a past-tense verb finds its base entry).
- **Handwriting input**: draw a character and pick it from the recognition candidates.
- **Word detail**: senses and word classes, example sentences, a per-kanji breakdown with readings, meanings and stroke counts, and a conjugation table.
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

`CLAUDE.md` has a more detailed architecture walkthrough, and `design/` holds the UI design reference.

## Building

Requirements: .NET 10 SDK with the MAUI Android workload, and [Git LFS](https://git-lfs.com/).

```
git lfs install
git clone https://github.com/aitorsansal/JapaneseDictionary.git
cd JapaneseDictionary

dotnet build JapaneseDictionary/JapaneseDictionary.csproj -f net10.0-android
dotnet test JapaneseDictionary.Tests/JapaneseDictionary.Tests.csproj
```

To build and deploy to a connected device or emulator, add `-t:Run` to the build command.

`dictionary.db3` is stored with Git LFS. If you clone without LFS installed you will get a small pointer file instead of the database, and the app will not start.

### Regenerating the dictionary database

The committed `dictionary.db3` is ready to use. You only need to rebuild it after changing a parser or the writer, or to refresh the data. Put the source files in `JapaneseDictionary.DataBuilder/RawData/` (this folder is gitignored because the files are large):

| File | Source |
|---|---|
| `JMdict_e` | [JMdict](https://www.edrdg.org/wiki/index.php/JMdict-EDICT_Dictionary_Project) (English edition) |
| `kanjidic2.xml` | [KANJIDIC2](https://www.edrdg.org/wiki/index.php/KANJIDIC_Project) |
| `kanjivg.xml` | [KanjiVG](https://kanjivg.tagaini.net/) (combined XML) |
| `jpn_indices.csv`, `sentences.csv` | [Tatoeba downloads](https://tatoeba.org/en/downloads) |

Then run `dotnet run` from `JapaneseDictionary.DataBuilder/`. It writes `JapaneseDictionary/Resources/Raw/dictionary.db3`.

## Credits and licenses

The dictionary data is not mine. It comes from the projects below, which are used under their respective licenses. The bundled `dictionary.db3` is derived from them and stays under their terms.

**Dictionary and kanji data**

- **JMdict** and **KANJIDIC2** are the property of the [Electronic Dictionary Research and Development Group](https://www.edrdg.org/) and are used in conformance with the Group's [licence](https://www.edrdg.org/edrdg/licence.html) ([CC BY-SA 4.0](https://creativecommons.org/licenses/by-sa/4.0/)).
- **KanjiVG** (stroke order data) is copyright Ulrich Apel and released under [CC BY-SA 3.0](https://creativecommons.org/licenses/by-sa/3.0/). Project page: <https://kanjivg.tagaini.net/>.
- **Example sentences** come from [Tatoeba](https://tatoeba.org/) and are licensed under [CC BY 2.0 FR](https://creativecommons.org/licenses/by/2.0/fr/). Sentences are credited to their contributors on Tatoeba, and each sentence in the app shows its Tatoeba id.

**Handwriting recognition**

- Character recognition uses the model from [DaKanji Single Kanji Recognition](https://github.com/CaptainDario/DaKanji-Single-Kanji-Recognition) (code under MIT). Character recognition powered by machine learning from Dariyooo (DaAppLab). The model was trained on the ETL Character Database and the KanjiVG dataset.
- Inference runs through [ONNX Runtime](https://github.com/microsoft/onnxruntime) (MIT).

**Fonts**

- [Archivo](https://github.com/Omnibus-Type/Archivo) and [Noto Serif JP](https://fonts.google.com/noto/specimen/Noto+Serif+JP): SIL Open Font License 1.1.
- Open Sans, included with the .NET MAUI template: Apache License 2.0.

**Libraries**: [CommunityToolkit.Mvvm](https://github.com/CommunityToolkit/dotnet) (MIT), [sqlite-net-pcl](https://github.com/praeclarum/sqlite-net) (MIT) and [SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw) (Apache 2.0).

## Development history

The first day of development happened locally without commits. The initial commits were reconstructed afterwards from file modification times, so they are grouped by when files last changed and do not each build on their own. History from the Android scaffolding commit onward is as it happened.
