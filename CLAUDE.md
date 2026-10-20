# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

The app's product name is **Shirabe** (launcher label, About page, header wordmark). The solution, project folders, namespaces and `ApplicationId` (`com.aitorsansal.japanesedictionary`) deliberately keep the original `JapaneseDictionary` working title, so don't rename those when changing user-facing text.

## Commands

- Build the app: `dotnet build JapaneseDictionary/JapaneseDictionary.csproj -f net10.0-android` — the target framework must be passed explicitly; the app only targets `net10.0-android` (the MAUI template's iOS/MacCatalyst/Windows scaffolding was removed).
- Build and deploy to a connected/adb-visible device or emulator: `dotnet build JapaneseDictionary/JapaneseDictionary.csproj -f net10.0-android -t:Run`
- Build the whole solution (app + Core + DataBuilder + Tests): `dotnet build JapaneseDictionary.sln`
- Run tests: `dotnet test JapaneseDictionary.Tests/JapaneseDictionary.Tests.csproj`
- Run a single test: `dotnet test JapaneseDictionary.Tests/JapaneseDictionary.Tests.csproj --filter "FullyQualifiedName~ClassName.MethodName"`
- Fetch the bundled dictionary database on a fresh clone: `powershell -File scripts/fetch-database.ps1` (or `sh scripts/fetch-database.sh`). `dictionary.db3` is gitignored and ships as a GitHub Release asset; the script downloads the release named in `database-release.txt` and verifies its SHA-256. The app build fails if the file is missing.
- Regenerate the dictionary database (only needed after touching a parser/writer in `JapaneseDictionary.DataBuilder`, or after updating the source corpora): from `JapaneseDictionary.DataBuilder/`, run `dotnet run`. Requires `RawData/` to be populated first (JMdict, KANJIDIC2, KanjiVG, and Tatoeba's `jpn_indices.csv` + `sentences.csv`) — these are **not** checked into git (gitignored, ~1.5GB of downloadable public corpora, and several individual files exceed GitHub's 100MB limit anyway). The tool writes to `../JapaneseDictionary/Resources/Raw/dictionary.db3`.

### Maintainer-only

These need the maintainer's credentials. Don't attempt them from a contributor checkout; describe the needed step in the PR instead.

- Publishing a regenerated database: upload `dictionary.db3` to a new GitHub Release and update both lines (tag and SHA-256) in `database-release.txt`.
- Publishing the privacy policy site (`web/public/privacy/index.html`, served at `shirabe.aitorsansal.com/privacy/` — the URL in the Play Console and on the About page): from `web/`, `npx wrangler deploy`. Editing the policy itself is fine for anyone: keep its claims in sync with the app (no internet permission, what's in Android backup per `data_extraction_rules.xml`) and update it in all three languages.

## Contributing

- Run the test suite before proposing changes; Core logic changes (search, deinflection, romaji, recognition) should come with tests.
- Never commit `dictionary.db3`, `RawData/` or other generated `.db3` files — they're gitignored for size and licensing reasons; don't force-add them.
- Only change `database-release.txt` together with an actual new release (maintainer-only, see above).

## Data licensing and attribution

The dictionary data is third-party and its licences require attribution, so treat the credits as load-bearing, not boilerplate:
- JMdict/EDICT and KANJIDIC2 — EDRDG licence; KanjiVG — CC BY-SA 3.0; Tatoeba sentences — CC BY 2.0 FR. The derived `dictionary.db3` is distributed under CC BY-SA 4.0 (`DATA-LICENSE.md`).
- Attribution lives in places that must stay consistent: the About page (`Views/AboutPage.xaml`, text in the `About_*` resx keys in all three languages), `README.md`, `DATA-LICENSE.md`, and `THIRD-PARTY-NOTICES.md` (which also covers the handwriting model, fonts and libraries, with full licence texts under `licenses/`).
- Don't remove, shorten or "clean up" these credits, and don't drop the per-sentence Tatoeba id shown in the app. Adding a new data source, model, font or library means adding its notice in the same change.
- The app code itself is MIT (`LICENSE`); that doesn't relicense the data.

## Architecture

### Solution layout

Four projects:
- **JapaneseDictionary** — the MAUI app (`net10.0-android`)
- **JapaneseDictionary.Core** — plain `net10.0` library holding all data/search/recognition logic, no MAUI dependency (`InternalsVisibleTo` the Tests project)
- **JapaneseDictionary.DataBuilder** — console tool, one-shot ETL from public dictionary corpora into `dictionary.db3`
- **JapaneseDictionary.Tests** — xUnit, references Core only

### Data pipeline

`DataBuilder` parses JMdict, KANJIDIC2, KanjiVG, and Tatoeba raw files (`RawData/`) into a single read-only SQLite file, `dictionary.db3`, bundled into the app as a `MauiAsset` (`Resources/Raw/**`, with `LogicalName` stripping the `Raw` prefix). At runtime SQLite needs a real file path, not a package stream, so `DatabaseInitializer` copies it out of the app package into `FileSystem.AppDataDirectory` once on first use. User data (recents/history) lives in a separate writable SQLite file, `userdata.db3`, in the same directory.

### Startup / DI

`MauiProgram.cs` wires DI. `AppDataContext` is a singleton owning the whole data layer (`DictionaryRepository`, `UserDataRepository`, `SearchService`, `DaKanjiRecognizerService`) — it's constructed synchronously but initialized asynchronously, so every consumer must call `EnsureInitializedAsync()` before touching its properties (thread-safe, runs once via a `SemaphoreSlim`). ViewModels and pages are DI-registered transient. `HandwritingSheetPage` isn't Shell-routed — it's a modal opened from multiple call sites, so it's resolved via the `App.Services` static service-locator escape hatch instead.

### Search

`SearchService.Search()` is the single entry point: exact match → deinflection (`Deinflector` undoes conjugation endings with a table of chained suffix rules, then candidates are looked up exactly and filtered by `WordClass`; works on kanji and kana alike) → prefix match → English gloss substring (English mode only, minimum 2 characters — an unindexed scan otherwise). Romaji input is auto-converted to hiragana/katakana before matching (`RomajiConverter`).

### Theming

`Resources/Styles/Colors.xaml` defines Light/Dark token pairs, plus fixed-role accents (same value in both themes, e.g. the purple draw button) and a five-hue palette (`Hue0`..`Hue4` Fill/Text/Border × Light/Dark) cycled across word-class tags, badges, and kanji meta chips. `Styles.xaml`'s implicit styles consume these via `AppThemeBinding`, which reacts automatically to `Application.Current.UserAppTheme`. `ThemeService` (DI singleton, also stashed into `Application.Resources["ThemeService"]` by `App.xaml.cs` so XAML can bind to it directly) owns the light/dark toggle and persists the choice via `Preferences`.

`Label.TextColor` is `Color`-typed, not `Brush`, so it can't be pre-packaged as a theme-reactive resource the way `Border.Stroke`/`Background` can. Per-item hue-cycled colors go through `Converters/HueConverters.cs` instead, via `MultiBinding` of `[item hue index, ThemeService.IsDark]`.

Two easy-to-hit MAUI XAML traps that already bit this codebase once each:
- `Border` has both `Background` (`Brush`) and `BackgroundColor` (`Color`). The implicit `Border` style sets `Background="Transparent"` (needed so Borders with only a `Stroke` still hit-test across their full bounds for `TapGestureRecognizer`), and that silently wins over any local `BackgroundColor`. Always set `Background`, not `BackgroundColor`, for a Border's visible fill.
- A `FlexLayout` nested inside another `FlexLayout` doesn't report its own wrapped height back to the outer one, so a second wrapped row silently gets clipped by whatever follows it.

### Localization

UI text is in English, Spanish and Catalan: `Resources/Localization/AppResources.resx` (neutral English) plus `.es.resx` / `.ca.resx`. The `AppResources` class is generated at build time into `obj/` (MSBuild `StronglyTyped*` metadata in the csproj), so there is no checked-in Designer.cs — adding a string means adding it to all three .resx files. XAML uses `{x:Static res:AppResources.Key}`; strings built in code (counts, formatted messages, Core's English conjugation names, the stored "Favourites" list name) go through `Services/UiText.cs`. Don't hardcode user-facing text.

`LanguageService` applies the saved choice (or the system language) as the UI culture at the very start of `MauiProgram`. Because x:Static is read when a page is built, changing language rebuilds the Shell. Only the UI is translated — glosses, gloss search and sentence translations stay English (the bundled JMdict/Tatoeba data is English-only). The Look Up popup's label in other apps' text-selection menu is an Android string resource (`Platforms/Android/Resources/values*/lookup_strings.xml`) and follows the system language.

### Android / ONNX

Handwriting recognition (`DaKanjiRecognizer`) loads `char_classifier.onnx` via `Microsoft.ML.OnnxRuntime`. Getting `libonnxruntime.so` into the Android APK needs three explicit workarounds in `JapaneseDictionary.csproj` (forcing `AndroidApplication=true` early, explicit `RuntimeIdentifiers` even in Debug, and manually adding the `AndroidLibrary` item from the package's own contents) — read the comments there before touching the OnnxRuntime version or Android target settings.

### Design reference

`design/design_handoff_dictionary/` contains a Claude Design HTML prototype (`Japanese Dictionary.dc.html`) and a `README.md` documenting the exact color tokens, type system (Archivo for UI chrome, Noto Serif JP for all Japanese-language text), spacing, and per-screen copy/layout. It's a design reference only, built with a small in-house templating runtime — the task is to recreate it as MAUI XAML/styles, not port the HTML/CSS/JS.
