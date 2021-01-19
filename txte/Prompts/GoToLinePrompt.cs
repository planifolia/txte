
using System;
using System.Collections.Generic;
using txte.Settings;
using txte.State;
using txte.Text;
using txte.TextDocument;

namespace txte.Prompts
{
    class GoToLineState
    {
        public int? Line = null;
    }

    class GoToLineHilighter : IOverlapHilighter
    {
        public GoToLineHilighter(GoToLineState state) => this.state = state;

        readonly GoToLineState state;

        public ColoredString Highlight(Line line)
        {
            if (!this.state.Line.HasValue) return line.Render;
            if (line.Index != this.state.Line.Value) return line.Render;
            return line.Render.Overlay(new Coloring(new[] { new ColorSpan(ColorSet.CurrentFound, new Range(0, line.Value.Length)) }));
        }
    }
    class GoToLinePrompt : IPrompt<int>
    {
        public GoToLinePrompt(string message, GoToLineState state, IDocument document, Setting setting)
        {
            this.state = state;
            this.document = document;
            this.savedPosition = document.ValuePosition;
            this.savedOffset = document.Offset;
            this.input= new(message, setting);
        }

        public CursorPosition? Cursor => this.input.Cursor;

        readonly GoToLineState state;
        readonly IDocument document;
        readonly ValuePosition savedPosition;
        readonly RenderPosition savedOffset;
        readonly InputPrompt input;

        public ModalProcessResult<int> ProcessKey(ConsoleKeyInfo keyInfo)
        {
            // filter unhandled keys
            if (keyInfo is not (
                { KeyChar: (>= '0' and <= '9')}
                or { Key: ConsoleKey.Backspace }
                or { Key: ConsoleKey.Escape }
                or { Key: ConsoleKey.Enter }
            )) return ModalUnhandled.Default;

            var baseResult = this.input.ProcessKey(keyInfo);
            if (baseResult is ModalOk<string>(var result))
            {
                return this.TryGoTo(result, out var resultLine) ? ModalOk.Create(resultLine) : ModalCancel.Default;
            }
            else if (baseResult is IModalRunning)
            {
                this.TryGoTo(this.input.Current, out var _);
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

        public IEnumerable<StyledString> ToStyledString() => this.input.ToStyledString();

        bool TryGoTo(string lineString, out int line)
        {
            if (
                int.TryParse(lineString, out line)
                && line > 0 && line <= this.document.Lines.Count
            )
            {
                this.state.Line = line - 1;
                this.document.ValuePosition = new ValuePosition(line - 1, 0);
                return true;
            }
            else
            {
                this.state.Line = null;
                this.RestorePosition();
                return false;
            }
        }
    }


}