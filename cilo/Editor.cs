using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace cilo
{
    static class SpecialKeys
    {
        public static readonly ConsoleKeyInfo CtrlBreak = new ConsoleKeyInfo((char)0x00, ConsoleKey.Pause, false, false, true);
        public static readonly ConsoleKeyInfo CtrlC = new ConsoleKeyInfo((char)0x03, ConsoleKey.C, false, false, true);
        public static readonly ConsoleKeyInfo CtrlQ = new ConsoleKeyInfo((char)0x11, ConsoleKey.Q, false, false, true);
    }


    enum EditorProcessingResults
    {
        Running,
        Quit,
    }

    interface IEditorConsole
    {
        int Height { get; }
        int EditorHeight { get; }
        int Width { get; }
        Size Size { get; }

        ConsoleKeyInfo ReadKey();
        void Clear();
        void RefreshScreen(
            Action<IScreenBuffer> drawEditorRows,
            Action<IScreenBuffer> drawStatusBar,
            Action<IScreenBuffer> drawMessageBar,
            Point cursor);
    }
    interface IScreenBuffer
    {
        void AppendRow(string value);
    }

    class Row
    {
        public Row(string value)
        {
            this.Value = value;
            this.UpdateRender(4);
        }

        public string Value { get; private set; }
        public string Render { get; private set; }

        public void UpdateRender(int tabSize)
        {
            //int tabs = 0;
            //for (int i = 0; i < this.Value.Length; i++)
            //{
            //    if (this.Value[i] == '\t') { tabs++; }
            //}
            //var renderBuilder = new StringBuilder(this.Value.Length + tabs * (tabSize - 1));
            var renderBuilder = new StringBuilder();
            for (int iValue = 0; iValue < this.Value.Length; iValue++)
            {
                if (this.Value[iValue] == '\t')
                {
                    renderBuilder.Append(' ');
                    while (renderBuilder.Length % tabSize != 0) { renderBuilder.Append(' '); }
                }
                else
                {
                    renderBuilder.Append(this.Value[iValue]);
                }
            }
            this.Render = renderBuilder.ToString();
        }

        public int ValueXToRenderX(int valueX, int tabSize)
        {
            int renderX = 0;
            for (int i = 0; i < valueX; i++)
            {
                if (this.Value[i] == '\t')
                {
                    renderX += (tabSize - 1) - (renderX % tabSize);
                }
                renderX++;
            }
            return renderX;
        }
    }

    class Document
    {
        public string Path { get; private set; }
        public List<Row> Rows { get; }
        public Point Cursor => new Point(this.renderPositionX - this.offset.X, this.valuePosition.Y - this.offset.Y);
        public Point RenderPosition => new Point(this.renderPositionX, this.valuePosition.Y);
        public Point ValuePosition => this.valuePosition;
        public Point Offset => this.offset;

        Point renderPosition;
        int renderPositionX;
        Point valuePosition;
        Point offset;

        public Document()
        {
            this.Path = null;
            this.Rows = new List<Row> { };
            this.valuePosition = new Point(0, 0);
            this.offset = new Point(0, 0);
        }

        public static Document Open(string path)
        {
            var doc = new Document();
            doc.Path = path;
            using (var reader = new StreamReader(path, Encoding.UTF8))
            {
                while (reader.ReadLine() is var line && line != null)
                {
                    doc.Rows.Add(new Row(line));
                }
            }

            return doc;
        }

        public void MoveLeft()
        {
            if (this.valuePosition.X > 0)
            {
                this.valuePosition.X--;
            }
            else if (this.valuePosition.Y > 0)
            {
                this.valuePosition.Y--;
                if (this.valuePosition.Y < this.Rows.Count)
                {
                    this.valuePosition.X = this.Rows[this.valuePosition.Y].Value.Length;
                }
            }
            this.ClampPosition();
        }
        public void MoveRight()
        {
            if (this.valuePosition.Y < this.Rows.Count)
            {
                var row = this.Rows[this.valuePosition.Y];
                if (this.valuePosition.X < row.Value.Length)
                {
                    this.valuePosition.X++;
                }
                else
                {
                    this.valuePosition.X = 0;
                    this.valuePosition.Y++;
                }
            }
            else
            {
                this.valuePosition.X = 0;
            }
            this.ClampPosition();
        }
        public void MoveUp()
        {
            if (this.valuePosition.Y > 0) { this.valuePosition.Y--; }
            this.ClampPosition();
        }
        public void MoveDown()
        {
            this.valuePosition.Y++;
            this.ClampPosition();
        }
        void ClampPosition()
        {
            if (this.valuePosition.Y < this.Rows.Count)
            {
                var row = this.Rows[this.valuePosition.Y];
                if (this.valuePosition.X > row.Value.Length) { this.valuePosition.X = row.Value.Length; }
            }
            else
            {
                this.valuePosition.X = 0;
                this.valuePosition.Y = this.Rows.Count;
            }
        }

        public void MoveHome()
        {
            this.valuePosition.X = 0;
        }
        public void MoveEnd()
        {
            if (this.valuePosition.Y < this.Rows.Count)
            {
                this.valuePosition.X = this.Rows[this.valuePosition.Y].Value.Length;
            }
            else
            {
                this.valuePosition.X = 0;
                this.valuePosition.Y = this.Rows.Count;
            }
        }
        public void MovePageUp(int consoleHeight)
        {
            this.valuePosition.Y = this.offset.Y;
            for (int i = 0; i < consoleHeight; i++)
            {
                this.MoveUp();
            }
        }
        public void MovePageDown(int consoleHeight)
        {
            this.valuePosition.Y = this.offset.Y + consoleHeight - 1;
            if (this.valuePosition.Y > this.Rows.Count)
            {
                this.valuePosition.Y = this.Rows.Count;
            }
            for (int i = 0; i < consoleHeight; i++)
            {
                this.MoveDown();
            }
        }
        public void MoveBeginOfFile()
        {
            this.valuePosition.X = 0;
        }
        public void MoveEndOfFile()
        {
            if (this.Rows.Count > 0)
            {
                var lastRowIndex = this.Rows.Count - 1;
                this.valuePosition.X = this.Rows[lastRowIndex].Value.Length;
                this.valuePosition.Y = lastRowIndex;
            }
            else
            {
                this.valuePosition.X = 0;
                this.valuePosition.Y = 0;
            }
        }

        public void UpdateOffset(IEditorConsole console)
        {
            this.renderPositionX = 0;
            if (this.valuePosition.Y < this.Rows.Count)
            {
                this.renderPositionX = this.Rows[this.valuePosition.Y].ValueXToRenderX(this.ValuePosition.X, 4);
            }

            if (this.valuePosition.Y < this.offset.Y)
            {
                this.offset.Y = this.valuePosition.Y;
            }
            if (this.valuePosition.Y >= this.offset.Y + console.EditorHeight)
            {
                this.offset.Y = this.valuePosition.Y - console.EditorHeight + 1;
            }
            if (this.renderPositionX < this.offset.X)
            {
                this.offset.X = this.renderPositionX;
            }
            if (this.renderPositionX >= this.offset.X + console.Width)
            {
                this.offset.X = this.renderPositionX - console.Width + 1;
            }
        }
    }

    class StatusMessage
    {
        public StatusMessage(string value, DateTime time)
        {
            this.Value = value;
            this.Time = time;
        }

        public string Value { get; }
        public DateTime Time { get; }
    }

    class Editor
    {
        public static string Version = "0.0.1";

        readonly IEditorConsole console;

        Document document;
        StatusMessage statusMessage;

        public Editor(IEditorConsole console)
        {
            this.console = console;
            this.statusMessage = null;
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
            this.console.RefreshScreen(
                this.DrawEditorRows,
                this.DrawSatausBar,
                this.DrawMessageBar,
                this.document.Cursor);
        }

        void DrawEditorRows(IScreenBuffer buffer)
        {
            for (int y = 0; y < this.console.EditorHeight; y++)
            {
                var docRow = y + this.document.Offset.Y;
                if (docRow < this.document.Rows.Count)
                {
                    var docText = this.document.Rows[docRow].Render;
                    var docLength = Math.Clamp(docText.Length - this.document.Offset.X, 0, this.console.Width);
                    buffer.AppendRow(docLength > 0 ? docText.Substring(this.document.Offset.X, docLength) : "");
                }
                else
                {
                    if (this.document.Rows.Count == 0 && y == this.console.EditorHeight / 3)
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
        void DrawSatausBar(IScreenBuffer buffer)
        {
            var fileName = this.document.Path != null ? Path.GetFileName(this.document.Path) : "[New File]";
            var documentRowCount = this.document.Rows.Count;
            var fileInfo = $"{fileName:.20} - {documentRowCount} lines";
            var positionInfo = $"{this.document.Cursor.Y}:{this.document.Cursor.X}";
            var padding = this.console.Width - fileInfo.Length - positionInfo.Length - 1;

            buffer.AppendRow(fileInfo + new string(' ', padding) + positionInfo);
        }
        void DrawMessageBar(IScreenBuffer buffer)
        {
            if (DateTime.Now - this.statusMessage.Time < TimeSpan.FromSeconds(5))
            {
                var text = this.statusMessage.Value;
                var textLength = Math.Min(text.Length, this.console.Width);
                buffer.AppendRow(text.Substring(0, textLength));
            }
            else
            {
                buffer.AppendRow("");
            }
        }

        public void SetStatusMessage(string value)
        {
            this.statusMessage = new StatusMessage(value, DateTime.Now);
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
                case ConsoleKey.PageDown:
                    return this.MovePage(key.Key);

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
            switch (key)
            {
                case ConsoleKey.PageUp:
                    this.document.MovePageUp(this.console.EditorHeight);
                    break;
                case ConsoleKey.PageDown:
                    this.document.MovePageDown(this.console.EditorHeight);
                    break;
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
}
