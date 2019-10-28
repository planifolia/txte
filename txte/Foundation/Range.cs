using System;
using System.Diagnostics.CodeAnalysis;

namespace txte
{
    readonly struct Range : IComparable<Range>
    {
        public Range(int begin, int end) => 
            (this.Begin, this.End) = (begin, end);

        readonly public int Begin;
        readonly public int End;
        public int Length => this.End - this.Begin;

        public override string ToString() => $"[{this.Begin}, {this.End})";
        public int CompareTo([AllowNull] Range other)
        {
            var begins = this.Begin.CompareTo(other.Begin);
            return (begins == 0) ? begins : this.End.CompareTo(other.End);
        }
    }
}