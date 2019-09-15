#if ESCAPE_SEQUENCE_ENHANCED || true

using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace cilo
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
        }

        public int Height => Console.WindowHeight;
        public int EditorHeight => this.Height - 2 /* status + message */;
        public int Width => Console.BufferWidth - 1;
        public Size Size => new Size(this.Width, this.Height);

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
            try
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
            catch(DllNotFoundException)
            {
                // Pass because this os may not be Windows. (Like Linux or Mac)
                // Maybe this OS can use escape sequences by default.
            }
        }

        public ConsoleKeyInfo ReadKey() => Console.ReadKey(true);

        public void Clear()
        {
            this.Write("\x1b[2J"); // Console.Clear();
            this.Write("\x1b[H"); // Console.SetCursorPosition(0, 0);
        }


        public void RefreshScreen(
            EditorSetting setting,
            Action<IScreen> drawEditorRows,
            Action<IScreen> drawSatusBar,
            Action<IScreen> drawMessageBar,
            Point cursor)
        {
            var buffer = new Buffer(this, setting.IsFullWidthAmbiguous);

            buffer.HideCursor();
            buffer.SetCursorPositionZero();
            drawEditorRows(buffer);
            buffer.ReverseColor();
            drawSatusBar(buffer);
            buffer.ResetColor();
            drawMessageBar(buffer);
            buffer.SetCursorPosition(cursor);
            buffer.ShowCursor();

            this.Write(buffer.ToString());
        }

        void Write(string value) => Console.Write(value);

        class Buffer : IScreen
        {
            public Buffer(ConsoleWithEscapeSequence console, bool ambuguosIsfullWidth)
            {
                this.buffer = new StringBuilder();
                this.console = console;
                this.size = console.Size;
                this.ambuguosIsfullWidth = ambuguosIsfullWidth;

                this.rowCount = 0;
            }
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
    }
}
#endif
