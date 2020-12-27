using System;
using System.Collections.Generic;
using System.Text;
using txte.State;
using txte.Text;

namespace txte.Prompts
{
    class InputPrompt : IPrompt, IPrompt<string>
    {
        public InputPrompt(string message)
        {
            this.Message = message;
            this.input = new StringBuilder();
        }

        public string Message { get; }

        readonly StringBuilder input;

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