using System;
using System.Collections.Generic;
using txte.State;
using txte.Text;

namespace txte.Prompts
{
    /// For render prompt
    interface IPrompt
    {
        IEnumerable<StyledString> ToStyledString();
        CursorPosition? Cursor { get; }
    }

    /// For render prompt and process input
    interface IPrompt<T> : IPrompt
    {
        ModalProcessResult<T> ProcessKey(ConsoleKeyInfo keyInfo);
    }

}