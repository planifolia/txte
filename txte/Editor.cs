using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace txte
{
    enum KeyProcessingResults
    {
        Running,
        Quit,
        Unhandled,
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
        public bool IsFullWidthAmbiguous { get; set; } = false;
        public int TabSize { get; set; } = 4;
    }

    class Editor
    {
        public static string Version = "0.0.1";


        public Editor(IConsole console, EditorSetting setting)
        {
            this.console = console;
            this.setting = setting;
            this.isTriedToQuit = false;
        }

        readonly IConsole console;

        Document document;
        TemporaryMessage statusMessage;
        EditorSetting setting;
        private bool isTriedToQuit;

        Size editArea => new Size(this.console.Size.Width, this.console.Size.Height - 2);

        public void SetDocument(Document document)
        {
            this.document = document;
        }

        public void SetStatusMessage(string value)
        {
            this.statusMessage = new TemporaryMessage(value, DateTime.Now);
        }

        public async Task Run()
        {
            while (true)
            {
                this.RefreshScreen();
                (var eventType, var keyInfo) = await this.console.ReadKeyOrTimeoutAsync();
                if (eventType == EventType.Timeout) { continue; }
                switch (await this.ProcessKeyPress(keyInfo))
                {
                    case KeyProcessingResults.Quit:
                        if (this.isTriedToQuit || !this.document.IsModified)
                        {
                            return;
                        }
                        else
                        {
                            this.isTriedToQuit = true;
                            this.SetStatusMessage("File has unsaved changes. Press Ctrl-Q once again to quit.");
                            break;
                        }
                    default:
                        this.isTriedToQuit = false;
                        continue;
                }
            }
        }

        void RefreshScreen()
        {
            this.document.UpdateOffset(this.editArea, this.setting);
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
            for (int y = 0; y < this.editArea.Height; y++)
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
            var render = this.document.Rows[docRow].Render;
            var sublenderLength =
                Math.Clamp(
                    render.GetConsoleLength(ambiguousSetting) - this.document.Offset.X,
                    0, this.console.Width
                );
            if (sublenderLength > 0)
            {
                var (subrender, startIsFragmented, endIsFragmented) =
                    render.SubConsoleString(this.document.Offset.X, sublenderLength, ambiguousSetting);

                var spans = new List<StyledString>();
                if (startIsFragmented) { spans.Add(new StyledString("]", ColorSet.Fragment)); }
                spans.Add(new StyledString(subrender));
                if (endIsFragmented) { spans.Add(new StyledString("[", ColorSet.Fragment)); }

                screen.AppendRow(spans);
            }
            else
            {
                screen.AppendRow("");
            }
        }

        void DrawOutofBounds(IScreen screen, int y)
        {
            if (this.document.Rows.Count == 0 && y == this.editArea.Height / 3)
            {
                var welcome = $"txte -- version {Version}";
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
            var fileNameLength = Math.Min(fileName.Length, 20);
            (var clippedFileName, _, _) = 
                fileName.SubConsoleString(0, fileNameLength, this.setting.IsFullWidthAmbiguous);
            var fileInfo = $"{clippedFileName}{(this.document.IsModified ? "(*)" : "")}";
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

        async Task<string?> Prompt(string promptPrefix, string promptSuffix)
        {
            var input = new StringBuilder();

            EventType prevEventType = EventType.UserAction;
            while (true)
            {
                this.SetStatusMessage($"{promptPrefix}{input.ToString()}{promptSuffix}");
                if (prevEventType != EventType.Timeout) { this.RefreshScreen(); }

                (var eventType, var keyInfo) = await this.console.ReadKeyOrTimeoutAsync();
                prevEventType = eventType;
                if (eventType == EventType.Timeout) continue;
                switch (keyInfo)
                {
                    case { Key: ConsoleKey.Backspace, Modifiers: (ConsoleModifiers)0 }:
                        input.Length = Math.Max(input.Length - 1, 0);
                        break;
                    case { Key: ConsoleKey.Escape, Modifiers: (ConsoleModifiers)0 }:
                        return null;
                    case { Key: ConsoleKey.Enter, Modifiers: (ConsoleModifiers)0 }:
                        return input.ToString();
                    default:
                        if (!char.IsControl(keyInfo.KeyChar))
                        { 
                            input.Append(keyInfo.KeyChar);
                        }
                        break;
                }
            }
        }

        async Task<KeyProcessingResults> ProcessKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Modifiers)
            {
                case ConsoleModifiers.Control | ConsoleModifiers.Alt | ConsoleModifiers.Shift:
                case ConsoleModifiers.Control | ConsoleModifiers.Shift:
                case ConsoleModifiers.Alt | ConsoleModifiers.Shift:
                    return this.ProcessOptionShiftKeyPress(keyInfo);
                case ConsoleModifiers.Control | ConsoleModifiers.Alt:
                case ConsoleModifiers.Control:
                case ConsoleModifiers.Alt:
                    return await this.ProcessOptionKeyPress(keyInfo);
                case ConsoleModifiers.Shift:
                    return this.ProcessShiftKeyPress(keyInfo);
                default:
                    return this.ProcessSingleKeyPress(keyInfo);
            }
        }

        KeyProcessingResults ProcessOptionShiftKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.E:
                    return this.SwitchAmbiguousWidth();

                default:
                    return KeyProcessingResults.Unhandled;
            }
        }

        async Task<KeyProcessingResults> ProcessOptionKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Q:
                    return KeyProcessingResults.Quit;

                case ConsoleKey.L:
                    return this.DelegateProcessing(this.console.Clear);

                case ConsoleKey.S:
                    return await this.Save();

                default:
                    return KeyProcessingResults.Unhandled;
            }
        }

        KeyProcessingResults ProcessShiftKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Home:
                    return this.DelegateProcessing(this.document.MoveStartOfFile);
                case ConsoleKey.End:
                    return this.DelegateProcessing(this.document.MoveEndOfFile);

                default:
                    if (char.IsControl(keyInfo.KeyChar))
                    {
                        return KeyProcessingResults.Unhandled;
                    }
                    return this.InsertChar(keyInfo.KeyChar);
            }
        }

        KeyProcessingResults ProcessSingleKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Home:
                    return this.DelegateProcessing(this.document.MoveHome);
                case ConsoleKey.End:
                    return this.DelegateProcessing(this.document.MoveEnd);

                case ConsoleKey.PageUp:
                case ConsoleKey.PageDown:
                    return this.MovePage(keyInfo.Key);

                case ConsoleKey.UpArrow:
                case ConsoleKey.DownArrow:
                case ConsoleKey.LeftArrow:
                case ConsoleKey.RightArrow:
                    return this.MoveCursor(keyInfo.Key);

                case ConsoleKey.Enter:
                    return this.DelegateProcessing(() => this.document.InsertNewLine(this.setting));

                case ConsoleKey.Backspace:
                    return this.DelegateProcessing(() => this.document.BackSpace(this.setting));
                case ConsoleKey.Delete:
                    return this.DelegateProcessing(() => this.document.DeleteChar(this.setting));
                        
                default:
                    if (char.IsControl(keyInfo.KeyChar))
                    {
                        return KeyProcessingResults.Unhandled;
                    }
                    return this.InsertChar(keyInfo.KeyChar);
            }
        }

        KeyProcessingResults DelegateProcessing(Action action)
        {
            action();
            return KeyProcessingResults.Running;
        }

        async Task<KeyProcessingResults> Save()
        {
            try
            {
                var savePath = this.document.Path ?? await this.Prompt("Save as:", "");
                if (savePath == null)
                {
                    this.SetStatusMessage("Save cancelled");
                    return KeyProcessingResults.Running;
                }
                this.document.Path = savePath;
                this.document.Save();
                this.SetStatusMessage("File is saved");
            }
            catch (IOException ex)
            {
                this.SetStatusMessage(ex.Message);
            }
            return KeyProcessingResults.Running;
        }

        KeyProcessingResults InsertChar(char c)
        {
            this.document.InsertChar(c, this.setting);
            return KeyProcessingResults.Running;
        }

        KeyProcessingResults MovePage(ConsoleKey key)
        {
            switch (key)
            {
                case ConsoleKey.PageUp:
                    this.document.MovePageUp(this.editArea.Height);
                    break;
                case ConsoleKey.PageDown:
                    this.document.MovePageDown(this.editArea.Height);
                    break;
            }
            return KeyProcessingResults.Running;
        }

        KeyProcessingResults MoveCursor(ConsoleKey key)
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
            return KeyProcessingResults.Running;
        }

        KeyProcessingResults SwitchAmbiguousWidth()
        {
            this.setting.IsFullWidthAmbiguous = !this.setting.IsFullWidthAmbiguous;
            var ambiguousSize =
                this.setting.IsFullWidthAmbiguous ? "Full Width"
                : "Half Width";
            this.SetStatusMessage($"East Asian Width / Ambiguous = {ambiguousSize}");
            return KeyProcessingResults.Running;
        }
    }
}
