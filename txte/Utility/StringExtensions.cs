using System;
using System.Collections.Generic;
using System.Text;

namespace txte
{
    static class StringExtensions
    {
        public static IEnumerable<int> Indices(this string @this, string value)
        {
            if (@this.Length == 0) { yield break; }
            int lastIndex = -1;
            while (@this.IndexOf(value, lastIndex + 1) is var found && found != -1)
            {
                lastIndex = found;
                yield return found;
                if (found == @this.Length - 1) { yield break; }
            }
        }
    }
}
