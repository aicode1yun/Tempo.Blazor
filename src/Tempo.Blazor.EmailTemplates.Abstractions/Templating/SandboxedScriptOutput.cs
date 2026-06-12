using System.Diagnostics;
using System.Text;
using Scriban.Runtime;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Templating;

/// <summary>
/// A Scriban output that enforces the output-length and wall-clock limits cooperatively on every
/// write, so a runaway template that keeps producing output is aborted promptly.
/// </summary>
internal sealed class SandboxedScriptOutput : IScriptOutput
{
    private readonly StringBuilder _builder = new();
    private readonly int _maxLength;
    private readonly TimeSpan _timeout;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    public SandboxedScriptOutput(int maxLength, TimeSpan timeout)
    {
        _maxLength = maxLength;
        _timeout = timeout;
    }

    public void Write(string text, int offset, int count)
    {
        if (_stopwatch.Elapsed > _timeout)
            throw new TimeoutException("Template rendering exceeded the allowed time.");
        if (_builder.Length + count > _maxLength)
            throw new InvalidOperationException("Template output exceeded the maximum allowed length.");
        _builder.Append(text, offset, count);
    }

    public ValueTask WriteAsync(string text, int offset, int count, CancellationToken cancellationToken)
    {
        Write(text, offset, count);
        return default;
    }

    public override string ToString() => _builder.ToString();
}
