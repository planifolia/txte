using System;

namespace txte
{
    static class MathExtensions
    {
        public static int AtMin(this int value, int least)
            => Math.Max(value, least);

        public static int AtMax(this int value, int most)
            => Math.Min(value, most);

        public static int Clamp(this int value, int min, int max)
            => Math.Clamp(value, min, max);
    }

}