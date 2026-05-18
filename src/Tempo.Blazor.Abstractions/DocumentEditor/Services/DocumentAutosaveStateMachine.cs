using Tempo.Blazor.DocumentEditor.Models;

namespace Tempo.Blazor.DocumentEditor.Services;

/// <summary>Small autosave state machine inspired by CKEditor pending action and autosave behavior.</summary>
public sealed class DocumentAutosaveStateMachine
{
    private DocumentAutosaveStatus _status = DocumentAutosaveStatus.Synchronized;
    private bool _pendingImmediateSave;
    private bool _canRetry;
    private string? _errorMessage;
    private int _attempt;

    /// <summary>Current immutable autosave state snapshot.</summary>
    public DocumentAutosaveState State => new()
    {
        Status = _status,
        HasPendingImmediateSave = _pendingImmediateSave,
        CanRetry = _canRetry,
        ErrorMessage = _errorMessage,
        Attempt = _attempt
    };

    /// <summary>Records a local editor change.</summary>
    public DocumentAutosaveState RegisterLocalChange()
    {
        _errorMessage = null;
        _canRetry = false;

        if (_status == DocumentAutosaveStatus.Saving)
        {
            _pendingImmediateSave = true;
            return State;
        }

        _status = DocumentAutosaveStatus.Waiting;
        return State;
    }

    /// <summary>Starts a save when the debounce interval elapsed.</summary>
    public DocumentAutosaveState DebounceElapsed()
    {
        if (_status == DocumentAutosaveStatus.Waiting)
        {
            StartSaving();
        }

        return State;
    }

    /// <summary>Starts a retry after a recoverable save error.</summary>
    public DocumentAutosaveState Retry()
    {
        if (_status == DocumentAutosaveStatus.Error && _canRetry)
        {
            StartSaving();
        }

        return State;
    }

    /// <summary>Marks the current save as successful.</summary>
    public DocumentAutosaveState SaveSucceeded()
    {
        _errorMessage = null;
        _canRetry = false;

        if (_pendingImmediateSave)
        {
            _pendingImmediateSave = false;
            _status = DocumentAutosaveStatus.Waiting;
            return State;
        }

        _status = DocumentAutosaveStatus.Synchronized;
        _attempt = 0;
        return State;
    }

    /// <summary>Marks the current save as failed.</summary>
    public DocumentAutosaveState SaveFailed(string? errorMessage, bool recoverable)
    {
        _status = DocumentAutosaveStatus.Error;
        _errorMessage = errorMessage;
        _canRetry = recoverable;
        return State;
    }

    /// <summary>Resets the state machine after a document load or explicit synchronization.</summary>
    public DocumentAutosaveState ResetSynchronized()
    {
        _status = DocumentAutosaveStatus.Synchronized;
        _pendingImmediateSave = false;
        _canRetry = false;
        _errorMessage = null;
        _attempt = 0;
        return State;
    }

    private void StartSaving()
    {
        _status = DocumentAutosaveStatus.Saving;
        _canRetry = false;
        _errorMessage = null;
        _attempt++;
    }
}
