using System;
using System.Collections.Generic;
using System.Text;
using txte.Settings;
using txte.State;
using txte.Text;

namespace txte.Prompts
{
    class InputPrompt : IPrompt<string>
    {
        public InputPrompt(string message, Setting setting)
        {
            this.Message = message;
            this.setting = setting;
            this.input = new StringBuilder();
        }

        public string Message { get; }

        readonly Setting setting;
        readonly StringBuilder input;

        public CursorPosition? Cursor =>
            new (
                0,
                this.Message.GetRenderLength(setting.AmbiguousCharIsFullWidth)
                + 1
                + this.Current.GetRenderLength(setting.AmbiguousCharIsFullWidth)
            );
        public string Current => this.input.ToString();

        public ModalProcessResult<string> ProcessKey(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo)
            {
                case { Key: ConsoleKey.Backspace, Modifiers: (ConsoleModifiers)0 }:
                    this.input.Length = (input.Length - 1).AtMin(0);
                    return ModalRunning.Default;

                case { Key: ConsoleKey.Escape, Modifiers: (ConsoleModifiers)0 }:
                    return ModalCancel.Default;

                case { Key: ConsoleKey.Enter, Modifiers: (ConsoleModifiers)0 }:
                    return ModalOk.Create(this.input.ToString());

                default:
                    if (!char.IsControl(keyInfo.KeyChar))
                    {
                        this.input.Append(keyInfo.KeyChar);
                        return ModalRunning.Default;
                    }
                    return ModalUnhandled.Default;
            }
        }

        public IEnumerable<StyledString> ToStyledString()
        {
            var styled = new List<StyledString>();
            styled.Add(new StyledString(this.Message, ColorSet.SystemMessage));
            styled.Add(new StyledString(" "));
            styled.Add(new StyledString(this.Current));
            return styled;
        }
    }
}