using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace txte
{
    class Editor
    {
        public static string Version = "0.0.1";


        public Editor(IConsole console, EditorSetting setting, Document document, Message firstMessage)
        {
            this.console = console;
            this.setting = setting;
            this.document = document;
            this.message = firstMessage;
            this.menu = new Menu(setting, SetupShortcuts());
            this.editKeyBinds = SetupEditKeyBinds();
        }

        readonly IConsole console;
        readonly EditorSetting setting;
        readonly Menu menu;
        readonly KeyBindSet editKeyBinds;
        readonly Temporary<IPrompt> prompt = new Temporary<IPrompt>();

        Document document;
        Message message;

        Size editArea => 
            new Size(
                this.console.Size.Width,
                this.console.Size.Height
                - (this.prompt.HasValue ? 1 : 0)
                - (this.message.IsValid ? 1 : 0)
                - 1
            );

        public async Task RunAsync()
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
                switch (await this.ProcessKeyPressAsync(keyInfo))
                {
                    case KeyProcessingResults.Quit:
                        return;
                    default:
                        break;
                }
                this.RefreshScreen(0);
            }
        }

        KeyBindSet SetupShortcuts() =>
            new KeyBindSet
            {
                [new KeyBind(new KeyCombination(ConsoleKey.F, false, true), "Find")] =
                    this.Find,
                [new KeyBind(new KeyCombination(ConsoleKey.Q, false, true), "Quit")] =
                    this.Quit,
                [new KeyBind(new KeyCombination(ConsoleKey.S, false, true), "Save")] =
                    this.Save,
                [new KeyBind(new KeyCombination(ConsoleKey.S, true, true), "Save As")] =
                    this.SaveAs,
                [new KeyBind(new KeyCombination(ConsoleKey.L, false, true), "Refresh")] =
                    () => this.DelegateTask(this.console.Clear),
                [new KeyBind(new KeyCombination(ConsoleKey.E, true, true), "Change East Asian Width")] =
                    this.SwitchAmbiguousWidth,
                [new KeyBind(new KeyCombination(ConsoleKey.L, true, true), "Change End of Line sequence")] =
                    this.SelectNewLine,
            };

        KeyBindSet SetupEditKeyBinds() =>
            new KeyBindSet
            {
                [new KeyBind(new KeyCombination(ConsoleKey.Home, false, false), "Move to Start of Line")] =
                    () => this.DelegateTask(this.document.MoveHome),
                [new KeyBind(new KeyCombination(ConsoleKey.End, false, false), "Move to End of Line")] =
                    () => this.DelegateTask(this.document.MoveEnd),
                [new KeyBind(new KeyCombination(ConsoleKey.Home, true, false), "Move to Start of File")] =
                    () => this.DelegateTask(this.document.MoveStartOfFile),
                [new KeyBind(new KeyCombination(ConsoleKey.End, true, false), "Move to End of File")] =
                    () => this.DelegateTask(this.document.MoveEndOfFile),
                [new KeyBind(new KeyCombination(ConsoleKey.PageUp, false, false), "Scroll to Previous Page")] =
                    () => this.DelegateTask(() => this.document.MovePageUp(this.editArea.Height)),
                [new KeyBind(new KeyCombination(ConsoleKey.PageDown, false, false), "Scroll to Next Page")] =
                    () => this.DelegateTask(() => this.document.MovePageDown(this.editArea.Height)),
                [new KeyBind(new KeyCombination(ConsoleKey.UpArrow, false, false), "Move Cursor Up")] =
                    () => this.DelegateTask(this.document.MoveUp),
                [new KeyBind(new KeyCombination(ConsoleKey.DownArrow, false, false), "Move Cursor Down")] =
                    () => this.DelegateTask(this.document.MoveDown),
                [new KeyBind(new KeyCombination(ConsoleKey.LeftArrow, false, false), "Move Cursor Left")] =
                    () => this.DelegateTask(this.document.MoveLeft),
                [new KeyBind(new KeyCombination(ConsoleKey.RightArrow, false, false), "Move Cursor Right")] =
                    () => this.DelegateTask(this.document.MoveRight),
                [new KeyBind(new KeyCombination(ConsoleKey.Enter, false, false), "Break Line")] =
                    () => this.DelegateTask(this.document.InsertNewLine),
                [new KeyBind(new KeyCombination(ConsoleKey.Backspace, false, false), "Delete a Left Letter")] =
                    () => this.DelegateTask(this.document.BackSpace),
                [new KeyBind(new KeyCombination(ConsoleKey.Delete, false, false), "Delete a Right Letter")] =
                    () => this.DelegateTask(this.document.DeleteChar),
            };

        Task<KeyProcessingResults> DelegateTask(Action action)
        {
            action();
            return KeyProcessingTaskResult.Running;
        }

        async Task OpenDocumentAsync(string path)
        {
            if (this.document.IsNew)
            {
                this.document = await Document.OpenAsync(path, this.setting);
            }
            else
            {
                throw new NotImplementedException("Document has already opened and other document is opened");
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
            this.document.UpdateOffset(this.editArea);
            this.console.RefreshScreen(
                from.AtMin(0),
                this.setting,
                this.RenderScreen,
                this.document.Cursor);
        }

        void RenderScreen(IScreen screen, int from)
        {
            this.DrawEditorRows(screen, from);
            this.DrawMessageBar(screen);
            this.DrawSatausBar(screen);
            this.DrawPromptBar(screen);
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

            if (y - titleRowCount >= this.menu.KeyBinds.KeyBinds.Count)
            {
                screen.AppendRow(new[]
                { 
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
                    new StyledString("~", ColorSet.OutOfBounds),
                });
                return;
            }
            else
            {
                var item = this.menu.KeyBinds.KeyBinds[y - titleRowCount];

                var leftLength = item.explanation.Length;
                var rightLength = item.keys.Select(x => x.Length).Sum() + (item.keys.Length - 1) * keyJoiner.Length;
                var leftPadding = (this.console.Width - separator.Length) / 2 - leftLength - 1;
                var spans = new List<StyledString>();
                spans.Add(new StyledString("~", ColorSet.OutOfBounds));
                spans.Add(new StyledString(new string(' ', leftPadding), ColorSet.OutOfBounds));
                spans.Add(new StyledString(item.explanation, ColorSet.OutOfBounds));
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
                screen.AppendRow(spans);
            }
        }

        void DrawOutofBounds(IScreen screen, int y)
        {
            if (this.document.IsNew && y == this.editArea.Height / 3)
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
            if (!this.message.IsValid) { return; }

            var text = this.message.Value;
            var textLength = text.Length.AtMax(this.console.Width);
            (var render, _, _) = text.SubConsoleString(0, textLength, this.setting.IsFullWidthAmbiguous);
            screen.AppendRow(new[]
            {
                new StyledString("~", ColorSet.OutOfBounds),
                new StyledString(new string(' ', this.console.Width - render.GetConsoleLength(this.setting.IsFullWidthAmbiguous) - 1)),
                new StyledString(render, ColorSet.OutOfBounds),
            });
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
            using var _ = this.menu.ShowWhileModal();
            using var message = new TemporaryMessage("hint: You can omit Crtl on the menu screen.");
            this.message = message;
            this.RefreshScreen(0);

            while (true)
            {
                (var eventType, var keyInfo) = await this.console.ReadKeyOrTimeoutAsync();
                if (eventType == EventType.Timeout)
                { 
                    this.DiscardMessage();
                    continue;
                }
                if (keyInfo.Key == ConsoleKey.Escape) { return KeyProcessingResults.Running; }
                
                if (this.menu.KeyBinds[keyInfo.ToKeyCombination().WithControl()] is { } function)
                {
                    message.Expire();
                    this.menu.Hide();
                    return await function();
                }

                this.RefreshScreen(0);
            }
        }

        async Task<KeyProcessingResults> ProcessKeyPressAsync(ConsoleKeyInfo keyInfo)
        {
            if (keyInfo is { Key: ConsoleKey.Escape, Modifiers: 0 })
            {
                return await this.OpenMenu();
            }
            if (this.menu.KeyBinds[keyInfo.ToKeyCombination()] is { } shortcut)
            {
                return await shortcut();
            }
            if (this.editKeyBinds[keyInfo.ToKeyCombination()] is { } editKey)
            {
                return await editKey();
            }
            if (!char.IsControl(keyInfo.KeyChar))
            {
                this.document.InsertChar(keyInfo.KeyChar);
                return KeyProcessingResults.Running;
            }
            return KeyProcessingResults.Unhandled;
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
            if ((confirm ?? Choice.No) == Choice.Yes)
            {
                return KeyProcessingResults.Quit;
            }
            else
            {
                return KeyProcessingResults.Running;
            }
        }

        async Task<KeyProcessingResults> Save() => 
            this.document.Path == null ? await this.SaveAs() : await this.SaveWithConfirm();

        async Task<KeyProcessingResults> SaveAs()
        {
            using var message = new TemporaryMessage("hint: Esc to cancel");
            this.message = message;
            var savePath = await this.Prompt(new InputPrompt("Save as:"));
            if (savePath == null)
            {
                this.message = new Message("Save is cancelled");
                return KeyProcessingResults.Running;
            }
            
            this.document.Path = savePath;
            await this.SaveWithConfirm();
            return KeyProcessingResults.Running;
        }

        async Task<KeyProcessingResults> SaveWithConfirm()
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

        async Task<KeyProcessingResults> Find()
        {
            using var message = new TemporaryMessage("hint: Esc to cancel");
            this.message = message;
            var savedPosition = this.document.ValuePosition;
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

        Task<KeyProcessingResults> SwitchAmbiguousWidth()
        {
            this.setting.IsFullWidthAmbiguous = !this.setting.IsFullWidthAmbiguous;
            var ambiguousSize =
                this.setting.IsFullWidthAmbiguous ? "Full Width"
                : "Half Width";
            this.message = new Message($"East Asian Width / Ambiguous = {ambiguousSize}");
            return KeyProcessingTaskResult.Running;
        }
    }
}
