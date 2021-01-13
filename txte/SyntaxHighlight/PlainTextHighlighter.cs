using System.Collections.Generic;
using System.Linq;

namespace txte.SyntaxHighlight
{
    class PlainTextHighLighter : HighlighterBase
    {
        public static PlainTextHighLighter Default = new PlainTextHighLighter();

        public override IEnumerable<string> Extensions { get; } = Enumerable.Empty<string>();

        public override string Language { get; } = "Plain Text";

        public override Syntex Syntax { get; } = new(new());
    }
}