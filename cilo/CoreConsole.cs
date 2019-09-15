#if !ESCAPE_SEQUENCE_ENHANCED

using System;
using System.Drawing;

namespace cilo
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
        public int EditorHeight => this.Height - 2 /* status + message */;
        public int Width => Console.BufferWidth - 1;
        public Size Size => new Size(this.Width, this.Height);

        ConsoleColor defaultColor;
        ConsoleColor defauldBackgroundColor;

        public ConsoleKeyInfo ReadKey() => Console.ReadKey(true);

        public void Clear() => Console.Clear();

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
            this.ResetColor();
            this.defaultColor = Console.ForegroundColor;
            this.defauldBackgroundColor = Console.BackgroundColor;
        }

        void ResetColor() => Console.ResetColor();
        void ReverseColor() =>
            (Console.ForegroundColor, Console.BackgroundColor) =
            (Console.BackgroundColor, Console.ForegroundColor);

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
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.BackgroundColor = this.console.defaultColor;
                Console.Write(fragment);
                Console.ResetColor();
            }

            public void AppendOuterRow(string value)
            {
                Console.ForegroundColor = ConsoleColor.DarkCyan;
                this.AppendRow(value);
                Console.ResetColor();
            }
        }
    }
}
#endif
