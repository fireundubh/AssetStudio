using System.Collections.Concurrent;

namespace AssetStudio
{
    public static class ConcurrentDictionaryExtensions
    {
        public static bool Add<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> d, TKey key, TValue value) => d.TryAdd(key, value);
    }
}