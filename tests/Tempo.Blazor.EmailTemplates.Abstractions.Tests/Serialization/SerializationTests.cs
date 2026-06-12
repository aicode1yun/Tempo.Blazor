using Tempo.Blazor.EmailTemplates.Abstractions.Model;
using Tempo.Blazor.EmailTemplates.Abstractions.Model.Blocks;
using Tempo.Blazor.EmailTemplates.Abstractions.Serialization;

namespace Tempo.Blazor.EmailTemplates.Abstractions.Tests.Serialization;

public class SerializationTests
{
    [Fact]
    public void RoundTrip_AllBlockTypes_PreservesStructureAndValues()
    {
        var doc = SampleDocuments.FullyPopulated();

        var json = EmailTemplateSerializer.Serialize(doc);
        var back = EmailTemplateSerializer.Deserialize(json);

        // structural identity
        SampleDocuments.CountAllBlocks(back).Should().Be(SampleDocuments.CountAllBlocks(doc));
        back.Subject.Should().Be(doc.Subject);
        back.Language.Should().Be("en");
        back.Styles.Fonts.Should().ContainSingle().Which.Name.Should().Be("Roboto");
        back.Styles.Styles[0].Inline.Should().BeTrue();

        var col = back.Sections[0].Columns[0];
        col.Blocks[0].Should().BeOfType<EmailTextBlock>()
            .Which.Content.Should().Be("<b>hi</b>");
        col.Blocks[0].VisibleWhen.Should().Be("is_premium");
        col.Blocks.OfType<EmailTableBlock>().Single().Rows[0].Cells[0].ColSpan.Should().Be(2);
        col.Blocks.OfType<EmailHeroBlock>().Single().Blocks.Should().ContainSingle();
        col.Blocks.OfType<EmailWrapperBlock>().Single()
            .Sections[0].Columns[0].Blocks[0].Should().BeOfType<EmailTextBlock>();
    }

    [Fact]
    public void Serialize_WritesDiscriminatorToken()
    {
        var doc = new EmailTemplateDocument();
        var sec = new EmailSection();
        var col = new EmailColumn();
        col.Blocks.Add(new EmailButtonBlock { Text = "x" });
        sec.Columns.Add(col);
        doc.Sections.Add(sec);

        var json = EmailTemplateSerializer.Serialize(doc);

        json.Should().Contain("\"$type\":\"button\"");
    }

    [Fact]
    public void Deserialize_UnknownBlockType_ThrowsClearError_NotCrash()
    {
        const string json = """
        {"sections":[{"columns":[{"blocks":[{"$type":"hologram","content":"x"}]}]}]}
        """;

        var act = () => EmailTemplateSerializer.Deserialize(json);

        act.Should().Throw<EmailTemplateSerializationException>()
            .Which.Message.Should().Contain("hologram");
    }

    [Fact]
    public void Deserialize_UnknownProperties_AreIgnored_ForwardCompat()
    {
        const string json = """
        {"subject":"S","futureField":123,"sections":[{"columns":[{"blocks":[
        {"$type":"text","content":"hi","brandNewProp":"ignored"}]}]}]}
        """;

        var doc = EmailTemplateSerializer.Deserialize(json);

        doc.Subject.Should().Be("S");
        doc.Sections[0].Columns[0].Blocks[0].Should().BeOfType<EmailTextBlock>()
            .Which.Content.Should().Be("hi");
    }

    [Fact]
    public void Deserialize_MalformedJson_ThrowsClearError()
    {
        var act = () => EmailTemplateSerializer.Deserialize("{ not json ");
        act.Should().Throw<EmailTemplateSerializationException>();
    }

    [Fact]
    public void Deserialize_UnicodeAndEmoji_Survive()
    {
        var doc = new EmailTemplateDocument { Subject = "Příliš žluťoučký 🐎 kůň" };
        var back = EmailTemplateSerializer.Deserialize(EmailTemplateSerializer.Serialize(doc));
        back.Subject.Should().Be("Příliš žluťoučký 🐎 kůň");
    }
}
