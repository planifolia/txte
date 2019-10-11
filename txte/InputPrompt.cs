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

        public IModalProcessResult<string> ProcessKey(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo)
            {
                case { Key: ConsoleKey.Backspace, Modifiers: (ConsoleModifiers)0 }:
                    this.input.Length = (input.Length - 1).AtMin(0);
                    return ModalRunning<string>.Default;

                case { Key: ConsoleKey.Escape, Modifiers: (ConsoleModifiers)0 }:
                    return ModalCancel<string>.Default;

                case { Key: ConsoleKey.Enter, Modifiers: (ConsoleModifiers)0 }:
                    return new ModalOk<string>(this.input.ToString());

                default:
                    if (!char.IsControl(keyInfo.KeyChar))
                    { 
                        this.input.Append(keyInfo.KeyChar);
                    return ModalRunning<string>.Default;
                    }
                    return ModalUnhandled<string>.Default;
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

        public IModalProcessResult<(string pattern, FindingStatus findStatus)> ProcessKey(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo)
            {
                // Search Forward
                case { Key: ConsoleKey.Tab, Modifiers: (ConsoleModifiers)0 }:
                    this.findingStatus.Direction = 1;
                    this.findedStatus = this.findingStatus;
                    this.findingStatus = this.callback(this.prompt.Current, this.findedStatus);
                    return ModalNeedsRefreash<(string pattern, FindingStatus findStatus)>.Default;

                // Search Backward
                case { Key: ConsoleKey.Tab, Modifiers: ConsoleModifiers.Shift }:
                    this.findingStatus.Direction = -1;
                    this.findedStatus = this.findingStatus;
                    this.findingStatus = this.callback(this.prompt.Current, this.findedStatus);
                    return ModalNeedsRefreash<(string pattern, FindingStatus findStatus)>.Default;

                default:
                    break;
            }
            var baseResult = this.prompt.ProcessKey(keyInfo);
            if (baseResult is ModalOk<string> result)
            {
                return 
                    new ModalOk<(string pattern, FindingStatus findStatus)>(
                        (result.Result, this.findedStatus)
                    );
            }
            else if (baseResult is ModalRunning<string>)
            {
                this.findedStatus = new FindingStatus{ Direction = 1, LastMatch = -1 };
                this.findingStatus = this.callback(this.prompt.Current, this.findedStatus);
                return ModalNeedsRefreash<(string pattern, FindingStatus findStatus)>.Default;
            }
            else if (baseResult is ModalCancel<string>)
            {
                return ModalCancel<(string pattern, FindingStatus findStatus)>.Default;
            }
            else
            {
                return ModalUnhandled<(string pattern, FindingStatus findStatus)>.Default;
            }
        }
        
        public IEnumerable<StyledString> ToStyledString() =>
            this.prompt.ToStyledString();
    }
    
}