using System.Collections.Generic;

namespace txte
{
    static class StringExtensions
    {
        public static IEnumerable<int> IndicesOf(this string @this, string value, bool allowOverlap)
        {
            var thisLength = @this.Length;
            if (thisLength == 0) yield break;

            var step = (allowOverlap) ? 1 : value.Length;

            int startIndex = 0;
            while (@this.IndexOf(value, startIndex) is var found && found != -1)
            {
                startIndex = found + step;
                yield return found;
                if (startIndex == thisLength) yield break;
            }
        }
    }
}
