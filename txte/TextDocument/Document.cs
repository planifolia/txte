using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using txte.ConsoleInterface;
using txte.Settings;
using txte.SyntaxHighlight;
using txte.Text;

namespace txte.TextDocument
{
    interface IDocument
    {
        Line.List Lines { get; }
        ValuePosition ValuePosition { get; set; }
        RenderPosition Offset { get; set; }
    }

    interface ISyntaxable
    {
        IHighlighter LanguageHighLighter { get; }
    }

    interface IOverlapHilighter
    {
        ColoredString Highlight(Line line);
    }

    class Document : IDocument, ISyntaxable
    {
        public static async Task<Document> OpenAsync(string path, Setting setting)
        {
            using var reader = new StreamReader(path, Encoding.UTF8);
            var text = await reader.ReadToEndAsync();

            var doc = new Document(setting)
            {
                IsNew = false,
                IsBlank = false,
                Path = path,
            };

            var newLine = AnyNewLinePattern.Match(text);
            if (newLine.Success)
            {
                doc.NewLineFormat = EndOfLineFormat.FromSequence(newLine.Value);
            }

            var lines = AnyNewLinePattern.Split(text);
            foreach (var line in lines)
            {
                doc.Lines.Add(new Line(doc, setting, line));
            }

            return doc;
        }

        static readonly Regex AnyNewLinePattern = new Regex(@"\r\n|\r|\n");

        public Document(Setting setting)
        {
            this.LanguageHighLighter = PlainTextHighLighter.Default;
            this.Path = null;
            this.NewLineFormat = EndOfLineFormat.FromSequence(Environment.NewLine);
            this.IsNew = true;
            this.IsBlank = true;
            this.Lines = new Line.List { };
            this.valuePosition = new (0, 0);
            this.offset = new (0, 0);
            this.setting = setting;
        }

        public string? Path
        {
            get => this.path;
            set
            {
                this.path = value;
                var highlighter = 
                    (value is { } availablePath) ? Highlighter.FromExtension(global::System.IO.Path.GetExtension(availablePath))
                    : PlainTextHighLighter.Default;
                if (this.LanguageHighLighter != highlighter)
                {
                    this.LanguageHighLighter = highlighter;
                    foreach (var line in this.Lines)
                    {
                        line.ClearSyntacCache();
                    }
                }
            }
        }
        public EndOfLineFormat NewLineFormat { get; set; }
        public bool IsNew { get; private set; }
        public bool IsBlank { get; private set; }
        public Line.List Lines { get; }
        public CursorPosition Cursor => new (this.valuePosition.Line - this.offset.Line, this.renderPositionColumn - this.offset.Column);
        public RenderPosition RenderPosition => new (this.valuePosition.Line, this.renderPositionColumn);
        public ValuePosition ValuePosition { get => this.valuePosition; set => this.valuePosition = value; }
        public RenderPosition Offset { get => this.offset; set => this.offset = value; }
        public bool IsModified => this.Lines.Any(x => x.IsModified);
        public IHighlighter LanguageHighLighter { get; set; }
        public Temporary<IOverlapHilighter> OverlapHilighter { get; } = new ();

        readonly Setting setting;

        string? path;
        int renderPositionColumn;
        ValuePosition valuePosition;
        RenderPosition offset;

        public void DrawLine(IScreen screen, int docLine)
        {
            var render = 
                (this.OverlapHilighter.HasValue) ? this.OverlapHilighter.Value.Highlight(this.Lines[docLine])
                : this.Lines[docLine].Render;
            var clippedLength =
                (render.GetRenderLength() - this.offset.Column).Clamp(0, screen.Width);
            if (clippedLength > 0)
            {
                var clippedRender =
                    render.SubRenderString(this.offset.Column, clippedLength);

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
            this.IsNew = false;

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
            this.IsBlank = false;
    
            if (this.valuePosition.Line == this.Lines.Count)
            {
                this.Lines.Add(new Line(this, this.setting, ""));
            }
            this.Lines[this.valuePosition.Line].InsertChar(c, this.valuePosition.Char);
            this.MoveRight();
        }

        public void InsertNewLine()
        {
            this.IsBlank = false;

            if (this.valuePosition.Char == 0)
            {
                // it condition contains that case: this.valuePosition.Line == this.Lines.Count.
                this.Lines.Insert(this.valuePosition.Line, new Line(this, this.setting, "", isNewLine: true));
            }
            else
            {
                this.Lines.Insert(
                    this.valuePosition.Line + 1,
                    new Line(
                        this,
                        this.setting,
                        this.Lines[this.valuePosition.Line].Value.Substring(this.valuePosition.Char),
                        isNewLine: true
                    )
                );
                this.Lines[this.valuePosition.Line] =
                    new Line(
                        this,
                        this.setting,
                        this.Lines[this.valuePosition.Line].Value.Substring(0, this.valuePosition.Char),
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

            if (position.Char == 0)
            {
                if (position.Line == 0)
                {
                    // Do nothing if at the start of file.
                }
                else if (position.Line == this.Lines.Count)
                {
                    // Only back cursor if at the end of file.
                    // Maybe this condition cannot be met.
                }
                else
                {
                    // Delete new line of previous line
                    this.Lines[position.Line - 1] =
                        new Line(
                            this,
                            this.setting,
                            this.Lines[position.Line - 1].Value + this.Lines[position.Line].Value,
                            isNewLine: true
                        );
                    this.Lines.RemoveAt(position.Line);
                }
            }
            else
            {
                this.Lines[position.Line].BackSpace(position.Char);
            }
        }

        public void DeleteChar()
        {
            if (this.Lines.Count == 0) return;
            if (this.valuePosition.Line == this.Lines.Count - 1
                && this.valuePosition.Char == this.Lines[^1].Value.Length)
            {
                return;
            }

            this.MoveRight();
            this.BackSpace();
        }

        public void MoveLeft()
        {
            if (this.valuePosition.Char > 0)
            {
                this.valuePosition.Char--;
            }
            else if (this.valuePosition.Line > 0)
            {
                this.valuePosition.Line--;
                if (this.valuePosition.Line < this.Lines.Count)
                {
                    this.valuePosition.Char = this.Lines[this.valuePosition.Line].Value.Length;
                }
            }
            this.ClampPosition();
        }
        public void MoveRight()
        {
            if (this.valuePosition.Line < this.Lines.Count)
            {
                var line = this.Lines[this.valuePosition.Line];
                if (this.valuePosition.Char < line.Value.Length)
                {
                    this.valuePosition.Char++;
                }
                else
                {
                    this.valuePosition.Char = 0;
                    this.valuePosition.Line++;
                }
            }
            else
            {
                this.valuePosition.Char = 0;
            }
            this.ClampPosition();
        }
        public void MoveUp()
        {
            if (this.valuePosition.Line > 0) { this.valuePosition.Line--; }
            this.ClampPosition();
        }
        public void MoveDown()
        {
            if (this.valuePosition.Line < this.Lines.Count - 1) { this.valuePosition.Line++; }
            this.ClampPosition();
        }
        void ClampPosition()
        {
            if (this.Lines.Count == 0)
            {
                this.valuePosition.Char = 0;
                this.valuePosition.Line = 0;
            }
            else if (this.valuePosition.Line < this.Lines.Count)
            {
                var line = this.Lines[this.valuePosition.Line];
                if (this.valuePosition.Char > line.Value.Length) { this.valuePosition.Char = line.Value.Length; }
            }
            else
            {
                this.valuePosition.Line = this.Lines.Count - 1;
                var line = this.Lines[this.valuePosition.Line];
                this.valuePosition.Char = line.Value.Length;
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
            if (this.valuePosition.Line > this.Lines.Count)
            {
                this.valuePosition.Line = this.Lines.Count;
            }
            for (int i = 0; i < repeat; i++)
            {
                this.MoveDown();
            }
        }

        public void MoveHome()
        {
            this.valuePosition.Char = 0;
        }
        public void MoveEnd()
        {
            if (this.valuePosition.Line < this.Lines.Count)
            {
                this.valuePosition.Char = this.Lines[this.valuePosition.Line].Value.Length;
            }
            else
            {
                this.valuePosition.Char = 0;
                this.valuePosition.Line = this.Lines.Count;
            }
        }

        public void MovePageUp(int consoleHeight)
        {
            this.valuePosition.Line = this.offset.Line;
            this.MoveUp(consoleHeight);
        }
        public void MovePageDown(int consoleHeight)
        {
            this.valuePosition.Line = this.offset.Line + consoleHeight - 1;
            this.MoveDown(consoleHeight);

        }

        public void MoveStartOfFile()
        {
            this.valuePosition.Char = 0;
            this.valuePosition.Line = 0;
        }
        public void MoveEndOfFile()
        {
            if (this.Lines.Count > 0)
            {
                var lastLineIndex = this.Lines.Count - 1;
                this.valuePosition.Char = this.Lines[lastLineIndex].Value.Length;
                this.valuePosition.Line = lastLineIndex;
            }
            else
            {
                this.valuePosition.Char = 0;
                this.valuePosition.Line = 0;
            }
        }

        public void UpdateOffset(Size editArea)
        {
            this.renderPositionColumn = 0;
            int overshoot = 0;
            if (this.valuePosition.Line < this.Lines.Count)
            {
                this.renderPositionColumn =
                    this.Lines[this.valuePosition.Line].ValueXToRenderX(this.valuePosition.Char);
                if (this.valuePosition.Char < this.Lines[this.valuePosition.Line].Value.Length)
                {
                    overshoot =
                        this.Lines[this.valuePosition.Line].Value[this.valuePosition.Char]
                        .GetEastAsianWidth(this.setting.AmbiguousCharIsFullWidth) - 1;
                }
            }

            if (this.valuePosition.Line < this.offset.Line)
            {
                this.offset.Line = this.valuePosition.Line;
            }
            if (this.valuePosition.Line >= this.offset.Line + editArea.Height)
            {
                this.offset.Line = this.valuePosition.Line - editArea.Height + 1;
            }
            if (this.renderPositionColumn < this.offset.Column)
            {
                this.offset.Column = this.renderPositionColumn;
            }
            if (this.renderPositionColumn >= this.offset.Column + editArea.Width)
            {
                this.offset.Column = this.renderPositionColumn - editArea.Width + 1 + overshoot;
            }
        }

        public CursorPosition UpdateCursor(Size editArea)
        {
            this.UpdateOffset(editArea);
            return this.Cursor;
        }
    }
}
