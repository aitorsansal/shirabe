using System.Text;
using System.Text.Json;

using JapaneseDictionary.Core.Export;
using JapaneseDictionary.Core.Models;

namespace JapaneseDictionary.Tests;

public class CompanionExportTests
{
    private const string DictionaryAttribution =
        "JMdict/EDICT © Electronic Dictionary Research and Development Group, CC BY-SA 4.0 (edrdg.org).";
    private const string JlptAttribution =
        " JLPT levels: Jonathan Waller (tanos.co.uk), CC BY, via stephenmk/yomitan-jlpt-vocab, CC BY-SA 4.0.";

    private static DictionaryEntry Entry(string form = "猫", string reading = "ねこ", int id = 5, params string[] glosses) => new(
        id, [new KanjiForm(form, true)], [new ReadingForm(reading, true, true, [])],
        [new WordSense([], [], glosses.Length == 0 ? ["cat"] : glosses, [], [])]);

    private static CompanionExport.Row R(DictionaryEntry? entry = null, int? jlpt = null, params TranslatedSense[] translations) =>
        new(entry ?? Entry(), jlpt, translations);

    private static CompanionExport.Result Build(params CompanionExport.Row[] rows) =>
        CompanionExport.Build(rows, null, Guid.Empty);

    private static string[] Meanings(JsonElement entry, string language) =>
        entry.GetProperty("glosses").GetProperty(language).EnumerateArray().Select(g => g.GetString()!).ToArray();

    [Fact]
    public void PrefersCommonReadingOverUncommonKanjiSpelling()
    {
        var rare = new DictionaryEntry(1, [new KanjiForm("其れでは", false)], [new ReadingForm("それでは", true, true, [])],
            [new WordSense([], [], ["well then"], [], [])]);
        var common = new DictionaryEntry(2, [new KanjiForm("先生", true)], [new ReadingForm("せんせい", true, true, [])],
            [new WordSense([], [], ["teacher"], [], [])]);
        var neither = new DictionaryEntry(3, [new KanjiForm("珍語", false)], [new ReadingForm("ちんご", false, true, [])],
            [new WordSense([], [], ["rare word"], [], [])]);

        using var json = JsonDocument.Parse(Build(R(rare), R(common), R(neither)).Json);

        var forms = json.RootElement.GetProperty("entries").EnumerateArray().Select(e => e.GetProperty("form").GetString());
        Assert.Equal(["それでは", "先生", "珍語"], forms);
    }

    [Fact]
    public void DropsGlossesThatAreOnlyABracket()
    {
        using var json = JsonDocument.Parse(Build(R(translations: [new("spa", 1, ["camisa", "(shirt)"])])).Json);

        Assert.Equal(["camisa"], Meanings(json.RootElement.GetProperty("entries")[0], "es"));
    }

    [Fact]
    public void RejoinsMeaningsSplitInsideBracketsAndDropsLanguageTags()
    {
        using var json = JsonDocument.Parse(Build(R(translations: [new("spa", 1, ["tomarse (tiempo", "dinero)", "(pt) pan"])])).Json);

        Assert.Equal(["tomarse (tiempo, dinero)", "pan"], Meanings(json.RootElement.GetProperty("entries")[0], "es"));
    }

    [Fact]
    public void DropsLoanwordOriginNotesFromMeanings()
    {
        using var json = JsonDocument.Parse(Build(R(Entry("ラジオ", "ラジオ", 7, "radio"), translations: [new("spa", 1, ["radio (eng:)"])])).Json);

        Assert.Equal(["radio"], Meanings(json.RootElement.GetProperty("entries")[0], "es"));
    }

    [Fact]
    public void KeepsLoanwordWhoseOnlyGlossRomanizesItsReading()
    {
        var result = Build(R(Entry("ラジオ", "ラジオ", 7, "radio")));

        Assert.Equal((1, 0), (result.Exported, result.Skipped));
    }

    [Fact]
    public void Build_RoundTripsContractFieldsWithoutBomOrEscapedJapanese()
    {
        var requestId = Guid.NewGuid();
        var result = CompanionExport.Build([R(translations: [new("spa", 1, ["gato"])])], "  Animals  ", requestId);
        Assert.Equal((byte)'{', result.Json[0]);
        Assert.Contains("猫", Encoding.UTF8.GetString(result.Json));
        Assert.False(result.Json.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
        Assert.Equal((1, 0, false), (result.Exported, result.Skipped, result.Truncated));
        using var json = JsonDocument.Parse(result.Json);
        var root = json.RootElement;
        Assert.Equal(["v", "requestId", "listName", "attribution", "entries"], root.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, root.GetProperty("v").GetInt32());
        Assert.Equal(requestId.ToString("D"), root.GetProperty("requestId").GetString());
        Assert.Equal("Animals", root.GetProperty("listName").GetString());
        var entry = root.GetProperty("entries")[0];
        Assert.Equal(["source", "entryId", "form", "reading", "glosses"], entry.EnumerateObject().Select(p => p.Name));
        Assert.Equal("jmdict", entry.GetProperty("source").GetString());
        Assert.Equal(5, entry.GetProperty("entryId").GetInt32());
        Assert.Equal("猫", entry.GetProperty("form").GetString());
        Assert.Equal("ねこ", entry.GetProperty("reading").GetString());
        Assert.Equal(["cat"], Meanings(entry, "en"));
        Assert.Equal(["gato"], Meanings(entry, "es"));
    }

    [Fact]
    public void Build_UsesMeaningGlossesAndPrimaryForms()
    {
        var entry = new DictionaryEntry(1, [new KanjiForm("有難う", false)], [new ReadingForm("ありがとう", true, true, [])],
            [new WordSense([], [], ["arigatou", "thank you"], [], ["uk"])]);
        using var json = JsonDocument.Parse(Build(R(entry)).Json);
        var exported = json.RootElement.GetProperty("entries")[0];
        Assert.Equal("ありがとう", exported.GetProperty("form").GetString());
        Assert.Equal("ありがとう", exported.GetProperty("reading").GetString());
        Assert.Equal(["thank you"], Meanings(exported, "en"));
    }

    [Fact]
    public void Build_TrimsDropsEmptyAndOverlongMeaningsBeforeKeepingFirstFivePerLanguage()
    {
        var longMeaning = new string('x', 81);
        var limitMeaning = new string('y', 80);
        string[] meanings = [" ", " first ", longMeaning, "", limitMeaning, "third", "fourth", "fifth", "sixth"];
        var result = Build(R(Entry(glosses: meanings), translations: [new("spa", 1, meanings)]));
        using var json = JsonDocument.Parse(result.Json);
        var entry = json.RootElement.GetProperty("entries")[0];
        string[] expected = ["first", limitMeaning, "third", "fourth", "fifth"];
        Assert.Equal(expected, Meanings(entry, "en"));
        Assert.Equal(expected, Meanings(entry, "es"));
        Assert.DoesNotContain(longMeaning[..80], Meanings(entry, "en"));
    }

    [Fact]
    public void Build_GroupsTranslationsAndOrdersSensesBeforeApplyingMeaningLimit()
    {
        var result = Build(R(translations:
        [
            new("spa", 3, ["six", "seven"]),
            new("ger", 1, ["Katze"]),
            new("spa", 1, ["one", "two"]),
            new("fre", 1, ["chat"]),
            new("spa", 2, ["three", "four", "five"]),
            new("ca", 1, ["gat"]),
        ]));
        using var json = JsonDocument.Parse(result.Json);
        var entry = json.RootElement.GetProperty("entries")[0];
        Assert.Equal(["one", "two", "three", "four", "five"], Meanings(entry, "es"));
        Assert.Equal(["Katze"], Meanings(entry, "de"));
        Assert.Equal(["chat"], Meanings(entry, "fr"));
        Assert.Equal(["gat"], Meanings(entry, "ca"));
    }

    [Fact]
    public void Build_OmitsLanguagesWithoutSurvivingMeanings()
    {
        var result = Build(R(translations: [new("spa", 1, [" ", new string('x', 81)]), new("fre", 1, [])]));
        using var json = JsonDocument.Parse(result.Json);
        Assert.Equal(["en"], json.RootElement.GetProperty("entries")[0].GetProperty("glosses").EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public void Build_SkipsEntriesWithoutEnglishEvenWithTranslations()
    {
        var empty = Entry() with { Senses = [] };
        var result = Build(R(empty, translations: [new("spa", 1, ["gato"])]),
            R(Entry(glosses: [" ", new string('x', 81)])), R());
        Assert.Equal((1, 2, false), (result.Exported, result.Skipped, result.Truncated));
    }

    [Theory]
    [InlineData("<cat", "ねこ")]
    [InlineData("cat>", "ねこ")]
    [InlineData("ca\nt", "ねこ")]
    [InlineData("ca\0t", "ねこ")]
    [InlineData("ca\u007ft", "ねこ")]
    [InlineData("猫", "<ねこ")]
    [InlineData("猫", "ねこ>")]
    [InlineData("猫", "ね\tこ")]
    [InlineData("猫", "ね\u0085こ")]
    public void Build_SkipsMarkupAndControlCharactersInEitherForm(string form, string reading)
    {
        var result = Build(R(Entry(form, reading)));
        Assert.Equal(0, result.Exported);
        Assert.Equal(1, result.Skipped);
    }

    [Theory]
    [InlineData(32, 32, 1)]
    [InlineData(33, 32, 0)]
    [InlineData(32, 33, 0)]
    public void Build_EnforcesFormAndReadingLength(int formLength, int readingLength, int exported)
    {
        var result = Build(R(Entry(new string('猫', formLength), new string('ね', readingLength))));
        Assert.Equal(exported, result.Exported);
        Assert.Equal(1 - exported, result.Skipped);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData(" \t ", null)]
    [InlineData("  My list  ", "My list")]
    public void Build_NormalizesListName(string? name, string? expected)
    {
        using var json = JsonDocument.Parse(CompanionExport.Build([], name, Guid.Empty).Json);
        Assert.Equal(expected, json.RootElement.GetProperty("listName").GetString());
    }

    [Theory]
    [InlineData(59, 59)]
    [InlineData(58, 60)]
    public void Build_CutsListNameWithoutSplittingSurrogatePair(int prefixLength, int expectedLength)
    {
        var name = new string('a', prefixLength) + "😀" + "tail";
        using var json = JsonDocument.Parse(CompanionExport.Build([], name, Guid.Empty).Json);
        var actual = json.RootElement.GetProperty("listName").GetString()!;
        Assert.Equal(expectedLength, actual.Length);
        Assert.Equal(name[..expectedLength], actual);
        Assert.False(char.IsHighSurrogate(actual[^1]));
    }

    [Fact]
    public void Build_CutsLongListNameToSixtyCharacters()
    {
        using var json = JsonDocument.Parse(CompanionExport.Build([], new string('a', 61), Guid.Empty).Json);
        Assert.Equal(new string('a', 60), json.RootElement.GetProperty("listName").GetString());
    }

    [Theory]
    [InlineData(1999, false)]
    [InlineData(2000, false)]
    [InlineData(2001, true)]
    public void Build_CapsExportedEntries(int count, bool truncated)
    {
        var rows = Enumerable.Range(1, count).Select(id => R(Entry(id: id)));
        var result = CompanionExport.Build(rows, null, Guid.Empty);
        using var json = JsonDocument.Parse(result.Json);
        var entries = json.RootElement.GetProperty("entries");
        Assert.Equal(Math.Min(count, 2000), result.Exported);
        Assert.Equal(result.Exported, entries.GetArrayLength());
        Assert.Equal(result.Exported, entries[result.Exported - 1].GetProperty("entryId").GetInt32());
        Assert.Equal(0, result.Skipped);
        Assert.Equal(truncated, result.Truncated);
    }

    [Fact]
    public void Build_CapCountsOnlyExportedEntriesAndStopsEnumerating()
    {
        IEnumerable<CompanionExport.Row> Rows()
        {
            yield return R(Entry("<invalid"));
            for (var i = 0; i < 2000; i++) yield return R();
            yield return R(jlpt: 5);
            throw new InvalidOperationException("Must stop at the cap.");
        }
        var result = CompanionExport.Build(Rows(), null, Guid.Empty);
        Assert.Equal((2000, 1, true), (result.Exported, result.Skipped, result.Truncated));
        using var json = JsonDocument.Parse(result.Json);
        Assert.Equal(DictionaryAttribution, json.RootElement.GetProperty("attribution").GetString());
    }

    [Fact]
    public void Build_AlwaysCreditsDictionaryIncludingEmptyExport()
    {
        using var empty = JsonDocument.Parse(Build().Json);
        using var plain = JsonDocument.Parse(Build(R()).Json);
        Assert.Equal(DictionaryAttribution, empty.RootElement.GetProperty("attribution").GetString());
        Assert.Equal(DictionaryAttribution, plain.RootElement.GetProperty("attribution").GetString());
    }

    [Fact]
    public void Build_WritesJlptAndAppendsCreditWhenAnyExportedEntryHasLevel()
    {
        using var json = JsonDocument.Parse(Build(R(), R(jlpt: 4)).Json);
        var entries = json.RootElement.GetProperty("entries");
        Assert.False(entries[0].TryGetProperty("jlpt", out _));
        Assert.Equal(4, entries[1].GetProperty("jlpt").GetInt32());
        Assert.Equal(DictionaryAttribution + JlptAttribution, json.RootElement.GetProperty("attribution").GetString());
    }

    [Fact]
    public void Build_SkippedEntriesDoNotAddJlptCredit()
    {
        var empty = Entry() with { Senses = [] };
        using var json = JsonDocument.Parse(Build(R(empty, 1), R(Entry("<invalid"), 2), R()).Json);
        Assert.Equal(DictionaryAttribution, json.RootElement.GetProperty("attribution").GetString());
    }
}
