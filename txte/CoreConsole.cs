using System;
using System.Collections.Generic;
using System.Drawing;

namespace txte
{
    class CoreConsole : ConsoleBase
    {

        public CoreConsole()
        {
            this.CheckConsoleRequirements();
            this.SetupConsoleMode();
            this.InitializeColorSettings();
        }

        public override int Height => Console.WindowHeight;
        public override int Width => Console.BufferWidth - 1;
        public override Size Size => new Size(this.Width, this.Height);

        OriginatedColor defaultForegroundColor;
        OriginatedColor defaultBackgroundColor;

        OriginatedColor ForegroundColor
        {
            get { return this._foregroundColor; }
            set
            {
                this._foregroundColor = value;
                Console.ForegroundColor = this._foregroundColor.GetColorFor(ColorType.Foreground);
            }
        }
        OriginatedColor _foregroundColor;

        OriginatedColor BackgroundColor
        {
            get { return this._backgroundColor; }
            set
            {
                this._backgroundColor = value;
                Console.BackgroundColor = this._backgroundColor.GetColorFor(ColorType.Background);
            }
        }
        OriginatedColor _backgroundColor;

        public override void RefreshScreen(
            int from,
            EditorSetting setting,
            Action<IScreen, int> drawEditorRows,
            Action<IScreen> drawSatusBar,
            Action<IScreen> drawMessageBar,
            Point cursor)
        {
            var screen = new Screen(this, from, setting.IsFullWidthAmbiguous);

            Console.CursorVisible = false;
            Console.SetCursorPosition(0, from);
            drawEditorRows(screen, from);
            this.ReverseColor();
            drawSatusBar(screen);
            this.ResetColor();
            drawMessageBar(screen);
            Console.SetCursorPosition(cursor.X, cursor.Y);
            Console.CursorVisible = true;
        }

        public override void Clear() => Console.Clear();

        protected override void ResetColor()
        {
            Console.ResetColor();
            this.ForegroundColor = this.defaultForegroundColor;
            this.BackgroundColor = this.defaultBackgroundColor;
        }

        protected override ConsoleKeyInfo ReadKey() => Console.ReadKey(true);

        void CheckConsoleRequirements()
        {
            if (Console.IsInputRedirected) { throw new EditorException("Console input is redirected."); }
            if (Console.IsOutputRedirected) { throw new EditorException("Console output is redirected."); }
        }

        void SetupConsoleMode()
        {
            // To treat Ctrl+C with ReadKey().
            Console.TreatControlCAsInput = true;
        }

        void InitializeColorSettings()
        {
            Console.ResetColor();
            this.ForegroundColor =
            this.defaultForegroundColor =
                new OriginatedColor(ColorType.Foreground, Console.ForegroundColor);
            this.BackgroundColor =
            this.defaultBackgroundColor 
                = new OriginatedColor(ColorType.Background, Console.BackgroundColor);
        }

        void ReverseColor()
        {
            (this.ForegroundColor, this.BackgroundColor) =
                (this.BackgroundColor, this.ForegroundColor);
        }


        class Screen : IScreen
        {
            public Screen(CoreConsole console, int from, bool ambuguosIsfullWidth)
            {
                this.console = console;
                this.size = console.Size;
                this.rowCount = from;
                this.ambuguosIsfullWidth = ambuguosIsfullWidth;
            }

            readonly CoreConsole console;
            readonly Size size;
            readonly bool ambuguosIsfullWidth;

            int rowCount;

            public void AppendRow(string value)
            {
                int written = 0;
                Console.Write(value);
                written += value.GetConsoleLength(this.ambuguosIsfullWidth);
                var padding = this.size.Width - written;
                if (padding > 0)
                {
                    Console.Write(new string(' ', padding));
                }
                if (this.rowCount < this.size.Height - 1)
                {
                    Console.WriteLine();
                }
                this.rowCount++;
            }

            public void AppendRow(IEnumerable<StyledString> spans)
            {
                int written = 0;
                foreach (var span in spans)
                {
                    this.AppendStyledString(span);
                    written += span.Value.GetConsoleLength(this.ambuguosIsfullWidth);
                }
                var padding = this.size.Width - written;
                if (padding > 0)
                {
                    Console.Write(new string(' ', padding));
                }
                if (this.rowCount < this.size.Height - 1)
                {
                    Console.WriteLine();
                }
                this.rowCount++;
            }

            public void AppendOuterRow(string value)
            {
                var foreground = this.console.ForegroundColor;
                var background = this.console.BackgroundColor;
                try
                {
                    this.console.ForegroundColor = new OriginatedColor(ConsoleColor.DarkCyan);
                    this.console.BackgroundColor = this.console.defaultBackgroundColor;
                    this.AppendRow(value);
                }
                finally
                {
                    this.console.ForegroundColor = foreground;
                    this.console.BackgroundColor = background;
                }
            }

            void AppendStyledString(StyledString span)
            {
                var foreground = this.console.ForegroundColor;
                var background = this.console.BackgroundColor;
                try
                {
                    this.console.ForegroundColor = this.GetConsoleColor(span.ColorSet.Foreground);
                    this.console.BackgroundColor = this.GetConsoleColor(span.ColorSet.Background);
                    Console.Write(span.Value);
                }
                finally
                {
                    this.console.ForegroundColor = foreground;
                    this.console.BackgroundColor = background;
                }
            }

            void AppendFragmentChar(char fragment)
            {
                var foreground = this.console.ForegroundColor;
                var background = this.console.BackgroundColor;
                try
                {
                    this.console.ForegroundColor = new OriginatedColor(ConsoleColor.Blue);
                    this.console.BackgroundColor = this.console.defaultForegroundColor;
                    Console.Write(fragment);
                }
                finally
                {
                    this.console.ForegroundColor = foreground;
                    this.console.BackgroundColor = background;
                }
            }

            OriginatedColor GetConsoleColor(ThemeColor color)
            {
                switch (color.Type)
                {
                    case ColorType.Foreground:
                        return this.console.defaultForegroundColor;
                    case ColorType.Background:
                        return this.console.defaultBackgroundColor;
                    case ColorType.User:
                    default:
                        return new OriginatedColor(color.Color);
                }
            }

        }

        struct OriginatedColor
        {
            public OriginatedColor(ConsoleColor color) : this(ColorType.User, color) { }

            public OriginatedColor(ColorType origin, ConsoleColor color)
            {
                this.Origin = origin;
                this.Color = color;
            }

            public readonly ColorType Origin;
            public readonly ConsoleColor Color; 

            public ConsoleColor GetColorFor(ColorType target)
            {
                // Treat the default color (-1) when reverse onsole color.

                // If this.Color is specified, return that color.
                if ((int)this.Color != -1) { return this.Color; }

                // Return the color assumed that color scheme is dark if the console color is reversed. 
                if (target == ColorType.Foreground  && this.Origin == ColorType.Background)
                {
                    return ConsoleColor.Black;
                }
                else if (target == ColorType.Background  && this.Origin == ColorType.Foreground)
                {
                    return ConsoleColor.Gray;
                }
                else
                {
                    return this.Color;
                }
            }
        }
    }
}