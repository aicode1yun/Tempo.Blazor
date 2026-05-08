using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmSigningAttachmentStepTests : LocalizationTestBase
{
    [Fact]
    public void Render_ImageStep_RendersUploadInput()
    {
        var cut = RenderComponent<TmSigningAttachmentStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Photo", Type = SigningFieldType.Image }));

        cut.Find(".tm-signing-attachment-step__input").Should().NotBeNull();
    }

    [Fact]
    public void Render_ImageAttachment_ShowsPreview()
    {
        var cut = RenderComponent<TmSigningAttachmentStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Photo", Type = SigningFieldType.Image })
            .Add(p => p.Attachments, [new TmSigningStepAttachment { Name = "photo.png", Url = "/photo.png" }]));

        cut.Find("img.tm-signing-attachment-step__preview").GetAttribute("src").Should().Be("/photo.png");
    }

    [Fact]
    public void Render_FileStep_AllowsMultipleUpload()
    {
        var cut = RenderComponent<TmSigningAttachmentStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Files", Type = SigningFieldType.File })
            .Add(p => p.AllowMultiple, true));

        cut.Find("input[type='file']").HasAttribute("multiple").Should().BeTrue();
    }

    [Fact]
    public void RemoveAttachment_InvokesAttachmentsChanged()
    {
        IReadOnlyList<TmSigningStepAttachment>? captured = null;
        var cut = RenderComponent<TmSigningAttachmentStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Files", Type = SigningFieldType.File })
            .Add(p => p.Attachments, [new TmSigningStepAttachment { Uuid = "file-1", Name = "contract.pdf" }])
            .Add(p => p.AttachmentsChanged, EventCallback.Factory.Create<IReadOnlyList<TmSigningStepAttachment>>(this, value => captured = value)));

        cut.Find(".tm-signing-attachment-step__remove").Click();

        captured.Should().BeEmpty();
    }

    [Fact]
    public void Render_Stamp_ShowsPlaceholderOrValue()
    {
        var cut = RenderComponent<TmSigningAttachmentStep>(parameters => parameters
            .Add(p => p.Field, new SigningField { Name = "Stamp", Type = SigningFieldType.Stamp })
            .Add(p => p.StampValue, "STAMP-001"));

        cut.Find(".tm-signing-attachment-step__stamp").TextContent.Should().Contain("STAMP-001");
    }
}
