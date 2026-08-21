using JapaneseDictionary.Core.Data.Schema;
using JapaneseDictionary.Core.Models;

using SQLite;

namespace JapaneseDictionary.DataBuilder;

/// <summary>
/// Writes parsed dictionary/kanji/example data into the app's bundled SQLite
/// file. Uses single-row <c>Insert</c> calls throughout rather than
/// <c>InsertAll</c> — plenty fast for a one-time build step (the whole write
/// takes well under a minute), and keeps every table's write path identical
/// and easy to reason about.
/// </summary>
public static class SqliteWriter
{
    public static void Write(
        string outputPath,
        IReadOnlyList<DictionaryEntry> entries,
        IReadOnlyList<KanjiInfo> kanji,
        IReadOnlyDictionary<string, List<string>> kanjiStrokes,
        IReadOnlyList<ExampleSentence> sentences)
    {
        if (File.Exists(outputPath)) File.Delete(outputPath);

        using var db = new SQLiteConnection(outputPath);

        db.CreateTable<WordEntryRow>();
        db.CreateTable<KanjiFormRow>();
        db.CreateTable<ReadingFormRow>();
        db.CreateTable<SenseRow>();
        db.CreateTable<GlossRow>();
        db.CreateTable<KanjiRow>();
        db.CreateTable<KanjiStrokeRow>();
        db.CreateTable<ExampleSentenceRow>();
        db.CreateTable<ExampleSentenceLinkRow>();

        var nextSenseId = 1;
        db.RunInTransaction(() =>
        {
            foreach (var entry in entries)
                WriteEntry(db, entry, ref nextSenseId);
        });

        db.RunInTransaction(() =>
        {
            foreach (var k in kanji)
            {
                db.Insert(new KanjiRow
                {
                    Character = k.Character,
                    StrokeCount = k.StrokeCount,
                    Grade = k.Grade,
                    JlptLevel = k.JlptLevel,
                    FrequencyRank = k.FrequencyRank,
                    RadicalNumber = k.RadicalNumber,
                    SkipCode = k.SkipCode,
                    OnReadingsJoined = string.Join(';', k.OnReadings),
                    KunReadingsJoined = string.Join(';', k.KunReadings),
                    MeaningsJoined = string.Join(';', k.Meanings),
                });
            }
        });

        db.RunInTransaction(() =>
        {
            foreach (var (character, paths) in kanjiStrokes)
            {
                for (var i = 0; i < paths.Count; i++)
                {
                    db.Insert(new KanjiStrokeRow
                    {
                        Character = character,
                        StrokeIndex = i + 1,
                        SvgPath = paths[i],
                    });
                }
            }
        });

        db.RunInTransaction(() =>
        {
            foreach (var s in sentences)
            {
                db.Insert(new ExampleSentenceRow
                {
                    Id = s.Id,
                    Japanese = s.Japanese,
                    English = s.English,
                });

                foreach (var entryId in s.LinkedEntryIds)
                {
                    db.Insert(new ExampleSentenceLinkRow
                    {
                        SentenceId = s.Id,
                        EntryId = entryId,
                    });
                }
            }
        });
    }

    private static void WriteEntry(SQLiteConnection db, DictionaryEntry entry, ref int nextSenseId)
    {
        var wordClasses = entry.Senses.SelectMany(s => s.WordClasses).Distinct().ToList();

        db.Insert(new WordEntryRow
        {
            Id = entry.Id,
            PrimaryText = entry.PrimaryText,
            PrimaryReading = entry.PrimaryReading,
            IsCommon = entry.KanjiForms.Any(k => k.IsCommon) || entry.Readings.Any(r => r.IsCommon),
            WordClassesJoined = string.Join(';', wordClasses),
        });

        foreach (var k in entry.KanjiForms)
        {
            db.Insert(new KanjiFormRow
            {
                EntryId = entry.Id,
                Text = k.Text,
                IsCommon = k.IsCommon,
            });
        }

        foreach (var r in entry.Readings)
        {
            db.Insert(new ReadingFormRow
            {
                EntryId = entry.Id,
                Text = r.Text,
                IsCommon = r.IsCommon,
            });
        }

        var senseOrder = 0;
        foreach (var sense in entry.Senses)
        {
            senseOrder++;
            var senseId = nextSenseId++;
            db.Insert(new SenseRow
            {
                Id = senseId,
                EntryId = entry.Id,
                SenseOrder = senseOrder,
                PartOfSpeechTagsJoined = string.Join(';', sense.PartOfSpeechTags),
                FieldsJoined = string.Join(';', sense.Fields),
                MiscTagsJoined = string.Join(';', sense.MiscTags),
            });

            var glossOrder = 0;
            foreach (var text in sense.Glosses)
            {
                db.Insert(new GlossRow
                {
                    SenseId = senseId,
                    GlossOrder = ++glossOrder,
                    Text = text,
                });
            }
        }
    }
}
