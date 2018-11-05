using System.Collections;
using System.Collections.Generic;

namespace AssetStudio.Extensions
{
    public static class EnumeratorExtensions
    {
        public static IEnumerable AsEnumerable(this IEnumerator e)
        {
            return WrappedEnumerator.Create(e);
        }

        public static IEnumerable<T> AsEnumerable<T>(this IEnumerator<T> e)
        {
            return WrappedEnumerator.Create(e);
        }
    }
}
