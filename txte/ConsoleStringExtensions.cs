﻿namespace cilo
{
    static class ConsoleStringExtensions
    {
        public static int GetConsoleLength(this string value, bool ambiguousIsFullWidth)
        {
            int length = 0;
            foreach (var c in value)
            {
                switch (c.GetEastAsianWidthType())
                {
                    case EastAsianWidthTypes.A:
                        length += ambiguousIsFullWidth ? 2 : 1;
                        break;
                    case EastAsianWidthTypes.F:
                    case EastAsianWidthTypes.W:
                        length += 2;
                        break;
                    case EastAsianWidthTypes.N:
                    case EastAsianWidthTypes.H:
                    case EastAsianWidthTypes.Na:
                    default:
                        length += 1;
                        break;
                }
            }
            return length;
        }

        public static int GetEastAsianWidth(this char value, bool ambiguousIsFullWidth)
        {

            switch (value.GetEastAsianWidthType())
            {
                case EastAsianWidthTypes.A:
                    return ambiguousIsFullWidth ? 2 : 1;
                case EastAsianWidthTypes.F:
                case EastAsianWidthTypes.W:
                    return 2;
                case EastAsianWidthTypes.N:
                case EastAsianWidthTypes.H:
                case EastAsianWidthTypes.Na:
                default:
                    return 1;
            }
        }

        public static (string value, bool hasFragmentedStart, bool hasFragmentedEnd) SubConsoleString(
            this string value, int start, int length, bool ambiguousIsFullWidth)
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