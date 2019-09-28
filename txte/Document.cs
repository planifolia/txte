using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace txte
{
    class Row
    {
        public Row(string value) : this(value, false) {}
        public Row(string value, bool asNewLine)
        {
            this.Value = value;
            this.IsModified = asNewLine;
            this.Render = "";
        }

        public string Value { get; private set; }
        public string Render { get; private set; }
        public bool IsModified { get; private set; }

        public void InsertChar(char c, int at)
        {
            this.Value =
                this.Value.Substring(0, at) + c + this.Value.Substring(at);
            this.IsModified = true;
        }

        public void BackSpace(int at)
        {
            // requires: (0 < at && at <= this.Value.Length)
            this.Value =
                this.Value.Substring(0, at - 1) + this.Value.Substring(at);
            this.IsModified = true;
        }

        internal void Establish()
        {
            this.IsModified = false;
        }

        public void UpdateRender(EditorSetting setting)
        {
            var tabSize = setting.TabSize;
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
        public static async Task<Document> OpenAsync(string path, EditorSetting setting)
        {
            var doc = new Document
            {
                Path = path
            };
            using (var reader = new StreamReader(path, Encoding.UTF8))
            {
                var text = await reader.ReadToEndAsync();
                var lines = Regex.Split(text, @"\r\n|\r|\n");
                foreach (var line in lines)
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

        public string? Path { get; set; }
        public bool IsUntouched => this.Rows.Count == 0;
        public List<Row> Rows { get; }
        public Point Cursor => new Point(this.renderPositionX - this.offset.X, this.valuePosition.Y - this.offset.Y);
        public Point RenderPosition => new Point(this.renderPositionX, this.valuePosition.Y);
        public Point ValuePosition => this.valuePosition;
        public Point Offset => this.offset;
        public bool IsModified => this.Rows.Any(x => x.IsModified);

        int renderPositionX;
        Point valuePosition;
        Point offset;

        public void Save()
        {
            if (this.Path == null) { return; }
            using (var file = new StreamWriter(this.Path, false, Encoding.UTF8))
            {
                int rowCount = 0;
                foreach (var row in this.Rows)
                {
                    file.Write(row.Value);
                    if (rowCount != this.Rows.Count - 1) { file.WriteLine(); }
                    row.Establish();
                    rowCount++;
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
            this.MoveRight();
        }

        public void InsertNewLine(EditorSetting setting)
        {
            if (this.valuePosition.X == 0)
            {
                // it condition contains that case: this.valuePosition.Y == this.Rows.Count.
                this.Rows.Insert(this.ValuePosition.Y, new Row("", asNewLine: true));
            }
            else
            {
                this.Rows.Insert(
                    this.ValuePosition.Y + 1,
                    new Row(
                        this.Rows[this.ValuePosition.Y].Value.Substring(this.valuePosition.X),
                        asNewLine: true
                    )
                );
                this.Rows[this.ValuePosition.Y] = 
                    new Row(
                        this.Rows[this.ValuePosition.Y].Value.Substring(0, this.valuePosition.X),
                        asNewLine: true
                    );
                this.Rows[this.valuePosition.Y].UpdateRender(setting);
                this.Rows[this.valuePosition.Y + 1].UpdateRender(setting);
            }
            this.MoveHome();
            this.MoveDown();
        }
        
        public void BackSpace(EditorSetting setting)
        {
            var position = this.valuePosition;

            this.MoveLeft();

            if (position.X == 0 && position.Y == 0) { return; }
            if (position.X == 0)
            {
                if (position.Y == 0)
                {
                    // Do nothing if at the start of file.
                }
                else if (position.Y == this.Rows.Count)
                {
                    // Only back cursor if at the end of file.
                    // Maybe this condition cannot be met.
                }
                else
                {
                    // Delete new line of previous line
                    this.Rows[position.Y - 1] =
                        new Row(
                            this.Rows[position.Y - 1].Value + this.Rows[position.Y].Value,
                            asNewLine: true
                        );
                    this.Rows[position.Y - 1].UpdateRender(setting);
                    this.Rows.RemoveAt(position.Y);
                }
            }
            else
            {
                this.Rows[position.Y].BackSpace(position.X);
                this.Rows[position.Y].UpdateRender(setting);
            }
        }

        public void DeleteChar(EditorSetting setting)
        {
            if (this.IsUntouched) { return; }
            if (this.valuePosition.Y == this.Rows.Count - 1 
                && this.valuePosition.X == this.Rows[^1].Value.Length)
            {
                return;
            }

            this.MoveRight();
            this.BackSpace(setting);
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
            if (this.IsUntouched)
            {
                this.valuePosition.X = 0;
                this.valuePosition.Y = 0;
            }
            else if (this.valuePosition.Y < this.Rows.Count)
            {
                var row = this.Rows[this.valuePosition.Y];
                if (this.valuePosition.X > row.Value.Length) { this.valuePosition.X = row.Value.Length; }
            }
            else
            {
                this.valuePosition.Y = this.Rows.Count - 1;
                var row = this.Rows[this.valuePosition.Y];
                this.valuePosition.X = row.Value.Length;
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
        public void MoveStartOfFile()
        {
            this.valuePosition.X = 0;
            this.valuePosition.Y = 0;
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

        public void UpdateOffset(Size editArea, EditorSetting setting)
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
            if (this.valuePosition.Y >= this.offset.Y + editArea.Height)
            {
                this.offset.Y = this.valuePosition.Y - editArea.Height + 1;
            }
            if (this.renderPositionX < this.offset.X)
            {
                this.offset.X = this.renderPositionX;
            }
            if (this.renderPositionX >= this.offset.X + editArea.Width)
            {
                this.offset.X = this.renderPositionX - editArea.Width + 1 + overshoot;
            }
        }
    }
}
