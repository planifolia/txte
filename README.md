# txte - a terminal-based text editor fot .net

## Supported .net version

* .net 5.0+

## How to use as dotnet global tool

Install txte as dotnet global toosl from github with [dotnet-git-tool](https://github.com/yaegaki/dotnet-git-tool).

```sh
# install
dotnet tool install -g dotnet-git-tool
dotnet git-tool install github.com/planifolia/txte/txte

# edit a new file
txte

# open and edit the file 
txte path/to/text.file

# uninstall
dotnet tool uninstall -g txte
dotnet tool uninstall -g dotnet-git-tool
```

## Key bindings

### Commands

The following commands perform with the <kbd>Ctrl</kbd>+`<key>` or <kbd>Esc</kbd> → `<key>` sequence, so that it works even if the terminal has hotkeys with the same key bindings.

| Comand | Key binding | On Menu |
|---|---|---|
| Open menu | <kbd>Esc</kbd> | (<kbd>Esc</kbd> to close menu) |
| Quit | <kbd>Ctrl</kbd>+<kbd>Q</kbd> | <kbd>Q</kbd> |
| Open | <kbd>Ctrl</kbd>+<kbd>O</kbd> | <kbd>O</kbd> |
| Save | <kbd>Ctrl</kbd>+<kbd>S </kbd>| <kbd>S</kbd> |
| Save as | <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>S | <kbd>Shift</kbd>+<kbd>S</kbd> |
| Close | <kbd>Ctrl</kbd>+<kbd>W</kbd> | <kbd>W</kbd> |
| Search | <kbd>Ctrl</kbd>+<kbd>F</kbd> | <kbd>F</kbd> |
| Go to line | <kbd>Ctrl</kbd>+<kbd>G</kbd> | <kbd>G</kbd> |
| Move to start of file | <kbd>Ctrl</kbd>+<kbd>Home</kbd> | <kbd>Home</kbd> |
| Move to end of file | <kbd>Ctrl</kbd>+<kbd>End</kbd> | <kbd>End</kbd> |
| Refresh screen | <kbd>Ctrl</kbd>+<kbd>L</kbd> | <kbd>L</kbd> |
| Select End of Line | <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>L</kbd> | <kbd>Shift</kbd>+<kbd>L</kbd> |
| Select East Asian Width | <kbd>Ctrl</kbd>+<kbd>Shift</kbd>+<kbd>E</kbd> | <kbd>Shift</kbd>+<kbd>E</kbd> |

### Other key bindings

| Action | Key binding |
|---|---|
| Move to start of line | <kbd>Home</kbd> |
| Move to end of lline | <kbd>End</kbd> |
| Scroll up to previous page | <kbd>PageUp</kbd> |
| Scroll down to next page | <kbd>PageDown</kbd> |
| Move cursor up quarter page | <kbd>Ctrl</kbd>+<kbd>↑</kbd> |
| Move cursor down quarter page | <kbd>Ctrl</kbd>+<kbd>↓</kbd> |

## Special Thanks

This project refer to the wonderful minimal text editor [kilo](https://github.com/antirez/kilo) and [Build Your Own Text Editor](https://viewsourcecode.org/snaptoken/kilo/) tutorial!
