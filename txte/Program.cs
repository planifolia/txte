using System;
using System.Linq;
using txte;
using txte.Settings;
using txte.ConsoleInterface;
using txte.TextEditor;
using txte.Text;

var options = args.Where(x => x.Length == 2 && x[0] == '-').ToArray();
var arguments = args.Except(options).ToArray();
try
{
    using var console = new CoreConsole(timeoutMillisec: 1000);
    var setting = new Setting();
    var editor = new Editor(console, setting);
    editor.ShowMessage(new Message(ColoredString.Concat(setting,
        ("hint: ", ColorSet.OutOfBounds),
        ("Esc", ColorSet.KeyExpression),
        (" to show menu", ColorSet.OutOfBounds)
    )));
    if (arguments.Length >= 1)
    {
        await editor.OpenDocumentAsync(arguments[0]);
    }
    await editor.RunAsync();
    return 0;
}
catch (EditorException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
