using Scriban;
using Scriban.Syntax;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Templating;

/// <summary>
/// Extracts the variable paths referenced by a template by walking the parsed AST. Loop variables are
/// excluded automatically (they parse as locals, not globals); loop pseudo-objects (<c>for</c>,
/// <c>while</c>…) are ignored explicitly. Both the full dotted path and its root are reported.
/// </summary>
public static class TemplateVariableExtractor
{
    private static readonly HashSet<string> Ignored = new(StringComparer.Ordinal)
        { "for", "while", "tablerow", "loop", "this", "empty" };

    /// <summary>Returns the distinct variable paths referenced by the template (empty when it has syntax errors).</summary>
    public static IReadOnlyList<string> Extract(string template)
        => ExtractInfos(template).Select(i => i.Path).ToList();

    /// <summary>
    /// Returns the distinct variables referenced by the template, each marked as a scalar or a
    /// collection (when used as a <c>for</c> iterator). Empty when the template has syntax errors.
    /// </summary>
    public static IReadOnlyList<TemplateVariableInfo> ExtractInfos(string template)
    {
        var parsed = Template.Parse(template);
        if (parsed.HasErrors || parsed.Page is null) return Array.Empty<TemplateVariableInfo>();

        var visitor = new VariableVisitor();
        visitor.Visit(parsed.Page);
        return visitor.BuildInfos();
    }

    private sealed class VariableVisitor : ScriptVisitor
    {
        private readonly List<string> _results = new();
        private readonly HashSet<string> _seen = new(StringComparer.Ordinal);
        private readonly HashSet<string> _collections = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _locals = new(StringComparer.Ordinal);

        public IReadOnlyList<TemplateVariableInfo> BuildInfos()
            => _results.Select(path => new TemplateVariableInfo(
                path, _collections.Contains(path) ? VariableKind.Collection : VariableKind.Scalar)).ToList();

        public override void Visit(ScriptVariableGlobal node) => RecordRoot(node.Name);

        public override void Visit(ScriptForStatement node)
        {
            if (node.Iterator is not null && TryResolve(node.Iterator, out var path, out var root))
            {
                _collections.Add(path);
                Record(root, path);
            }
            else if (node.Iterator is not null)
            {
                Visit(node.Iterator);
            }

            var localName = (node.Variable as ScriptVariable)?.Name;
            if (localName is not null) PushLocal(localName);
            if (node.Body is not null) Visit(node.Body);
            if (localName is not null) PopLocal(localName);
            if (node.Else is not null) Visit(node.Else);
        }

        private void PushLocal(string name)
            => _locals[name] = _locals.TryGetValue(name, out var c) ? c + 1 : 1;

        private void PopLocal(string name)
        {
            if (_locals.TryGetValue(name, out var c) && c > 1) _locals[name] = c - 1;
            else _locals.Remove(name);
        }

        private bool IsLocal(string root) => _locals.ContainsKey(root);

        public override void Visit(ScriptMemberExpression node)
        {
            if (TryResolve(node, out var path, out var root))
                Record(root, path);
            else
                base.Visit(node);
        }

        public override void Visit(ScriptIndexerExpression node)
        {
            if (TryResolve(node, out var path, out var root))
                Record(root, path);
            else
                base.Visit(node.Target);

            if (node.Index is not null) Visit(node.Index); // the index expression may reference variables
        }

        private static bool TryResolve(ScriptExpression expression, out string path, out string root)
        {
            path = string.Empty;
            root = string.Empty;
            var parts = new List<string>();
            ScriptExpression? current = expression;
            while (current is not null)
            {
                switch (current)
                {
                    case ScriptMemberExpression { Member.Name: { } memberName } member:
                        parts.Add(memberName);
                        current = member.Target;
                        break;
                    case ScriptIndexerExpression indexer:
                        current = indexer.Target; // the index itself is visited separately
                        break;
                    case ScriptVariableGlobal global:
                        root = global.Name;
                        parts.Add(global.Name);
                        parts.Reverse();
                        path = string.Join('.', parts);
                        return true;
                    default:
                        return false; // target is not a plain variable (e.g. a function call)
                }
            }
            return false;
        }

        private void Record(string root, string path)
        {
            if (Ignored.Contains(root) || IsLocal(root)) return;
            Add(root);
            Add(path);
        }

        private void RecordRoot(string name)
        {
            if (Ignored.Contains(name) || IsLocal(name)) return;
            Add(name);
        }

        private void Add(string value)
        {
            if (_seen.Add(value)) _results.Add(value);
        }
    }
}
