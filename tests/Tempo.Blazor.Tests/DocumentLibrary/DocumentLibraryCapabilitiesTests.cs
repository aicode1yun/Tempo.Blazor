using Tempo.Blazor.DocumentLibrary;

namespace Tempo.Blazor.Tests.DocumentLibrary;

/// <summary>
/// Tests for <see cref="DocumentLibraryCapabilities"/> — the flags the open dialog reads
/// to decide which management affordances (new folder / rename / delete / search) to show.
/// </summary>
public class DocumentLibraryCapabilitiesTests
{
    [Fact]
    public void None_HasNoFlags()
    {
        var caps = DocumentLibraryCapabilities.None;

        caps.HasFlag(DocumentLibraryCapabilities.CreateFolder).Should().BeFalse();
        caps.HasFlag(DocumentLibraryCapabilities.Rename).Should().BeFalse();
        caps.HasFlag(DocumentLibraryCapabilities.Delete).Should().BeFalse();
        caps.HasFlag(DocumentLibraryCapabilities.Search).Should().BeFalse();
    }

    [Fact]
    public void Flags_Combine_AndAreDetectedIndividually()
    {
        var caps = DocumentLibraryCapabilities.CreateFolder | DocumentLibraryCapabilities.Delete;

        caps.HasFlag(DocumentLibraryCapabilities.CreateFolder).Should().BeTrue();
        caps.HasFlag(DocumentLibraryCapabilities.Delete).Should().BeTrue();
        caps.HasFlag(DocumentLibraryCapabilities.Rename).Should().BeFalse();
        caps.HasFlag(DocumentLibraryCapabilities.Search).Should().BeFalse();
    }

    [Fact]
    public void All_IncludesEveryFlag()
    {
        var all = DocumentLibraryCapabilities.All;

        all.Should().HaveFlag(DocumentLibraryCapabilities.CreateFolder);
        all.Should().HaveFlag(DocumentLibraryCapabilities.Rename);
        all.Should().HaveFlag(DocumentLibraryCapabilities.Delete);
        all.Should().HaveFlag(DocumentLibraryCapabilities.Search);
    }
}
