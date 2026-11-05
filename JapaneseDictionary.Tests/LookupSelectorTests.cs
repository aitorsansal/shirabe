using JapaneseDictionary.Core.Search;

namespace JapaneseDictionary.Tests;

public class LookupSelectorTests
{
    [Fact]
    public void PickEntry_NullReading_ReturnsFirstForm() =>
        Assert.Equal(3, LookupSelector.PickEntry([3, 2, 1], null));

    [Fact]
    public void PickEntry_EmptyReading_ReturnsFirstForm() =>
        Assert.Equal(3, LookupSelector.PickEntry([3, 2, 1], []));

    [Fact]
    public void PickEntry_Intersection_PreservesFormRank() =>
        Assert.Equal(2, LookupSelector.PickEntry([3, 2, 1], [1, 2]));

    [Fact]
    public void PickEntry_NoIntersection_ReturnsFirstForm() =>
        Assert.Equal(3, LookupSelector.PickEntry([3, 2, 1], [4, 5]));

    [Fact]
    public void PickEntry_EmptyForms_ReturnsNull()
    {
        Assert.Null(LookupSelector.PickEntry([], null));
        Assert.Null(LookupSelector.PickEntry([], []));
        Assert.Null(LookupSelector.PickEntry([], [1]));
    }
}
