using System;

namespace cilo
{
    class Program
    {

        static int Main(string[] args)
        {
            //Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            try
            {
                var document =
                    (args.Length >= 1) ? Document.Open(args[0])
                    : new Document();
                var console = new ConsoleWithEscapeSequence();
                var editor = new Editor(console);
                editor.SetDocument(document);
                editor.SetStatusMessage("HELP: Ctrl-Q to quit, Esc to open menu...");
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
