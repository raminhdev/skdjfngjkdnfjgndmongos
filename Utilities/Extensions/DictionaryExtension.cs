namespace Utilities.Extensions
{
    public static class DictionaryExtension
    {
        public static Dictionary<X, Y> AddRange<X, Y>(this Dictionary<X, Y> sourceDict, Dictionary<X, Y> destDict)
        {
            foreach (var item in destDict)
                if (!sourceDict.ContainsKey(item.Key))
                    sourceDict.Add(item.Key, item.Value);

            return sourceDict;
        }

        public static Dictionary<X, Y> UpsertRange<X, Y>(this Dictionary<X, Y> sourceDict, Dictionary<X, Y> destDict)
        {
            foreach (var item in destDict)
                sourceDict[item.Key] = item.Value;

            return sourceDict;
        }
    }
}
