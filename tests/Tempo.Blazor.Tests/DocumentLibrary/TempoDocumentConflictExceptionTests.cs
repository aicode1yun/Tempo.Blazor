using Tempo.Blazor.DocumentLibrary;

namespace Tempo.Blazor.Tests.DocumentLibrary;

/// <summary>
/// Tests for <see cref="TempoDocumentConflictException"/> — thrown when an optimistic
/// concurrency token (<c>expectedModifiedAt</c>) no longer matches the stored document.
/// </summary>
public class TempoDocumentConflictExceptionTests
{
    [Fact]
    public void CarriesKindIdAndCurrentModifiedAt()
    {
        var id = Guid.NewGuid();
        var current = new DateTime(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);

        var ex = new TempoDocumentConflictException(TempoDocumentKind.Wireframe, id, current);

        ex.Kind.Should().Be(TempoDocumentKind.Wireframe);
        ex.DocumentId.Should().Be(id);
        ex.CurrentModifiedAt.Should().Be(current);
    }

    [Fact]
    public void IsAnException_WithMeaningfulMessage()
    {
        var ex = new TempoDocumentConflictException(
            TempoDocumentKind.Diagram, Guid.NewGuid(), DateTime.UtcNow);

        ex.Should().BeAssignableTo<Exception>();
        ex.Message.Should().Contain("modified");
    }
}
