using System;
using System.Linq;
using System.Threading.Tasks;

namespace txte
{
    class Program
    {

        static async Task<int> Main(string[] args)
        {
            var options = args.Where(x => x.Length == 2 && x[0] == '-').ToArray();
            var arguments = args.Except(options).ToArray();
            try
            {
                using var console = 
                    (options.Contains("-e")) ? (IConsole) new ConsoleWithEscapeSequence()
                    : new CoreConsole();

                var setting = new EditorSetting();
                var document =
                    (arguments.Length >= 1) ? await Document.OpenAsync(arguments[0], setting)
                    : new Document();
                var editor = new Editor(console, setting, document, new Message("hint: Esc Key to show menu."));
                await editor.RunAsync();
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
