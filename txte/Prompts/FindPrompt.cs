using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using txte.State;
using txte.Text;
using txte.TextDocument;

namespace txte.Prompts
{
    class FindingHilighter : IOverlapHilighter, IDisposable
    {
        public FindingHilighter(TextFinder finder) => this.finder = finder;

        readonly TextFinder finder;

        public ColoredString Highlight(Row row)
        {
            if (this.finder.Current.Length == 0) return row.Render;

            var indices = row.Value.IndicesOf(this.finder.Current, allowOverlap: false);
            var founds =
                indices
                .Select(x =>
                {
                    var boundaries = row.Boundaries;
                    return new ColorSpan(
                        ((this.finder.CurrentMatch.X == x && this.finder.CurrentMatch.Y == row.Index) ? ColorSet.CurrentFound : ColorSet.Found),
                        new Range(boundaries[x], boundaries[x + this.finder.Current.Length])
                    );
                })
                .ToList();
            return row.Render.Overlay(new Coloring(founds));
        }

        public void Dispose()
        {
            // only to represent that this refers to an IDisposable object
        }
    }

    class TextFinder : IDisposable
    {
        const int NOT_FOUND = -1;
        const int FORWARD = 1;
        const int BACKWORD = -1;

        public TextFinder(FindPrompt prompt, IDocument document)
        {
            this.prompt = prompt;
            this.prompt.FindFirst += this.FindFirst;
            this.prompt.FindNext += this.FindNext;
            this.prompt.FindPrevious += this.FindPrevious;
            this.prompt.Cancel += this.RestorePosition;

            this.document = document;
            this.savedPosition = this.document.ValuePosition;
            this.savedOffset = this.document.Offset;

            this.Current = "";
            this.CurrentMatch = savedPosition;
        }
        public string Current { get; private set; }
        public Point CurrentMatch { get; private set; }

        readonly FindPrompt prompt;
        readonly IDocument document;
        readonly Point savedPosition;
        readonly Point savedOffset;
        private bool disposedValue;

        public void FindFirst(string query)
        {
            this.Current = query;
            this.document.ValuePosition = this.CurrentMatch = this.FindPosition(FORWARD, this.savedPosition);
        }

        public void FindNext() =>
            this.document.ValuePosition = this.CurrentMatch = this.FindPosition(FORWARD, this.CurrentMatch);
        public void FindPrevious() =>
            this.document.ValuePosition = this.CurrentMatch = this.FindPosition(BACKWORD, this.CurrentMatch);

        Point FindPosition(int direction, Point lastMatch)
        {
            var docRows = this.document.Rows;
            var query = this.Current;
            
            var findingCol = lastMatch.X;
            var findingRow = lastMatch.Y;

            // for round trip to the rest part of the same line
            for (var i = 0; i < docRows.Count + 1; i++)
            {
                if (findingCol == NOT_FOUND)
                {
                    findingRow += direction;

                    // wrap arround at the start / end of file
                    if (findingRow == -1)
                    {
                        findingRow = docRows.Count - 1;
                    }
                    else if (findingRow == docRows.Count)
                    {
                        findingRow = 0;
                    }

                    if (direction == FORWARD)
                    {
                        findingCol = 0;
                    }
                    else
                    {
                        findingCol = docRows[findingRow].Value.Length - 1;
                    }
                }
                else
                {
                    findingCol += (direction == FORWARD) ? query.Length : -1;

                    // move from the start / end of line to next line
                    if (findingCol == -1 || findingCol == docRows[findingRow].Value.Length)
                    {
                        findingRow += direction;

                        // wrap arround at the start / end of file
                        if (findingRow == -1)
                        {
                            findingRow = docRows.Count - 1;
                        }
                        else if (findingRow == docRows.Count)
                        {
                            findingRow = 0;
                        }

                        if (direction == FORWARD)
                        {
                            findingCol = 0;
                        }
                        else
                        {
                            findingCol = docRows[findingRow].Value.Length - 1;
                        }
                    }
                }
                if (docRows[findingRow].Value.Length == 0)
                {
                    findingCol = NOT_FOUND;
                    continue;
                }
                if (direction == FORWARD)
                {
                    findingCol = docRows[findingRow].Value.IndexOf(query, findingCol);
                }
                else
                {
                    findingCol =
                        docRows[findingRow].Value
                        .IndicesOf(query, allowOverlap: false)
                        .Where(x => x <= findingCol)
                        .DefaultIfEmpty(NOT_FOUND).Max();
                }
                if (findingCol != NOT_FOUND)
                {
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

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    this.prompt.FindFirst -= this.FindFirst;
                    this.prompt.FindNext -= this.FindNext;
                    this.prompt.FindPrevious -= this.FindPrevious;
                    this.prompt.Cancel -= this.RestorePosition;
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    class FindPrompt : IPrompt, IPrompt<string>
    {

        public FindPrompt(string message)
        {
            this.prompt = new InputPrompt(message);
        }

        public event Action<string>? FindFirst;
        public event Action? FindNext;
        public event Action? FindPrevious;
        public event Action? Cancel;
        public event Action? Confirm;


        readonly InputPrompt prompt;

        public ModalProcessResult<string> ProcessKey(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo)
            {
                // Search Forward
                case { Key: ConsoleKey.Tab, Modifiers: (ConsoleModifiers)0 }:
                    this.FindNext?.Invoke();
                    return ModalNeedsRefreash.Default;

                // Search Backward
                case { Key: ConsoleKey.Tab, Modifiers: ConsoleModifiers.Shift }:
                    this.FindPrevious?.Invoke();
                    return ModalNeedsRefreash.Default;

                default:
                    break;
            }

            var baseResult = this.prompt.ProcessKey(keyInfo);

            if (baseResult is ModalOk<string> result)
            {
                this.Confirm?.Invoke();
                return ModalOk.Create(result.Result);
            }
            else if (baseResult is IModalRunning)
            {
                this.FindFirst?.Invoke(this.prompt.Current);
                return ModalNeedsRefreash.Default;
            }
            else if (baseResult is IModalCancel)
            {
                // restore cursor position
                this.Cancel?.Invoke();
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