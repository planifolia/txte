using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using txte.Settings;
using txte.Text;

namespace txte.TextDocument
{
    class StringLayer
    {
        public StringLayer(string value)
        {
            this.value = value;
            this.IsUpdated = true;
        }

        public bool IsUpdated { get; private set; }

        public string Value
        {
            get => this.value;
            set 
            {
                this.value = value;
                this.IsUpdated = true;
            }
        }

        string value;

        public void FetchUpdate(Action<string> action)
        {
            action(this.value);
            this.IsUpdated = false;
        }
    }
    class FigureStringLayer
    {
        public FigureStringLayer(Setting setting, StringLayer source)
        {
            this.setting = setting;
            this.source = source;
            this.IsUpdated = true;

            this.Update();
        }

        public bool IsUpdated;
        public string Value
        {
            get
            {
                if (this.source.IsUpdated)
                {
                    this.Update();
                }
                return this.value;
            }
        }

        public IReadOnlyList<int> Boundaries
        {
            get
            {
                if (this.source.IsUpdated)
                {
                    this.Update();
                }
                return this.boundaries;
            }
        }

        readonly Setting setting;
        readonly StringLayer source;

        string value = default!;
        int[] boundaries = default!;

        void Update()
        {
            this.source.FetchUpdate(this.Update);
        }

        void Update(string source)
        {
            this.boundaries = new int[source.Length + 1];
            var tabSize = this.setting.TabSize;
            var ambiguousSetting = this.setting.AmbiguousCharIsFullWidth;
            var figureBuilder = new StringBuilder();
            int figurePosition = 0;
            for (int iSource = 0; iSource < source.Length; iSource++)
            {
                var currentChar = source[iSource];
                this.boundaries[iSource] = figurePosition;
                if (currentChar == '\t')
                {
                    figureBuilder.Append(' ');
                    figurePosition++;
                    while (figureBuilder.Length % tabSize != 0)
                    {
                        figureBuilder.Append(' ');
                        figurePosition++;
                    }
                }
                else
                {
                    figureBuilder.Append(currentChar);
                    figurePosition += currentChar.GetEastAsianWidth(ambiguousSetting);
                }
            }
            this.boundaries[source.Length] = figurePosition;
            this.value = figureBuilder.ToString();
            this.IsUpdated = true;
        }
    }
    class ColoredStringLayser
    {
        public ColoredStringLayser(ColoredString value)
        {
            this.Value = value;
            this.IsUpdated = true;
        }

        public bool IsUpdated;
        public ColoredString Value;
    }
    class ColorLayer
    {
        public ColorLayer(bool isShown, ImmutableSortedSet<ColorSpan> colors)
        {
            this.IsShown = isShown;
            this.Colors = colors;
            this.IsUpdated = true;
        }

        public bool IsUpdated;
        public bool IsShown;
        public ImmutableSortedSet<ColorSpan> Colors;
    }

    class Line
    {
        public class List : List<Line>
        {
            public new Line this[int index]
            {
                get {
                    var line = base[index];
                    line.Index = index;
                    return line;
                }
                set => base[index] = value;
            }
        }

        public Line(ISyntaxable syntaxable, Setting setting, string value) : this(syntaxable, setting, value, false) { }
        public Line(ISyntaxable syntaxable, Setting setting, string value, bool isNewLine)
        {
            // Index is set by Line.List
            this.Index = -1;

            this.setting = setting;
            this.syntaxable = syntaxable;
            this.IsModified = isNewLine;
            this.valueLayer = new StringLayer(value);
            this.figureLayer = new FigureStringLayer(setting, this.valueLayer);
            this.syntaxCache = null!;
        }

        public int Index { get; private set; }
        public string Value => this.valueLayer.Value;
        public IReadOnlyList<int> Boundaries => this.figureLayer.Boundaries;

        public ColorLayer SyntaxColorLayer { get; set; } = default!;

        public ColoredString Render
        {
            get
            {
                if (this.syntaxCache is null || this.figureLayer.IsUpdated)
                {
                    this.syntaxCache = this.syntaxable.LanguageHighLighter.HighlightSingleLine(this.setting, this.figureLayer.Value);
                    this.figureLayer.IsUpdated = false;
                }
                return this.syntaxCache!;
            }
        }

        public bool IsModified { get; private set; }

        readonly Setting setting;
        readonly ISyntaxable syntaxable;

        readonly StringLayer valueLayer;
        readonly FigureStringLayer figureLayer;
        ColoredString? syntaxCache;

        public void InsertChar(char c, int at)
        {
            this.valueLayer.Value =
                this.Value.Substring(0, at) + c + this.Value.Substring(at);
            this.IsModified = true;
        }

        public void BackSpace(int at)
        {
            // requires: (0 < at && at <= this.Value.Length)
            this.valueLayer.Value =
                this.Value.Substring(0, at - 1) + this.Value.Substring(at);
            this.IsModified = true;
        }

        public void Establish(Action<string> action)
        {
            action(this.Value);
            this.IsModified = false;
        }

        public void ClearSyntacCache() => this.syntaxCache = null;


        public int ValueXToRenderX(int valueX)
        {
            return this.figureLayer.Boundaries[valueX];
        }

        public int RenderXToValueX(int renderX)
        {
            var boundaries = this.figureLayer.Boundaries;
            for (int iValueX = 0; iValueX < this.Value.Length; iValueX++)
            {
                if (boundaries[iValueX] > renderX) return iValueX;
            }
            return this.Value.Length;
        }
    }
}