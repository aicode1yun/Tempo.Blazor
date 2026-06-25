using System.Text.RegularExpressions;
using FluentValidation;
using Microsoft.Extensions.Localization;
using Tempo.Reporting.Abstractions.Definitions;
using Tempo.Reporting.Abstractions.Resources;

namespace Tempo.Reporting.Abstractions.Validation;

/// <summary>Validates report parameter definitions.</summary>
public sealed class ReportParameterDefinitionValidator : AbstractValidator<ReportParameterDefinition>
{
    private static readonly Regex ParameterNameRegex = new(
        "^[A-Za-z_][A-Za-z0-9_]*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    /// <summary>Creates a parameter validator.</summary>
    public ReportParameterDefinitionValidator(IStringLocalizer<ReportingValidationResources> localizer)
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithErrorCode("ReportParameter.Name.Required")
            .WithMessage(_ => localizer["ReportParameter_Name_Required"])
            .Must(name => string.IsNullOrWhiteSpace(name) || ParameterNameRegex.IsMatch(name))
            .WithErrorCode("ReportParameter.Name.Invalid")
            .WithMessage(_ => localizer["ReportParameter_Name_Invalid"]);

        RuleFor(x => x)
            .Must(x => !x.AllowMultipleValues || x.DataType == ReportParameterType.List)
            .WithErrorCode("ReportParameter.Multiple.RequiresList")
            .WithMessage(_ => localizer["ReportParameter_Multiple_RequiresList"]);

        RuleFor(x => x)
            .Must(x => !x.Hidden || !string.IsNullOrWhiteSpace(x.DefaultExpression))
            .WithErrorCode("ReportParameter.Hidden.RequiresDefault")
            .WithMessage(_ => localizer["ReportParameter_Hidden_RequiresDefault"]);

        RuleFor(x => x)
            .Custom((parameter, context) =>
            {
                var values = parameter.AvailableValues;
                if (values is null)
                {
                    return;
                }

                if (values.Kind == ReportParameterAvailableValuesKind.Static && values.StaticValues.Count == 0)
                {
                    context.AddFailure(CreateFailure(
                        nameof(parameter.AvailableValues),
                        "ReportParameter.AvailableValues.Static.Required",
                        localizer["ReportParameter_AvailableValues_Static_Required"]));
                }

                if (values.Kind == ReportParameterAvailableValuesKind.DataSet &&
                    (string.IsNullOrWhiteSpace(values.DataSetName) || string.IsNullOrWhiteSpace(values.ValueField)))
                {
                    context.AddFailure(CreateFailure(
                        nameof(parameter.AvailableValues),
                        "ReportParameter.AvailableValues.DataSet.Required",
                        localizer["ReportParameter_AvailableValues_DataSet_Required"]));
                }
            });
    }

    private static FluentValidation.Results.ValidationFailure CreateFailure(
        string propertyName,
        string errorCode,
        string message)
        => new(propertyName, message) { ErrorCode = errorCode };
}
