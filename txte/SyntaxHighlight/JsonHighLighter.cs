using System.Collections.Generic;
using txte.Text;

namespace txte.SyntaxHighlight
{
    class JsonHighLighter : HighlighterBase
    {
        public static JsonHighLighter Default = new JsonHighLighter();

        public override IEnumerable<string> Extensions { get; } = new[] { ".json" };
        public override string Language { get; } = "JSON";

        public override Syntex Syntax { get; } = new(new()
        {
            ["number"] = new Keyword("[+-]?\\d+(?:\\.\\d+)?(?:(?:[eE][+-]?\\d+)|(?:\\*10\\^[+-]?\\d+))?", ColorSet.SyntaxNumeric),
            ["constant"] = new Keyword("\\b(true|false|null)\\b", ColorSet.SyntaxKeyword),
            ["name"] = new Keyword("\"((?:(?!(?<!\\\\)\").)*?)(?<!\\\\)\"\\s*\\:", ColorSet.SyntaxIdentifier)
            {
                "escape-sequence",
            },
            ["string"] = new Keyword("\"((?:(?!(?<!\\\\)\").)*?)(?<!\\\\)\"", ColorSet.SyntaxString)
            {
                "escape-sequence",
            },
            ["escape-sequence"] = new Keyword("\\\\[\"\\/bfnrtu]", ColorSet.SyntaxStringKeyword),
        })
        {
            "number",
            "constant",
            "name",
            "string",
        };
    }
}