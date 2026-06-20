using FluentAssertions;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmAuditTrailViewerTests : LocalizationTestBase
{
    [Fact]
    public void Render_DocumentsSignersNetworkVerificationAndAuditPdf()
    {
        var cut = RenderComponent<TmAuditTrailViewer>(parameters => parameters
            .Add(p => p.Trail, CreateTrail()));

        cut.Markup.Should().Contain("contract.pdf");
        cut.Markup.Should().Contain("sha256-contract");
        cut.Markup.Should().Contain("Alex Johnson");
        cut.Markup.Should().Contain("alex@example.test");
        cut.Markup.Should().Contain("203.0.113.10");
        cut.Markup.Should().Contain("Europe/Prague");
        cut.Markup.Should().Contain("SMS");
        cut.Find(".tm-audit-trail-viewer__audit-pdf").GetAttribute("href").Should().Be("/audit.pdf");
    }

    [Fact]
    public void Render_AuditEvents()
    {
        var cut = RenderComponent<TmAuditTrailViewer>(parameters => parameters
            .Add(p => p.Trail, CreateTrail()));

        cut.Markup.Should().Contain("Document opened");
        cut.Markup.Should().Contain("Mozilla");
    }

    [Fact]
    public void Render_LocalizationSnapshotShowsCultureFallbackAndResolvedLabels()
    {
        var cut = RenderComponent<TmAuditTrailViewer>(parameters => parameters
            .Add(p => p.Trail, CreateTrail())
            .Add(p => p.LocalizationSnapshot, CreateLocalizationSnapshot()));

        cut.Markup.Should().Contain("Signing culture");
        cut.Markup.Should().Contain("en-US");
        cut.Markup.Should().Contain("cs-CZ");
        cut.Markup.Should().Contain("Original PDF language");
        cut.Markup.Should().Contain("Full name");
        cut.Markup.Should().Contain("Email");
        cut.Find("[data-field-uuid='recipient-name']").TextContent.Should().Contain("Full name");
    }

    private static SigningAuditTrail CreateTrail()
    {
        return new SigningAuditTrail
        {
            AuditPdfUrl = "/audit.pdf",
            Documents =
            [
                new SigningAuditTrailDocument
                {
                    FileName = "contract.pdf",
                    Checksum = "sha256-contract"
                }
            ],
            Signers =
            [
                new SigningAuditTrailSigner
                {
                    FullName = "Alex Johnson",
                    Email = "alex@example.test",
                    IpAddress = "203.0.113.10",
                    UserAgent = "Mozilla",
                    TimeZone = "Europe/Prague",
                    VerificationMethod = "SMS"
                }
            ],
            Events =
            [
                new SigningAuditTrailEvent
                {
                    Label = "Document opened",
                    Actor = "Alex Johnson",
                    IpAddress = "203.0.113.10",
                    UserAgent = "Mozilla",
                    TimeZone = "Europe/Prague",
                    VerificationMethod = "SMS"
                }
            ]
        };
    }

    private static SigningSubmissionLocalizationSnapshot CreateLocalizationSnapshot()
    {
        return new SigningSubmissionLocalizationSnapshot
        {
            Culture = "en-US",
            FallbackCulture = "cs-CZ",
            PdfContentTranslated = false,
            Fields =
            [
                new SigningSubmissionFieldLocalizationSnapshot
                {
                    FieldUuid = "recipient-name",
                    Label = "Full name",
                    Title = "Recipient full name",
                    Options =
                    [
                        new SigningSubmissionOptionLocalizationSnapshot
                        {
                            OptionUuid = "delivery-email",
                            Value = "email",
                            Label = "Email"
                        }
                    ]
                }
            ]
        };
    }
}
