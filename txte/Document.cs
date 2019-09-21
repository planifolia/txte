using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;

namespace cilo
{
    class Row
    {
        public Row(string value)
        {
            this.Value = value;
        }

        public string Value { get; private set; }
        public string Render { get; private set; }

        public void InsertChar(char c, int at)
        {
            this.Value =
                this.Value.Substring(0, at) + c + this.Value.Substring(at);
        }

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
                foreach (var row in doc.Rows)
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

        public void Save()
        {
            if (this.Path == null) { return; }
            using (var file = new StreamWriter(this.Path, false, Encoding.UTF8))
            {
                foreach (var row in this.Rows)
                {
                    file.WriteLine(row.Value);
                }
            }
        }

        public void InsertChar(char c, EditorSetting setting)
        {

            if (this.valuePosition.Y == this.Rows.Count)
            {
                this.Rows.Add(new Row(""));
            }
            this.Rows[this.valuePosition.Y].InsertChar(c, this.valuePosition.X);
            this.Rows[this.valuePosition.Y].UpdateRender(setting);
            this.valuePosition.X++;
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

        public void UpdateOffset(IConsole console, EditorSetting setting)
        {
            this.renderPositionX = 0;
            int overshoot = 0;
            if (this.valuePosition.Y < this.Rows.Count)
            {
                this.renderPositionX =
                    this.Rows[this.valuePosition.Y].ValueXToRenderX(this.ValuePosition.X, setting);
                if (this.valuePosition.X < this.Rows[this.valuePosition.Y].Value.Length)
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
}
