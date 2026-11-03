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

const string outputPath = "../JapaneseDictionary/Resources/Raw/dictionary.db3";
Log($"Writing {outputPath}...");
SqliteWriter.Write(outputPath, entries, kanji, kanjiStrokes, sentences, translatedSenses, sentenceTranslations);

// The app recopies its installed dictionary whenever this value changes (see
// DatabaseInitializer), so every regenerated database must get a new one.
const string versionPath = "../JapaneseDictionary/Resources/Raw/dictionary.version";
var dataVersion = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss'Z'");
File.WriteAllText(versionPath, dataVersion + "\n");
Log($"Data version {dataVersion} written to {versionPath}");

var size = new FileInfo(outputPath).Length;
Log($"Done. {size / 1024.0 / 1024.0:F1} MB");
