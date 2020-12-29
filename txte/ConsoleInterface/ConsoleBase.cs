using System;
using System.Collections.Generic;
using System.Drawing;
using txte.Settings;
using txte.Text;

namespace txte.ConsoleInterface
{
    interface IConsoleInput
    {
        bool KeyAvailable { get; }
        IAsyncEnumerable<InputEventArgs> ReadKeysOrTimeoutAsync();
    }

    interface IConsoleOutput
    {
        int Height { get; }
        int Width { get; }
        Size Size { get; }
        bool ShowsAmbiguousCharAsFullWidth { get; }
        void RefreshScreen(
            int from,
            Setting setting,
            Action<IScreen, int> RenderScreen,
            Point cursor);
        void Clear();
    }

    interface IConsole : IConsoleInput, IConsoleOutput, IDisposable
    {
    }

    interface IScreen
    {
        int Width { get; }

        void AppendRow(string value);
        void AppendRow(IEnumerable<StyledString> spans);
        void AppendOuterRow(string value);
    }
}
