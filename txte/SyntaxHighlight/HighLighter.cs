using System.Collections.Generic;
using System.Linq;
using txte.Settings;
using txte.Text;

namespace txte.SyntaxHighlight
{
    interface IHighlighter
    {
        IEnumerable<string> Extensions { get; }
        string Language { get; }
        Syntex Syntax { get; }

        ColoredString HighlightSingleLine(Setting setting, string value);
    }

    class Highlighter
    {
        public static IHighlighter FromExtension(string ext) =>
            highlighters
            .Where(x => x.Extensions.Contains(ext.ToLower()))
            .FirstOrDefault() ?? PlainTextHighLighter.Default;

        static List<IHighlighter> highlighters = new()
        {
            JsonHighLighter.Default,
        };
    }

    abstract class HighlighterBase : IHighlighter
    {

        public abstract IEnumerable<string> Extensions { get; }
        public abstract string Language { get; }
        public abstract Syntex Syntax { get; }

        public ColoredString HighlightSingleLine(Setting setting, string value)
        {
            if (!Syntax.Any()) return new ColoredString(setting, value);

            var state = new Stack<(int Begin, int End, ISyntax Syntax, List<ColorSpan> Colors)>();
            var valueLength = value.Length;
            state.Push((0, valueLength, Syntax, new List<ColorSpan>()));
            int start = 0;
            while (start < valueLength) {
                var currentState = state.Peek();
                if(
                    currentState.Syntax
                    .Select(x => (Syntax: x, Match: x.Syntax.Match(value[start..currentState.End])))
                    .Where(x => x.Match.Success)
                    .OrderBy(x => x.Match.Index).ThenBy(x => x.Syntax.Priority)
                    .FirstOrDefault()
                    is {Syntax: { Syntax: var syntax }, Match: { } match}
                )
                {
                    var begin = start + match.Index;
                    var end = start + match.Index + match.Length;
                    if (syntax.IsEnclose)
                    {
                        state.Push((begin, valueLength, syntax, new List<ColorSpan>()));
                        start = end;
                    }
                    else if (syntax.ApplicationOrder.Any())
                    {
                        state.Push((begin, end, syntax, new List<ColorSpan>()));
                    }
                    else
                    {
                        state.Peek().Colors.Add(
                            new ColorSpan(syntax.Color, new Range(begin, end))
                        );
                        start = end;
                    }
                    continue;
                }

                if (state.Peek().Syntax is Enclose enclosedSyntax)
                {
                    if (enclosedSyntax.Match(value[start..]) is var enclosedMatch && enclosedMatch.Success)
                    {
                        var closed = state.Pop();
                        var closedBegin = closed.Begin;
                        var closedEnd = start + enclosedMatch.Index + enclosedMatch.Length;
                        var baseColor = state.Peek().Colors;

                        int prevEnd = closedBegin;
                        foreach (var innerSpan in closed.Colors)
                        {
                            if (prevEnd < innerSpan.ValueRange.Begin)
                            {
                                baseColor.Add(
                                    new ColorSpan(enclosedSyntax.Color, new Range(prevEnd, innerSpan.ValueRange.Begin))
                                );
                            }
                            baseColor.Add(innerSpan);
                            prevEnd = innerSpan.ValueRange.End;
                        }
                        if (prevEnd < closedEnd)
                        {
                            baseColor.Add(
                                new ColorSpan(enclosedSyntax.Color, new Range(prevEnd, closedEnd))
                            );
                        }

                        start = closedEnd;
                        continue;
                    }
                }

                if (state.Peek().Syntax is Keyword keywordSyntax)
                {
                    var closed = state.Pop();
                    var closedBegin = closed.Begin;
                    var closedEnd = closed.End;
                    var baseColor = state.Peek().Colors;

                    int prevEnd = closedBegin;
                    foreach (var innerSpan in closed.Colors)
                    {
                        if (prevEnd < innerSpan.ValueRange.Begin)
                        {
                            baseColor.Add(
                                new ColorSpan(keywordSyntax.Color, new Range(prevEnd, innerSpan.ValueRange.Begin))
                            );
                        }
                        baseColor.Add(innerSpan);
                        prevEnd = innerSpan.ValueRange.End;
                    }
                    if (prevEnd < closedEnd)
                    {
                        baseColor.Add(
                            new ColorSpan(keywordSyntax.Color, new Range(prevEnd, closedEnd))
                        );
                    }

                    start = closedEnd;
                    continue;
                }

                break;
            }

            return new ColoredString(setting, value).Overlay(new Coloring(state.Pop().Colors));
        }
    }

}