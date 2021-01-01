using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using txte.Settings;
using txte.State;
using txte.Input;
using txte.Text;
using txte.ConsoleInterface;
using txte.TextDocument;
using txte.Prompts;

namespace txte.TextEditor
{
    class Editor
    {
        public static string Version =
            Assembly.GetEntryAssembly()!
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!
            .InformationalVersion
            .ToString();


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
            this.RefreshScreen(this.document.UpdateCursor(this.editArea));
            await foreach (var (eventType, keyInfo) in this.console.ReadKeysOrTimeoutAsync())
            {
                if (eventType == EventType.Timeout)
                {
                    this.FadeMessage(this.document.UpdateCursor(this.editArea));
                    continue;
                }
                switch (await this.ProcessKeyPressAsync(keyInfo))
                {
                    case ProcessResult.Quit:
                        return;
                    default:
                        break;
                }
                if (this.console.KeyAvailable)
                {
                    continue; //skip if muitiple chars are imput by IME, etc.
                }
                this.RefreshScreen(this.document.UpdateCursor(this.editArea));
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

                [new KeyBind(new KeyCombination(ConsoleKey.Tab, false, false), "Insert Tab Letter")] =
                    () => this.DelegateTask(() => this.document.InsertChar('\t')),
            };

        Task<ProcessResult> DelegateTask(Action action)
        {
            action();
            return ProcessTaskResult.Running;
        }

        async Task OpenDocumentAsync(string path)
        {
            if (!this.document.IsNew) throw
                new NotImplementedException("Document has already opened and other document is opened");

            this.document = await Document.OpenAsync(path, this.setting);
        }

        void FadeMessage(CursorPosition? cursor)
        {
            var editAreaHeight = this.editArea.Height;
            this.message.CheckExpiration(DateTime.Now);
            if (editAreaHeight != this.editArea.Height)
            {
                this.RefreshScreen(cursor, editAreaHeight);
            }
        }

        void RefreshScreen(CursorPosition? cursor) => this.RefreshScreen(cursor, 0);

        void RefreshScreen(CursorPosition? cursor, int from)
        {
            this.console.RefreshScreen(
                from.AtMin(0),
                this.setting,
                this.RenderScreen,
                cursor);
        }

        void RenderScreen(IScreen screen, int from)
        {
            this.DrawEditorLines(screen, from);
            this.DrawMessageBar(screen);
            this.DrawSatausBar(screen);
            this.DrawPromptBar(screen);
        }

        void DrawEditorLines(IScreen screen, int from)
        {
            bool ambiguousSetting = this.setting.AmbiguousCharIsFullWidth;
            for (int line = from; line < this.editArea.Height; line++)
            {
                var docLine = line + this.document.Offset.Line;
                if (this.menu.IsShown)
                {
                    this.DrawMenu(screen, line);
                }
                else if (docLine < this.document.Lines.Count)
                {
                    this.DrawDocumentLine(screen, ambiguousSetting, docLine);
                }
                else
                {
                    this.DrawOutofBounds(screen, line);
                }
            }
        }

        void DrawDocumentLine(IScreen screen, bool ambiguousSetting, int docLine)
        {
            this.document.DrawLine(screen, docLine);
        }

        void DrawMenu(IScreen screen, int line)
        {
            var keyJoiner = " + ";
            var separator = "  ";
            var titleLineCount = 2;

            if (line - titleLineCount >= this.menu.KeyBinds.KeyBinds.Count)
            {
                screen.AppendLine(new[]
                {
                    new StyledString("~", ColorSet.OutOfBounds),
                });
            }
            else if (line == 0)
            {
                var message = "Shortcuts";
                var messageLength = message.Length;
                var leftPadding = (this.console.Width - messageLength) / 2 - 1;
                screen.AppendLine(new[]
                {
                    new StyledString("~", ColorSet.OutOfBounds),
                    new StyledString(new string(' ', leftPadding), ColorSet.OutOfBounds),
                    new StyledString(message, ColorSet.OutOfBounds),
                });
            }
            else if (line == 1)
            {
                screen.AppendLine(new[]
                {
                    new StyledString("~", ColorSet.OutOfBounds),
                });
                return;
            }
            else
            {
                var item = this.menu.KeyBinds.KeyBinds[line - titleLineCount];

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
                    if (i != 0)
                    {
                        spans.Add(new StyledString(keyJoiner, ColorSet.OutOfBounds));
                    }
                    spans.Add(new StyledString(key, ColorSet.KeyExpression));
                    i++;
                }
                screen.AppendLine(spans);
            }
        }

        void DrawOutofBounds(IScreen screen, int line)
        {
            if (this.document.IsNew && line == this.editArea.Height / 3)
            {
                var welcome = $"txte -- version {Version}";
                var welcomeLength = welcome.Length.AtMax(this.console.Width);
                var padding = (this.console.Width - welcomeLength) / 2;
                var lineBuffer = new StringBuilder();
                if (padding > 0)
                {
                    lineBuffer.Append("~");
                }
                for (int i = 1; i < padding; i++)
                {
                    lineBuffer.Append(" ");
                }
                lineBuffer.Append(welcome.Substring(0, welcomeLength));

                screen.AppendOuterLine(lineBuffer.ToString());
            }
            else
            {
                screen.AppendOuterLine("~");
            }
        }
        void DrawPromptBar(IScreen screen)
        {
            if (this.prompt.HasValue)
            {
                screen.AppendLine(this.prompt.Value.ToStyledString());
            }
        }
        void DrawSatausBar(IScreen screen)
        {
            var fileName = this.document.Path != null ? Path.GetFileName(this.document.Path) : "[New File]";
            var fileNameLength = fileName.Length.AtMax(20);
            (var clippedFileName, _, _) =
                fileName.SubRenderString(0, fileNameLength, this.setting.AmbiguousCharIsFullWidth);
            var fileInfo = $"{clippedFileName}{(this.document.IsModified ? "(*)" : "")}";
            var positionInfo = $"{this.document.RenderPosition.Line}:{this.document.RenderPosition.Column} {this.document.NewLineFormat.Name}";
            var padding = this.console.Width - fileInfo.Length - positionInfo.Length;

            screen.AppendLine(new[] { new StyledString(fileInfo + new string(' ', padding) + positionInfo, ColorSet.SystemMessage) });
        }
        void DrawMessageBar(IScreen screen)
        {
            if (!this.message.IsValid) return;

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
            screen.AppendLine(prefix.Concat(render.ToStyledStrings()));
        }

        async Task<ModalProcessResult<TResult>> Prompt<TResult>(IPrompt<TResult> prompt)
        {
            using (this.prompt.SetTemporary(prompt))
            {
                this.RefreshScreen(prompt.Cursor.OffsetPrompt(this.console.Size.Height - 1));
                await foreach (var (eventType, keyInfo) in this.console.ReadKeysOrTimeoutAsync())
                {
                    if (eventType == EventType.Timeout)
                    {
                        this.FadeMessage(prompt.Cursor.OffsetPrompt(this.console.Size.Height - 1));
                        continue;
                    }

                    var state = prompt.ProcessKey(keyInfo);
                    if (state is IModalOk || state is IModalCancel)
                    {
                        return state;
                    }

                    if (state is IModalNeedsRefreash)
                    {
                        this.RefreshScreen(prompt.Cursor.OffsetPrompt(this.console.Size.Height - 1));
                    }
                    else
                    {
                        this.RefreshScreen(prompt.Cursor.OffsetPrompt(this.console.Size.Height - 1), this.editArea.Height);
                    }
                }
            }

            // Maybe Console is dead.
            return ModalCancel.Default;
        }


        private async Task<ProcessResult> OpenMenu()
        {
            using (this.menu.ShowWhileModal())
            {
                using var message =
                    new TemporaryMessage(ColoredString.Concat(this.setting,
                        ("hint: You can omit ", ColorSet.OutOfBounds),
                        ("Crtl", ColorSet.KeyExpression),
                        (" on the menu screen", ColorSet.OutOfBounds)
                    ));
                this.message = message;

                this.RefreshScreen(null);

                await foreach (var (eventType, keyInfo) in this.console.ReadKeysOrTimeoutAsync())
                {
                    if (eventType == EventType.Timeout)
                    {
                        this.FadeMessage(null);
                        continue;
                    }
                    if (keyInfo.Key == ConsoleKey.Escape) { return ProcessResult.Running; }

                    if (this.menu.KeyBinds[keyInfo.ToKeyCombination().WithControl()] is { } function)
                    {
                        message.Expire();
                        this.menu.Hide();
                        return await function();
                    }

                    this.RefreshScreen(null);
                }
            }

            // Maybe Console is dead.
            return ProcessResult.Quit;
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
            if (!this.document.IsModified) return ProcessResult.Quit;

            var promptResult =
                await this.Prompt(
                    ChoosePrompt.Create(
                        "File has unsaved changes. Quit without saving?",
                        new[] { Choice.No, Choice.Yes }
                    )
                );

            if (promptResult is ModalOk<Choice>(var confirm) && confirm == Choice.Yes)
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

            var promptInput = await this.Prompt(new InputPrompt("Save as:", this.setting));
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

            var finder = new TextFinder(this.document.Lines);

            using (this.document.OverlapHilighter.SetTemporary(new FindingHilighter(finder)))
            {
                await this.Prompt(new FindPrompt("Search:", finder, this.document, this.setting));
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
                        ("hint: Set according to your terminal font. Default is estimated from your terminal", ColorSet.OutOfBounds)));
            this.message = message;
            var modalResult =
                await this.Prompt(
                    ChoosePrompt.Create(
                        "Change East Asian Width - Ambiguous:",
                        EAWAmbiguousFormat.All,
                        this.setting.AmbiguousFormat
                    )
                );
            if (modalResult is ModalOk<EAWAmbiguousFormat>(var selection))
            {
                this.setting.SetAmbiguousCharWidthFormat(this.console, selection);
                if (selection.IsFullWidth.HasValue)
                {
                    this.message =
                        new Message(ColoredString.Concat(this.setting,
                            ($"East Asian Width - Ambiguous = {selection}", ColorSet.OutOfBounds)));
                }
                else
                {
                    this.message =
                        new Message(ColoredString.Concat(this.setting,
                            (
                                $"East Asian Width - Ambiguous = Default (Estimated to {((this.console.ShowsAmbiguousCharAsFullWidth) ? "Full-Width" : "Half-Width")})",
                                ColorSet.OutOfBounds
                            )
                        ));
                }
            }

            return ProcessResult.Running;
        }
    }
}
