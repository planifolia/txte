using System;
using System.Threading.Tasks;

namespace txte
{
    class Program
    {

        static async Task<int> Main(string[] args)
        {
            try
            {
                var console = new CoreConsole();
                try
                {
                    var setting = new EditorSetting();
                    var document =
                        (args.Length >= 1) ? Document.Open(args[0], setting)
                        : new Document();
                    var editor = new Editor(console, setting);
                    editor.SetDocument(document);
                    editor.SetStatusMessage("HELP: Ctrl-Q to quit, Ctrl-Alt-A to switch EAW ambiguous width...");
                    await editor.Run();
                    return 0;
                }
                finally
                {
                    console.ResetColor();
                    console.Clear();
                }
            }
            catch(EditorException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}
