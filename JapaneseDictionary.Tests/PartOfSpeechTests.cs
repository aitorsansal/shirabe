using JapaneseDictionary.Core.Models;

using Xunit;

namespace JapaneseDictionary.Tests;

public class PartOfSpeechTests
{
    [Theory]
    [InlineData("n", PartOfSpeechGroup.Noun)]
    [InlineData("n-suf", PartOfSpeechGroup.Noun)]
    [InlineData("pn", PartOfSpeechGroup.Noun)]
    [InlineData("ctr", PartOfSpeechGroup.Noun)]
    [InlineData("v1", PartOfSpeechGroup.Verb)]
    [InlineData("v5u", PartOfSpeechGroup.Verb)]
    [InlineData("v5k-s", PartOfSpeechGroup.Verb)]
    [InlineData("vs", PartOfSpeechGroup.Verb)]
    [InlineData("vs-i", PartOfSpeechGroup.Verb)]
    [InlineData("vt", PartOfSpeechGroup.Verb)]
    [InlineData("vi", PartOfSpeechGroup.Verb)]
    [InlineData("vk", PartOfSpeechGroup.Verb)]
    [InlineData("v2k-s", PartOfSpeechGroup.Verb)]
    [InlineData("v4r", PartOfSpeechGroup.Verb)]
    [InlineData("aux-v", PartOfSpeechGroup.Verb)]
    [InlineData("adj-i", PartOfSpeechGroup.Adjective)]
    [InlineData("adj-na", PartOfSpeechGroup.Adjective)]
    [InlineData("adj-no", PartOfSpeechGroup.Adjective)]
    [InlineData("adv", PartOfSpeechGroup.Adverb)]
    [InlineData("adv-to", PartOfSpeechGroup.Adverb)]
    [InlineData("exp", PartOfSpeechGroup.Other)]
    [InlineData("prt", PartOfSpeechGroup.Other)]
    [InlineData("suf", PartOfSpeechGroup.Other)]
    [InlineData("conj", PartOfSpeechGroup.Other)]
    [InlineData("aux", PartOfSpeechGroup.Other)]
    [InlineData("aux-adj", PartOfSpeechGroup.Other)]
    [InlineData("unc", PartOfSpeechGroup.Other)]
    [InlineData("", PartOfSpeechGroup.Other)]
    public void GroupOf_ClassifiesTags(string tag, PartOfSpeechGroup expected) =>
        Assert.Equal(expected, PartOfSpeech.GroupOf(tag));
}
