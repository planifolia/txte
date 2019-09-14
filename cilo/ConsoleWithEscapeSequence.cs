using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;

namespace cilo
{
    class ConsoleWithEscapeSequence : IEditorConsole
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

        public int Height => Console.WindowHeight;
        public int EditorHeight => this.Height - 2 /* status + message */;
        public int Width => Console.BufferWidth;
        public Size Size => new Size(this.Width, this.Height);

        public ConsoleKeyInfo ReadKey()
        {
            return Console.ReadKey(true);
        }

        public void Write(string value)
        {
            Console.Write(value);
        }

        public void Clear()
        {
            this.Write("\x1b[2J"); // Console.Clear();
            this.Write("\x1b[H"); // Console.SetCursorPosition(0, 0);
        }

        public void RefreshScreen(
            Action<IScreenBuffer> drawEditorRows,
            Action<IScreenBuffer> drawSatusBar,
            Action<IScreenBuffer> drawMessageBar,
            Point cursor)
        {
            var buffer = new Buffer(this.Size);

            buffer.AppendHideCursor();
            buffer.AppendSetCursorPositionZero();
            drawEditorRows(buffer);
            buffer.AppendReverseColor();
            drawSatusBar(buffer);
            buffer.AppendResetColor();
            drawMessageBar(buffer);
            buffer.AppendSetCursorPosition(cursor);
            buffer.AppendShowCursor();

            this.Write(buffer.ToString());
        }

        class Buffer : IScreenBuffer
        {
            readonly StringBuilder buffer;
            readonly Size size;

            int rowCount;

            public Buffer(Size size)
            {
                this.buffer = new StringBuilder();
                this.size = size;

                this.rowCount = 0;
            }

            public void AppendRow(string value)
            {
                this.buffer.Append(value);
                this.AppendClearLine();
                if (this.rowCount < this.size.Height - 1)
                {
                    this.AppendNewLine();
                }
                this.rowCount++;
            }

            void AppendClearLine()
            {
                this.buffer.Append("\x1b[K");
            }

            void AppendNewLine()
            {
                this.buffer.Append("\r\n");
            }

            public void AppendSetCursorPositionZero()
            {
                this.buffer.Append("\x1b[H");
                // Console.SetCursorPosition(0, 0);
            }
            public void AppendSetCursorPosition(Point cursor)
            {
                this.AppendSetCursorPosition(cursor.X, cursor.Y);
            }
            public void AppendSetCursorPosition(int x, int y)
            {
                this.buffer.Append($"\x1b[{y + 1};{x + 1}H");
                // Console.SetCursorPosition(cursor.X, cursor.Y);
            }

            public void AppendHideCursor()
            {
                this.buffer.Append("\x1b[?25l");
            }
            public void AppendShowCursor()
            {
                this.buffer.Append("\x1b[?25h");
            }

            public void AppendReverseColor()
            {
                this.buffer.Append("\x1b[7m");
            }
            public void AppendResetColor()
            {
                this.buffer.Append("\x1b[m");
            }

            public override string ToString() => this.buffer.ToString();
        }
    }
}
