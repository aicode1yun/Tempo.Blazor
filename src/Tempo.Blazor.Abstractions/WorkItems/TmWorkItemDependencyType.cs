namespace Tempo.Blazor.Abstractions.WorkItems;

/// <summary>Type of dependency relationship between two work items.</summary>
public enum TmWorkItemDependencyType
{
    /// <summary>Successor starts after predecessor finishes (default).</summary>
    FinishToStart = 0,

    /// <summary>Successor starts after predecessor starts.</summary>
    StartToStart = 1,

    /// <summary>Successor finishes after predecessor finishes.</summary>
    FinishToFinish = 2,

    /// <summary>Successor finishes after predecessor starts.</summary>
    StartToFinish = 3
}
