using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace txte
{
    interface IConsoleInput
    {
        Task<InputEventArgs> ReadKeyOrTimeoutAsync();
    }

    interface IConsoleOutput
    {
        int Height { get; }
        int Width { get; }
        Size Size { get; }
        void RefreshScreen(
            int from,
            EditorSetting setting,
            Action<IScreen, int> RenderScreen,
            Point cursor);
        void Clear();
    }

    interface IConsole : IConsoleInput, IConsoleOutput, IDisposable
    {
    }

    interface IScreen
    {
        void AppendRow(string value);
        void AppendRow(IEnumerable<StyledString> spans);
        void AppendOuterRow(string value);
    }
}
