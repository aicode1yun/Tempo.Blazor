namespace Tempo.Blazor.NotionEditor.Enums;

/// <summary>Supported AI text improvement modes for Notion editor integrations.</summary>
public enum AiImproveMode
{
    /// <summary>Correct grammar and spelling while preserving meaning.</summary>
    Grammar,

    /// <summary>Make the text shorter while keeping key points.</summary>
    Shorten,

    /// <summary>Expand the text with useful context and connective wording.</summary>
    Lengthen,

    /// <summary>Adjust tone while preserving the original intent.</summary>
    ChangeTone,

    /// <summary>Rewrite the text in simpler language.</summary>
    Simplify,

    /// <summary>Translate the text according to the provider or request context.</summary>
    Translate
}
