using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace txte
{
    enum KeyProcessingResults
    {
        Running,
        Quit,
        Unhandled,
    }

    class Editor
    {
        public static string Version = "0.0.1";


        public Editor(IConsole console, EditorSetting setting, Document document, Message firstMessage)
        {
            this.console = console;
            this.setting = setting;
            this.document = document;
            this.prompt = new TemporaryValue<IPrompt>();
            this.message = firstMessage;
            this.menu = new Menu(setting);
        }

        readonly IConsole console;
        readonly Menu menu;
        public Message message;

        Document document;
        TemporaryValue<IPrompt> prompt;
        EditorSetting setting;

        Size editArea => 
            new Size(
                this.console.Size.Width,
                this.console.Size.Height
                - (this.prompt.HasValue ? 1 : 0)
                - (this.message.IsValid ? 1 : 0)
                - 1
            );

        public async Task Run()
        {
            this.RefreshScreen(0);
            while (true)
            {
                (var eventType, var keyInfo) = await this.console.ReadKeyOrTimeoutAsync();
                if (eventType == EventType.Timeout)
                { 
                    this.DiscardMessage();
                    continue;
                }
                switch (await this.ProcessKeyPress(keyInfo))
                {
                    case KeyProcessingResults.Quit:
                        return;
                    default:
                        break;
                }
                this.RefreshScreen(0);
            }
        }

        void DiscardMessage()
        {
            var editAreaHeight = this.editArea.Height;
            this.message.CheckExpiration(DateTime.Now);
            if (editAreaHeight != this.editArea.Height)
            {
                this.RefreshScreen(editAreaHeight);
            }
        }

        void RefreshScreen(int from)
        {
            this.document.UpdateOffset(this.editArea, this.setting);
            this.console.RefreshScreen(
                from.AtMin(0),
                this.setting,
                this.RenderScreen,
                this.document.Cursor);
        }

        void RenderScreen(IScreen screen, int from)
        {
            this.DrawEditorRows(screen, from);
            this.DrawSatausBar(screen);
            this.DrawPromptBar(screen);
            this.DrawMessageBar(screen);
        }

        void DrawEditorRows(IScreen screen, int from)
        {
            bool ambiguousSetting = this.setting.IsFullWidthAmbiguous;
            for (int y = from; y < this.editArea.Height; y++)
            {
                var docRow = y + this.document.Offset.Y;
                if (this.menu.IsShown)
                {
                    this.DrawMenu(screen, y);
                }
                else if (docRow < this.document.Rows.Count)
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
                (render.GetConsoleLength(ambiguousSetting) - this.document.Offset.X)
                .Clamp(0, this.console.Width);
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
        
        void DrawMenu(IScreen screen, int y)
        {
            var keyJoiner = " + ";
            var separator = "  ";
            var titleRowCount = 2;

            if (y - titleRowCount >= this.menu.Items.Count)
            {
                screen.AppendRow(new[]
                { 
                    // new StyledString(new string(' ', this.console.Width), ColorSet.SystemMessage),
                    new StyledString("~", ColorSet.OutOfBounds),
                });
            }
            else if (y == 0)
            {
                var message = "Menu / Shortcuts";
                var messageLength = message.Length;
                var leftPadding = (this.console.Width - messageLength) / 2 - 1;
                screen.AppendRow(new[]
                { 
                    new StyledString("~", ColorSet.OutOfBounds),
                    new StyledString(new string(' ', leftPadding), ColorSet.OutOfBounds),
                    new StyledString(message, ColorSet.OutOfBounds),
                });
            }
            // else if (y == 1)
            // {
            //     var message1 = "hint: You can omit ";
            //     var key = "Crtl";
            //     var message2 = " on the menu screen.";
            //     var messageLength = message1.Length + key.Length + message2.Length;
            //     var leftPadding = (this.console.Width - messageLength) / 2 - 1;
            //     screen.AppendRow(new[]
            //     { 
            //         new StyledString("~", ColorSet.OutOfBounds),
            //         new StyledString(new string(' ', leftPadding), ColorSet.OutOfBounds),
            //         new StyledString(message1, ColorSet.OutOfBounds),
            //         new StyledString(key, ColorSet.KeyExpression),
            //         new StyledString(message2, ColorSet.OutOfBounds),
            //         // new StyledString(new string(' ', this.console.Width - message1.Length - key.Length - message2.Length), ColorSet.SystemMessage),
            //     });
            // }
            else if (y == 1)
            {
                screen.AppendRow(new[]
                { 
                    // new StyledString(new string(' ', this.console.Width), ColorSet.SystemMessage),
                    new StyledString("~", ColorSet.OutOfBounds),
                });
                return;
            }
            else
            {
                var item = this.menu.Items[y - titleRowCount];

                var leftLength = item.effect.Length;
                var rightLength = item.keys.Select(x => x.Length).Sum() + (item.keys.Length - 1) * keyJoiner.Length;
                var leftPadding = (this.console.Width - separator.Length) / 2 - leftLength - 1;
                // var rightPadding = this.console.Width - leftLength - rightLength - leftPadding - 2;
                var spans = new List<StyledString>();
                spans.Add(new StyledString("~", ColorSet.OutOfBounds));
                spans.Add(new StyledString(new string(' ', leftPadding), ColorSet.OutOfBounds));
                spans.Add(new StyledString(item.effect, ColorSet.OutOfBounds));
                spans.Add(new StyledString(separator, ColorSet.OutOfBounds));
                int i = 0;
                foreach (var key in item.keys)
                {
                    if (i != 0) {
                        spans.Add(new StyledString(keyJoiner, ColorSet.OutOfBounds));
                    }
                    spans.Add(new StyledString(key, ColorSet.KeyExpression));
                    i++;
                }
                // spans.Add(new StyledString(new string(' ', rightPadding), ColorSet.SystemMessage));
                screen.AppendRow(spans);
            }
        }

        void DrawOutofBounds(IScreen screen, int y)
        {
            if (this.document.IsUntouched && y == this.editArea.Height / 3)
            {
                var welcome = $"txte -- version {Version}";
                var welcomeLength = welcome.Length.AtMax(this.console.Width);
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
        void DrawPromptBar(IScreen screen)
        {
            if (this.prompt.HasValue)
            {
                screen.AppendRow(this.prompt.Value.ToStyledString());
            }
        }
        void DrawSatausBar(IScreen screen)
        {
            var fileName = this.document.Path != null ? Path.GetFileName(this.document.Path) : "[New File]";
            var fileNameLength = fileName.Length.AtMax(20);
            (var clippedFileName, _, _) = 
                fileName.SubConsoleString(0, fileNameLength, this.setting.IsFullWidthAmbiguous);
            var fileInfo = $"{clippedFileName}{(this.document.IsModified ? "(*)" : "")}";
            var positionInfo = $"{this.document.Cursor.Y}:{this.document.Cursor.X} {this.document.NewLineFormat.Name}";
            var padding = this.console.Width - fileInfo.Length - positionInfo.Length;

            screen.AppendRow(new[] { new StyledString(fileInfo + new string(' ', padding) + positionInfo, ColorSet.SystemMessage) });
        }
        void DrawMessageBar(IScreen screen)
        {
            if (this.message.IsValid)
            {
                var text = this.message.Value;
                var textLength = text.Length.AtMax(this.console.Width);
                screen.AppendRow(text.Substring(0, textLength));
            }
        }
        
        async Task<TResult?> Prompt<TResult>(IPrompt<TResult> prompt) where TResult: class
        {
            using var _ = this.prompt.SetTemporary(prompt);
            this.RefreshScreen(0);
            while (true)
            {
                (var eventType, var keyInfo) = await this.console.ReadKeyOrTimeoutAsync();
                if (eventType == EventType.Timeout)
                { 
                    this.DiscardMessage();
                    continue;
                }
                (var state, var input) = prompt.ProcessKey(keyInfo);
                if (state == KeyProcessingResults.Quit) { return input; }
                this.RefreshScreen(0);
                //this.RefreshScreen(this.editArea.Height);
            }
        }
        

        private async Task<KeyProcessingResults> OpenMenu()
        {
            this.menu.IsShown = true;
            using var message = new TemporaryMessage("hint: You can omit Crtl on the menu screen.");
            this.message = message;
            this.RefreshScreen(0);
            try
            {
                while (true)
                {
                    (var eventType, var keyInfo) = await this.console.ReadKeyOrTimeoutAsync();
                    if (eventType == EventType.Timeout)
                    { 
                        this.DiscardMessage();
                        continue;
                    }
                    if (keyInfo.Key == ConsoleKey.Escape) { return KeyProcessingResults.Running; }
                    this.menu.IsShown = false;
                    switch (keyInfo.Modifiers)
                    {
                        case ConsoleModifiers.Control | ConsoleModifiers.Shift:
                        case ConsoleModifiers.Shift:
                        {
                            if (await this.ProcessOptionShiftKeyPress(keyInfo) is var result && result != KeyProcessingResults.Unhandled)
                            {
                                return result;
                            }
                            break;
                        }
                        case ConsoleModifiers.Control:
                        case (ConsoleModifiers)0:
                        {
                            if (await this.ProcessOptionKeyPress(keyInfo) is var result && result != KeyProcessingResults.Unhandled)
                            {
                                return result;
                            }
                            break;
                        }
                        default:
                            break;
                    }
                    this.menu.IsShown = true;
                    this.RefreshScreen(0);
                }
            }
            finally
            {
                this.menu.IsShown = false;
            }
        }

        async Task<KeyProcessingResults> ProcessKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Modifiers)
            {
                case ConsoleModifiers.Control | ConsoleModifiers.Shift:
                    return await this.ProcessOptionShiftKeyPress(keyInfo);
                case ConsoleModifiers.Control:
                    return await this.ProcessOptionKeyPress(keyInfo);
                case ConsoleModifiers.Shift:
                    return this.ProcessShiftKeyPress(keyInfo);
                default:
                    return await this.ProcessSingleKeyPress(keyInfo);
            }
        }

        async Task<KeyProcessingResults> ProcessOptionShiftKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.E:
                    return this.SwitchAmbiguousWidth();

                case ConsoleKey.L:
                    return await this.SelectNewLine();

                case ConsoleKey.S:
                    return await this.SaveAs();

                default:
                    return KeyProcessingResults.Unhandled;
            }
        }

        async Task<KeyProcessingResults> ProcessOptionKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.F:
                    return await this.Find();

                case ConsoleKey.Q:
                    return await this.Quit();

                case ConsoleKey.L:
                    return this.DelegateProcessing(this.console.Clear);

                case ConsoleKey.S:
                    return this.document.Path == null ? await this.SaveAs() : await this.Save();

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

        async Task<KeyProcessingResults> ProcessSingleKeyPress(ConsoleKeyInfo keyInfo)
        {
            switch (keyInfo.Key)
            {
                case ConsoleKey.Escape:
                    return await this.OpenMenu();
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

        async Task<KeyProcessingResults> Quit()
        {
            if (!this.document.IsModified) { return KeyProcessingResults.Quit; }

            var confirm = 
                await Prompt(
                    new ChoosePrompt(
                        "File has unsaved changes. Quit without saving?",
                        new[] { Choice.No, Choice.Yes }
                    )
                );
            if ((confirm ?? Choice.No) == Choice.No)
            {
                return KeyProcessingResults.Quit;
            }
            else
            {
                return KeyProcessingResults.Running;
            }
        }

        async Task<KeyProcessingResults> SelectNewLine()
        {
            var selection = 
                await Prompt(
                    new ChoosePrompt(
                        "Change end of line sequence:",
                        NewLineFormat.All,
                        this.document.NewLineFormat
                    )
                );
            if (selection is NewLineFormat newLineFormat)
            {
                this.document.NewLineFormat = newLineFormat;
                return KeyProcessingResults.Running;
            }

            return KeyProcessingResults.Running;
        }

        async Task<KeyProcessingResults> Save()
        {
            try
            {
                if (File.Exists(this.document.Path))
                {
                    var confirm = 
                        await Prompt(
                            new ChoosePrompt(
                                "Override?",
                                new[] { Choice.No, Choice.Yes }
                            )
                        );
                    if ((confirm ?? Choice.No) == Choice.No)
                    {
                        this.message = new Message("Save is cancelled");
                        return KeyProcessingResults.Running;
                    }
                }
                this.document.Save();
                this.message = new Message("File is saved");
            }
            catch (IOException ex)
            {
                this.message = new Message(ex.Message);
            }
            return KeyProcessingResults.Running;
        }
        
        async Task<KeyProcessingResults> SaveAs()
        {
            var message = new Message("hint: Esc to cancel");
            this.message = message;
            var savePath = await this.Prompt(new InputPrompt("Save as:"));
            if (savePath == null)
            {
                this.message = new Message("Save is cancelled");
                return KeyProcessingResults.Running;
            }
            
            this.document.Path = savePath;
            await this.Save();
            return KeyProcessingResults.Running;
        }

        async Task<KeyProcessingResults> Find()
        {
            var savedPosition = this.document.ValuePosition;
            using var message = new TemporaryMessage("hint: Esc to cancel");
            this.message = message;
            var query = await this.Prompt(new InputPrompt("Search:", (x, _) => this.document.Find(x)));
            if (query != null)
            {
                this.document.Find(query);
            }
            else
            {
                this.document.ValuePosition = savedPosition;
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
            this.message = new Message($"East Asian Width / Ambiguous = {ambiguousSize}");
            return KeyProcessingResults.Running;
        }
    }
}
