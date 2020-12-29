using txte.ConsoleInterface;

namespace txte.Settings
{
    class Setting
    {
        public bool AmbiguousCharIsFullWidth => ambiguousCharIsFullWidth;
        public EAWAmbiguousFormat AmbiguousFormat => ambiguousFormat;
        public int TabSize { get; set; } = 4;

        bool ambiguousCharIsFullWidth = false;
        EAWAmbiguousFormat ambiguousFormat = EAWAmbiguousFormat.Default;

        public void SetAmbiguousCharWidthFormat(IConsoleOutput console, EAWAmbiguousFormat format)
        {
            this.ambiguousFormat = format;
            this.ambiguousCharIsFullWidth = format.IsFullWidth ?? console.ShowsAmbiguousCharAsFullWidth;
        }
    }
}