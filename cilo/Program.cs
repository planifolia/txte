using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace cilo
{
    static class SpecialKeys
    {
        public static readonly ConsoleKeyInfo CtrlBreak = new ConsoleKeyInfo((char)0x00, ConsoleKey.Pause, false, false, true);
        public static readonly ConsoleKeyInfo CtrlC = new ConsoleKeyInfo((char)0x03, ConsoleKey.C, false, false, true);
        public static readonly ConsoleKeyInfo CtrlQ = new ConsoleKeyInfo((char)0x11, ConsoleKey.Q, false, false, true);
    }

    class Program
    {

        static int Main(string[] args)
        {
            //Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            try
            {
                var document =
                    (args.Length >= 1) ? Document.Open(args[0])
                    : new Document();
                var console = new EditorConsole();
                var editor = new Editor(console);
                editor.SetDocument(document);
                editor.Run();
                return 0;
            }
            catch(EditorException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }

    enum EditorProcessingResults
    {
        Running,
        Quit,
    }

    class EditorConsole
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

        public EditorConsole()
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

        public void RefreshScreen(Action<IScreenBuffer> drawEditorRows, Point cursor)
        {
            var buffer = new Buffer(this.Size);

            buffer.AppendHideCursorSequence();
            buffer.AppendSetCursorPositionZeroSequence();
            drawEditorRows(buffer);
            buffer.AppendSetCursorPositionSequence(cursor);
            buffer.AppendShowCursorSequence();

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
                this.AppendClearLineSequence();
                if (this.rowCount < this.size.Height - 1)
                {
                    this.AppendNewLineSequence();
                }
                ++this.rowCount;
            }

            void AppendClearLineSequence()
            {
                this.buffer.Append("\x1b[K");
            }

            void AppendNewLineSequence()
            {
                this.buffer.Append("\r\n");
            }

            public void AppendSetCursorPositionZeroSequence()
            {
                this.buffer.Append("\x1b[H");
                // Console.SetCursorPosition(0, 0);
            }
            public void AppendSetCursorPositionSequence(Point cursor)
            {
                this.AppendSetCursorPositionSequence(cursor.X, cursor.Y);
            }
            public void AppendSetCursorPositionSequence(int x, int y)
            {
                this.buffer.Append($"\x1b[{y + 1};{x + 1}H");
                // Console.SetCursorPosition(cursor.X, cursor.Y);
            }

            public void AppendHideCursorSequence()
            {
                this.buffer.Append("\x1b[?25l");
            }
            public void AppendShowCursorSequence()
            {
                this.buffer.Append("\x1b[?25h");
            }

            public override string ToString() => this.buffer.ToString();
        }
    }
    interface IScreenBuffer
    {
        void AppendRow(string value);
    }

    class Document
    {
        public List<string> Rows { get; }
        public Point Cursor => new Point(this.position.X - this.offset.X, this.position.Y - this.offset.Y);
        public Point Position => this.position;
        public Point Offset => this.offset;

        Point position;
        Point offset;

        public Document()
        {
            this.Rows = new List<string> { };
            this.position = new Point(0, 0);
            this.offset = new Point(0, 0);
        }

        public static Document Open(string path)
        {
            var doc = new Document();
            using (var reader = new StreamReader(path, Encoding.UTF8))
            {
                while (reader.ReadLine() is var line && line != null)
                {
                    doc.Rows.Add(line);
                }

            }

            return doc;
        }

        public void MoveLeft()
        {
            if (this.position.X > 0)
            {
                --this.position.X;
            }
            else if (this.position.Y > 0)
            {
                --this.position.Y;
                if (this.position.Y < this.Rows.Count)
                {
                    this.position.X = this.Rows[this.position.Y].Length;
                }
            }
            ClampPosition();
        }
        public void MoveRight()
        {
            if (this.position.Y < this.Rows.Count)
            {
                var row = this.Rows[this.position.Y];
                if (this.position.X < row.Length)
                {
                    ++this.position.X;
                }
                else
                {
                    this.position.X = 0;
                    ++this.position.Y;
                }
            }
            else
            {
                this.position.X = 0;
            }
            ClampPosition();
        }
        public void MoveUp()
        {
            if (this.position.Y > 0) { --this.position.Y; }
            ClampPosition();
        }
        public void MoveDown() {
            ++this.position.Y;
            ClampPosition();
        }
        void ClampPosition()
        {
            if (this.position.Y < this.Rows.Count)
            {
                var row = this.Rows[this.position.Y];
                if (this.position.X > row.Length) { this.position.X = row.Length; }
            }
            else
            {
                this.position.X = 0;
                this.position.Y = this.Rows.Count;
            }
        }

        public void MoveHome()
        {
            this.position.X = 0;
        }
        public void MoveEnd()
        {
            if (this.position.Y < this.Rows.Count)
            {
                this.position.X = this.Rows[this.position.Y].Length;
            }
            else
            {
                this.position.X = 0;
                this.position.Y = this.Rows.Count;
            }
        }
        public void MoveBeginOfFile()
        {
            this.position.X = 0;
        }
        public void MoveEndOfFile()
        {
            if (this.Rows.Count > 0)
            {
                var lastRowIndex = Rows.Count - 1;
                this.position.X = this.Rows[lastRowIndex].Length;
                this.position.Y = lastRowIndex;
            }
            else
            {
                this.position.X = 0;
                this.position.Y = 0;
            }
        }

        public void UpdateOffset(EditorConsole console)
        {
            if (this.position.Y < this.offset.Y)
            {
                this.offset.Y = this.position.Y;
            }
            if (this.position.Y >= this.offset.Y + console.Height)
            {
                this.offset.Y = this.position.Y - console.Height + 1;
            }
            if (this.position.X < this.offset.X)
            {
                this.offset.X = this.position.X;
            }
            if (this.position.X >= this.offset.X + console.Width)
            {
                this.offset.X = this.position.X - console.Width + 1;
            }
        }

    }

    class Editor
    {
        public static string Version = "0.0.1";

        readonly EditorConsole console;

        Document document;

        public Editor(EditorConsole console)
        {
            this.console = console;
        }

        public void SetDocument(Document document)
        {
            this.document = document;
        }

        public void Run()
        {
            while (true)
            {
                this.RefreshScreen();
                switch (this.ProcessKeyPress(this.console.ReadKey()))
                {
                    case EditorProcessingResults.Quit:
                        this.console.Clear();
                        return;
                    default:
                        continue;
                }
            }
        }



        void RefreshScreen()
        {
            this.document.UpdateOffset(this.console);
            this.console.RefreshScreen(this.DrawEditorRows, this.document.Cursor);
        }

        void DrawEditorRows(IScreenBuffer buffer)
        {
            for (int y = 0; y < this.console.Height; ++y)
            {
                var docRow = y + this.document.Offset.Y;
                if (docRow < this.document.Rows.Count)
                {
                    var docText = this.document.Rows[docRow];
                    var docLength = Math.Clamp(docText.Length - this.document.Offset.X, 0, this.console.Width);
                    buffer.AppendRow(docLength >0 ? docText.Substring(this.document.Offset.X, docLength) : "");
                }
                else
                {
                    if (this.document.Rows.Count == 0 && y == this.console.Height / 3)
                    {
                        var welcome = $"Cilo editor -- version {Version}";
                        var welcomeLength = Math.Min(welcome.Length, this.console.Width);
                        var padding = (this.console.Width - welcomeLength) / 2;
                        var rowBuffer = new StringBuilder();
                        if (padding > 0)
                        {
                            rowBuffer.Append("~");
                        }
                        for (int i = 1; i < padding; i++)
                        {
                            rowBuffer.Append(" ");
                        }
                        rowBuffer.Append(welcome.Substring(0, welcomeLength));

                        buffer.AppendRow(rowBuffer.ToString());
                    }
                    else
                    {
                        buffer.AppendRow("~");
                    }
                }
            }
        }

        public EditorProcessingResults ProcessKeyPress(ConsoleKeyInfo key)
        {
            if (key == SpecialKeys.CtrlQ)
            {
                this.console.Clear();
                return EditorProcessingResults.Quit;
            }
            switch (key.Key)
            {
                case ConsoleKey.Home:
                    this.document.MoveHome();
                    return EditorProcessingResults.Running;
                case ConsoleKey.End:
                    this.document.MoveEnd();
                    return EditorProcessingResults.Running;

                case ConsoleKey.PageUp:
                    return MovePage(ConsoleKey.UpArrow);
                case ConsoleKey.PageDown:
                    return MovePage(ConsoleKey.DownArrow);

                case ConsoleKey.UpArrow:
                case ConsoleKey.DownArrow:
                case ConsoleKey.LeftArrow:
                case ConsoleKey.RightArrow:
                    return this.MoveCursor(key.Key);
            }

            return EditorProcessingResults.Running;
        }

        public EditorProcessingResults MovePage(ConsoleKey key)
        {
            for (int i = 0; i < this.console.Height; ++i) {
                this.MoveCursor(key);
            }

            return EditorProcessingResults.Running;
        }

        public EditorProcessingResults MoveCursor(ConsoleKey key)
        {
            switch (key)
            {
                case ConsoleKey.LeftArrow:
                    this.document.MoveLeft();
                    break;
                case ConsoleKey.RightArrow:
                    this.document.MoveRight();
                    break;
                case ConsoleKey.UpArrow:
                    this.document.MoveUp();
                    break;
                case ConsoleKey.DownArrow:
                    this.document.MoveDown();
                    break;
            }
            return EditorProcessingResults.Running;
        }
    }

    [Serializable]
    public class EditorException : Exception
    {
        public EditorException() { }
        public EditorException(string message) : base(message) { }
        public EditorException(string message, Exception inner) : base(message, inner) { }
        protected EditorException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
