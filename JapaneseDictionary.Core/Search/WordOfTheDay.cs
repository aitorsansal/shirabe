namespace JapaneseDictionary.Core.Search;

public static class WordOfTheDay
{
    public static int PickIndex(DateOnly date, int poolSize) =>
        poolSize <= 0 ? 0 : (int)((long)date.DayNumber * 1000003 % poolSize);
}
