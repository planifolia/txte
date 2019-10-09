using System.Threading.Tasks;

namespace txte
{
    enum KeyProcessingResults
    {
        Running,
        Quit,
        Unhandled,
    }
    static class KeyProcessingTaskResult
    {
        public static readonly Task<KeyProcessingResults> Running = 
            new ValueTask<KeyProcessingResults>(KeyProcessingResults.Running).AsTask();
        public static readonly Task<KeyProcessingResults> Quit = 
            new ValueTask<KeyProcessingResults>(KeyProcessingResults.Quit).AsTask();
        public static readonly Task<KeyProcessingResults> Unhandled = 
            new ValueTask<KeyProcessingResults>(KeyProcessingResults.Unhandled).AsTask();
    }
}