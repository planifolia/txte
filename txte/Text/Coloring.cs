using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace txte.Text
{
    class Coloring : IReadOnlyList<ColorSpan>
    {
        public Coloring(IReadOnlyList<ColorSpan> colors) => this.colors = colors.ToList();

        readonly IReadOnlyList<ColorSpan> colors;

        public int Count => this.colors.Count;

        public ColorSpan this[int index] => this.colors[index];

        public Coloring Normalize()
        {
            var normalized = new List<ColorSpan>();
            foreach (var color in this.colors.OrderBy(x => x.ValueRange))
            {
                if (color.ValueRange.Length == 0) continue;

                if (normalized.Count == 0)
                {
                    normalized.Add(color);
                }
                else if (normalized[^1].ValueRange.End <= color.ValueRange.Begin)
                {
                    normalized.Add(color);
                }
            }

            return new Coloring(normalized);
        }

        public Coloring Clip(int begin, int end)
        {
            if (end - begin <= 0) return new Coloring(new ColorSpan[] { });

            var newList = new List<ColorSpan>();
            foreach (var color in this.colors)
            {
                if (color.ValueRange.End < begin) continue;
                if (end < color.ValueRange.Begin) break;

                var newRange =
                    new Range(color.ValueRange.Begin.AtMin(begin), color.ValueRange.End.AtMax(end));
                if (newRange.Length <= 0) continue;

                newList.Add(new ColorSpan(color.Color, newRange));
            }

            return new Coloring(newList);
        }

        public Coloring Overlay(Coloring overlay)
        {
            var mergedColor = new List<ColorSpan>();

            var originalColors = new Stack<ColorSpan>(this.colors.Reverse());
            if (originalColors.Count == 0) return this;

            foreach (var overColor in overlay)
            {
                while (true)
                {
                    if (!originalColors.TryPop(out var before)) break;

                    if (before.ValueRange.End <= overColor.ValueRange.Begin)
                    {
                        // [--base--)
                        //          [--over--)
                        mergedColor.Add(before);
                    }
                    else
                    {
                        // [--base----...
                        //           [--over--)
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
                        mergedColor.Add(overColor);

                        // ...--base-----)
                        // ...--over--)
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

                        while (true)
                        {
                            if (!originalColors.TryPop(out var overlapped)) break;

                            if (overlapped.ValueRange.End <= overColor.ValueRange.End)
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
                                break;
                            }
                        }
                        break;
                    }
                }
            }

            // rest
            foreach (var color in originalColors)
            {
                mergedColor.Add(color);
            }

            return new Coloring(mergedColor);
        }

        public IEnumerator<ColorSpan> GetEnumerator() => this.colors.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }
}
