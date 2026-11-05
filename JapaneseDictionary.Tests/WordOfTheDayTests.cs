using JapaneseDictionary.Core.Search;

namespace JapaneseDictionary.Tests;

public class WordOfTheDayTests
{
    [Fact]
    public void PickIndex_IsDeterministic()
    {
        var date = new DateOnly(2026, 11, 5);
        Assert.Equal(1972, WordOfTheDay.PickIndex(date, 7700));
        Assert.Equal(WordOfTheDay.PickIndex(date, 7700), WordOfTheDay.PickIndex(date, 7700));
    }

    [Fact]
    public void PickIndex_StaysInRangeAndVariesAcrossDays()
    {
        var date = new DateOnly(2026, 11, 1);
        var indices = Enumerable.Range(0, 30).Select(day => WordOfTheDay.PickIndex(date.AddDays(day), 7700)).ToList();
        Assert.All(indices, index => Assert.InRange(index, 0, 7699));
        Assert.True(indices.Distinct().Count() >= 25);
        Assert.Equal(0, WordOfTheDay.PickIndex(DateOnly.MinValue, 7700));
        Assert.InRange(WordOfTheDay.PickIndex(DateOnly.MaxValue, int.MaxValue), 0, int.MaxValue - 1);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void PickIndex_HandlesSmallPools(int poolSize) =>
        Assert.Equal(0, WordOfTheDay.PickIndex(new DateOnly(2026, 11, 5), poolSize));
}
