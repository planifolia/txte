using System;

namespace txte.Text
{
    enum ColorType
    {
        User,
        Foreground,
        Background,
    }

    struct ThemeColor
    {
        public static readonly ThemeColor Foreground = new ThemeColor(ColorType.Foreground);
        public static readonly ThemeColor Background = new ThemeColor(ColorType.Background);

        public ThemeColor(ColorType type) : this(type, default) { }

        public ThemeColor(ColorType type, ConsoleColor color)
        {
            this.Type = type;
            this.Color = color;
        }

        public readonly ColorType Type;
        public readonly ConsoleColor Color;

        public static implicit operator ThemeColor(ConsoleColor color) =>
            new ThemeColor(ColorType.User, color);

        public override string ToString() =>
            this.Type switch
            {
                ColorType.User => this.Color.ToString(),
                ColorType.Foreground => "Foreground",
                ColorType.Background => "Background",
                _ => "?"
            };
    }

    struct ColorSet
    {
        public static readonly ColorSet Default =
            new ColorSet(ThemeColor.Foreground, ThemeColor.Background);
        public static readonly ColorSet Reversed =
            new ColorSet(ThemeColor.Background, ThemeColor.Foreground);
        public static readonly ColorSet Fragment =
            new ColorSet(ConsoleColor.Blue, ThemeColor.Foreground);

        public static readonly ColorSet LineNumber =
            new ColorSet(ConsoleColor.DarkGray, ThemeColor.Background);

        public static readonly ColorSet Found =
            new ColorSet(ConsoleColor.Black, ConsoleColor.Gray);
        public static readonly ColorSet CurrentFound =
            new ColorSet(ConsoleColor.Black, ConsoleColor.Yellow);

        public static readonly ColorSet SyntaxNumeric =
            new ColorSet(ConsoleColor.Green, ThemeColor.Background);

        public static readonly ColorSet SyntaxKeyword =
            new ColorSet(ConsoleColor.Blue, ThemeColor.Background);

        public static readonly ColorSet SyntaxIdentifier =
            new ColorSet(ConsoleColor.Cyan, ThemeColor.Background);

        public static readonly ColorSet SyntaxString =
            new ColorSet(ConsoleColor.DarkYellow, ThemeColor.Background);

        public static readonly ColorSet SyntaxStringKeyword =
            new ColorSet(ConsoleColor.Green, ThemeColor.Background);

        public static readonly ColorSet SyntaxError =
            new ColorSet(ConsoleColor.Red, ThemeColor.Background);

        public static readonly ColorSet OutOfBounds =
            new ColorSet(ConsoleColor.DarkCyan, ThemeColor.Background);
        public static readonly ColorSet SystemMessage =
            new ColorSet(ConsoleColor.White, ConsoleColor.DarkCyan);

        public static readonly ColorSet KeyExpression =
            new ColorSet(ConsoleColor.Gray, ConsoleColor.DarkGray);

        public ColorSet(ThemeColor foreground, ThemeColor background)
        {
            this.Foreground = foreground;
            this.Background = background;
        }

        public readonly ThemeColor Foreground;
        public readonly ThemeColor Background;

        public override string ToString() => $"<{this.Foreground}:{this.Background}>";
    }
}
