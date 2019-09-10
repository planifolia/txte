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

        public int RowsCount => Console.WindowHeight;
        public int ColumnsCount => Console.WindowWidth;

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
    }

    class Buffer
    {
        readonly StringBuilder buffer;

        public Buffer()
        {
            this.buffer = new StringBuilder();
        }

        public void Append(string value)
        {
            this.buffer.Append(value);
        }

        public void AppendClearSequence()
        {
            this.buffer.Append("\x1b[2J");
            // Console.Clear();
        }

        public void AppendClearLineSequence()
        {
            this.buffer.Append("\x1b[K");
        }

        public void AppendSetCursorPositionZeroSequence()
        {
            this.buffer.Append("\x1b[H");
            // Console.SetCursorPosition(0, 0);
        }
        public void AppendSetCursorPositionSequence(Point cursor)
        {
            this.buffer.Append($"\x1b[{cursor.Y + 1};{cursor.X + 1}H");
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

    class Document
    {
        public List<string> Rows { get; }
        public int RowCount { get; set; }

        public Document()
        {
            this.Rows = new List<string> { };
            this.RowCount = 0;
        }

        public static Document Open(string path)
        {
            var doc = new Document();
            using (var reader = new StreamReader(path, Encoding.UTF8))
            {
                while (reader.ReadLine() is var line && line != null)
                {
                    doc.Rows.Add(line);
                    doc.RowCount++;
                }

            }

            return doc;
        }
    }

    class Editor
    {
        public static string Version = "0.0.1";

        readonly EditorConsole console;

        Point cursor;
        Document docment;

        public Editor(EditorConsole console)
        {
            this.console = console;
            this.cursor = new Point(0, 0);
        }

        public void SetDocument(Document document)
        {
            this.docment = document;
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


        public void RefreshScreen()
        {
            var buffer = new Buffer();

            buffer.AppendHideCursorSequence();
            //buffer.AppendClearSequence();
            buffer.AppendSetCursorPositionZeroSequence();
            this.DrawEditorRows(buffer);
            buffer.AppendSetCursorPositionSequence(this.cursor);
            buffer.AppendShowCursorSequence();

            this.console.Write(buffer.ToString());
        }

        public void DrawEditorRows(Buffer buffer)
        {
            for (int y = 0; y < this.console.RowsCount; y++)
            {
                if (y >= this.docment.RowCount)
                {
                    if (this.docment.RowCount == 0 && y == this.console.RowsCount / 3)
                    {
                        var welcome = $"Cilo editor -- version {Version}";
                        var welcomeLength = Math.Min(welcome.Length, this.console.ColumnsCount);
                        var padding = (this.console.ColumnsCount - welcomeLength) / 2;
                        if (padding > 0)
                        {
                            buffer.Append("~");
                        }
                        for (int i = 1; i < padding; i++)
                        {
                            buffer.Append(" ");
                        }
                        buffer.Append(welcome.Substring(0, welcomeLength));
                    }
                    else
                    {
                        buffer.Append("~");
                    }
                }
                else
                {
                    var docText = this.docment.Rows[y];
                    var docLength = Math.Min(docText.Length, this.console.ColumnsCount);
                    buffer.Append(docText.Substring(0, docLength));
                }
                
                buffer.AppendClearLineSequence();
                if (y < this.console.RowsCount - 1)
                {
                    buffer.Append("\r\n");
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
                    this.cursor.X = 0;
                    return EditorProcessingResults.Running;
                case ConsoleKey.End:
                    this.cursor.X = console.ColumnsCount;
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
            for (int i = 0; i < this.console.RowsCount; i++) {
                this.MoveCursor(key);
            }

            return EditorProcessingResults.Running;
        }

        public EditorProcessingResults MoveCursor(ConsoleKey key)
        {
            switch (key)
            {
                case ConsoleKey.LeftArrow:
                    if (this.cursor.X > 0) { this.cursor.X--; }
                    break;
                case ConsoleKey.RightArrow:
                    if (this.cursor.X < this.console.ColumnsCount - 1) { this.cursor.X++; }
                    break;
                case ConsoleKey.UpArrow:
                    if (this.cursor.Y > 0) { this.cursor.Y--; }
                    break;
                case ConsoleKey.DownArrow:
                    if (this.cursor.Y < this.console.RowsCount - 1) { this.cursor.Y++; }
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
