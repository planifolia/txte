using System;

namespace txte
{
    class Program
    {

        static int Main(string[] args)
        {
            try
            {
                var setting = new EditorSetting();
                var document =
                    (args.Length >= 1) ? Document.Open(args[0], setting)
                    : new Document();
                var editor = new Editor(console, setting);
                editor.SetDocument(document);
                editor.SetStatusMessage("HELP: Ctrl-Q to quit, Alt-A to switch EAW ambiguous width...");
                editor.Run();
                return 0;
            }
            catch(EditorException ex)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }
    }
}
