using System.Diagnostics;

using JapaneseDictionary.DataBuilder;
using JapaneseDictionary.DataBuilder.Parsers;

SQLitePCL.Batteries_V2.Init();

var sw = Stopwatch.StartNew();

void Log(string message) => Console.WriteLine($"[{sw.Elapsed:mm\\:ss}] {message}");

Log("Parsing JMdict...");
var entries = JmdictParser.Parse("RawData/JMdict_e");
Log($"  {entries.Count} dictionary entries");

Log("Parsing KANJIDIC2...");
var kanji = Kanjidic2Parser.Parse("RawData/kanjidic2.xml");
Log($"  {kanji.Count} kanji");

Log("Parsing KanjiVG...");
var kanjiStrokes = KanjiVgParser.Parse("RawData/kanjivg.xml");
Log($"  stroke data for {kanjiStrokes.Count} characters");

Log("Parsing Tatoeba example sentences (this cross-references JMdict, may take a moment)...");
var sentences = TatoebaParser.Parse("RawData/jpn_indices.csv", "RawData/sentences.csv", entries);
Log($"  {sentences.Count} example sentences, {sentences.Count(s => s.LinkedEntryIds.Count > 0)} linked to at least one entry");

const string outputPath = "../JapaneseDictionary/Resources/Raw/dictionary.db3";
Log($"Writing {outputPath}...");
SqliteWriter.Write(outputPath, entries, kanji, kanjiStrokes, sentences);

var size = new FileInfo(outputPath).Length;
Log($"Done. {size / 1024.0 / 1024.0:F1} MB");
