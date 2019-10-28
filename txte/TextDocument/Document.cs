using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using txte.ConsoleInterface;
using txte.Settings;
using txte.Text;

namespace txte.TextDocument
{
    interface IDocument
    {
        List<Row> Rows { get; }
        Point ValuePosition { get; set; }
        Point Offset { get; set; }
    }

    interface ITextFinder
    {
        string Current { get; }
        Coloring Highlight(Row row);
    }

    class Document : IDocument
    {
        public static async Task<Document> OpenAsync(string path, Setting setting)
        {
            var doc = new Document(setting)
            {
                Path = path
            };

            using var reader = new StreamReader(path, Encoding.UTF8);
            var text = await reader.ReadToEndAsync();
            var newLine = AnyNewLinePattern.Match(text);
            if (newLine.Success) {
                doc.NewLineFormat = EndOfLineFormat.FromSequence(newLine.Value);
            }
            var lines = AnyNewLinePattern.Split(text);
            foreach (var line in lines)
            {
                doc.Rows.Add(new Row(setting, line));
            }

            return doc;
        }

        static readonly Regex AnyNewLinePattern = new Regex(@"\r\n|\r|\n");

        public Document(Setting setting)
        {
            this.Path = null;
            this.NewLineFormat = EndOfLineFormat.FromSequence(Environment.NewLine);
            this.Rows = new List<Row> { };
            this.valuePosition = new Point(0, 0);
            this.offset = new Point(0, 0);
            this.setting = setting;
        }

        public string? Path { get; set; }
        public EndOfLineFormat NewLineFormat { get; set; }
        public bool IsNew => this.Rows.Count == 0;
        public List<Row> Rows { get; }
        public Point Cursor => new Point(this.renderPositionX - this.offset.X, this.valuePosition.Y - this.offset.Y);
        public Point RenderPosition => new Point(this.renderPositionX, this.valuePosition.Y);
        public Point ValuePosition { get => this.valuePosition; set => this.valuePosition = value; }
        public Point Offset { get => this.offset; set => this.offset = value; }
        public bool IsModified => this.Rows.Any(x => x.IsModified);
        public Temporary<ITextFinder> Finding { get; } = new Temporary<ITextFinder>();

        readonly Setting setting;

        int renderPositionX;
        Point valuePosition;
        Point offset;

        public void DrawRow(IScreen screen, int docRow)
        {
            var renderBase = this.Rows[docRow].Render;
            ColoredString render;
            if (this.Finding.HasValue && this.Finding.Value.Current.Length != 0)
            {
                var founds = this.Finding.Value.Highlight(this.Rows[docRow]);
                render = renderBase.Overlay(founds);
            }
            else
            {
                render = renderBase;
            }
            var clippedLength =
                (render.GetRenderLength() - this.Offset.X).Clamp(0, screen.Width);
            if (clippedLength > 0)
            {
                var clippedRender =
                    render.SubRenderString(this.Offset.X, clippedLength);

                screen.AppendRow(clippedRender.ToStyledStrings());
            }
            else
            {
                screen.AppendRow("");
            }
        }

        public void Save()
        {
            if (this.Path == null) { return; }
            using var file = new StreamWriter(this.Path, false, Encoding.UTF8);
            int rowCount = 0;
            foreach (var row in this.Rows)
            {
                row.Establish(x => file.Write(x));
                if (rowCount != this.Rows.Count - 1) { file.Write(this.NewLineFormat.Sequence); }
                rowCount++;
            }
        }

        public void InsertChar(char c)
        {
            if (this.valuePosition.Y == this.Rows.Count)
            {
                this.Rows.Add(new Row(this.setting, ""));
            }
            this.Rows[this.valuePosition.Y].InsertChar(c, this.valuePosition.X);
            this.MoveRight();
        }

        public void InsertNewLine()
        {
            if (this.valuePosition.X == 0)
            {
                // it condition contains that case: this.valuePosition.Y == this.Rows.Count.
                this.Rows.Insert(this.ValuePosition.Y, new Row(this.setting, "", isNewLine: true));
            }
            else
            {
                this.Rows.Insert(
                    this.ValuePosition.Y + 1,
                    new Row(
                        this.setting,
                        this.Rows[this.ValuePosition.Y].Value.Substring(this.valuePosition.X),
                        isNewLine: true
                    )
                );
                this.Rows[this.ValuePosition.Y] = 
                    new Row(
                        this.setting,
                        this.Rows[this.ValuePosition.Y].Value.Substring(0, this.valuePosition.X),
                        isNewLine: true
                    );
            }
            this.MoveHome();
            this.MoveDown();
        }
        
        public void BackSpace()
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
                            this.setting,
                            this.Rows[position.Y - 1].Value + this.Rows[position.Y].Value,
                            isNewLine: true
                        );
                    this.Rows.RemoveAt(position.Y);
                }
            }
            else
            {
                this.Rows[position.Y].BackSpace(position.X);
            }
        }

        public void DeleteChar()
        {
            if (this.IsNew) { return; }
            if (this.valuePosition.Y == this.Rows.Count - 1 
                && this.valuePosition.X == this.Rows[^1].Value.Length)
            {
                return;
            }

            this.MoveRight();
            this.BackSpace();
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
            if (this.valuePosition.Y < this.Rows.Count - 1) { this.valuePosition.Y++; }
            this.ClampPosition();
        }
        void ClampPosition()
        {
            if (this.IsNew)
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

        public void MoveUp(int repeat)
        {
            for (int i = 0; i < repeat; i++)
            {
                this.MoveUp();
            }
        }
        public void MoveDown(int repeat)
        {
            if (this.valuePosition.Y > this.Rows.Count)
            {
                this.valuePosition.Y = this.Rows.Count;
            }
            for (int i = 0; i < repeat; i++)
            {
                this.MoveDown();
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
            this.MoveUp(consoleHeight);
        }
        public void MovePageDown(int consoleHeight)
        {
            this.valuePosition.Y = this.offset.Y + consoleHeight - 1;
            this.MoveDown(consoleHeight);

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

        public void UpdateOffset(Size editArea)
        {
            this.renderPositionX = 0;
            int overshoot = 0;
            if (this.valuePosition.Y < this.Rows.Count)
            {
                this.renderPositionX =
                    this.Rows[this.valuePosition.Y].ValueXToRenderX(this.ValuePosition.X);
                if (this.valuePosition.X < this.Rows[this.valuePosition.Y].Value.Length)
                {
                    overshoot =
                        this.Rows[this.valuePosition.Y].Value[this.valuePosition.X]
                        .GetEastAsianWidth(this.setting.IsFullWidthAmbiguous) - 1;
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
