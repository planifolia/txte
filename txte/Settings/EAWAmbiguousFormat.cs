using System.Collections.Generic;

namespace txte.Settings
{
    class EAWAmbiguousFormat : IChoice
    {
        public static readonly EAWAmbiguousFormat Default = 
            new EAWAmbiguousFormat("Default", null, 'd');
        public static readonly EAWAmbiguousFormat HalfWidth = 
            new EAWAmbiguousFormat("Half-Width", false, 'h');
        public static readonly EAWAmbiguousFormat FullWidth = 
            new EAWAmbiguousFormat("Full-Width", true, 'f');

        public static readonly IReadOnlyList<EAWAmbiguousFormat> All =
            new EAWAmbiguousFormat[] { Default, HalfWidth, FullWidth };

        public string Name { get; }
        public bool? IsFullWidth { get; }

        public char Shortcut { get; }

        public override string ToString() => this.Name;

        private EAWAmbiguousFormat(string name, bool? isFullWidth, char shortcut)
        {
            this.Name = name;
            this.IsFullWidth = isFullWidth;
            this.Shortcut = shortcut;
        }
    }
}