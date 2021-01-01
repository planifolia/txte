namespace txte.Text
{
    static class ConsoleStringExtensions
    {
        public static int GetRenderLength(this string value, bool ambiguousIsFullWidth)
        {
            int length = 0;
            foreach (var c in value)
            {
                length += GetEastAsianWidth(c, ambiguousIsFullWidth);
            }
            return length;
        }

        public static int GetEastAsianWidth(this char value, bool ambiguousIsFullWidth) =>
            value.GetWidthType() switch
            {
                EastAsianWidthTypes.A => ambiguousIsFullWidth ? 2 : 1,
                EastAsianWidthTypes.F or EastAsianWidthTypes.W => 2,
                EastAsianWidthTypes.N or EastAsianWidthTypes.H or EastAsianWidthTypes.Na or _ => 1,
            };

        public static (string value, bool hasFragmentedStart, bool hasFragmentedEnd) SubRenderString(
            this string value, int start, int length, bool ambiguousIsFullWidth
        )
        {
            int consolePos = 0;
            int valuePos = 0;

            while (consolePos < start)
            {
                consolePos += value[valuePos].GetEastAsianWidth(ambiguousIsFullWidth);
                valuePos++;
            }

            var startIsFragmented = start < consolePos;
            var valueStart = valuePos;
            if (value.Length <= valueStart)
            {
                return ("", startIsFragmented, false);
            }

            while (consolePos < start + length)
            {
                consolePos += value[valuePos].GetEastAsianWidth(ambiguousIsFullWidth);
                valuePos++;
            }

            var endIsFragmented = start + length < consolePos;
            var valueEnd = valuePos - (endIsFragmented ? 1 : 0);
            return (value.Substring(valueStart, valueEnd - valueStart), startIsFragmented, endIsFragmented);

        }
    }
}
