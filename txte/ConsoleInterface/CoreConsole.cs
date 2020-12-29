using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using txte.Settings;
using txte.Text;

namespace txte.ConsoleInterface
{
    class CoreConsole : IConsole
    {

        public CoreConsole(int timeoutMillisec)
        {
            this.CheckConsoleRequirements();
            this.CheckSpec();
            this.SetupConsoleMode();
            this.InitializeColorSettings();
            this.keyReader = new CoreConsoleKeyReader(timeoutMillisec);
        }

        public int Height => Console.WindowHeight;
        public int Width => Console.BufferWidth;
        public Size Size => new Size(this.Width, this.Height);

        public bool ShowsAmbiguousCharAsFullWidth { get; private set; }

        public bool KeyAvailable => Console.KeyAvailable;

        readonly CoreConsoleKeyReader keyReader;

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


        public IAsyncEnumerable<InputEventArgs> ReadKeysOrTimeoutAsync()
            => this.keyReader.ReadKeysOrTimeoutAsync();

        public void RefreshScreen(
            int from,
            Setting setting,
            Action<IScreen, int> renderScreen,
            Point cursor)
        {
            var screen = new Screen(this, from, setting.AmbiguousCharIsFullWidth);

            Console.CursorVisible = false;
            Console.SetCursorPosition(0, from);
            renderScreen(screen, from);
            Console.SetCursorPosition(cursor.X, cursor.Y);
            Console.CursorVisible = true;
        }

        public void Clear() => Console.Clear();

        void ResetColor()
        {
            Console.ResetColor();
            this.ForegroundColor = this.defaultForegroundColor;
            this.BackgroundColor = this.defaultBackgroundColor;
        }


        void CheckConsoleRequirements()
        {
            if (Console.IsInputRedirected) { throw new EditorException("Console input is redirected."); }
            if (Console.IsOutputRedirected) { throw new EditorException("Console output is redirected."); }
        }

        void CheckSpec()
        {
            this.ShowsAmbiguousCharAsFullWidth = this.EstimateAmbiguousCharWidth() == 2;
        }

        int EstimateAmbiguousCharWidth()
        {
            var testChars = new [] {'Å', 'α', 'Я', '○'};
            return
                testChars.Select(x =>
                {
                    var start = Console.CursorLeft;
                    Console.Write(x);
                    var count = Console.CursorLeft - start;
                    Console.Write(new string('\b', count));
                    return count;
                })
                .Max();
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

            public int Width => this.console.Width;

            readonly CoreConsole console;
            readonly Size size;
            readonly bool ambuguosIsfullWidth;

            int rowCount;

            public void AppendRow(string value)
            {
                int written = 0;
                Console.Write(value);
                written += value.GetRenderLength(this.ambuguosIsfullWidth);
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
                    written += span.Value.GetRenderLength(this.ambuguosIsfullWidth);
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
                if (target == ColorType.Foreground && this.Origin == ColorType.Background)
                {
                    return ConsoleColor.Black;
                }
                else if (target == ColorType.Background && this.Origin == ColorType.Foreground)
                {
                    return ConsoleColor.Gray;
                }
                else
                {
                    return this.Color;
                }
            }
        }

        #region IDisposable Support
        bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!this.disposedValue)
            {
                if (disposing) { }

                this.ResetColor();
                this.Clear();

                this.keyReader.Dispose();

                this.disposedValue = true;
            }
        }

        ~CoreConsole()
        {
            this.Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}