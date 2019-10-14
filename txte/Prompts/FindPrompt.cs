using System;
using System.Collections.Generic;
using System.Drawing;
using txte.State;
using txte.Text;
using txte.TextDocument;

namespace txte.Prompts
{
    struct FindingStatus
    {
        public int LastMatch;
        public int Direction;
    }

    class FindPrompt : IPrompt, IPrompt<(string pattern, FindingStatus findStatus)>, ITextFinder
    {
     
        public FindPrompt(string message, IDocument documen)
        {
            this.prompt = new InputPrompt(message);
            this.document = documen;
            this.savedPosition = this.document.ValuePosition;
            this.savedOffset = this.document.Offset;

            this.findingStatus = new FindingStatus { Direction = 1, LastMatch = -1 };
            this.findedStatus = this.findingStatus;
        }

        public string Current => this.prompt.Current;

        readonly InputPrompt prompt;
        readonly IDocument document;
        readonly Point savedPosition;
        readonly Point savedOffset;
        FindingStatus findedStatus;
        FindingStatus findingStatus;

        public ModalProcessResult<(string pattern, FindingStatus findStatus)> ProcessKey(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo)
            {
                // Search Forward
                case { Key: ConsoleKey.Tab, Modifiers: (ConsoleModifiers)0 }:
                    this.findingStatus.Direction = 1;
                    this.findedStatus = this.findingStatus;
                    this.findingStatus = this.Find(this.prompt.Current, this.findedStatus);
                    return ModalNeedsRefreash.Default;

                // Search Backward
                case { Key: ConsoleKey.Tab, Modifiers: ConsoleModifiers.Shift }:
                    this.findingStatus.Direction = -1;
                    this.findedStatus = this.findingStatus;
                    this.findingStatus = this.Find(this.prompt.Current, this.findedStatus);
                    return ModalNeedsRefreash.Default;

                default:
                    break;
            }

            var baseResult = this.prompt.ProcessKey(keyInfo);

            if (baseResult is ModalOk<string> result)
            {
                return ModalOk.Create((result.Result, this.findedStatus));
            }
            else if (baseResult is IModalRunning)
            {
                this.findedStatus = new FindingStatus{ Direction = 1, LastMatch = -1 };
                this.findingStatus = this.Find(this.prompt.Current, this.findedStatus);
                return ModalNeedsRefreash.Default;
            }
            else if (baseResult is IModalCancel)
            {
                // restore cursor position
                this.document.ValuePosition = this.savedPosition;
                this.document.Offset = this.savedOffset;

                return ModalCancel.Default;
            }
            else
            {
                return ModalUnhandled.Default;
            }
        }
        
        public IEnumerable<StyledString> ToStyledString() =>
            this.prompt.ToStyledString();

        FindingStatus Find(string query, FindingStatus finding)
        {
            // find first word when beginning finding or losing pattern
            if (finding.LastMatch == -1) { finding.Direction = 1; }

            int current = finding.LastMatch;
            for (int i = 0; i < this.document.Rows.Count; i++)
            {
                current += finding.Direction;
                // wrap arround at start / end of file
                if (current == -1)
                {
                    current = this.document.Rows.Count - 1;
                }
                else if (current == this.document.Rows.Count)
                {
                    current = 0;
                }

                var index = this.document.Rows[current].Value.IndexOf(query);
                if (index >= 0)
                {
                    finding.LastMatch = current;
                    this.document.ValuePosition = new Point(index, current);
                    // to scroll up found word to top of screen
                    this.document.Offset = new Point(this.document.Offset.X, this.document.Rows.Count);
                    break;
                }
            }

            return finding;
        }
    }
}