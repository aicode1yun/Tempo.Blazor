using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Tempo.Blazor.Abstractions.Models;
using Tempo.Blazor.Services;

namespace Tempo.Blazor.Components.Scheduler;

/// <summary>Dialog for importing tasks from Excel, MS Project XML, or JIRA.</summary>
public partial class TmGanttImportDialog
{
    private enum ImportTab { Excel, Mpp, Jira }

    private ImportTab _tab = ImportTab.Excel;
    private string? _selectedFileName;
    private IBrowserFile? _selectedFile;
    private string _jiraUrl = string.Empty;
    private string _jiraToken = string.Empty;
    private string _jiraProject = string.Empty;
    private string? _errorMessage;
    private bool _isImporting;

    /// <summary>Whether the dialog is visible.</summary>
    [Parameter] public bool IsOpen { get; set; }

    /// <summary>Fires when import completes successfully.</summary>
    [Parameter] public EventCallback<IReadOnlyList<GanttTask>> OnImportCompleted { get; set; }

    /// <summary>Fires when import fails with an error message.</summary>
    [Parameter] public EventCallback<string> OnImportError { get; set; }

    /// <summary>Fires when the dialog should close.</summary>
    [Parameter] public EventCallback OnClose { get; set; }

    private void OnFileChangedAsync(InputFileChangeEventArgs e)
    {
        _selectedFile = e.File;
        _selectedFileName = e.File.Name;
        _errorMessage = null;
    }

    private async Task ImportAsync()
    {
        _errorMessage = null;
        _isImporting = true;
        try
        {
            IReadOnlyList<GanttTask> tasks;
            if (_tab == ImportTab.Excel)
            {
                if (_selectedFile is null) return;
                using var stream = _selectedFile.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024);
                tasks = GanttExcelImporter.Import(stream);
            }
            else if (_tab == ImportTab.Mpp)
            {
                if (_selectedFile is null) return;
                using var stream = _selectedFile.OpenReadStream(maxAllowedSize: 50 * 1024 * 1024);
                tasks = GanttMppImporter.Import(stream);
            }
            else
            {
                using var importer = new GanttJiraImporter(new HttpClient());
                tasks = await importer.ImportAsync(_jiraUrl, _jiraToken, _jiraProject);
            }
            await OnImportCompleted.InvokeAsync(tasks);
            await OnClose.InvokeAsync();
        }
        catch (GanttImportAuthException)
        {
            _errorMessage = Loc["TmGantt_ImportError"].Replace("{0}", "authentication failed");
            await OnImportError.InvokeAsync(_errorMessage);
        }
        catch (Exception ex)
        {
            _errorMessage = Loc["TmGantt_ImportError"].Replace("{0}", ex.Message);
            await OnImportError.InvokeAsync(_errorMessage);
        }
        finally
        {
            _isImporting = false;
        }
    }
}
