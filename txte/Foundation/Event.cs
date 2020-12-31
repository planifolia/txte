using System;

namespace txte
{
    class InputEvent
    {
        public InputEvent(InputEventArgs args)
        {
            this.Args = args;
            this.IsHandled = false;
        }
        public readonly InputEventArgs Args;
        public bool IsHandled;
    }
    class InputEventArgs
    {
        public InputEventArgs(EventType eventType, ConsoleKeyInfo consoleKeyInfo)
        {
            this.EventType = eventType;
            this.ConsoleKeyInfo = consoleKeyInfo;
        }

        public readonly EventType EventType;
        public readonly ConsoleKeyInfo ConsoleKeyInfo;

        public void Deconstruct(out EventType eventType, out ConsoleKeyInfo consoleKeyInfo)
        {
            eventType = this.EventType;
            consoleKeyInfo = this.ConsoleKeyInfo;
        }
    }

    enum EventType
    {
        Timeout,
        UserAction,
    }
}
