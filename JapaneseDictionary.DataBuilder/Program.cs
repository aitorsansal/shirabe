using System.Diagnostics;

using JapaneseDictionary.Core.Models;
using JapaneseDictionary.DataBuilder;
using JapaneseDictionary.DataBuilder.Parsers;

SQLitePCL.Batteries_V2.Init();

var sw = Stopwatch.StartNew();

void Log(string message) => Console.WriteLine($"[{sw.Elapsed:mm\\:ss}] {message}");

Log("Parsing JMdict...");
var jmdict = JmdictParser.Parse("RawData/JMdict");
var entries = jmdict.Entries;
var translatedSenses = jmdict.TranslatedSenses;
Log($"  {entries.Count} dictionary entries");
foreach (var lang in TranslationLanguages.All)
    Log($"  {translatedSenses.Count(kv => kv.Value.Any(s => s.Lang == lang.Code))} entries with {lang.Code} glosses");

Log("Parsing KANJIDIC2...");
var kanji = Kanjidic2Parser.Parse("RawData/kanjidic2.xml");
Log($"  {kanji.Count} kanji");

Log("Parsing KanjiVG...");
var kanjiStrokes = KanjiVgParser.Parse("RawData/kanjivg.xml");
Log($"  stroke data for {kanjiStrokes.Count} characters");

Log("Parsing Tatoeba example sentences (this cross-references JMdict, may take a moment)...");
var sentences = TatoebaParser.Parse("RawData/jpn_indices.csv", "RawData/sentences.csv", entries);
Log($"  {sentences.Count} example sentences, {sentences.Count(s => s.LinkedEntryIds.Count > 0)} linked to at least one entry");

Log("Collecting Tatoeba sentence translations (streams the ~450 MB links file)...");
var sentenceTranslations = TatoebaParser.ParseTranslations("RawData/links.csv", "RawData/sentences.csv", sentences.Select(s => s.Id));
foreach (var lang in TranslationLanguages.All)
    Log($"  {sentenceTranslations.Count(t => t.Lang == lang.Code)} sentences with a {lang.Code} translation");

Log("Matching Kanjium pitch accents to entries...");
var pitch = PitchAccentParser.Parse("RawData/accents.txt", entries);
Log($"  {pitch.ByEntry.Count} entries with pitch accent ({pitch.ByEntry.Sum(kv => kv.Value.Count)} readings); {pitch.Rows} Kanjium rows, {pitch.MalformedRows} malformed, {pitch.UnmatchedRows} unmatched");
Log($"  {pitch.KanaRowMatchesOnKanjiEntries} kana-only row matches landed on entries that have kanji spellings");

Log("Reading JLPT levels...");
var (wordJlpt, wordJlptUnmatched) = JlptParser.ParseWords("RawData/jlpt", entries.Select(e => e.Id).ToHashSet());
var kanjiJlpt = JlptParser.ParseKanji("RawData/JLPT_kanji_ALL.json");
var knownKanji = kanji.Select(k => k.Character).ToHashSet();
for (var level = 5; level >= 1; level--)
    Log($"  N{level}: {wordJlpt.Count(kv => kv.Value == level)} words ({wordJlptUnmatched[level]} list rows not in JMdict), {kanjiJlpt.Count(kv => kv.Value == level && knownKanji.Contains(kv.Key))} kanji");
Log($"  {kanjiJlpt.Count(kv => !knownKanji.Contains(kv.Key))} listed kanji not in KANJIDIC2");

const string outputPath = "../JapaneseDictionary/Resources/Raw/dictionary.db3";
Log($"Writing {outputPath}...");
SqliteWriter.Write(outputPath, entries, kanji, kanjiStrokes, sentences, translatedSenses, sentenceTranslations, pitch.ByEntry, wordJlpt, kanjiJlpt);

// The app recopies its installed dictionary whenever this value changes (see
// DatabaseInitializer), so every regenerated database must get a new one.
const string versionPath = "../JapaneseDictionary/Resources/Raw/dictionary.version";
var dataVersion = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
File.WriteAllText(versionPath, dataVersion + "\n");
Log($"Data version {dataVersion} written to {versionPath}");

var size = new FileInfo(outputPath).Length;
Log($"Done. {size / 1024.0 / 1024.0:F1} MB");
