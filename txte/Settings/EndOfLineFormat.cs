using System;
using System.Collections.Generic;
using System.Linq;

namespace txte.Settings
{
    class EndOfLineFormat : IChoice
    {
        public static readonly EndOfLineFormat LF = new EndOfLineFormat("LF", "\n", 'l');
        public static readonly EndOfLineFormat CR = new EndOfLineFormat("CR", "\r", 'm');
        public static readonly EndOfLineFormat CRLF = new EndOfLineFormat("CRLF", "\r\n", 'w');

        public static readonly IReadOnlyList<EndOfLineFormat> All =
            new EndOfLineFormat[] { LF, CR, CRLF };

        public static EndOfLineFormat FromSequence(string sequence) => 
            sequence switch
            {
                "\n" => EndOfLineFormat.LF,
                "\r" => EndOfLineFormat.CR,
                "\r\n" => EndOfLineFormat.CRLF,
                _ => throw new Exception($"The new line format is not supported: {EndOfLineFormat.ToHexadecimals(sequence)}({sequence.Length} chars)"),
            };
        static string ToHexadecimals(string sequence) =>
            string.Concat(sequence.Select(x => $"0x{(int)x:X}"));

        public string Name { get; }
        public string Sequence { get; }

        public char Shortcut { get; }

        public override string ToString() => this.Sequence;

        private EndOfLineFormat(string name, string sequence, char shortcut)
        {
            this.Name = name;
            this.Sequence = sequence;
            this.Shortcut = shortcut;
        }
    }
}