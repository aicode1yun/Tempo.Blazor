using Bunit;
using FluentAssertions;
using Microsoft.AspNetCore.Components;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Components.Signing;
using Tempo.Blazor.Tests.Localization;

namespace Tempo.Blazor.Tests.Components.Signing;

public class TmPdfSignatureVerificationTests : LocalizationTestBase
{
    [Fact]
    public void Render_EmptyUploadState()
    {
        var requested = false;
        var cut = RenderComponent<TmPdfSignatureVerification>(parameters => parameters
            .Add(p => p.OnVerifyRequested, EventCallback.Factory.Create(this, () => requested = true)));

        cut.Markup.Should().Contain("Verify a signed PDF");
        cut.FindAll(".tm-icon-unknown").Should().BeEmpty();
        cut.Find(".tm-pdf-signature-verification__verify").Click();
        requested.Should().BeTrue();
    }

    [Theory]
    [InlineData(SigningPdfVerificationStatus.Loading, "Verifying PDF")]
    [InlineData(SigningPdfVerificationStatus.Verified, "PDF verified")]
    [InlineData(SigningPdfVerificationStatus.ChecksumNotFound, "Checksum not found")]
    [InlineData(SigningPdfVerificationStatus.MalformedPdf, "Malformed PDF")]
    public void Render_StatusStates(SigningPdfVerificationStatus status, string expected)
    {
        var cut = RenderComponent<TmPdfSignatureVerification>(parameters => parameters
            .Add(p => p.Result, new SigningPdfVerificationResult { Status = status }));

        cut.Markup.Should().Contain(expected);
        cut.FindAll(".tm-icon-unknown").Should().BeEmpty();
    }

    [Fact]
    public void Render_VerifiedDetailsAndSignatures()
    {
        var cut = RenderComponent<TmPdfSignatureVerification>(parameters => parameters
            .Add(p => p.Result, new SigningPdfVerificationResult
            {
                Status = SigningPdfVerificationStatus.Verified,
                FileName = "signed.pdf",
                Checksum = "sha256-demo",
                Signatures =
                [
                    new SigningPdfSignatureInfo
                    {
                        SignerName = "Alex Johnson",
                        SignerEmail = "alex@example.test",
                        VerificationMethod = "SMS",
                        CertificateSubject = "CN=Alex"
                    }
                ]
            }));

        cut.Markup.Should().Contain("signed.pdf");
        cut.Markup.Should().Contain("sha256-demo");
        cut.Markup.Should().Contain("Alex Johnson");
        cut.Markup.Should().Contain("CN=Alex");
    }
}
