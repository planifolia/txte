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
        public TextFinder(IDocument document)
        {

            this.document = document;
            this.savedPosition = this.document.ValuePosition;
            this.savedOffset = this.document.Offset;

            this.Current = "";
            this.direction = 1;
            this.lastMatch = -1;
        }
        public string Current { get; private set; }

        readonly IDocument document;
        readonly Point savedPosition;
        readonly Point savedOffset;

        int lastMatch;
        int direction;

        public void FindFirst(string query)
        {
            this.Current = query;
            this.direction = 1;
            this.lastMatch = -1;
            this.Find();
        }

        public void FindNext(string query)
        {
            this.Current = query;
            this.direction = 1;
            this.Find();
        }
        public void FindPrevious(string query)
        {
            this.Current = query;
            this.direction = -1;
            this.Find();
        }

        public void Find()
        {
            // find first word when beginning finding or losing pattern
            if (this.lastMatch == -1) { this.direction = 1; }

            int findingRow = this.lastMatch;
            for (int i = 0; i < this.document.Rows.Count; i++)
            {
                findingRow += this.direction;
                // wrap arround at start / end of file
                if (findingRow == -1)
                {
                    findingRow = this.document.Rows.Count - 1;
                }
                else if (findingRow == this.document.Rows.Count)
                {
                    findingRow = 0;
                }

                var index = this.document.Rows[findingRow].Value.IndexOf(this.Current);
                if (index >= 0)
                {
                    this.lastMatch = findingRow;
                    this.document.ValuePosition = new Point(index, findingRow);
                    // to scroll up found word to top of screen
                    this.document.Offset = new Point(this.document.Offset.X, this.document.Rows.Count);
                    break;
                }
            }
        }

        public void RestorePosition()
        {
            // restore cursor position
            this.document.ValuePosition = this.savedPosition;
            this.document.Offset = this.savedOffset;
        }

        public Coloring Highlight(Row row)
        {
            var indices = row.Value.Indices(this.Current);
            var founds =
                indices
                .Select(
                    x =>
                    {
                        var boundaries = row.Boundaries;
                        return new ColorSpan(
                            ColorSet.Found,
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
                    this.finder.FindNext(this.prompt.Current);
                    return ModalNeedsRefreash.Default;

                // Search Backward
                case { Key: ConsoleKey.Tab, Modifiers: ConsoleModifiers.Shift }:
                    this.finder.FindPrevious(this.prompt.Current);
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