﻿using System;
using System.Collections.Generic;
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
            CursorPosition? cursor);
        void Clear();
    }

    interface IConsole : IConsoleInput, IConsoleOutput, IDisposable
    {
    }

    interface IScreen
    {
        int Width { get; }

        void AppendLine(string value);
        void AppendLine(IEnumerable<StyledString> spans);
        void AppendOuterLine(string value);
    }
}
