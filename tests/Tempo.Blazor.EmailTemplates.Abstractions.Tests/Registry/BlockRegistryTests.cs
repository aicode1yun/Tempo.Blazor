using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;
using Tempo.Blazor.EmailTemplates.Abstractions.Registry;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Registry;

public class BlockRegistryTests
{
    [Fact]
    public void GetAll_ReturnsAllFourteenBuiltInBlockTypes()
    {
        var registry = new BlockRegistry();

        var types = registry.GetAll().Select(d => d.Type).Distinct().ToList();

        types.Should().BeEquivalentTo(Enum.GetValues<BlockType>());
        registry.GetAll().Should().HaveCount(14);
        registry.GetAll().Should().OnlyContain(d =>
            !string.IsNullOrWhiteSpace(d.NameKey) && !string.IsNullOrWhiteSpace(d.Category));
    }

    [Fact]
    public void CreateInstance_ReturnsBlockWithMjmlDefaults()
    {
        var registry = new BlockRegistry();

        var button = registry.CreateInstance(BlockType.Button);

        button.Should().BeOfType<EmailButtonBlock>()
            .Which.BackgroundColor.Should().Be("#414141");
    }

    [Fact]
    public void CreateInstance_ReturnsDistinctInstancesEachCall()
    {
        var registry = new BlockRegistry();
        registry.CreateInstance(BlockType.Text).Should()
            .NotBeSameAs(registry.CreateInstance(BlockType.Text));
    }

    [Fact]
    public void RegisterCustom_AddsExternallyDefinedBlockDescriptor()
    {
        var registry = new BlockRegistry();
        registry.RegisterCustom(new BlockDescriptor(
            "quote", BlockType.Text, "block.quote.name", "quote", "content",
            () => new EmailTextBlock { Content = "<blockquote></blockquote>", FontStyle = "italic" }));

        registry.GetAll().Should().HaveCount(15);
        var quote = registry.CreateById("quote");
        quote.Should().BeOfType<EmailTextBlock>().Which.FontStyle.Should().Be("italic");
    }

    [Fact]
    public void CreateById_UnknownId_Throws()
    {
        var registry = new BlockRegistry();
        var act = () => registry.CreateById("does-not-exist");
        act.Should().Throw<ArgumentException>();
    }
}
