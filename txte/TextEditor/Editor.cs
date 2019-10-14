﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using txte.Settings;
using txte.State;
using txte.Input;
using txte.Text;
using txte.ConsoleInterface;
using txte.TextDocument;
using txte.Prompts;
using System.Collections.Immutable;

namespace txte.TextEditor
{
    class Editor
    {
        public static string Version = "0.0.1";


        public Editor(IConsole console, Setting setting, Document document, Message firstMessage)
        {
            this.console = console;
            this.setting = setting;
            this.document = document;
            this.message = firstMessage;
            this.menu = new Menu(this.SetupShortcuts());
            this.editKeyBinds = this.SetupEditKeyBinds();
        }

        readonly IConsole console;
        readonly Setting setting;
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
                    this.FadeMessage();
                    continue;
                }
                switch (await this.ProcessKeyPressAsync(keyInfo))
                {
                    case ProcessResult.Quit:
                        return;
                    default:
                        break;
                }
                if (Console.KeyAvailable)
                {
                    continue; //skip if muitiple chars are imput by IME, etc.
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
                    this.ChangeEndOfLine,
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

                [new KeyBind(new KeyCombination(ConsoleKey.UpArrow, false, true), "Move Cursor Up Quarter Page")] =
                    () => this.DelegateTask(() => this.document.MoveUp(this.editArea.Height / 4)),
                [new KeyBind(new KeyCombination(ConsoleKey.DownArrow, false, true), "Move Cursor Down Quarter Page")] =
                    () => this.DelegateTask(() => this.document.MoveDown(this.editArea.Height / 4)),

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

        Task<ProcessResult> DelegateTask(Action action)
        {
            action();
            return ProcessTaskResult.Running;
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

        void FadeMessage()
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
            this.document.DrawRow(screen, docRow);
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
                var message = "Shortcuts";
                var messageLength = message.Length;
                var leftPadding = (this.console.Width - messageLength) / 2 - 1;
                screen.AppendRow(new[]
                { 
                    new StyledString("~", ColorSet.OutOfBounds),
                    new StyledString(new string(' ', leftPadding), ColorSet.OutOfBounds),
                    new StyledString(message, ColorSet.OutOfBounds),
                });
            }
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
                fileName.SubRenderString(0, fileNameLength, this.setting.IsFullWidthAmbiguous);
            var fileInfo = $"{clippedFileName}{(this.document.IsModified ? "(*)" : "")}";
            var positionInfo = $"{this.document.RenderPosition.Y}:{this.document.RenderPosition.X} {this.document.NewLineFormat.Name}";
            var padding = this.console.Width - fileInfo.Length - positionInfo.Length;

            screen.AppendRow(new[] { new StyledString(fileInfo + new string(' ', padding) + positionInfo, ColorSet.SystemMessage) });
        }
        void DrawMessageBar(IScreen screen)
        {
            if (!this.message.IsValid) { return; }

            var text = this.message.Value;
            var textLength = text.GetRenderLength();
            var displayLength = textLength.AtMax(this.console.Width);
            var render = text.SubRenderString(textLength - displayLength, displayLength);
            var prefix = 
                (displayLength < this.console.Width) ? new[]
                {
                    new StyledString("~", ColorSet.OutOfBounds),
                    new StyledString(new string(' ', this.console.Width - displayLength - 1)),
                }
                : Enumerable.Empty<StyledString>();
            screen.AppendRow(prefix.Concat(render.ToStyledStrings()));
        }
        
        async Task<ModalProcessResult<TResult>> Prompt<TResult>(IPrompt<TResult> prompt)
        {
            using var _ = this.prompt.SetTemporary(prompt);

            this.RefreshScreen(0);
            while (true)
            {
                (var eventType, var keyInfo) = await this.console.ReadKeyOrTimeoutAsync();
                if (eventType == EventType.Timeout)
                { 
                    this.FadeMessage();
                    continue;
                }

                var state = prompt.ProcessKey(keyInfo);
                if (state is IModalOk || state is IModalCancel)
                {
                    return state;
                }

                if (state is IModalNeedsRefreash)
                {
                    this.RefreshScreen(0);
                }
                else
                {
                    this.RefreshScreen(this.editArea.Height);
                }
            }
        }
        

        private async Task<ProcessResult> OpenMenu()
        {
            using var _ = this.menu.ShowWhileModal();

            using var message = 
                new TemporaryMessage(ColoredString.Concat(this.setting,
                    ("hint: You can omit ", ColorSet.OutOfBounds),
                    ("Crtl", ColorSet.KeyExpression),
                    (" on the menu screen", ColorSet.OutOfBounds)
                ));
            this.message = message;

            this.RefreshScreen(0);

            while (true)
            {
                (var eventType, var keyInfo) = await this.console.ReadKeyOrTimeoutAsync();
                if (eventType == EventType.Timeout)
                { 
                    this.FadeMessage();
                    continue;
                }
                if (keyInfo.Key == ConsoleKey.Escape) { return ProcessResult.Running; }
                
                if (this.menu.KeyBinds[keyInfo.ToKeyCombination().WithControl()] is { } function)
                {
                    message.Expire();
                    this.menu.Hide();
                    return await function();
                }

                this.RefreshScreen(0);
            }
        }

        async Task<ProcessResult> ProcessKeyPressAsync(ConsoleKeyInfo keyInfo)
        {
            if (keyInfo is { Key: ConsoleKey.Escape, Modifiers: 0 })
            {
                return await this.OpenMenu();
            }

            var KeyCombination = keyInfo.ToKeyCombination();
            if (this.menu.KeyBinds[KeyCombination] is { } shortcut)
            {
                return await shortcut();
            }
            if (this.editKeyBinds[KeyCombination] is { } editKey)
            {
                return await editKey();
            }

            var keyChar = keyInfo.KeyChar;
            if (!char.IsControl(keyChar))
            {
                this.document.InsertChar(keyChar);
                return ProcessResult.Running;
            }

            return ProcessResult.Unhandled;
        }

        async Task<ProcessResult> Quit()
        {
            if (!this.document.IsModified) { return ProcessResult.Quit; }

            var promptResult = 
                await this.Prompt(
                    ChoosePrompt.Create(
                        "File has unsaved changes. Quit without saving?",
                        new[] { Choice.No, Choice.Yes }
                    )
                );
            if (promptResult is ModalOk<Choice>(var confirm) && confirm  == Choice.Yes)
            {
                return ProcessResult.Quit;
            }
            else
            {
                return ProcessResult.Running;
            }
        }

        async Task<ProcessResult> Save() => 
            await ((this.document.Path == null) ? this.SaveAs() : this.SaveWithConfirm());

        async Task<ProcessResult> SaveAs()
        {
            using var message =
                new TemporaryMessage(ColoredString.Concat(this.setting,
                    ("hint: ", ColorSet.OutOfBounds),
                    ("Esc", ColorSet.KeyExpression),
                    (" to cancel", ColorSet.OutOfBounds)
                ));
            this.message = message;

            var promptInput = await this.Prompt(new InputPrompt("Save as:"));
            if (promptInput is ModalOk<string>(var savePath))
            {
                this.document.Path = savePath;
                await this.SaveWithConfirm();
                return ProcessResult.Running;
            }
            else
            {
                this.message =
                    new Message(ColoredString.Concat(this.setting,
                        ("Save is cancelled", ColorSet.OutOfBounds)));
                return ProcessResult.Running;
            }
        }

        async Task<ProcessResult> SaveWithConfirm()
        {
            try
            {
                if (File.Exists(this.document.Path))
                {
                    var promptResult = 
                        await this.Prompt(
                            ChoosePrompt.Create(
                                "Override?",
                                new[] { Choice.No, Choice.Yes }
                            )
                        );
                    if (promptResult is ModalOk<Choice>(var confirm) && confirm == Choice.No)
                    {
                        this.message =
                            new Message(ColoredString.Concat(this.setting,
                                ("Save is cancelled", ColorSet.OutOfBounds)));
                        return ProcessResult.Running;
                    }
                }
                this.document.Save();
                this.message =
                    new Message(ColoredString.Concat(this.setting,
                        ("File is saved", ColorSet.OutOfBounds)));
            }
            catch (IOException ex)
            {
                this.message =
                    new Message(ColoredString.Concat(this.setting,
                        (ex.Message, ColorSet.Default)));
            }

            return ProcessResult.Running;
        }

        async Task<ProcessResult> Find()
        {
            using var message =
                new TemporaryMessage(ColoredString.Concat(this.setting,
                    ("hint: You can omit ", ColorSet.OutOfBounds),
                    ("Esc", ColorSet.KeyExpression),
                    (" to cancel, (", ColorSet.OutOfBounds),
                    ("Shift", ColorSet.KeyExpression),
                    (" +) ", ColorSet.OutOfBounds),
                    ("Tab", ColorSet.KeyExpression),
                    (" to explor", ColorSet.OutOfBounds)
                ));
            this.message = message;

            var savedPosition = this.document.ValuePosition;
            var savedOffset = this.document.Offset;

            var finder = new FindPrompt("Search:", this.document);
            using var _ = this.document.Finding.SetTemporary(finder);

            var promptResult = await this.Prompt(finder);
            if (promptResult is ModalOk<(string pattern, FindingStatus status)>(var query))
            {
                // remain cursor position
            }
            else
            {
                // restore cursor position
                this.document.ValuePosition = savedPosition;
                this.document.Offset = savedOffset;
            }

            return ProcessResult.Running;
        }

        async Task<ProcessResult> ChangeEndOfLine()
        {
            var promptResult = 
                await this.Prompt(
                    ChoosePrompt.Create(
                        "Change End of Line sequence:",
                        EndOfLineFormat.All,
                        this.document.NewLineFormat
                    )
                );
            if (promptResult is ModalOk<EndOfLineFormat>(var selection))
            {
                this.document.NewLineFormat = selection;
            }

            return ProcessResult.Running;
        }

        async Task<ProcessResult> SwitchAmbiguousWidth()
        {
            using var message =
                    new TemporaryMessage(ColoredString.Concat(this.setting,
                        ("hint: Set according to your terminal font. Usually Half-Width", ColorSet.OutOfBounds)));
            this.message = message;
            var modalResult = 
                await this.Prompt(
                    ChoosePrompt.Create(
                        "Change East Asian Width - Ambiguous:",
                        EAWAmbiguousFormat.All,
                        EAWAmbiguousFormat.FromSetting(this.setting.IsFullWidthAmbiguous)
                    )
                );
            if (modalResult is ModalOk<EAWAmbiguousFormat>(var selection))
            {
                this.setting.IsFullWidthAmbiguous = selection.IsFullWidthAmbiguous;
                this.message =
                    new Message(ColoredString.Concat(this.setting,
                        ($"East Asian Width - Ambiguous = {selection}", ColorSet.OutOfBounds)));
            }

            return ProcessResult.Running;
        }
    }
}