using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace cilo
{
    static class SpecialKeys
    {
        public static readonly ConsoleKeyInfo CtrlBreak = 
            new ConsoleKeyInfo((char)0x00, ConsoleKey.Pause, false, false, true);
        public static readonly ConsoleKeyInfo CtrlC = 
            new ConsoleKeyInfo((char)0x03, ConsoleKey.C, false, false, true);
        public static readonly ConsoleKeyInfo CtrlQ = 
            new ConsoleKeyInfo((char)0x11, ConsoleKey.Q, false, false, true);
    }


    enum EditorProcessingResults
    {
        Running,
        Quit,
        Queued,
    }

    class TemporaryMessage
    {
        public TemporaryMessage(string value, DateTime time)
        {
            this.Value = value;
            this.Time = time;
        }

        public string Value { get; }
        public DateTime Time { get; }
    }

    class EditorSetting
    {
        public bool IsFullWidthAmbiguous { get; set; } = true;
        public int TabSize { get; set; } = 4;
    }

    class Editor
    {
        public static string Version = "0.0.1";

        public Editor(IConsole console, EditorSetting setting)
        {
            this.eventQueue = new EventQueue(this.ProcessLoop);
            this.console = console;
            this.setting = setting;
        }

        readonly EventQueue eventQueue;
        readonly IConsole console;

        Document document;
        TemporaryMessage statusMessage;
        EditorSetting setting;

        public void SetDocument(Document document)
        {
            this.document = document;
        }

        public void SetStatusMessage(string value)
        {
            this.statusMessage = new TemporaryMessage(value, DateTime.Now);
        }

        public void Run()
        {
            this.RefreshScreen();
            using (var cancellationController = new CancellationTokenSource())
            {
                var userInput = CreateEventTask(
                    EventType.UserAction,
                    this.console.ReadKey,
                    cancellationController.Token
                );
                var timeout = CreateEventTask(
                    EventType.Timeout,
                    this.GenerateTimeout,
                    cancellationController.Token
                );

                Task.WaitAny(userInput, timeout);
                cancellationController.Cancel();
            }
            this.console.Clear();
        }

        ConsoleKeyInfo GenerateTimeout()
        {
            Thread.Sleep(1000);
            return default;
        }

        Task CreateEventTask(EventType eventType, Func<ConsoleKeyInfo> inputGenerator, CancellationToken cancellationToken) => Task.Run(() =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var keyInfo = inputGenerator();
                if (cancellationToken.IsCancellationRequested) { return; }
                switch (this.eventQueue.PostEvent(new InputEventArgs(eventType, keyInfo)))
                {
                    case EditorProcessingResults.Quit:
                        return;
                    default:
                        continue;
                }
            }
        }, cancellationToken);

        EditorProcessingResults ProcessLoop(InputEventArgs inputEvent)
        {
            var result = this.ProcessKeyPress(inputEvent.ConsoleKeyInfo);
            this.RefreshScreen();
            return result;

        }

        void RefreshScreen()
        {
            this.document.UpdateOffset(this.console, this.setting);
            this.console.RefreshScreen(
                this.setting,
                this.DrawEditorRows,
                this.DrawSatausBar,
                this.DrawMessageBar,
                this.document.Cursor);
        }

        void DrawEditorRows(IScreen screen)
        {
            bool ambiguousSetting = this.setting.IsFullWidthAmbiguous;
            for (int y = 0; y < this.console.EditorHeight; y++)
            {
                var docRow = y + this.document.Offset.Y;
                if (docRow < this.document.Rows.Count)
                {
                    this.DrawDocumentRow(screen, ambiguousSetting, docRow);
                }
                else
                {
                    this.DrawOutofBounds(screen, y);
                }
            }
        }

        void DrawDocumentRow(IScreen screen, bool ambiguousSetting, int docRow)
        {
            var docText = this.document.Rows[docRow].Render;
            var docLength =
                Math.Clamp(
                    docText.GetConsoleLength(ambiguousSetting) - this.document.Offset.X,
                    0, this.console.Width
                );
            if (docLength > 0)
            {
                var (render, startIsFragmented, endIsFragmented) =
                    docText.SubConsoleString(this.document.Offset.X, docLength, ambiguousSetting);

                screen.AppendFragmentedRow(render, startIsFragmented, endIsFragmented);
            }
            else
            {
                screen.AppendRow("");
            }
        }

        void DrawOutofBounds(IScreen screen, int y)
        {
            if (this.document.Rows.Count == 0 && y == this.console.EditorHeight / 3)
            {
                var welcome = $"Cilo editor -- version {Version}";
                var welcomeLength = Math.Min(welcome.Length, this.console.Width);
                var padding = (this.console.Width - welcomeLength) / 2;
                var rowBuffer = new StringBuilder();
                if (padding > 0)
                {
                    rowBuffer.Append("~");
                }
                for (int i = 1; i < padding; i++)
                {
                    rowBuffer.Append(" ");
                }
                rowBuffer.Append(welcome.Substring(0, welcomeLength));

                screen.AppendOuterRow(rowBuffer.ToString());
            }
            else
            {
                screen.AppendOuterRow("~");
            }
        }
        void DrawSatausBar(IScreen screen)
        {
            var fileName = this.document.Path != null ? Path.GetFileName(this.document.Path) : "[New File]";
            var documentRowCount = this.document.Rows.Count;
            var fileInfo = $"{fileName:.20} - {documentRowCount} lines";
            var positionInfo = $"{this.document.Cursor.Y}:{this.document.Cursor.X}";
            var padding = this.console.Width - fileInfo.Length - positionInfo.Length;

            screen.AppendRow(fileInfo + new string(' ', padding) + positionInfo);
        }
        void DrawMessageBar(IScreen screen)
        {
            if (DateTime.Now - this.statusMessage.Time < TimeSpan.FromSeconds(5))
            {
                var text = this.statusMessage.Value;
                var textLength = Math.Min(text.Length, this.console.Width);
                screen.AppendRow(text.Substring(0, textLength));
            }
            else
            {
                screen.AppendRow("");
            }
        }

        EditorProcessingResults ProcessKeyPress(ConsoleKeyInfo key)
        {
            if (key == SpecialKeys.CtrlQ)
            {
                return EditorProcessingResults.Quit;
            }
            switch (key.Key)
            {
                case ConsoleKey.Home:
                    this.document.MoveHome();
                    return EditorProcessingResults.Running;
                case ConsoleKey.End:
                    this.document.MoveEnd();
                    return EditorProcessingResults.Running;

                case ConsoleKey.PageUp:
                case ConsoleKey.PageDown:
                    return this.MovePage(key.Key);

                case ConsoleKey.UpArrow:
                case ConsoleKey.DownArrow:
                case ConsoleKey.LeftArrow:
                case ConsoleKey.RightArrow:
                    return this.MoveCursor(key.Key);

                case ConsoleKey.Enter:
                    // TODO: implement this
                    return EditorProcessingResults.Running;

                case ConsoleKey.Backspace:
                    // TODO: implement this
                    return EditorProcessingResults.Running;

                case ConsoleKey.A:
                    if ((key.Modifiers & ConsoleModifiers.Alt) != 0)
                    {
                        return this.SwitchAmbiguousWidth();
                    }
                    goto default;
                case ConsoleKey.S:
                    if ((key.Modifiers & ConsoleModifiers.Control) != 0)
                    {
                        return this.Save();
                    }
                    goto default;
                default:
                    if (char.IsControl(key.KeyChar))
                    {
                        return EditorProcessingResults.Running;
                    }
                    return this.InsertChar(key.KeyChar);
            }

            
        }
        EditorProcessingResults Save()
        {
            try
            {
                this.document.Save();
                this.SetStatusMessage("File is saved");
            }
            catch (IOException ex)
            {
                this.SetStatusMessage(ex.Message);
            }
            return EditorProcessingResults.Running;
        }

        EditorProcessingResults InsertChar(char c)
        {
            this.document.InsertChar(c, this.setting);
            return EditorProcessingResults.Running;
        }
        EditorProcessingResults MovePage(ConsoleKey key)
        {
            switch (key)
            {
                case ConsoleKey.PageUp:
                    this.document.MovePageUp(this.console.EditorHeight);
                    break;
                case ConsoleKey.PageDown:
                    this.document.MovePageDown(this.console.EditorHeight);
                    break;
            }
            return EditorProcessingResults.Running;
        }

        EditorProcessingResults MoveCursor(ConsoleKey key)
        {
            switch (key)
            {
                case ConsoleKey.LeftArrow:
                    this.document.MoveLeft();
                    break;
                case ConsoleKey.RightArrow:
                    this.document.MoveRight();
                    break;
                case ConsoleKey.UpArrow:
                    this.document.MoveUp();
                    break;
                case ConsoleKey.DownArrow:
                    this.document.MoveDown();
                    break;
            }
            return EditorProcessingResults.Running;
        }

        EditorProcessingResults SwitchAmbiguousWidth()
        {
            this.setting.IsFullWidthAmbiguous = !this.setting.IsFullWidthAmbiguous;
            var ambiguousSize =
                this.setting.IsFullWidthAmbiguous ? "Full Width"
                : "Half Width";
            this.SetStatusMessage($"East Asian Width / Ambiguous = {ambiguousSize}");
            return EditorProcessingResults.Running;
        }
    }
}
