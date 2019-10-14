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
            var colors = new SortedSet<ColorSpan>();
            foreach (var (value, color) in parts)
            {
                if (string.IsNullOrEmpty(value)) { continue; } 
                var current = body.Length;
                body.Append(value);
                colors.Add(new ColorSpan(color, new Range(current, body.Length)));
            }
            return new ColoredString(setting, body.ToString(), false, colors.ToImmutableSortedSet(), false);
        }

        public ColoredString(Setting setting, string body)
            : this (
                setting,
                body,
                false,
                (body.Length == 0)
                    ? ImmutableSortedSet<ColorSpan>.Empty
                    : new SortedSet<ColorSpan>
                    {
                        new ColorSpan(ColorSet.Default, new Range(0, body.Length))
                    }.ToImmutableSortedSet(),
                false)
        { }

        public ColoredString(
            Setting setting, string body,
            bool hasFlagmentedHead, ImmutableSortedSet<ColorSpan> colors, bool hasFlagmentedTail
        ) =>
            (this.setting, this.body, this.colors, this.hasFragmentedHead, this.hasFragmentedTail) = 
                (setting, body, colors, hasFlagmentedHead, hasFlagmentedTail);

        readonly Setting setting; 
        readonly string body;
        readonly bool hasFragmentedHead;
        readonly ImmutableSortedSet<ColorSpan> colors;
        readonly bool hasFragmentedTail;

        public ColoredString Overlay(ImmutableSortedSet<ColorSpan> ovarlay)
        {
            if (this.colors.Count == 0) { return this; }

            var valueBegin = this.colors[0].ValueRange.Begin;
            var valueEnd = this.colors[^1].ValueRange.End;

            var clipedOverlay = new SortedSet<ColorSpan>();
            using (var colorSource = ovarlay.GetEnumerator())
            {
                // add head span
                while(true)
                {
                    if (!colorSource.MoveNext()) { break; }
                    var head = colorSource.Current;
                    if (valueBegin < head.ValueRange.End)
                    {
                        clipedOverlay.Add(new ColorSpan(head.Color,
                            new Range(head.ValueRange.Begin.AtMin(valueBegin), head.ValueRange.End)));
                        break;
                    }
                    
                }
                // add body span
                while(true)
                {
                    if (!colorSource.MoveNext()) { break; }
                    var body = colorSource.Current;
                    if (body.ValueRange.End < valueEnd)
                    {
                        clipedOverlay.Add(new ColorSpan(body.Color,
                            new Range(body.ValueRange.Begin.AtMin(valueBegin), body.ValueRange.End)));
                    }
                    else if (valueEnd < body.ValueRange.Begin)
                    {
                        clipedOverlay.Add(new ColorSpan(body.Color,
                            new Range(body.ValueRange.Begin, body.ValueRange.End.AtMax(valueEnd))));
                    }
                    else
                    {
                        break; // out of range
                    }
                }
            }
            var mergedColor = new SortedSet<ColorSpan>();
            var originalColors = new Stack<ColorSpan>(this.colors.Reverse());
            if (originalColors.Count == 0) { return this; }
            foreach (var overColor in clipedOverlay)
            {
                while(true)
                {
                    
                    if (!originalColors.TryPop(out var before)) { break; }
                    if (before.ValueRange.End <= overColor.ValueRange.Begin)
                    {
                        // [--base--)
                        //          [--over--)
                        mergedColor.Add(before);
                    }
                    else
                    {
                        // [--base----...       || [--base--...
                        //           [--over--) || [--over------)
                        if (before.ValueRange.Begin < overColor.ValueRange.Begin)
                        {
                            mergedColor.Add(
                                new ColorSpan(
                                    before.Color,
                                    new Range(
                                        before.ValueRange.Begin,
                                        before.ValueRange.End.AtMax(overColor.ValueRange.Begin)
                                    )
                                )
                            );
                        }
                        if (overColor.ValueRange.End < before.ValueRange.End)
                        {
                            originalColors.Push(
                                new ColorSpan(
                                    before.Color,
                                    new Range(
                                        before.ValueRange.Begin.AtMin(overColor.ValueRange.End),
                                        before.ValueRange.End
                                    )
                                )
                            );
                        }
                        break;
                    }
                }
                while(true)
                {
                    if (!originalColors.TryPop(out var overlapped)) { break; }
                    if (overColor.ValueRange.End <= overlapped.ValueRange.Begin)
                    {
                        mergedColor.Add(overColor);
                        originalColors.Push(overlapped);
                        break; // move to next
                    }

                    if (overlapped.ValueRange.Begin < overColor.ValueRange.Begin)
                    {
                        mergedColor.Add(
                            new ColorSpan(
                                overlapped.Color, 
                                new Range(
                                    overlapped.ValueRange.Begin,
                                    overlapped.ValueRange.End.AtMax(overColor.ValueRange.Begin)
                                )
                            )
                        );
                    }
                    else if (overlapped.ValueRange.End <= overColor.ValueRange.End)
                    {
                        // overrided all
                    }
                    else
                    {
                        originalColors.Push(
                            new ColorSpan(
                                overlapped.Color, 
                                new Range(
                                    overlapped.ValueRange.Begin.AtMin(overColor.ValueRange.End),
                                    overlapped.ValueRange.End
                                )
                            )
                        );
                    }
                }
            }
            // rest
            foreach (var color in originalColors)
            {
                mergedColor.Add(color);
            }
            return new ColoredString(
                this.setting, this.body, 
                this.hasFragmentedHead, mergedColor.ToImmutableSortedSet(), this.hasFragmentedTail);
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
            using var colorSource = this.colors.GetEnumerator();
            bool ambiguousIsFullWidth = this.setting.IsFullWidthAmbiguous;
            int renderPos = 0;
            int valuePos = this.colors[0].ValueRange.Begin;

            // this colord string is empty.
            if (!colorSource.MoveNext())
            {
                return new ColoredString(
                    this.setting, this.body,
                    this.hasFragmentedHead, ImmutableSortedSet.Create<ColorSpan>(), this.hasFragmentedTail
                );
            }

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

            // remove before start
            while (colorSource.Current.ValueRange.End < valueStart)
            {
                if (!colorSource.MoveNext())
                {
                    // or not in original range
                    return new ColoredString(
                        this.setting, this.body,
                        startIsFragmented, ImmutableSortedSet.Create<ColorSpan>(), this.hasFragmentedTail
                    );
                }
            }

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

            var subColors = new SortedSet<ColorSpan>();
            // add first span
            if (colorSource.Current.ValueRange.End < valueEnd)
            {
                var first = colorSource.Current;
                subColors.Add(new ColorSpan(first.Color,
                    new Range(first.ValueRange.Begin.AtMin(valueStart), first.ValueRange.End)));
                
                if (!colorSource.MoveNext())
                {
                    // original range is too short
                    return new ColoredString(
                        this.setting, this.body,
                        startIsFragmented, ImmutableSortedSet.Create<ColorSpan>(), this.hasFragmentedTail
                    );
                }
            }
            // add middle span
            while (colorSource.Current.ValueRange.End < valueEnd - 1)
            {
                var middle = colorSource.Current;
                subColors.Add(new ColorSpan(middle.Color, middle.ValueRange));
                
                if (!colorSource.MoveNext())
                {
                    // original range is too short
                    return new ColoredString(
                        this.setting, this.body,
                        startIsFragmented, ImmutableSortedSet.Create<ColorSpan>(), this.hasFragmentedTail
                    );
                }
            }
            // add last span
            var last = colorSource.Current;
            subColors.Add(new ColorSpan(last.Color,
                new Range(last.ValueRange.Begin, last.ValueRange.End.AtMax(valueEnd))));

            return new ColoredString(
                this.setting, this.body,
                startIsFragmented, subColors.ToImmutableSortedSet(), endIsFragmented
            );
        }

        public int GetRenderLength()
        {
            if (this.colors.Count == 0) { return 0; }

            var ambiguousIsFullWidth = this.setting.IsFullWidthAmbiguous;
            var start = this.colors[0].ValueRange.Begin;
            var end = this.colors[^1].ValueRange.End;
            int length = 0;
            for (int i = start; i < end; i ++)
            {
                length += body[i].GetEastAsianWidth(ambiguousIsFullWidth);
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

        public int CompareTo(ColorSpan other)
        {
            if (this.ValueRange.Begin < other.ValueRange.Begin) { return -1; }
            if (other.ValueRange.Begin < this.ValueRange.Begin) { return 1; }

            if (this.ValueRange.End < other.ValueRange.End) { return -1; }
            if (other.ValueRange.End < this.ValueRange.End) { return 1; }

            return 0;
        }
    }

}