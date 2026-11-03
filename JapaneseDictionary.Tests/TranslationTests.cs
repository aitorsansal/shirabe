using JapaneseDictionary.Core.Data;
using JapaneseDictionary.Core.Models;

using Xunit;

namespace JapaneseDictionary.Tests;

/// <summary>Spanish/French/German meanings and sentence translations, against the
/// real dictionary.db3 (skipped if it hasn't been built yet), plus the pure
/// language helpers.</summary>
public class TranslationTests : IDisposable
{
    private const string DbPath = "../../../../JapaneseDictionary/Resources/Raw/dictionary.db3";
    private const int Taberu = 1358280;
    private const int Kami = 1311530;
    private readonly DictionaryRepository? repo;

    public TranslationTests()
    {
        if (File.Exists(DbPath))
            repo = new DictionaryRepository(DbPath);
    }

    public void Dispose() => repo?.Dispose();

    [SkippableFact]
    public void Taberu_HasSpanishComer()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var senses = repo!.GetTranslatedSenses(Taberu, ["spa"]);
        Assert.All(senses, s => Assert.Equal("spa", s.Lang));
        Assert.Contains(senses, s => s.Glosses.Contains("comer"));
    }

    [SkippableFact]
    public void Taberu_HasGermanEssen()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var senses = repo!.GetTranslatedSenses(Taberu, ["ger"]);
        Assert.Contains(senses, s => s.Glosses.Any(g => g.Contains("essen")));
        Assert.Equal(Enumerable.Range(1, senses.Count), senses.Select(s => s.Order));
    }

    [SkippableFact]
    public void Kami_HasFrenchPapier()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var senses = repo!.GetTranslatedSenses(Kami, ["fre"]);
        Assert.Contains(senses, s => s.Glosses.Contains("papier"));
    }

    [SkippableFact]
    public void EmptyLanguageList_ReturnsNothing()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        Assert.Empty(repo!.GetTranslatedSenses(Taberu, []));

        var ids = repo.GetExampleSentences(Taberu, 20).Select(s => s.Id).ToList();
        Assert.NotEmpty(ids);
        Assert.Empty(repo.GetSentenceTranslations(ids, []));
    }

    [SkippableFact]
    public void Taberu_SomeExampleSentenceHasSpanishTranslation()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var ids = repo!.GetExampleSentences(Taberu, 50).Select(s => s.Id).ToList();
        var translations = repo.GetSentenceTranslations(ids, ["spa"]);

        Assert.NotEmpty(translations);
        Assert.All(translations, t =>
        {
            Assert.Equal("spa", t.Lang);
            Assert.Contains(t.SentenceId, ids);
            Assert.NotEmpty(t.Text);
        });
        Assert.Equal(translations.Count, translations.Select(t => t.SentenceId).Distinct().Count());
    }

    [SkippableFact]
    public void EnglishSensesOfTaberu_AreUnchanged()
    {
        Skip.If(repo == null, "dictionary.db3 not built");
        var entry = repo!.GetEntry(Taberu);
        Assert.NotNull(entry);
        Assert.Contains(entry!.Senses, s => s.Glosses.Contains("to eat"));
        Assert.All(entry.Senses, s => Assert.NotEmpty(s.Glosses));
    }

    [Theory]
    [InlineData("es-ES", "spa")]
    [InlineData("es", "spa")]
    [InlineData("ca-ES", "spa")]
    [InlineData("ca", "spa")]
    [InlineData("fr-CA", "fre")]
    [InlineData("de_AT", "ger")]
    [InlineData("DE", "ger")]
    public void DefaultFor_PicksTheDeviceLanguage(string culture, string expected) =>
        Assert.Equal([expected], TranslationLanguages.DefaultFor(culture));

    [Theory]
    [InlineData("en-GB")]
    [InlineData("ja-JP")]
    [InlineData("")]
    [InlineData(null)]
    public void DefaultFor_OtherLanguagesSelectNothing(string? culture) =>
        Assert.Empty(TranslationLanguages.DefaultFor(culture));

    [Fact]
    public void Normalize_DropsUnknownCodesAndKeepsListOrder()
    {
        Assert.Equal(["spa", "ger"], TranslationLanguages.Normalize(["ger", "xx", "spa", "spa"]));
        Assert.Empty(TranslationLanguages.Normalize([]));
    }

    [Fact]
    public void LanguageList_HasEndonymsAndUniqueCodes()
    {
        Assert.Equal(["Español", "Français", "Deutsch"], TranslationLanguages.All.Select(l => l.Endonym));
        Assert.Equal(TranslationLanguages.All.Count, TranslationLanguages.All.Select(l => l.Code).Distinct().Count());
        Assert.Equal("fra", TranslationLanguages.Find("fre")!.TatoebaCode);
    }
}
