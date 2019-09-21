using System;
using System.Drawing;

namespace txte
{
    interface IConsole
    {
        int Height { get; }
        int EditorHeight { get; }
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
        void AppendFragmentedRow(string value, bool startIsFragmented, bool endIsFragmented);
        void AppendOuterRow(string value);
    }
}
