namespace txte
{
    readonly struct Range
    {
        public Range(int begin, int end) => 
            (this.Begin, this.End) = (begin, end);

        readonly public int Begin;
        readonly public int End;
        public int Length => this.End - this.Begin;
    }
}