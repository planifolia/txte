
using System;

namespace txte
{
    struct ValuePosition
    {
        public ValuePosition(int line, int @char)
        {
            this.Line = line;
            this.Char = @char;
        }

        public int Line;
        public int Char;

        public void Deconstruct(out int line, out int @char)
        {
            line = this.Line;
            @char = this.Char;
        }

        public override string ToString() => $"Ln: {this.Line}, Char: {this.Char}";

        public static explicit operator Point(ValuePosition position) => new Point(position.Char, position.Line);
    }

    struct RenderPosition
    {
        public RenderPosition(int line, int column)
        {
            this.Line = line;
            this.Column = @column;
        }

        public int Line;
        public int Column;

        public void Deconstruct(out int line, out int column)
        {
            line = this.Line;
            column = this.Column;
        }

        public override string ToString() => $"Ln: {this.Line}, Col: {this.Column}";

        public static explicit operator Point(RenderPosition position) => new Point(position.Column, position.Line);
    }
}