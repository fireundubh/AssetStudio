using System.Collections;
using System.Collections.Generic;

namespace AssetStudio.Extensions
{
    public class WrappedEnumerator : IEnumerable
    {
        protected readonly IEnumerator enumerator;

        // ReSharper disable once MemberCanBeProtected.Global
        public WrappedEnumerator(IEnumerator enumerator)
        {
            this.enumerator = enumerator;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.enumerator;
        }

        public static WrappedEnumerator Create(IEnumerator e)
        {
            return new WrappedEnumerator(e);
        }

        public static WrappedEnumerator<T> Create<T>(IEnumerator<T> e)
        {
            return new WrappedEnumerator<T>(e);
        }
    }

    public sealed class WrappedEnumerator<T> : WrappedEnumerator, IEnumerable<T>
    {
        // ReSharper disable once SuggestBaseTypeForParameter
        public WrappedEnumerator(IEnumerator<T> enumerator) : base(enumerator)
        {
        }

        public IEnumerator<T> GetEnumerator()
        {
            return (IEnumerator<T>) this.enumerator;
        }
    }
}
