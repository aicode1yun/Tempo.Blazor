using Xunit;

namespace Tempo.Blazor.Tests.Components.Spreadsheet;

/// <summary>
/// Groups every test class that touches the process-wide static <c>SpreadsheetClipboard</c> into a
/// single xUnit collection so they never run in parallel with one another (which would race on the
/// shared clipboard state).
/// </summary>
[CollectionDefinition("SpreadsheetClipboard")]
public sealed class SpreadsheetClipboardCollection;
