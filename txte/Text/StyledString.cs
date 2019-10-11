namespace txte.Text
{
    class StyledString
    {
        public static readonly StyledString LeftFragment = new StyledString("]", ColorSet.Fragment);
        public static readonly StyledString RightFragment = new StyledString("[", ColorSet.Fragment);
        public static readonly StyledString Fragment = new StyledString("*", ColorSet.Fragment);

        public StyledString(string value) : this(value, ColorSet.Default) { }

        public StyledString(string value, ColorSet colorSet)
        {
            this.Value = value;
            this.ColorSet = colorSet;
        }

        public readonly string Value;
        public readonly ColorSet ColorSet;
    }
}