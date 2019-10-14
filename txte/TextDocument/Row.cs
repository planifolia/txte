using System.Collections.Immutable;
using System.Text;
using txte.Settings;
using txte.Text;

namespace txte.TextDocument
{
    class StringLayser
    {
        public StringLayser(string value)
        {
            this.value = value;
            this.IsUpdated = true;
        }

        public bool IsUpdated;
        public string Value
        {
            get => this.value;
            set => (this.value, this.IsUpdated) = (value, true);
        }
        string value;

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

    class Row
    {
        public Row(Setting setting, string value) : this(setting, value, false) {}
        public Row(Setting setting, string value, bool asNewLine)
        {
            this.setting = setting;
            this.valueLayer = new StringLayser(value);
            this.IsModified = asNewLine;
            this.renderLayer = new StringLayser("");
        }

        public string Value => this.valueLayer.Value;

        public ColorLayer SyntaxColorLayer { get; set; } = default!;

        public ColoredString Render
        {
            get
            {
                if (this.valueLayer.IsUpdated) {this.UpdateRender(); }
                if (this.renderLayer.IsUpdated)
                {
                    this.syntaxCache = new ColoredString(this.setting, this.renderLayer.Value);
                    this.renderLayer.IsUpdated = false;
                }
                return this.syntaxCache;
            }
        }
        
        public bool IsModified { get; private set; }

        readonly Setting setting;

        StringLayser valueLayer;
        StringLayser renderLayer;
        ColoredString syntaxCache;

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

        public void Establish()
        {
            this.IsModified = false;
        }

        void UpdateRender()
        {
            var tabSize = this.setting.TabSize;
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
            this.renderLayer.Value = renderBuilder.ToString();
            this.valueLayer.IsUpdated = false;
        }

        public int ValueXToRenderX(int valueX)
        {
            int tabSize = this.setting.TabSize;
            bool ambiguousSetting = this.setting.IsFullWidthAmbiguous;
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
        
        public int RenderXToValueX(int renderX)
        {   
            int tabSize = this.setting.TabSize;
            bool ambiguousSetting = this.setting.IsFullWidthAmbiguous;
            int renderXChecked = 0;
            for (int valueX = 0; valueX < this.Value.Length; valueX++)
            {
                if (this.Value[valueX] == '\t')
                {
                    renderXChecked += (tabSize - 1) - (renderXChecked % tabSize);
                }
                renderXChecked += this.Value[valueX].GetEastAsianWidth(ambiguousSetting);
                
                if (renderXChecked > renderX) return valueX;
            }
            return this.Value.Length;
        }
    }
}