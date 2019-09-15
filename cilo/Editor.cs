using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace cilo
{
    static class SpecialKeys
    {
        public static readonly ConsoleKeyInfo CtrlBreak = 
            new ConsoleKeyInfo((char)0x00, ConsoleKey.Pause, false, false, true);
        public static readonly ConsoleKeyInfo CtrlC = 
            new ConsoleKeyInfo((char)0x03, ConsoleKey.C, false, false, true);
        public static readonly ConsoleKeyInfo CtrlQ = 
            new ConsoleKeyInfo((char)0x11, ConsoleKey.Q, false, false, true);
    }


    enum EditorProcessingResults
    {
        Running,
        Quit,
    }

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

    class Row
    {
        public Row(string value)
        {
            this.Value = value;
        }

        public string Value { get; private set; }
        public string Render { get; private set; }

        public void UpdateRender(EditorSetting setting)
        {
            var tabSize = setting.TabSize;
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

        public int ValueXToRenderX(int valueX, EditorSetting setting)
        {
            int tabSize = setting.TabSize;
            bool ambiguousSetting = setting.IsFullWidthAmbiguous;
            int renderX = 0;
            for (int i = 0; i < valueX; i++)
            {
                if (this.Value[i] == '\t')
                {
                    renderX += (tabSize - 1) - (renderX % tabSize);
                }
                renderX += this.Value[i].GetEastAsianWidth(ambiguousSetting);
            }
            return renderX;
        }
    }

    class Document
    {
        public static Document Open(string path, EditorSetting setting)
        {
            var doc = new Document
            {
                Path = path
            };
            using (var reader = new StreamReader(path, Encoding.UTF8))
            {
                while (reader.ReadLine() is var line && line != null)
                {
                    doc.Rows.Add(new Row(line));
                }
                foreach(var row in doc.Rows)
                {
                    row.UpdateRender(setting);
                }
            }

            return doc;
        }

        public Document()
        {
            this.Path = null;
            this.Rows = new List<Row> { };
            this.valuePosition = new Point(0, 0);
            this.offset = new Point(0, 0);
        }

        public string Path { get; private set; }
        public List<Row> Rows { get; }
        public Point Cursor => new Point(this.renderPositionX - this.offset.X, this.valuePosition.Y - this.offset.Y);
        public Point RenderPosition => new Point(this.renderPositionX, this.valuePosition.Y);
        public Point ValuePosition => this.valuePosition;
        public Point Offset => this.offset;

        int renderPositionX;
        Point valuePosition;
        Point offset;

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

        public void UpdateOffset(IConsole console, EditorSetting setting)
        {
            this.renderPositionX = 0;
            int overshoot = 0;
            if (this.valuePosition.Y < this.Rows.Count)
            {
                this.renderPositionX = 
                    this.Rows[this.valuePosition.Y].ValueXToRenderX(this.ValuePosition.X, setting);
                if (valuePosition.X < this.Rows[this.valuePosition.Y].Value.Length)
                {
                    overshoot =
                        this.Rows[this.valuePosition.Y].Value[this.valuePosition.X]
                        .GetEastAsianWidth(setting.IsFullWidthAmbiguous) - 1;
                }
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
                this.offset.X = this.renderPositionX - console.Width + 1 + overshoot;
            }
        }
    }

    class TemporaryMessage
    {
        public TemporaryMessage(string value, DateTime time)
        {
            this.Value = value;
            this.Time = time;
        }

        public string Value { get; }
        public DateTime Time { get; }
    }

    class EditorSetting
    {
        public bool IsFullWidthAmbiguous { get; set; } = true;
        public int TabSize { get; set; } = 4;
    }

    class Editor
    {
        public static string Version = "0.0.1";

        public Editor(IConsole console, EditorSetting setting)
        {
            this.console = console;
            this.setting = setting;
        }

        readonly IConsole console;

        Document document;
        TemporaryMessage statusMessage;
        EditorSetting setting;

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

        public void SetStatusMessage(string value)
        {
            this.statusMessage = new TemporaryMessage(value, DateTime.Now);
        }

        void RefreshScreen()
        {
            this.document.UpdateOffset(this.console, this.setting);
            this.console.RefreshScreen(
                this.setting,
                this.DrawEditorRows,
                this.DrawSatausBar,
                this.DrawMessageBar,
                this.document.Cursor);
        }

        void DrawEditorRows(IScreen screen)
        {
            bool ambiguousSetting = this.setting.IsFullWidthAmbiguous;
            for (int y = 0; y < this.console.EditorHeight; y++)
            {
                var docRow = y + this.document.Offset.Y;
                if (docRow < this.document.Rows.Count)
                {
                    DrawDocumentRow(screen, ambiguousSetting, docRow);
                }
                else
                {
                    DrawOutofBounds(screen, y);
                }
            }
        }

        void DrawDocumentRow(IScreen screen, bool ambiguousSetting, int docRow)
        {
            var docText = this.document.Rows[docRow].Render;
            var docLength =
                Math.Clamp(
                    docText.GetConsoleLength(ambiguousSetting) - this.document.Offset.X,
                    0, this.console.Width
                );
            if (docLength > 0)
            {
                var (render, startIsFragmented, endIsFragmented) =
                    docText.SubConsoleString(this.document.Offset.X, docLength, ambiguousSetting);

                screen.AppendFragmentedRow(render, startIsFragmented, endIsFragmented);
            }
            else
            {
                screen.AppendRow("");
            }
        }

        void DrawOutofBounds(IScreen screen, int y)
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

                screen.AppendOuterRow(rowBuffer.ToString());
            }
            else
            {
                screen.AppendOuterRow("~");
            }
        }
        void DrawSatausBar(IScreen screen)
        {
            var fileName = this.document.Path != null ? Path.GetFileName(this.document.Path) : "[New File]";
            var documentRowCount = this.document.Rows.Count;
            var fileInfo = $"{fileName:.20} - {documentRowCount} lines";
            var positionInfo = $"{this.document.Cursor.Y}:{this.document.Cursor.X}";
            var padding = this.console.Width - fileInfo.Length - positionInfo.Length;

            screen.AppendRow(fileInfo + new string(' ', padding) + positionInfo);
        }
        void DrawMessageBar(IScreen screen)
        {
            if (DateTime.Now - this.statusMessage.Time < TimeSpan.FromSeconds(5))
            {
                var text = this.statusMessage.Value;
                var textLength = Math.Min(text.Length, this.console.Width);
                screen.AppendRow(text.Substring(0, textLength));
            }
            else
            {
                screen.AppendRow("");
            }
        }

        EditorProcessingResults ProcessKeyPress(ConsoleKeyInfo key)
        {
            if (key == SpecialKeys.CtrlQ)
            {
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
                case ConsoleKey.A:
                    if ((key.Modifiers & ConsoleModifiers.Alt) != 0)
                    {
                        return SwitchAmbiguousWidth();
                    }
                    break;
            }

            return EditorProcessingResults.Running;
        }

        EditorProcessingResults MovePage(ConsoleKey key)
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

        EditorProcessingResults MoveCursor(ConsoleKey key)
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

        EditorProcessingResults SwitchAmbiguousWidth()
        {
            this.setting.IsFullWidthAmbiguous = !this.setting.IsFullWidthAmbiguous;
            var ambiguousSize =
                this.setting.IsFullWidthAmbiguous ? "Full Width"
                : "Half Width";
            this.SetStatusMessage($"East Asian Width / Ambiguous = {ambiguousSize}");
            return EditorProcessingResults.Running;
        }
    }
}
