namespace txte
{
    class StyledString
    {
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