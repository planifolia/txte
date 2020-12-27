using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using txte.Settings;

namespace txte.Text
{
    class ColoredString
    {
        public static ColoredString Concat(Setting setting, params (string value, ColorSet color)[] parts)
        {
            var body = new StringBuilder();
            var colors = new List<ColorSpan>();
            foreach (var (value, color) in parts)
            {
                if (string.IsNullOrEmpty(value)) { continue; }
                var current = body.Length;
                body.Append(value);
                colors.Add(new ColorSpan(color, new Range(current, body.Length)));
            }
            return new ColoredString(setting, body.ToString(), false, new Coloring(colors), false);
        }

        public ColoredString(Setting setting, string body)
            : this(
                setting,
                body,
                false,
                new Coloring(
                    (body.Length == 0)
                    ? new List<ColorSpan>()
                    : new List<ColorSpan>
                    {
                        new ColorSpan(ColorSet.Default, new Range(0, body.Length))
                    }
                ),
                false)
        { }

        public ColoredString(
            Setting setting, string body,
            bool hasFlagmentedHead, Coloring colors, bool hasFlagmentedTail
        ) =>
            (this.setting, this.body, this.colors, this.hasFragmentedHead, this.hasFragmentedTail) =
                (setting, body, colors, hasFlagmentedHead, hasFlagmentedTail);

        readonly Setting setting;
        readonly string body;
        readonly bool hasFragmentedHead;
        readonly Coloring colors;
        readonly bool hasFragmentedTail;

        public ColoredString Overlay(Coloring ovarlay)
        {
            if (this.colors.Count == 0) { return this; }

            var valueBegin = this.colors[0].ValueRange.Begin;
            var valueEnd = this.colors[^1].ValueRange.End;

            var clipedOverlay = ovarlay.Clip(valueBegin, valueEnd);


            return new ColoredString(
                this.setting, this.body,
                this.hasFragmentedHead, colors.Overlay(clipedOverlay), this.hasFragmentedTail);
        }

        public IEnumerable<StyledString> ToStyledStrings()
        {
            if (this.hasFragmentedHead) { yield return StyledString.LeftFragment; }
            foreach (var color in this.colors)
            {
                yield return new StyledString(
                    this.body.Substring(color.ValueRange.Begin, color.ValueRange.End - color.ValueRange.Begin),
                    color.Color
                );
            }
            if (this.hasFragmentedTail) { yield return StyledString.RightFragment; }
        }

        public ColoredString SubRenderString(int renderStart, int renderLength)
        {
            if (this.colors.Count == 0)
            {
                return this;
            }
            using var colorSource = this.colors.GetEnumerator();
            bool ambiguousIsFullWidth = this.setting.IsFullWidthAmbiguous;
            int renderPos = 0;
            int rangeBegin = this.colors[0].ValueRange.Begin;
            int rangeEnd = this.colors[0].ValueRange.End;
            int valuePos = rangeBegin;

            if (this.hasFragmentedHead)
            {
                renderPos++;
            }
            while (renderPos < renderStart)
            {
                renderPos += this.body[valuePos].GetEastAsianWidth(ambiguousIsFullWidth);
                valuePos++;
            }
            var startIsFragmented = renderStart < renderPos;
            var valueStart = valuePos;

            // find last position
            while (renderPos < renderStart + renderLength)
            {
                renderPos += this.body[valuePos].GetEastAsianWidth(ambiguousIsFullWidth);
                valuePos++;
            }

            // if the last position of substring is the same as the one of original string,
            // the same value is computed.
            var endIsFragmented = renderStart + renderLength < renderPos;
            var valueEnd = valuePos - (endIsFragmented ? 1 : 0);

            return new ColoredString(
                this.setting, this.body,
                startIsFragmented, this.colors.Clip(valueStart, valueEnd), endIsFragmented
            );
        }

        public int GetRenderLength()
        {
            if (this.colors.Count == 0) { return 0; }

            var ambiguousIsFullWidth = this.setting.IsFullWidthAmbiguous;
            var start = this.colors[0].ValueRange.Begin;
            var end = this.colors[^1].ValueRange.End;
            int length = 0;
            for (int i = start; i < end; i++)
            {
                length += this.body[i].GetEastAsianWidth(ambiguousIsFullWidth);
            }
            return length;
        }
    }


    class ColorSpan : IComparable<ColorSpan>
    {
        public ColorSpan(ColorSet color, Range valueRange) =>
            (this.Color, this.ValueRange) = (color, valueRange);

        public Range ValueRange;
        public ColorSet Color;

        public int CompareTo(ColorSpan? other)
        {
            if (other == null) { return 1; }
            if (this.ValueRange.Begin < other.ValueRange.Begin) { return -1; }
            if (other.ValueRange.Begin < this.ValueRange.Begin) { return 1; }

            if (this.ValueRange.End < other.ValueRange.End) { return -1; }
            if (other.ValueRange.End < this.ValueRange.End) { return 1; }

            return 0;
        }

        public override string ToString() => $"{this.Color}{this.ValueRange}";
    }

}