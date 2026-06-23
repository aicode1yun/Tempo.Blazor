using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Localization;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Resources;

namespace Tempo.Reporting.Abstractions.Validation;

/// <summary>Validates report definitions with localized messages and stable error codes.</summary>
public sealed class ReportDefinitionValidator : AbstractValidator<ReportDefinition>
{
    private readonly IStringLocalizer<ReportingValidationResources> _localizer;

    /// <summary>Creates a report definition validator.</summary>
    public ReportDefinitionValidator(IStringLocalizer<ReportingValidationResources> localizer)
    {
        _localizer = localizer;

        RuleFor(x => x.SchemaVersion)
            .Equal(ReportDefinition.CurrentSchemaVersion)
            .WithErrorCode("ReportDefinition.SchemaVersion.Unsupported")
            .WithMessage(_ => _localizer["ReportDefinition_SchemaVersion_Unsupported"]);

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("ReportDefinition.Name.Required")
            .WithMessage(_ => _localizer["ReportDefinition_Name_Required"])
            .MaximumLength(200)
            .WithErrorCode("ReportDefinition.Name.TooLong")
            .WithMessage(_ => _localizer["ReportDefinition_Name_TooLong"]);

        RuleFor(x => x.PageSetup)
            .Custom(ValidatePageSetup);

        RuleForEach(x => x.Parameters)
            .SetValidator(new ReportParameterDefinitionValidator(localizer));

        RuleFor(x => x)
            .Custom(ValidateDefinitionGraph);
    }

    private void ValidatePageSetup(ReportPageSetup setup, ValidationContext<ReportDefinition> context)
    {
        if (setup.PageSize.Width <= 0 || setup.PageSize.Height <= 0)
        {
            AddFailure(context, "PageSetup", "ReportDefinition.PageSetup.Size", "ReportDefinition_PageSetup_Size");
        }

        var margins = setup.Margins;
        if (margins.Left < 0 ||
            margins.Top < 0 ||
            margins.Right < 0 ||
            margins.Bottom < 0 ||
            margins.Left + margins.Right >= setup.PageSize.Width ||
            margins.Top + margins.Bottom >= setup.PageSize.Height)
        {
            AddFailure(context, "PageSetup.Margins", "ReportDefinition.PageSetup.Margins", "ReportDefinition_PageSetup_Margins");
        }
    }

    private void ValidateDefinitionGraph(ReportDefinition definition, ValidationContext<ReportDefinition> context)
    {
        if (definition.Bands.Detail is null)
        {
            AddFailure(context, "Bands.Detail", "ReportDefinition.Bands.Detail.Required", "ReportDefinition_Bands_Detail_Required");
        }

        AddDuplicateNameFailure(
            context,
            definition.Parameters.Select(p => p.Name),
            "Parameters",
            "ReportDefinition.Parameters.Name.Duplicate",
            "ReportDefinition_Parameters_Name_Duplicate");

        foreach (var dataSet in definition.DataSets)
        {
            if (string.IsNullOrWhiteSpace(dataSet.Name))
            {
                AddFailure(context, "DataSets.Name", "ReportDefinition.DataSets.Name.Required", "ReportDefinition_DataSets_Name_Required");
            }
        }

        AddDuplicateNameFailure(
            context,
            definition.DataSets.Select(d => d.Name),
            "DataSets",
            "ReportDefinition.DataSets.Name.Duplicate",
            "ReportDefinition_DataSets_Name_Duplicate");

        var bands = EnumerateBands(definition.Bands).ToList();
        foreach (var band in bands)
        {
            if (band.Height < 0)
            {
                AddFailure(context, "Bands.Height", "ReportDefinition.Bands.Height", "ReportDefinition_Bands_Height");
            }
        }

        var elements = bands.SelectMany(b => b.Elements).ToList();
        foreach (var element in elements)
        {
            ValidateElement(context, element);
        }

        AddDuplicateNameFailure(
            context,
            elements.Select(e => e.Id),
            "Bands.Elements",
            "ReportDefinition.Elements.Id.Duplicate",
            "ReportDefinition_Elements_Id_Duplicate");
    }

    private void ValidateElement(ValidationContext<ReportDefinition> context, ReportElement element)
    {
        if (string.IsNullOrWhiteSpace(element.Id))
        {
            AddFailure(context, "Bands.Elements.Id", "ReportDefinition.Elements.Id.Required", "ReportDefinition_Elements_Id_Required");
        }

        if (element.X < 0 || element.Y < 0 || element.Width < 0 || element.Height < 0 || element.Width + element.Height <= 0)
        {
            AddFailure(context, "Bands.Elements.Bounds", "ReportElement.Bounds.Invalid", "ReportElement_Bounds_Invalid");
        }

        switch (element)
        {
            case ReportTextBoxElement textBox:
                if (string.IsNullOrWhiteSpace(textBox.Text) && string.IsNullOrWhiteSpace(textBox.Expression))
                {
                    AddFailure(context, "Bands.Elements.Text", "ReportTextBox.Content.Required", "ReportTextBox_Content_Required");
                }
                break;
            case ReportImageElement image:
                if (string.IsNullOrWhiteSpace(image.Source))
                {
                    AddFailure(context, "Bands.Elements.Source", "ReportImage.Source.Required", "ReportImage_Source_Required");
                }
                break;
            case ReportTableElement table:
                if (table.Columns.Count == 0)
                {
                    AddFailure(context, "Bands.Elements.Columns", "ReportTable.Columns.Required", "ReportTable_Columns_Required");
                }
                break;
            case ReportChartElement chart:
                if (chart.Series.Count == 0)
                {
                    AddFailure(context, "Bands.Elements.Series", "ReportChart.Series.Required", "ReportChart_Series_Required");
                }

                foreach (var series in chart.Series)
                {
                    if (string.IsNullOrWhiteSpace(series.CategoryExpression))
                    {
                        AddFailure(context, "Bands.Elements.Series.CategoryExpression", "ReportChart.Series.CategoryExpression.Required", "ReportChart_Series_CategoryExpression_Required");
                    }

                    if (string.IsNullOrWhiteSpace(series.ValueExpression))
                    {
                        AddFailure(context, "Bands.Elements.Series.ValueExpression", "ReportChart.Series.ValueExpression.Required", "ReportChart_Series_ValueExpression_Required");
                    }
                }

                break;
            case ReportSubReportElement subReport:
                if (string.IsNullOrWhiteSpace(subReport.ReportId))
                {
                    AddFailure(context, "Bands.Elements.ReportId", "ReportSubReport.ReportId.Required", "ReportSubReport_ReportId_Required");
                }
                break;
        }
    }

    private static IEnumerable<ReportBand> EnumerateBands(ReportBandCollection bands)
    {
        if (bands.ReportHeader is not null)
        {
            yield return bands.ReportHeader;
        }

        if (bands.ReportFooter is not null)
        {
            yield return bands.ReportFooter;
        }

        if (bands.PageHeader is not null)
        {
            yield return bands.PageHeader;
        }

        if (bands.PageFooter is not null)
        {
            yield return bands.PageFooter;
        }

        if (bands.Detail is not null)
        {
            yield return bands.Detail;
        }

        foreach (var group in bands.Groups)
        {
            if (group.GroupHeader is not null)
            {
                yield return group.GroupHeader;
            }

            if (group.GroupFooter is not null)
            {
                yield return group.GroupFooter;
            }
        }
    }

    private void AddDuplicateNameFailure(
        ValidationContext<ReportDefinition> context,
        IEnumerable<string> names,
        string propertyName,
        string errorCode,
        string resourceKey)
    {
        var hasDuplicate = names
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .GroupBy(name => name, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);

        if (hasDuplicate)
        {
            AddFailure(context, propertyName, errorCode, resourceKey);
        }
    }

    private void AddFailure(
        ValidationContext<ReportDefinition> context,
        string propertyName,
        string errorCode,
        string resourceKey)
    {
        context.AddFailure(new ValidationFailure(propertyName, _localizer[resourceKey])
        {
            ErrorCode = errorCode,
        });
    }
}
