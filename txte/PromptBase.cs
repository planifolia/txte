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
    interface IPrompt<T> : IPrompt
    {
        ModalProcessResult<T> ProcessKey(ConsoleKeyInfo keyInfo);
    }

}