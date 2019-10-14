using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using txte.Settings;
using txte.Text;

namespace txte.ConsoleInterface
{
    class ConsoleWithEscapeSequence : IConsole
    {
        const int STD_OUTPUT_HANDLE = -11;
        const uint ENABLE_VIRTUAL_TERMINAL_PROCESSING = 0x0004;
        const uint DISABLE_NEWLINE_AUTO_RETURN = 0x0008;

        [DllImport("kernel32.dll")]
        static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

        [DllImport("kernel32.dll")]
        static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr GetStdHandle(int nStdHandle);

        [DllImport("kernel32.dll")]
        static extern uint GetLastError();

        public ConsoleWithEscapeSequence()
        {
            this.CheckConsoleRequirements();
            this.SetupConsoleMode();
            this.keyReader = new CoreConsoleKeyReader();
        }

        public int Height => Console.WindowHeight;
        public int Width => Console.BufferWidth;
        public Size Size => new Size(this.Width, this.Height);

        public bool KeyAvailable => Console.KeyAvailable;

        readonly CoreConsoleKeyReader keyReader;

        public async Task<InputEventArgs> ReadKeyOrTimeoutAsync()
            => await this.keyReader.ReadKeyOrTimeoutAsync();

        public void RefreshScreen(
            int from,
            Setting setting,
            Action<IScreen, int> RenderScreen,
            Point cursor)
        {
            var buffer = new Buffer(this, from, setting.IsFullWidthAmbiguous);

            buffer.HideCursor();
            buffer.SetCursorPosition(new Point(0, from));
            RenderScreen(buffer, from);
            buffer.SetCursorPosition(cursor);
            buffer.ShowCursor();

            this.Write(buffer.ToString());
        }

        public void Clear()
        {
            this.Write("\x1b[2J"); // Console.Clear();
            this.Write("\x1b[H"); // Console.SetCursorPosition(0, 0);
        }

        void ResetColor()
        {
            this.Write("\x1b[m");
        }

        void CheckConsoleRequirements()
        {
            if (Console.IsInputRedirected) { throw new EditorException("Console input is redirected."); }
            if (Console.IsOutputRedirected) { throw new EditorException("Console output is redirected."); }
        }

        void SetupConsoleMode()
        {
            // To treat Ctrl+C with ReadKey().
            Console.TreatControlCAsInput = true;

            // (For Windows) Enable escape cequences.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var iStdOut = GetStdHandle(STD_OUTPUT_HANDLE);
                if (!GetConsoleMode(iStdOut, out var consoleMode))
                {
                    throw new EditorException("Failed to get output console mode.");
                }

                consoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;
                if (!SetConsoleMode(iStdOut, consoleMode))
                {
                    throw new EditorException($"Failed to set output console mode, error code: {GetLastError()}");
                }
            }
        }

        void Write(string value) => Console.Write(value);

        class Buffer : IScreen
        {
            public Buffer(ConsoleWithEscapeSequence console, int from, bool ambuguosIsfullWidth)
            {
                this.buffer = new StringBuilder();
                this.console = console;
                this.size = console.Size;
                this.ambuguosIsfullWidth = ambuguosIsfullWidth;

                this.rowCount = from;
            }

            public int Width => this.console.Width;

            readonly StringBuilder buffer;
            readonly ConsoleWithEscapeSequence console;
            readonly Size size;
            readonly bool ambuguosIsfullWidth;

            int rowCount;

            public override string ToString() => this.buffer.ToString();

            public void AppendRow(string value)
            {
                this.buffer.Append(value);
                this.ClearLine();
                if (this.rowCount < this.size.Height - 1)
                {
                    this.AppendNewLine();
                }
                this.rowCount++;
            }
            public void AppendRow(IEnumerable<StyledString> spans)
            {
                foreach (var span in spans)
                {

                    this.buffer.Append(span.Value);
                    this.ResetColor();
                }
                this.ClearLine();
                if (this.rowCount < this.size.Height - 1)
                {
                    this.AppendNewLine();
                }
                this.rowCount++;
            }

            public void AppendFragmentedRow(string value, bool startIsFragmented, bool endIsFragmented)
            {
                if (startIsFragmented) { this.AppendFragmentChar(']'); }
                this.buffer.Append(value);
                if (endIsFragmented) { this.AppendFragmentChar('['); }
                this.ClearLine();
                if (this.rowCount < this.size.Height - 1)
                {
                    this.AppendNewLine();
                }
                this.rowCount++;
            }
            public void AppendOuterRow(string value)
            {
                this.buffer.Append("\x1b[36m");
                this.AppendRow(value);
                this.ResetColor();
            }

            internal void SetCursorPositionZero() => this.buffer.Append("\x1b[H");
            // ... Console.SetCursorPosition(0, 0);
            internal void SetCursorPosition(Point cursor) => this.SetCursorPosition(cursor.X, cursor.Y);
            internal void SetCursorPosition(int x, int y) => this.buffer.Append($"\x1b[{y + 1};{x + 1}H");
            // ... Console.SetCursorPosition(cursor.X, cursor.Y);

            internal void HideCursor() => this.buffer.Append("\x1b[?25l");
            internal void ShowCursor() => this.buffer.Append("\x1b[?25h");

            internal void ReverseColor() => this.buffer.Append("\x1b[7m");
            internal void ResetColor() => this.buffer.Append("\x1b[m");

            void AppendFragmentChar(char fragment)
            {
                this.buffer.Append("\x1b[31m\x1b[47m");
                this.buffer.Append(fragment);
                this.ResetColor();
            }

            void ClearLine() => this.buffer.Append("\x1b[K");

            void AppendNewLine() => this.buffer.Append("\r\n");
        }

        #region IDisposable Support
        private bool disposedValue = false; // 重複する呼び出しを検出するには

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing) { }

                this.ResetColor();
                this.Clear();

                this.keyReader.Dispose();

                this.disposedValue = true;
            }
        }

        ~ConsoleWithEscapeSequence()
        {
          // このコードを変更しないでください。クリーンアップ コードを上の Dispose(bool disposing) に記述します。
          Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}