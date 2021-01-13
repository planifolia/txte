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
                "escape-sequence-unicode",
            },
            ["string"] = new Keyword("\"((?:(?!(?<!\\\\)\").)*?)(?<!\\\\)\"", ColorSet.SyntaxString)
            {
                "escape-sequence",
                "escape-sequence-unicode",
            },
            ["escape-sequence"] = new Keyword("\\\\[\"\\/bfnrt]", ColorSet.SyntaxStringKeyword),
            ["escape-sequence-unicode"] = new Keyword("\\\\u[0-9A-fa-f]{4}", ColorSet.SyntaxStringKeyword),
        })
        {
            "number",
            "constant",
            "name",
            "string",
        };
    }
}