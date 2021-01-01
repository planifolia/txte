using System;
using System.Collections.Generic;
using System.Linq;
using txte.Settings;
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
                    return new ColorSpan(
                        ((this.finder.CurrentMatch is {} found && found.Char == x && found.Line == line.Index) ? ColorSet.CurrentFound : ColorSet.Found),
                        new Range(x, x + this.finder.Current.Length)
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
        public ValuePosition? CurrentMatch { get; private set; }

        readonly Line.List lines;

        public ValuePosition? Find(string query) => this.CurrentMatch = this.Find(query, new (0, 0));

        public ValuePosition? Find(string query, ValuePosition from)
        {
            this.Current = query;
            return this.CurrentMatch = this.FindPosition(Direction.Initial, from);
        }

        public ValuePosition? FindNext() => this.CurrentMatch = this.FindPosition(Direction.Forword, this.CurrentMatch);

        public ValuePosition? FindPrevious() => this.CurrentMatch = this.FindPosition(Direction.Backword, this.CurrentMatch);

        ValuePosition? FindPosition(Direction direction, ValuePosition? lastMatch)
        {
            // When the query string is not found in this document, ignore FindNext() / FindPrevious()
            if (!lastMatch.HasValue) return null;

            var docLines = this.lines;
            var query = this.Current;

            var finding = lastMatch.Value;

            if (direction == Direction.Initial)
            {
                finding.Char = docLines[finding.Line].Value.IndexOf(query, finding.Char);
                if (finding.Char != NOT_FOUND) return finding;
            }

            // for round trip to the rest part of the same line
            for (var i = 0; i < docLines.Count + 1; i++)
            {
                if (finding.Char == NOT_FOUND)
                {
                    MoveLine(ref finding, direction, docLines);
                }
                else
                {
                    finding.Char += (direction != Direction.Backword) ? query.Length : -1;
                    // move from the start / end of line to next line
                    if (finding.Char == -1 || finding.Char == docLines[finding.Line].Value.Length)
                    {
                        MoveLine(ref finding, direction, docLines);
                    }
                }

                // if the line is empty, the query string cannot be found
                if (docLines[finding.Line].Value.Length == 0)
                {
                    finding.Char = NOT_FOUND;
                    continue;
                }

                if (direction != Direction.Backword)
                {
                    finding.Char = docLines[finding.Line].Value.IndexOf(query, finding.Char);
                }
                else
                {
                    finding.Char =
                        docLines[finding.Line].Value
                        .IndicesOf(query, allowOverlap: false)
                        .Where(x => x <= finding.Char)
                        .DefaultIfEmpty(NOT_FOUND).Max();
                }
                if (finding.Char != NOT_FOUND) return finding;
            }
            return null;

            static void MoveLine(ref ValuePosition finding, Direction direction, Line.List docLines)
            {
                if (direction != Direction.Backword)
                {
                    finding.Line++;
                    // wrap arround at the start / end of file
                    if (finding.Line >= docLines.Count)
                    {
                        finding.Line = 0;
                    }

                    finding.Char = 0;
                }
                else
                {
                    finding.Line--;
                    // wrap arround at the start / end of file
                    if (finding.Line < 0)
                    {
                        finding.Line = docLines.Count - 1;
                    }

                    finding.Char = docLines[finding.Line].Value.Length - 1;
                }
            }
        }
    }

    class FindPrompt : IPrompt<string>
    {

        public FindPrompt(string message, TextFinder finder, IDocument document, Setting setting)
        {
            this.prompt = new InputPrompt(message, setting);
            this.finder = finder;
            this.document = document;
            this.savedPosition = this.document.ValuePosition;
            this.savedOffset = this.document.Offset;
            this.setting = setting;
        }

        public CursorPosition? Cursor => this.prompt.Cursor;

        readonly InputPrompt prompt;
        readonly TextFinder finder;
        readonly IDocument document;
        readonly ValuePosition savedPosition;
        readonly RenderPosition savedOffset;
        readonly Setting setting;

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