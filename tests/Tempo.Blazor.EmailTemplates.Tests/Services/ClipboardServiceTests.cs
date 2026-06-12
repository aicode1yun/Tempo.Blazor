using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;
using Tempo.Blazor.EmailTemplates.Services;

namespace Tempo.Blazor.EmailTemplates.Tests.Services;

public class ClipboardServiceTests
{
    [Fact]
    public void New_CannotPaste()
    {
        new ClipboardService().CanPaste.Should().BeFalse();
    }

    [Fact]
    public void Copy_ThenPaste_ReturnsEquivalentBlockWithNewId()
    {
        var clipboard = new ClipboardService();
        var original = new EmailTextBlock { Content = "hello" };

        clipboard.Copy(original);
        clipboard.CanPaste.Should().BeTrue();

        var pasted = clipboard.Paste()!;
        pasted.Should().BeOfType<EmailTextBlock>();
        ((EmailTextBlock)pasted).Content.Should().Be("hello");
        pasted.Id.Should().NotBe(original.Id);
    }

    [Fact]
    public void Paste_TwiceGivesDistinctIds()
    {
        var clipboard = new ClipboardService();
        clipboard.Copy(new EmailButtonBlock { Text = "x" });

        var first = clipboard.Paste()!;
        var second = clipboard.Paste()!;

        first.Id.Should().NotBe(second.Id);
    }

    [Fact]
    public void Copy_StoresIndependentSnapshot()
    {
        var clipboard = new ClipboardService();
        var original = new EmailTextBlock { Content = "v1" };
        clipboard.Copy(original);

        original.Content = "changed";

        ((EmailTextBlock)clipboard.Paste()!).Content.Should().Be("v1");
    }
}
