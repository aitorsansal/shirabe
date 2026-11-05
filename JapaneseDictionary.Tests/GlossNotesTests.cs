using JapaneseDictionary.Core.Search;

namespace JapaneseDictionary.Tests;

public class GlossNotesTests
{
    [Theory]
    [InlineData("(eng: timely) oportuno", "oportuno")]
    [InlineData("tiro con arco (eng: archery)", "tiro con arco")]
    [InlineData("arquitectura (eng:architecture)", "arquitectura")]
    [InlineData("producto nacional bruto (eng: gross national product, GNP)", "producto nacional bruto")]
    [InlineData("caramelo (por:)", "caramelo")]
    [InlineData("pan (fre: pain) blanco", "pan blanco")]
    public void WithoutOrigin_DropsTheLoanwordNote(string gloss, string expected) =>
        Assert.Equal(expected, GlossNotes.WithoutOrigin(gloss));

    [Theory]
    [InlineData("tal que así (lit: en lo que se tarda en decir \"Ah!\")")]
    [InlineData("comer")]
    [InlineData("detener (algo)")]
    [InlineData("hora: 10")]
    public void WithoutOrigin_LeavesOtherTextAlone(string gloss) =>
        Assert.Equal(gloss, GlossNotes.WithoutOrigin(gloss));

    [Fact]
    public void WithoutOrigin_NoteOnly_ComesBackUnchanged() =>
        Assert.Equal("(eng: curry)", GlossNotes.WithoutOrigin("(eng: curry)"));
}
