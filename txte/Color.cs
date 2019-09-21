using System;

namespace txte
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
    }
    
    struct ColorSet
    {
        public static readonly ColorSet Default =
            new ColorSet(ThemeColor.Foreground, ThemeColor.Background);
        public static readonly ColorSet OutOfBounds =
            new ColorSet(ConsoleColor.DarkCyan, ThemeColor.Background);
        public static readonly ColorSet Fragment = 
            new ColorSet(ConsoleColor.Blue, ThemeColor.Foreground);

        public ColorSet(ThemeColor foreground, ThemeColor background)
        {
            this.Foreground = foreground;
            this.Background = background;
        }

        public readonly ThemeColor Foreground;
        public readonly ThemeColor Background;
    }
}
