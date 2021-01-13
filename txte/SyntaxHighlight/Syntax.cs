using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text.RegularExpressions;
using txte.Text;

namespace txte.SyntaxHighlight
{
    interface ISyntax : IEnumerable<(int Priority, ISyntax Syntax)>
    {
        ISyntaxParentConnector? ParentConnector { get; set; }
        ISyntaxStorage Storage { get; }
        IEnumerable<string> ApplicationOrder { get; }

        bool IsEnclose { get; }

        ColorSet Color { get; }

        Match Match(string value);
        Match EndMatch(string value);
    }

    interface ISyntaxParentConnector
    {
        ISyntax? Parent { get; set; }
    }

    interface ISyntaxStorage : ISyntaxParentConnector, IDictionary<string, ISyntax>
    {

    }

    class SyntaxStorage : ISyntaxStorage
    {
        public ISyntax? Parent { get; set; }

        readonly Dictionary<string, ISyntax> inner = new();

        public ISyntax this[string key]
        {
            get => this.inner[key];
            set
            {
                value.ParentConnector = this;
                this.inner[key] = value;
            }
        }

        public ICollection<string> Keys => this.inner.Keys;

        public ICollection<ISyntax> Values => this.inner.Values;

        public int Count => this.inner.Count;

        public bool IsReadOnly => ((IDictionary<string, ISyntax>)this.inner).IsReadOnly;

        public void Add(string key, ISyntax value)
        {
            value.ParentConnector = this;
            this.inner[key] = value;
        }

        public void Add(KeyValuePair<string, ISyntax> item)
        {
            item.Value.ParentConnector = this;
            ((IDictionary<string, ISyntax>)this.inner).Add(item);
        }

        public void Clear() => this.inner.Clear();

        public bool Contains(KeyValuePair<string, ISyntax> item) => this.inner.Contains(item);

        public bool ContainsKey(string key) => this.inner.ContainsKey(key);

        public void CopyTo(KeyValuePair<string, ISyntax>[] array, int arrayIndex) => ((IDictionary<string, ISyntax>)this.inner).CopyTo(array, arrayIndex);

        public IEnumerator<KeyValuePair<string, ISyntax>> GetEnumerator() => this.inner.GetEnumerator();
        public bool Remove(string key) => this.inner.Remove(key);

        public bool Remove(KeyValuePair<string, ISyntax> item) => ((IDictionary<string, ISyntax>)this.inner).Remove(item);

        public bool TryGetValue(string key, [MaybeNullWhen(false)] out ISyntax value) => this.inner.TryGetValue(key, out value);

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    class Syntex : ISyntax, IEnumerable<ISyntax>
    {
        public Syntex(SyntaxStorage storage)
        {
            storage.Parent = this;
            this.Storage = storage;
            this.applicationOrder = new();
        }

        public ISyntaxParentConnector? ParentConnector { get; set; }
        public ISyntaxStorage Storage { get; }
        public IEnumerable<string> ApplicationOrder => applicationOrder;

        public virtual bool IsEnclose { get; } = false;

        public virtual ColorSet Color { get; } = ColorSet.Default;

        public virtual Match Match(string value) => global::System.Text.RegularExpressions.Match.Empty;
        public virtual Match EndMatch(string value) => global::System.Text.RegularExpressions.Match.Empty;

        List<string> applicationOrder;

        public void Add(string syntaxName) => this.applicationOrder.Add(syntaxName);

        public IEnumerator<(int Priority, ISyntax Syntax)> GetEnumerator() => 
            this.ApplicationOrder
            .Select((x, i) => (i, syntax: this.FindStoredSyntax(x)))
            .Where(x => x.syntax is not null).OfType<(int, ISyntax)>()
            .GetEnumerator();

        public bool Any() => this.applicationOrder.Any();

        IEnumerator<ISyntax> IEnumerable<ISyntax>.GetEnumerator() => this.ApplicationOrder.Select(x => this.FindStoredSyntax(x)).OfType<ISyntax>().GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<(int Priority, ISyntax Sintax)>)this).GetEnumerator();


        ISyntax? FindStoredSyntax(string name)
        {
            ISyntax? current = this;
            while (current is not null)
            {
                if (current.Storage.TryGetValue(name, out var syntax)) return syntax;
                current = current.ParentConnector?.Parent;
            }
            return null;
        }

    }


    class Keyword : Syntex
    {
        public Keyword(string pattern, ColorSet color) : base(new())
        {
            this.pattern = pattern;
            this.Color = color;
        }

        readonly string pattern;

        public override bool IsEnclose { get; } = false;

        public override ColorSet Color { get; }

        public override Match Match(string value) => Regex.Match(value, this.pattern);
        public override Match EndMatch(string value) => throw new InvalidOperationException();

    }

    class Enclose : Syntex
    {
        public Enclose(string beginPattern, string endPattern, ColorSet color) : base(new())
        {
            this.beginPattern = beginPattern;
            this.endPattern = endPattern;
            this.Color = color;
        }

        readonly string beginPattern;
        readonly string endPattern;

        public override bool IsEnclose { get; } = true;
        public override ColorSet Color { get; }

        public override Match Match(string value) => Regex.Match(value, this.beginPattern);
        public override Match EndMatch(string value) => Regex.Match(value, this.beginPattern);

    }

}