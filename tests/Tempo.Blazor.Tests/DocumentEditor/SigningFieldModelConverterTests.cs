using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.Tests.DocumentEditor;

/// <summary>
/// Maps the canvas engine's signing field descriptors (one per field, with normalized 0..1 areas) into
/// the shared <see cref="SigningField"/> model (plan S2.15/S2.16). A body field has one area; a
/// header/footer field has many — all on the one field. The attachment uuid is applied to every area.
/// </summary>
public class SigningFieldModelConverterTests
{
    [Fact]
    public void ToSigningFields_MapsCorePropertiesAndAreas()
    {
        var descriptors = new[]
        {
            new DocumentSigningFieldDescriptor
            {
                Uuid = "field-1",
                FieldType = "signature",
                SubmitterUuid = "signer",
                Required = true,
                Label = "Signature",
                Areas = [new DocumentSigningFieldAreaDescriptor { Page = 0, X = 0.1, Y = 0.8, Width = 0.3, Height = 0.06 }],
            },
        };

        var field = descriptors.ToSigningFields("editor-export").Single();

        field.Uuid.Should().Be("field-1");
        field.Type.Should().Be(SigningFieldType.Signature);
        field.SubmitterUuid.Should().Be("signer");
        field.Required.Should().BeTrue();
        field.Title.Should().Be("Signature");
        field.Areas.Should().ContainSingle();
        field.Areas[0].AttachmentUuid.Should().Be("editor-export");
        field.Areas[0].Page.Should().Be(0);
        field.Areas[0].X.Should().BeApproximately(0.1, 1e-9);
        field.Areas[0].Width.Should().BeApproximately(0.3, 1e-9);
    }

    [Fact]
    public void ToSigningFields_AppliesAttachmentToEveryAreaOfAMultiAreaField()
    {
        var descriptors = new[]
        {
            new DocumentSigningFieldDescriptor
            {
                Uuid = "footer-field",
                FieldType = "initials",
                SubmitterUuid = "signer",
                Areas =
                [
                    new DocumentSigningFieldAreaDescriptor { Page = 0, X = 0.4, Y = 0.95, Width = 0.12, Height = 0.04 },
                    new DocumentSigningFieldAreaDescriptor { Page = 1, X = 0.4, Y = 0.95, Width = 0.12, Height = 0.04 },
                    new DocumentSigningFieldAreaDescriptor { Page = 2, X = 0.4, Y = 0.95, Width = 0.12, Height = 0.04 },
                ],
            },
        };

        var field = descriptors.ToSigningFields("editor-export").Single();

        field.Type.Should().Be(SigningFieldType.Initials);
        field.Areas.Should().HaveCount(3);
        field.Areas.Select(area => area.Page).Should().ContainInOrder(0, 1, 2);
        field.Areas.Should().OnlyContain(area => area.AttachmentUuid == "editor-export");
    }

    [Theory]
    [InlineData("text", SigningFieldType.Text)]
    [InlineData("signature", SigningFieldType.Signature)]
    [InlineData("initials", SigningFieldType.Initials)]
    [InlineData("dateNow", SigningFieldType.DateNow)]
    [InlineData("number", SigningFieldType.Number)]
    [InlineData("checkbox", SigningFieldType.Checkbox)]
    [InlineData("radio", SigningFieldType.Radio)]
    [InlineData("phone", SigningFieldType.Phone)]
    [InlineData("kba", SigningFieldType.Kba)]
    public void ToSigningFields_ParsesFieldTypeCaseInsensitively(string raw, SigningFieldType expected)
    {
        var field = new[] { new DocumentSigningFieldDescriptor { Uuid = "f", FieldType = raw, Areas = [] } }
            .ToSigningFields("a")
            .Single();

        field.Type.Should().Be(expected);
    }

    [Fact]
    public void ToSigningFields_MapsChoiceOptions()
    {
        var field = new[]
        {
            new DocumentSigningFieldDescriptor
            {
                Uuid = "f", FieldType = "select", Areas = [],
                Options = [new DocumentSigningFieldOptionDescriptor { Value = "a", Label = "Option A" }],
            },
        }.ToSigningFields("a").Single();

        field.Options.Should().ContainSingle();
        field.Options[0].Value.Should().Be("a");
    }
}
