using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;

namespace txte
{
    interface IConsole : IDisposable
    {
        int Height { get; }
        int Width { get; }
        Size Size { get; }

        Task<InputEventArgs> ReadKeyOrTimeoutAsync();
        void RefreshScreen(
            int from,
            EditorSetting setting,
            Action<IScreen, int> drawEditorRows,
            Action<IScreen> drawStatusBar,
            Action<IScreen> drawMessageBar,
            Point cursor);
        void Clear();
    }

    interface IScreen
    {
        void AppendRow(string value);
        void AppendRow(IEnumerable<StyledString> spans);
        void AppendOuterRow(string value);
    }
}
