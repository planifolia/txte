using System;
using System.Collections.Generic;
using System.Text;

namespace txte
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

    class FindPrompt : IPrompt, IPrompt<(string pattern, FindingStatus findStatus)>
    {
     
        public FindPrompt(string message, Func<string, FindingStatus, FindingStatus> callback)
        {
            this.prompt =new InputPrompt(message);
            this.callback = callback;
            this.findingStatus = new FindingStatus { Direction = 1, LastMatch = -1 };
            this.findedStatus = findingStatus;
        }

        readonly InputPrompt prompt;
        readonly Func<string, FindingStatus, FindingStatus> callback;
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
                    this.findingStatus = this.callback(this.prompt.Current, this.findedStatus);
                    return ModalNeedsRefreash.Default;

                // Search Backward
                case { Key: ConsoleKey.Tab, Modifiers: ConsoleModifiers.Shift }:
                    this.findingStatus.Direction = -1;
                    this.findedStatus = this.findingStatus;
                    this.findingStatus = this.callback(this.prompt.Current, this.findedStatus);
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
                this.findingStatus = this.callback(this.prompt.Current, this.findedStatus);
                return ModalNeedsRefreash.Default;
            }
            else if (baseResult is IModalCancel)
            {
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