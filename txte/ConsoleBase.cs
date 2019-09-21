using System;
using System.Collections.Generic;
using System.Drawing;

namespace txte
{
    interface IConsole
    {
        int Height { get; }
        int Width { get; }
        Size Size { get; }

        ConsoleKeyInfo ReadKey();
        void Clear();
        void RefreshScreen(
            EditorSetting setting,
            Action<IScreen> drawEditorRows,
            Action<IScreen> drawStatusBar,
            Action<IScreen> drawMessageBar,
            Point cursor);
    }

    interface IScreen
    {
        void AppendRow(string value);
        void AppendRow(IEnumerable<StyledString> spans);
        void AppendOuterRow(string value);
    }
}
