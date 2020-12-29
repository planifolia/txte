using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using txte.State;
using txte.Text;
using txte.TextDocument;

namespace txte.Prompts
{
    struct FindingStatus
    {
    }

    class TextFinder : ITextFinder
    {
        const int NOT_FOUND = -1;
        const int FORWARD = 1;
        const int BACKWORD = -1;

        public TextFinder(IDocument document)
        {

            this.document = document;
            this.savedPosition = this.document.ValuePosition;
            this.savedOffset = this.document.Offset;

            this.Current = "";
            this.CurrentMatch = new Point(NOT_FOUND, NOT_FOUND);
        }
        public string Current { get; private set; }
        public Point CurrentMatch { get; private set; }

        readonly IDocument document;
        readonly Point savedPosition;
        readonly Point savedOffset;

        public void FindFirst(string query)
        {
            this.Current = query;
            this.CurrentMatch = this.FindPosition(query, FORWARD, new Point(NOT_FOUND, NOT_FOUND));
        }

        public void FindNext()
        {
            this.CurrentMatch = this.FindPosition(this.Current, FORWARD, this.CurrentMatch);
        }
        public void FindPrevious()
        {
            this.CurrentMatch = this.FindPosition(this.Current, BACKWORD, this.CurrentMatch);
        }

        Point FindPosition(string query, int direction, Point lastMatch)
        {
            int findingRow = lastMatch.Y;
            int findingCol = lastMatch.X;
            // for round trip to the rest part of the same line
            int rowArround = this.document.Rows.Count + 1;

            for (int i = 0; i < rowArround + 1; i++)
            {
                if (findingCol == NOT_FOUND)
                {
                    findingRow += direction;

                    // wrap arround at the start / end of file
                    if (findingRow == -1)
                    {
                        findingRow = this.document.Rows.Count - 1;
                    }
                    else if (findingRow == this.document.Rows.Count)
                    {
                        findingRow = 0;
                    }

                    if (direction == FORWARD)
                    {
                        findingCol = 0;
                    }
                    else
                    {
                        findingCol = this.document.Rows[findingRow].Value.Length - 1;
                    }
                }
                else
                {
                    findingCol += direction;

                    // move from the start / end of line to next line
                    if (findingCol == -1 || findingCol == this.document.Rows[findingRow].Value.Length)
                    {
                        findingRow += direction;

                        // wrap arround at the start / end of file
                        if (findingRow == -1)
                        {
                            findingRow = this.document.Rows.Count - 1;
                        }
                        else if (findingRow == this.document.Rows.Count)
                        {
                            findingRow = 0;
                        }

                        if (direction == FORWARD)
                        {
                            findingCol = 0;
                        }
                        else
                        {
                            findingCol = this.document.Rows[findingRow].Value.Length - 1;
                        }
                    }
                }
                if (this.document.Rows[findingRow].Value.Length == 0) {
                    findingCol = NOT_FOUND;
                    continue;
                }
                findingCol =
                    direction == 1 ? this.document.Rows[findingRow].Value.IndexOf(query, findingCol)
                    : this.document.Rows[findingRow].Value.LastIndexOf(query, findingCol);
                if (findingCol != NOT_FOUND)
                {
                    this.document.ValuePosition = new Point(findingCol, findingRow);
                    return new Point(findingCol, findingRow);
                }
            }
            return lastMatch;
        }

        public void RestorePosition()
        {
            // restore cursor position
            this.document.ValuePosition = this.savedPosition;
            this.document.Offset = this.savedOffset;
        }

        public Coloring Highlight(int rowIndex, Row row)
        {
            var indices = row.Value.Indices(this.Current);
            var founds =
                indices
                .Select(
                    x =>
                    {
                        var boundaries = row.Boundaries;
                        return new ColorSpan(
                            ((this.CurrentMatch.X == x && this.CurrentMatch.Y == rowIndex) ? ColorSet.CurrentFound : ColorSet.Found),
                            new Range(boundaries[x], boundaries[x + this.Current.Length])
                        );
                    }
                )
                .ToList();
            return new Coloring(founds);
        }
    }

    class FindPrompt : IPrompt, IPrompt<string>
    {

        public FindPrompt(string message, TextFinder finder)
        {
            this.prompt = new InputPrompt(message);
            this.finder = finder;
        }


        readonly InputPrompt prompt;
        readonly TextFinder finder;

        public ModalProcessResult<string> ProcessKey(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo)
            {
                // Search Forward
                case { Key: ConsoleKey.Tab, Modifiers: (ConsoleModifiers)0 }:
                    this.finder.FindNext();
                    return ModalNeedsRefreash.Default;

                // Search Backward
                case { Key: ConsoleKey.Tab, Modifiers: ConsoleModifiers.Shift }:
                    this.finder.FindPrevious();
                    return ModalNeedsRefreash.Default;

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
                this.finder.FindFirst(this.prompt.Current);
                return ModalNeedsRefreash.Default;
            }
            else if (baseResult is IModalCancel)
            {
                // restore cursor position
                this.finder.RestorePosition();
                return ModalCancel.Default;
            }
            else
            {
                return ModalUnhandled.Default;
            }
        }

        public IEnumerable<StyledString> ToStyledString() =>
            this.prompt.ToStyledString();

    }
}