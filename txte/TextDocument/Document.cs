using System;
using System.Collections.Generic;
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
        Line.List Lines { get; }
        Point ValuePosition { get; set; }
        Point Offset { get; set; }
    }

    interface IOverlapHilighter
    {
        ColoredString Highlight(Line line);
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
            if (newLine.Success)
            {
                doc.NewLineFormat = EndOfLineFormat.FromSequence(newLine.Value);
            }

            var lines = AnyNewLinePattern.Split(text);
            foreach (var line in lines)
            {
                doc.Lines.Add(new Line(setting, line));
            }

            return doc;
        }

        static readonly Regex AnyNewLinePattern = new Regex(@"\r\n|\r|\n");

        public Document(Setting setting)
        {
            this.Path = null;
            this.NewLineFormat = EndOfLineFormat.FromSequence(Environment.NewLine);
            this.Lines = new Line.List { };
            this.valuePosition = new Point(0, 0);
            this.offset = new Point(0, 0);
            this.setting = setting;
        }

        public string? Path { get; set; }
        public EndOfLineFormat NewLineFormat { get; set; }
        public bool IsNew => this.Lines.Count == 0;
        public Line.List Lines { get; }
        public Point Cursor => new Point(this.renderPositionX - this.offset.X, this.valuePosition.Y - this.offset.Y);
        public Point RenderPosition => new Point(this.renderPositionX, this.valuePosition.Y);
        public Point ValuePosition { get => this.valuePosition; set => this.valuePosition = value; }
        public Point Offset { get => this.offset; set => this.offset = value; }
        public bool IsModified => this.Lines.Any(x => x.IsModified);
        public Temporary<IOverlapHilighter> OverlapHilighter { get; } = new ();

        readonly Setting setting;

        int renderPositionX;
        Point valuePosition;
        Point offset;

        public void DrawLine(IScreen screen, int docLine)
        {
            var render = 
                (this.OverlapHilighter.HasValue) ? this.OverlapHilighter.Value.Highlight(this.Lines[docLine])
                : this.Lines[docLine].Render;
            var clippedLength =
                (render.GetRenderLength() - this.Offset.X).Clamp(0, screen.Width);
            if (clippedLength > 0)
            {
                var clippedRender =
                    render.SubRenderString(this.Offset.X, clippedLength);

                screen.AppendLine(clippedRender.ToStyledStrings());
            }
            else
            {
                screen.AppendLine("");
            }
        }

        public void Save()
        {
            if (this.Path == null) return;

            using var file = new StreamWriter(this.Path, false, Encoding.UTF8);
            int lineCount = 0;
            foreach (var line in this.Lines)
            {
                line.Establish(x => file.Write(x));
                if (lineCount != this.Lines.Count - 1) { file.Write(this.NewLineFormat.Sequence); }
                lineCount++;
            }
        }

        public void InsertChar(char c)
        {
            if (this.valuePosition.Y == this.Lines.Count)
            {
                this.Lines.Add(new Line(this.setting, ""));
            }
            this.Lines[this.valuePosition.Y].InsertChar(c, this.valuePosition.X);
            this.MoveRight();
        }

        public void InsertNewLine()
        {
            if (this.valuePosition.X == 0)
            {
                // it condition contains that case: this.valuePosition.Y == this.Lines.Count.
                this.Lines.Insert(this.ValuePosition.Y, new Line(this.setting, "", isNewLine: true));
            }
            else
            {
                this.Lines.Insert(
                    this.ValuePosition.Y + 1,
                    new Line(
                        this.setting,
                        this.Lines[this.ValuePosition.Y].Value.Substring(this.valuePosition.X),
                        isNewLine: true
                    )
                );
                this.Lines[this.ValuePosition.Y] =
                    new Line(
                        this.setting,
                        this.Lines[this.ValuePosition.Y].Value.Substring(0, this.valuePosition.X),
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
                else if (position.Y == this.Lines.Count)
                {
                    // Only back cursor if at the end of file.
                    // Maybe this condition cannot be met.
                }
                else
                {
                    // Delete new line of previous line
                    this.Lines[position.Y - 1] =
                        new Line(
                            this.setting,
                            this.Lines[position.Y - 1].Value + this.Lines[position.Y].Value,
                            isNewLine: true
                        );
                    this.Lines.RemoveAt(position.Y);
                }
            }
            else
            {
                this.Lines[position.Y].BackSpace(position.X);
            }
        }

        public void DeleteChar()
        {
            if (this.IsNew) { return; }
            if (this.valuePosition.Y == this.Lines.Count - 1
                && this.valuePosition.X == this.Lines[^1].Value.Length)
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
                if (this.valuePosition.Y < this.Lines.Count)
                {
                    this.valuePosition.X = this.Lines[this.valuePosition.Y].Value.Length;
                }
            }
            this.ClampPosition();
        }
        public void MoveRight()
        {
            if (this.valuePosition.Y < this.Lines.Count)
            {
                var line = this.Lines[this.valuePosition.Y];
                if (this.valuePosition.X < line.Value.Length)
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
            if (this.valuePosition.Y < this.Lines.Count - 1) { this.valuePosition.Y++; }
            this.ClampPosition();
        }
        void ClampPosition()
        {
            if (this.IsNew)
            {
                this.valuePosition.X = 0;
                this.valuePosition.Y = 0;
            }
            else if (this.valuePosition.Y < this.Lines.Count)
            {
                var line = this.Lines[this.valuePosition.Y];
                if (this.valuePosition.X > line.Value.Length) { this.valuePosition.X = line.Value.Length; }
            }
            else
            {
                this.valuePosition.Y = this.Lines.Count - 1;
                var line = this.Lines[this.valuePosition.Y];
                this.valuePosition.X = line.Value.Length;
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
            if (this.valuePosition.Y > this.Lines.Count)
            {
                this.valuePosition.Y = this.Lines.Count;
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
            if (this.valuePosition.Y < this.Lines.Count)
            {
                this.valuePosition.X = this.Lines[this.valuePosition.Y].Value.Length;
            }
            else
            {
                this.valuePosition.X = 0;
                this.valuePosition.Y = this.Lines.Count;
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
            if (this.Lines.Count > 0)
            {
                var lastLineIndex = this.Lines.Count - 1;
                this.valuePosition.X = this.Lines[lastLineIndex].Value.Length;
                this.valuePosition.Y = lastLineIndex;
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
            if (this.valuePosition.Y < this.Lines.Count)
            {
                this.renderPositionX =
                    this.Lines[this.valuePosition.Y].ValueXToRenderX(this.ValuePosition.X);
                if (this.valuePosition.X < this.Lines[this.valuePosition.Y].Value.Length)
                {
                    overshoot =
                        this.Lines[this.valuePosition.Y].Value[this.valuePosition.X]
                        .GetEastAsianWidth(this.setting.AmbiguousCharIsFullWidth) - 1;
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
