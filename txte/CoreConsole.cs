using System;
using System.Drawing;

namespace txte
{
    class CoreConsole : IConsole
    {

        public CoreConsole()
        {
            this.CheckConsoleRequirements();
            this.SetupConsoleMode();
            this.InitializeColorSettings();
        }

        public int Height => Console.WindowHeight;
        public int Width => Console.BufferWidth - 1;
        public Size Size => new Size(this.Width, this.Height);

        OriginatedColor defaultForegroundColor;
        OriginatedColor defaultBackgroundColor;

        OriginatedColor ForegroundColor
        {
            get { return this._foregroundColor; }
            set
            {
                this._foregroundColor = value;
                Console.ForegroundColor = this._foregroundColor.GetColorFor(ColorTarget.Foreground);
            }
        }
        OriginatedColor _foregroundColor;

        OriginatedColor BackgroundColor
        {
            get { return this._backgroundColor; }
            set
            {
                this._backgroundColor = value;
                Console.BackgroundColor = this._backgroundColor.GetColorFor(ColorTarget.Background);
            }
        }
        OriginatedColor _backgroundColor;
        

        public ConsoleKeyInfo ReadKey() => Console.ReadKey(true);

        public void Clear() => Console.Clear();

        public void ResetColor()
        {
            Console.ResetColor();
            this.ForegroundColor = this.defaultForegroundColor;
            this.BackgroundColor = this.defaultBackgroundColor;
        }

        public void RefreshScreen(
            EditorSetting setting,
            Action<IScreen> drawEditorRows,
            Action<IScreen> drawSatusBar,
            Action<IScreen> drawMessageBar,
            Point cursor)
        {
            var screen = new Screen(this, setting.IsFullWidthAmbiguous);

            Console.CursorVisible = false;
            Console.SetCursorPosition(0, 0);
            drawEditorRows(screen);
            this.ReverseColor();
            drawSatusBar(screen);
            this.ResetColor();
            drawMessageBar(screen);
            Console.SetCursorPosition(cursor.X, cursor.Y);
            Console.CursorVisible = true;
        }

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
                new OriginatedColor(ColorTarget.Foreground, Console.ForegroundColor);
            this.BackgroundColor =
            this.defaultBackgroundColor 
                = new OriginatedColor(ColorTarget.Background, Console.BackgroundColor);
        }

        void ReverseColor()
        {
            (this.ForegroundColor, this.BackgroundColor) =
                (this.BackgroundColor, this.ForegroundColor);
        }

        class Screen : IScreen
        {
            public Screen(CoreConsole console, bool ambuguosIsfullWidth)
            {
                this.console = console;
                this.size = console.Size;
                this.rowCount = 0;
                this.ambuguosIsfullWidth = ambuguosIsfullWidth;
            }

            readonly CoreConsole console;
            readonly Size size;
            readonly bool ambuguosIsfullWidth;

            int rowCount;

            public void AppendRow(string value)
            {
                Console.Write(value + new string(' ', this.size.Width - value.GetConsoleLength(this.ambuguosIsfullWidth)));
                if (this.rowCount < this.size.Height - 1)
                {
                    Console.WriteLine();
                }
                this.rowCount++;
            }

            public void AppendFragmentedRow(string value, bool startIsFragmented, bool endIsFragmented)
            {
                if (startIsFragmented) { this.AppendFragmentChar(']'); }
                Console.Write(value);
                if (endIsFragmented) { this.AppendFragmentChar('['); }
                Console.Write(new string(
                    ' ',
                    this.size.Width
                    - value.GetConsoleLength(this.ambuguosIsfullWidth)
                    - (startIsFragmented ? 1 : 0)
                    - (endIsFragmented ? 1 : 0)
                ));
                if (this.rowCount < this.size.Height - 1)
                {
                    Console.WriteLine();
                }
                this.rowCount++;
            }

            void AppendFragmentChar(char fragment)
            {
                this.console.ForegroundColor = new OriginatedColor(ConsoleColor.Blue);
                this.console.BackgroundColor = this.console.defaultForegroundColor;
                Console.Write(fragment);
                this.console.ResetColor();
            }

            public void AppendOuterRow(string value)
            {
                this.console.ForegroundColor = new OriginatedColor(ConsoleColor.DarkCyan);
                this.console.BackgroundColor = this.console.defaultBackgroundColor;
                this.AppendRow(value);
                this.console.ResetColor();
            }
        }

        enum ColorTarget
        {
            User,
            Foreground,
            Background,
        }
        struct OriginatedColor
        {
            public OriginatedColor(ConsoleColor color) : this(ColorTarget.User, color) { }

            public OriginatedColor(ColorTarget origin, ConsoleColor color)
            {
                this.Origin = origin;
                this.Color = color;
            }

            public readonly ColorTarget Origin;
            public readonly ConsoleColor Color; 

            public ConsoleColor GetColorFor(ColorTarget target)
            {
                // Treat the default color (-1) when reverse onsole color.

                // If this.Color is specified, return that color.
                if ((int)this.Color != -1) { return this.Color; }

                // Return the color assumed that color scheme is dark if the console color is reversed. 
                if (target == ColorTarget.Foreground  && this.Origin == ColorTarget.Background)
                {
                    return ConsoleColor.Black;
                }
                else if (target == ColorTarget.Background  && this.Origin == ColorTarget.Foreground)
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