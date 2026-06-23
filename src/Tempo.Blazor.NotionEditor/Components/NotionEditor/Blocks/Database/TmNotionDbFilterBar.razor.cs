using Microsoft.AspNetCore.Components;
using Tempo.Blazor.NotionEditor.Enums;
using Tempo.Blazor.NotionEditor.Interfaces;
using Tempo.Blazor.NotionEditor.Models;

namespace Tempo.Blazor.Components.NotionEditor.Blocks.Database;

public partial class TmNotionDbFilterBar : ComponentBase
{
    internal enum ValueInputKind { None, Text, Number, Date, SelectOptions }

    internal sealed class FilterConditionModel
    {
        public Guid Id { get; } = Guid.NewGuid();
        public Guid? FieldId { get; set; }
        public NotionFilterOperator Operator { get; set; } = NotionFilterOperator.Contains;
        public string ValueText { get; set; } = string.Empty;
    }

    internal sealed class FilterGroupModel
    {
        public Guid Id { get; } = Guid.NewGuid();
        public FilterLogic Logic { get; set; } = FilterLogic.And;
        public List<FilterConditionModel> Conditions { get; } = [];
        public List<FilterGroupModel> NestedGroups { get; } = [];
    }

    // ── Parameters ───────────────────────────────────────────────────────────

    [Parameter, EditorRequired] public IReadOnlyList<IDatabaseField>     Fields   { get; set; } = [];
    [Parameter]                 public INotionDatabaseFilter?             Filter   { get; set; }
    [Parameter]                 public bool                               ReadOnly { get; set; }

    [Parameter] public EventCallback<INotionDatabaseFilter?> OnFilterChanged { get; set; }
    [Parameter] public EventCallback                         OnClose         { get; set; }

    // ── State ─────────────────────────────────────────────────────────────────

    private FilterGroupModel _root        = new();
    private bool             _initialized;

    protected override void OnParametersSet()
    {
        if (!_initialized)
        {
            _root        = Filter is not null ? FromFilter(Filter) : new FilterGroupModel();
            _initialized = true;
        }
    }

    // ── Model conversion ──────────────────────────────────────────────────────

    private static FilterGroupModel FromFilter(INotionDatabaseFilter f)
    {
        var g = new FilterGroupModel { Logic = f.Logic };
        foreach (var c in f.Conditions)
            g.Conditions.Add(new FilterConditionModel
            {
                FieldId   = c.FieldId,
                Operator  = c.Operator,
                ValueText = c.Value?.ToString() ?? string.Empty
            });
        foreach (var nf in f.NestedFilters)
            g.NestedGroups.Add(FromFilter(nf));
        return g;
    }

    private static INotionDatabaseFilter ToFilter(FilterGroupModel g) =>
        new NotionDatabaseFilter
        {
            Logic         = g.Logic,
            Conditions    = g.Conditions
                .Where(c => c.FieldId.HasValue)
                .Select(c => new NotionFilterCondition(c.FieldId!.Value, c.Operator, ParseValue(c)))
                .ToList(),
            NestedFilters = g.NestedGroups
                .Select(ng => (INotionDatabaseFilter)ToFilter(ng))
                .ToList()
        };

    private static object? ParseValue(FilterConditionModel c) =>
        c.ValueText.Length > 0 ? (object)c.ValueText : null;

    // ── Derived ───────────────────────────────────────────────────────────────

    internal bool IsEmpty =>
        _root.Conditions.Count == 0 && _root.NestedGroups.Count == 0;

    public int ActiveConditionCount =>
        CountActive(_root.Conditions) +
        _root.NestedGroups.Sum(g => CountActive(g.Conditions));

    private static int CountActive(IEnumerable<FilterConditionModel> conds) =>
        conds.Count(c => c.FieldId.HasValue);

    // ── Mutations ─────────────────────────────────────────────────────────────

    private async Task SetRootLogicAsync(FilterLogic logic)
    {
        _root.Logic = logic;
        await EmitAsync();
    }

    private async Task SetGroupLogicAsync(FilterGroupModel group, FilterLogic logic)
    {
        group.Logic = logic;
        await EmitAsync();
    }

    private async Task AddConditionAsync(FilterGroupModel group)
    {
        var first = Fields.FirstOrDefault();
        group.Conditions.Add(new FilterConditionModel
        {
            FieldId  = first?.Id,
            Operator = first is not null ? GetOperatorsForField(first.Type)[0] : NotionFilterOperator.Contains
        });
        await EmitAsync();
    }

    private async Task AddGroupAsync()
    {
        var nested = new FilterGroupModel
        {
            Logic = _root.Logic == FilterLogic.And ? FilterLogic.Or : FilterLogic.And
        };
        _root.NestedGroups.Add(nested);
        await AddConditionAsync(nested);
    }

    private async Task RemoveConditionAsync(FilterGroupModel group, FilterConditionModel cond)
    {
        group.Conditions.Remove(cond);
        await EmitAsync();
    }

    private async Task RemoveGroupAsync(FilterGroupModel group)
    {
        _root.NestedGroups.Remove(group);
        await EmitAsync();
    }

    private async Task SetFieldAsync(FilterConditionModel cond, string? fieldIdStr)
    {
        if (Guid.TryParse(fieldIdStr, out var id))
        {
            cond.FieldId = id;
            var field    = Fields.FirstOrDefault(f => f.Id == id);
            if (field is not null)
            {
                var ops = GetOperatorsForField(field.Type);
                if (!ops.Contains(cond.Operator))
                    cond.Operator = ops[0];
            }
        }
        else
        {
            cond.FieldId = null;
        }
        cond.ValueText = string.Empty;
        await EmitAsync();
    }

    private async Task SetOperatorAsync(FilterConditionModel cond, string opStr)
    {
        if (Enum.TryParse<NotionFilterOperator>(opStr, out var op))
        {
            cond.Operator  = op;
            cond.ValueText = string.Empty;
        }
        await EmitAsync();
    }

    private async Task SetValueAsync(FilterConditionModel cond, string value)
    {
        cond.ValueText = value;
        await EmitAsync();
    }

    private async Task ClearAllAsync()
    {
        _root = new FilterGroupModel();
        await EmitAsync();
    }

    private async Task EmitAsync()
    {
        await OnFilterChanged.InvokeAsync(IsEmpty ? null : ToFilter(_root));
    }

    // ── Operator logic ────────────────────────────────────────────────────────

    internal static IReadOnlyList<NotionFilterOperator> GetOperatorsForField(DatabaseFieldType type) => type switch
    {
        DatabaseFieldType.Text or DatabaseFieldType.Email or
        DatabaseFieldType.Phone or DatabaseFieldType.Url or DatabaseFieldType.Formula =>
        [
            NotionFilterOperator.Contains,     NotionFilterOperator.NotContains,
            NotionFilterOperator.Equals,       NotionFilterOperator.NotEquals,
            NotionFilterOperator.StartsWith,   NotionFilterOperator.EndsWith,
            NotionFilterOperator.IsEmpty,      NotionFilterOperator.IsNotEmpty
        ],

        DatabaseFieldType.Number or DatabaseFieldType.Rollup =>
        [
            NotionFilterOperator.Equals,              NotionFilterOperator.NotEquals,
            NotionFilterOperator.GreaterThan,         NotionFilterOperator.GreaterThanOrEqual,
            NotionFilterOperator.LessThan,            NotionFilterOperator.LessThanOrEqual,
            NotionFilterOperator.IsEmpty,             NotionFilterOperator.IsNotEmpty
        ],

        DatabaseFieldType.Select or DatabaseFieldType.Status =>
        [
            NotionFilterOperator.Equals,    NotionFilterOperator.NotEquals,
            NotionFilterOperator.IsEmpty,   NotionFilterOperator.IsNotEmpty
        ],

        DatabaseFieldType.MultiSelect =>
        [
            NotionFilterOperator.Contains,    NotionFilterOperator.NotContains,
            NotionFilterOperator.IsEmpty,     NotionFilterOperator.IsNotEmpty
        ],

        DatabaseFieldType.Date or DatabaseFieldType.DateRange or
        DatabaseFieldType.CreatedTime or DatabaseFieldType.LastEditedTime =>
        [
            NotionFilterOperator.Equals,     NotionFilterOperator.Before,
            NotionFilterOperator.After,      NotionFilterOperator.OnOrBefore,
            NotionFilterOperator.OnOrAfter,  NotionFilterOperator.IsEmpty,
            NotionFilterOperator.IsNotEmpty, NotionFilterOperator.ThisWeek,
            NotionFilterOperator.PastWeek,   NotionFilterOperator.PastMonth,
            NotionFilterOperator.NextWeek,   NotionFilterOperator.NextMonth
        ],

        DatabaseFieldType.Checkbox =>
        [
            NotionFilterOperator.IsChecked, NotionFilterOperator.IsUnchecked
        ],

        DatabaseFieldType.Person or DatabaseFieldType.CreatedBy or DatabaseFieldType.LastEditedBy =>
        [
            NotionFilterOperator.Contains,    NotionFilterOperator.NotContains,
            NotionFilterOperator.IsEmpty,     NotionFilterOperator.IsNotEmpty
        ],

        _ =>
        [
            NotionFilterOperator.Contains,    NotionFilterOperator.NotContains,
            NotionFilterOperator.IsEmpty,     NotionFilterOperator.IsNotEmpty
        ]
    };

    internal ValueInputKind GetInputKind(FilterConditionModel cond)
    {
        if (!cond.FieldId.HasValue) return ValueInputKind.Text;
        var field = Fields.FirstOrDefault(f => f.Id == cond.FieldId.Value);
        if (field is null) return ValueInputKind.Text;

        return cond.Operator switch
        {
            NotionFilterOperator.IsEmpty or NotionFilterOperator.IsNotEmpty or
            NotionFilterOperator.IsChecked or NotionFilterOperator.IsUnchecked or
            NotionFilterOperator.ThisWeek or NotionFilterOperator.PastWeek or
            NotionFilterOperator.PastMonth or NotionFilterOperator.NextWeek or
            NotionFilterOperator.NextMonth => ValueInputKind.None,

            _ when field.Type is DatabaseFieldType.Number or DatabaseFieldType.Rollup
                => ValueInputKind.Number,

            _ when field.Type is DatabaseFieldType.Date or DatabaseFieldType.DateRange or
                   DatabaseFieldType.CreatedTime or DatabaseFieldType.LastEditedTime
                => ValueInputKind.Date,

            NotionFilterOperator.Equals or NotionFilterOperator.NotEquals
                when field.Type is DatabaseFieldType.Select or DatabaseFieldType.Status
                => ValueInputKind.SelectOptions,

            _ => ValueInputKind.Text
        };
    }

    internal IReadOnlyList<string> GetSelectOptions(FilterConditionModel cond)
    {
        if (!cond.FieldId.HasValue) return [];
        var field = Fields.FirstOrDefault(f => f.Id == cond.FieldId.Value);
        return field?.Config switch
        {
            ISelectFieldConfig sel => sel.Options.Select(o => o.Name).ToList(),
            IStatusFieldConfig sc  => sc.Groups.SelectMany(g => g.Options).Select(o => o.Name).ToList(),
            _                      => []
        };
    }

    private string OpLabel(NotionFilterOperator op) => Loc[$"TmNotionDbFilterBar_Op_{op}"];
}
