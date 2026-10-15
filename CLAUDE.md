# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

The app's product name is **Shirabe** (launcher label, About page, header wordmark). The solution, project folders, namespaces and `ApplicationId` (`com.aitorsansal.japanesedictionary`) deliberately keep the original `JapaneseDictionary` working title, so don't rename those when changing user-facing text.

## Commands

- Build the app: `dotnet build JapaneseDictionary/JapaneseDictionary.csproj -f net10.0-android` — the target framework must be passed explicitly; the app only targets `net10.0-android` (the MAUI template's iOS/MacCatalyst/Windows scaffolding was removed).
- Build and deploy to a connected/adb-visible device or emulator: `dotnet build JapaneseDictionary/JapaneseDictionary.csproj -f net10.0-android -t:Run`
- Build the whole solution (app + Core + DataBuilder + Tests): `dotnet build JapaneseDictionary.sln`
- Run tests: `dotnet test JapaneseDictionary.Tests/JapaneseDictionary.Tests.csproj`
- Run a single test: `dotnet test JapaneseDictionary.Tests/JapaneseDictionary.Tests.csproj --filter "FullyQualifiedName~ClassName.MethodName"`
- Regenerate the bundled dictionary database (only needed after touching a parser/writer in `JapaneseDictionary.DataBuilder`, or after updating the source corpora): from `JapaneseDictionary.DataBuilder/`, run `dotnet run`. Requires `RawData/` to be populated first (JMdict, KANJIDIC2, KanjiVG, and Tatoeba's `jpn_indices.csv` + `sentences.csv`) — these are **not** checked into git (gitignored, ~1.5GB of downloadable public corpora, and several individual files exceed GitHub's 100MB limit anyway). The tool writes to `../JapaneseDictionary/Resources/Raw/dictionary.db3` (tracked via **Git LFS** — run `git lfs install` once after cloning, or you'll get a pointer file instead of the real database).

## Architecture

### Solution layout

Four projects:
- **JapaneseDictionary** — the MAUI app (`net10.0-android`)
- **JapaneseDictionary.Core** — plain `net10.0` library holding all data/search/recognition logic, no MAUI dependency (`InternalsVisibleTo` the Tests project)
- **JapaneseDictionary.DataBuilder** — console tool, one-shot ETL from public dictionary corpora into `dictionary.db3`
- **JapaneseDictionary.Tests** — xUnit, references Core only

### Data pipeline

`DataBuilder` parses JMdict, KANJIDIC2, KanjiVG, and Tatoeba raw files (`RawData/`) into a single read-only SQLite file, `dictionary.db3`, bundled into the app as a `MauiAsset` (`Resources/Raw/**`, with `LogicalName` stripping the `Raw` prefix). At runtime SQLite needs a real file path, not a package stream, so `DatabaseInitializer.EnsureDictionaryDatabaseAsync` copies it out of the app package into `FileSystem.AppDataDirectory` once on first use. User data (recents/history) lives in a separate writable SQLite file, `userdata.db3`, in the same directory.

### Startup / DI

`MauiProgram.cs` wires DI. `AppDataContext` is a singleton owning the whole data layer (`DictionaryRepository`, `UserDataRepository`, `SearchService`, `DaKanjiRecognizerService`) — it's constructed synchronously but initialized asynchronously, so every consumer must call `EnsureInitializedAsync()` before touching its properties (thread-safe, runs once via a `SemaphoreSlim`). ViewModels and pages are DI-registered transient. `HandwritingSheetPage` isn't Shell-routed — it's a modal opened from multiple call sites, so it's resolved via the `App.Services` static service-locator escape hatch instead.

### Search

`SearchService.Search()` is the single entry point: exact match → deinflection (`Deinflector.Deinflect` undoes conjugation endings with chained suffix rules in `Deinflector.Rules.cs`, then candidates are looked up exactly and filtered by `WordClass`; works on kanji and kana alike) → prefix match → English gloss substring (English mode only, minimum 2 characters — an unindexed scan otherwise). Romaji input is auto-converted to hiragana/katakana before matching (`RomajiConverter`).

### Theming

`Resources/Styles/Colors.xaml` defines Light/Dark token pairs, plus fixed-role accents (same value in both themes, e.g. the purple draw button) and a five-hue palette (`Hue0`..`Hue4` Fill/Text/Border × Light/Dark) cycled across word-class tags, badges, and kanji meta chips. `Styles.xaml`'s implicit styles consume these via `AppThemeBinding`, which reacts automatically to `Application.Current.UserAppTheme`. `ThemeService` (DI singleton, also stashed into `Application.Resources["ThemeService"]` by `App.xaml.cs` so XAML can bind to it directly) owns the light/dark toggle and persists the choice via `Preferences`.

`Label.TextColor` is `Color`-typed, not `Brush`, so it can't be pre-packaged as a theme-reactive resource the way `Border.Stroke`/`Background` can. Per-item hue-cycled colors go through `Converters/HueConverters.cs` instead, via `MultiBinding` of `[item hue index, ThemeService.IsDark]`.

Two easy-to-hit MAUI XAML traps that already bit this codebase once each:
- `Border` has both `Background` (`Brush`) and `BackgroundColor` (`Color`). The implicit `Border` style sets `Background="Transparent"` (needed so Borders with only a `Stroke` still hit-test across their full bounds for `TapGestureRecognizer`), and that silently wins over any local `BackgroundColor`. Always set `Background`, not `BackgroundColor`, for a Border's visible fill.
- A `FlexLayout` nested inside another `FlexLayout` doesn't report its own wrapped height back to the outer one, so a second wrapped row silently gets clipped by whatever follows it.

### Android / ONNX

Handwriting recognition (`DaKanjiRecognizer`) loads `char_classifier.onnx` via `Microsoft.ML.OnnxRuntime`. Getting `libonnxruntime.so` into the Android APK needs three explicit workarounds in `JapaneseDictionary.csproj` (forcing `AndroidApplication=true` early, explicit `RuntimeIdentifiers` even in Debug, and manually adding the `AndroidLibrary` item from the package's own contents) — read the comments there before touching the OnnxRuntime version or Android target settings.

### Design reference

`design/design_handoff_dictionary/` contains a Claude Design HTML prototype (`Japanese Dictionary.dc.html`) and a `README.md` documenting the exact color tokens, type system (Archivo for UI chrome, Noto Serif JP for all Japanese-language text), spacing, and per-screen copy/layout. It's a design reference only, built with a small in-house templating runtime — the task is to recreate it as MAUI XAML/styles, not port the HTML/CSS/JS.
