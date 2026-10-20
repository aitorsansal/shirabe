namespace JapaneseDictionary.Core.Search;

// Tiebreak between equally common words: how many Tatoeba sentences are linked to
// each, in coarse bands so near counts stay tied. Below MinCount it says nothing,
// because some everyday words (大切, 韓国) have no linked sentences at all and a
// rare homophone with a handful must not outrank them.
internal static class ExampleEvidence
{
    private const int MinCount = 20;
    private const double BandBase = 4;

    public static int Band(int count) =>
        count < MinCount ? 0 : 1 + (int)Math.Log((double)count / MinCount, BandBase);
}
