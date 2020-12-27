using System.Collections.Generic;

namespace txte.Settings
{
    class EAWAmbiguousFormat : IChoice
    {
        public static readonly EAWAmbiguousFormat HalfWidth = new EAWAmbiguousFormat("Half-Width", false, 'h');
        public static readonly EAWAmbiguousFormat FullWidth = new EAWAmbiguousFormat("Full-Width", true, 'f');

        public static readonly IReadOnlyList<EAWAmbiguousFormat> All =
            new EAWAmbiguousFormat[] { HalfWidth, FullWidth };

        public static EAWAmbiguousFormat FromSetting(bool ambiguousIsFullWidth) =>
            ambiguousIsFullWidth ? EAWAmbiguousFormat.FullWidth : EAWAmbiguousFormat.HalfWidth;

        public string Name { get; }
        public bool IsFullWidthAmbiguous { get; }

        public char Shortcut { get; }

        public override string ToString() => this.Name;

        private EAWAmbiguousFormat(string name, bool isFullWidthAmbiguous, char shortcut)
        {
            this.Name = name;
            this.IsFullWidthAmbiguous = isFullWidthAmbiguous;
            this.Shortcut = shortcut;
        }
    }
}