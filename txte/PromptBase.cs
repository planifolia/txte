using System;
using System.Collections.Generic;

namespace txte
{
    /// For render prompt
    interface IPrompt
    {
        IEnumerable<StyledString> ToStyledString();
    }

    /// For render prompt and process input
    interface IPrompt<TResult> : IPrompt where TResult: class
    {
        (ModalProcessResult, TResult?) ProcessKey(ConsoleKeyInfo keyInfo);
    }

}