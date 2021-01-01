using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using txte.State;
using txte.Text;
using txte.TextDocument;

namespace txte.Prompts
{
    class FindingHilighter : IOverlapHilighter
    {
        public FindingHilighter(TextFinder finder) => this.finder = finder;

        readonly TextFinder finder;

        public ColoredString Highlight(Line line)
        {
            if (this.finder.Current.Length == 0) return line.Render;

            var indices = line.Value.IndicesOf(this.finder.Current, allowOverlap: false);
            var founds =
                indices
                .Select(x =>
                {
                    var boundaries = line.Boundaries;
                    return new ColorSpan(
                        ((this.finder.CurrentMatch is {} found && found.X == x && found.Y == line.Index) ? ColorSet.CurrentFound : ColorSet.Found),
                        new Range(boundaries[x], boundaries[x + this.finder.Current.Length])
                    );
                })
                .ToList();
            return line.Render.Overlay(new Coloring(founds));
        }
    }

    class TextFinder
    {
        const int NOT_FOUND = -1;
        enum Direction
        {
            Initial,
            Forword,
            Backword,
        }

        public TextFinder(Line.List lines)
        {
            this.lines = lines;

            this.Current = "";
            this.CurrentMatch = null;
        }

        public string Current { get; private set; }
        public Point? CurrentMatch { get; private set; }

        readonly Line.List lines;

        public Point? Find(string query) => this.CurrentMatch = this.Find(query, new Point(0, 0));

        public Point? Find(string query, Point from)
        {
            this.Current = query;
            return this.CurrentMatch = this.FindPosition(Direction.Initial, from);
        }

        public Point? FindNext() => this.CurrentMatch = this.FindPosition(Direction.Forword, this.CurrentMatch);

        public Point? FindPrevious() => this.CurrentMatch = this.FindPosition(Direction.Backword, this.CurrentMatch);

        Point? FindPosition(Direction direction, Point? lastMatch)
        {
            // When the query string is not found in this document, ignore FindNext() / FindPrevious()
            if (!lastMatch.HasValue) return null;

            var docLines = this.lines;
            var query = this.Current;

            int findingCol = lastMatch.Value.X;
            int findingLine = lastMatch.Value.Y;

            if (direction == Direction.Initial)
            {
                findingCol = docLines[findingLine].Value.IndexOf(query, findingCol);
                if (findingCol != NOT_FOUND) return new Point(findingCol, findingLine);
            }

            // for round trip to the rest part of the same line
            for (var i = 0; i < docLines.Count + 1; i++)
            {
                if (findingCol == NOT_FOUND)
                {
                    (findingLine, findingCol) = MoveLine(findingLine, findingCol, direction, docLines);
                }
                else
                {
                    findingCol += (direction != Direction.Backword) ? query.Length : -1;
                    // move from the start / end of line to next line
                    if (findingCol == -1 || findingCol == docLines[findingLine].Value.Length)
                    {
                        (findingLine, findingCol) = MoveLine(findingLine, findingCol, direction, docLines);
                    }
                }

                // if the line is empty, the query string cannot be found
                if (docLines[findingLine].Value.Length == 0)
                {
                    findingCol = NOT_FOUND;
                    continue;
                }

                if (direction != Direction.Backword)
                {
                    findingCol = docLines[findingLine].Value.IndexOf(query, findingCol);
                }
                else
                {
                    findingCol =
                        docLines[findingLine].Value
                        .IndicesOf(query, allowOverlap: false)
                        .Where(x => x <= findingCol)
                        .DefaultIfEmpty(NOT_FOUND).Max();
                }
                if (findingCol != NOT_FOUND) return new Point(findingCol, findingLine);
            }
            return null;

            static (int, int) MoveLine(int findingLine, int findingCol, Direction direction, Line.List docLines)
            {
                if (direction != Direction.Backword)
                {
                    findingLine++;
                    // wrap arround at the start / end of file
                    if (findingLine >= docLines.Count)
                    {
                        findingLine = 0;
                    }

                    findingCol = 0;
                }
                else
                {
                    findingLine--;
                    // wrap arround at the start / end of file
                    if (findingLine < 0)
                    {
                        findingLine = docLines.Count - 1;
                    }

                    findingCol = docLines[findingLine].Value.Length - 1;
                }

                return (findingLine, findingCol);
            }
        }
    }

    class FindPrompt : IPrompt, IPrompt<string>
    {

        public FindPrompt(string message, IDocument document, TextFinder finder)
        {
            this.prompt = new InputPrompt(message);
            this.document = document;
            this.savedPosition = this.document.ValuePosition;
            this.savedOffset = this.document.Offset;
            this.finder = finder;
        }

        readonly InputPrompt prompt;
        readonly IDocument document;
        readonly Point savedPosition;
        readonly Point savedOffset;
        readonly TextFinder finder;

        public ModalProcessResult<string> ProcessKey(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo)
            {
                // Search Forward
                case { Key: ConsoleKey.Tab, Modifiers: (ConsoleModifiers)0 }:
                {
                        if (this.finder.FindNext() is { } found)
                        {
                            this.document.ValuePosition = found;
                        }
                        return ModalNeedsRefreash.Default;
                }

                // Search Backward
                case { Key: ConsoleKey.Tab, Modifiers: ConsoleModifiers.Shift }:
                {
                    if (this.finder.FindPrevious() is { } found)
                    {
                        this.document.ValuePosition = found;
                    }
                    return ModalNeedsRefreash.Default;
                }

                default:
                    break;
            }

            var baseResult = this.prompt.ProcessKey(keyInfo);

            if (baseResult is ModalOk<string> result)
            {
                return ModalOk.Create(result.Result);
            }
            else if (baseResult is IModalRunning)
            {
                if (this.finder.Find(this.prompt.Current, this.savedPosition)  is { } found)
                {
                    this.document.ValuePosition = found;
                }
                return ModalNeedsRefreash.Default;
            }
            else if (baseResult is IModalCancel)
            {
                // restore cursor position
                this.RestorePosition();
                return ModalCancel.Default;
            }
            else
            {
                return ModalUnhandled.Default;
            }
        }

        public void RestorePosition()
        {
            // restore cursor position
            this.document.ValuePosition = this.savedPosition;
            this.document.Offset = this.savedOffset;
        }

        public IEnumerable<StyledString> ToStyledString() =>
            this.prompt.ToStyledString();

    }
}