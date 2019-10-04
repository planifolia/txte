using System;
using System.Collections.Generic;
using System.Text;

namespace txte
{
    class InputPrompt : IPrompt, IPrompt<string>
    {
        public InputPrompt(string message, Action<string, ConsoleKeyInfo>? callback = null)
        {
            this.Message = message;
            this.input = new StringBuilder();
            this.callback = callback;
        }

        public string Message { get; }

        readonly StringBuilder input;
        readonly Action<string, ConsoleKeyInfo>? callback;

        public string Current => this.input.ToString();

        public (KeyProcessingResults, string?) ProcessKey(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo)
            {
                case { Key: ConsoleKey.Backspace, Modifiers: (ConsoleModifiers)0 }:
                    this.input.Length = (input.Length - 1).AtMin(0);
                    this.callback?.Invoke(this.input.ToString(), keyInfo);
                    return (KeyProcessingResults.Running, default);
                case { Key: ConsoleKey.Escape, Modifiers: (ConsoleModifiers)0 }:
                    return (KeyProcessingResults.Quit, default);
                case { Key: ConsoleKey.Enter, Modifiers: (ConsoleModifiers)0 }:
                    return (KeyProcessingResults.Quit, this.input.ToString());
                default:
                    if (!char.IsControl(keyInfo.KeyChar))
                    { 
                        this.input.Append(keyInfo.KeyChar);
                        this.callback?.Invoke(this.input.ToString(), keyInfo);
                    }
                    return (KeyProcessingResults.Running, default);
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