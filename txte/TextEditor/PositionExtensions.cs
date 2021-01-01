namespace txte.TextEditor
{
    static class PositionExtensions
    {
        public static CursorPosition? OffsetPrompt(this in CursorPosition? value, int lines) =>
            (value is {} shown) ? new (shown.Line + lines, shown.Column) : null;
    }

}