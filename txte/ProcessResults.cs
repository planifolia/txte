using System.Threading.Tasks;

namespace txte
{
    enum ProcessResult
    {
        Running,
        Quit,
        Unhandled,
    }
    static class ProcessTaskResult
    {
        public static readonly Task<ProcessResult> Running = 
            new ValueTask<ProcessResult>(ProcessResult.Running).AsTask();
        public static readonly Task<ProcessResult> Quit = 
            new ValueTask<ProcessResult>(ProcessResult.Quit).AsTask();
        public static readonly Task<ProcessResult> Unhandled = 
            new ValueTask<ProcessResult>(ProcessResult.Unhandled).AsTask();
    }

    enum ModalProcessResult
    {
        NeedsRefreash,
        Running,
        Ok,
        Cancel,
        Unhandled,
    }
}