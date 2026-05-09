using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Tempo.Blazor.Abstractions.Models;

namespace Tempo.Blazor.Components.Signing;

/// <summary>Designs PDF signing templates with page overlays, field palette, selection, and field settings.</summary>
public partial class TmPdfTemplateDesigner
{
    private const double DefaultPageWidth = 1000;
    private const double DefaultPageHeight = 1000;
    private const double MinWidth = 0.02;
    private const double MinHeight = 0.02;

    private static readonly SigningFieldType[] DefaultAllowedFieldTypes =
    [
        SigningFieldType.Text,
        SigningFieldType.Signature,
        SigningFieldType.Initials,
        SigningFieldType.Date,
        SigningFieldType.Number,
        SigningFieldType.Checkbox,
        SigningFieldType.Radio,
        SigningFieldType.Select,
        SigningFieldType.Multiple,
        SigningFieldType.File,
        SigningFieldType.Image,
        SigningFieldType.Stamp,
        SigningFieldType.Phone
    ];

    private readonly List<SigningField> _fields = [];
    private readonly HashSet<string> _selectedFieldUuids = [];
    private IReadOnlyList<SigningField>? _lastFields;
    private SigningFieldType? _drawType;
    private SigningFieldType? _dragType;
    private DrawState? _drawState;
    private MoveState? _moveState;
    private ResizeState? _resizeState;
    private ContextMenuState? _contextMenu;
    private readonly List<SigningField> _clipboardFields = [];
    private readonly Dictionary<string, ElementReference> _pageSurfaceRefs = [];
    private string? _clipboardStatus;
    private IJSObjectReference? _jsModule;
    private bool _isDetecting;
    private string? _detectionError;

    /// <summary>Document pages available for template design.</summary>
    [Parameter] public IReadOnlyList<SigningDocumentPage> Documents { get; set; } = [];

    /// <summary>Current signing fields.</summary>
    [Parameter] public IReadOnlyList<SigningField> Fields { get; set; } = [];

    /// <summary>Callback invoked when the signing fields collection changes.</summary>
    [Parameter] public EventCallback<IReadOnlyList<SigningField>> FieldsChanged { get; set; }

    /// <summary>Signer roles available for assigning newly created fields.</summary>
    [Parameter] public IReadOnlyList<SigningSubmitterRole> SubmitterRoles { get; set; } = [];

    /// <summary>Currently selected submitter role identifier used for new fields.</summary>
    [Parameter] public string? SelectedSubmitterUuid { get; set; }

    /// <summary>Field types visible in the palette. Defaults to common signing field types.</summary>
    [Parameter] public IReadOnlyList<SigningFieldType>? AllowedFieldTypes { get; set; }

    /// <summary>Whether editing controls should be disabled.</summary>
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Whether to render a compact mobile-oriented designer layout.</summary>
    [Parameter] public bool MobileMode { get; set; }

    /// <summary>Optional async detector that returns fields to add to the template.</summary>
    [Parameter] public Func<Task<IReadOnlyList<SigningField>>>? OnDetectFields { get; set; }

    /// <summary>Additional CSS classes for the root element.</summary>
    [Parameter] public string? Class { get; set; }

    /// <summary>Additional HTML attributes passed to the root element.</summary>
    [Parameter(CaptureUnmatchedValues = true)]
    public Dictionary<string, object>? AdditionalAttributes { get; set; }

    [Inject] private IJSRuntime JS { get; set; } = default!;

    private IReadOnlyList<SigningFieldType> AllowedTypes => AllowedFieldTypes is { Count: > 0 }
        ? AllowedFieldTypes
        : DefaultAllowedFieldTypes;

    private SigningField? SelectedField => _selectedFieldUuids.Count == 1
        ? _fields.FirstOrDefault(item => _selectedFieldUuids.Contains(item.Uuid))
        : null;

    private int SelectedFieldCount => _selectedFieldUuids.Count(uuid => _fields.Any(item => item.Uuid == uuid));

    private string RootClass
    {
        get
        {
            var classes = new List<string> { "tm-pdf-template-designer" };
            AddClass(classes, _drawType.HasValue, "tm-pdf-template-designer--drawing");
            AddClass(classes, _dragType.HasValue, "tm-pdf-template-designer--dragging");
            AddClass(classes, Disabled, "tm-pdf-template-designer--disabled");
            AddClass(classes, MobileMode, "tm-pdf-template-designer--mobile");

            if (!string.IsNullOrWhiteSpace(Class))
            {
                classes.Add(Class);
            }

            return string.Join(" ", classes);
        }
    }

    private string PaletteClass => MobileMode
        ? "tm-pdf-template-designer__palette tm-pdf-template-designer__palette--compact"
        : "tm-pdf-template-designer__palette";

    private string ContextMenuStyle => _contextMenu is null
        ? string.Empty
        : string.Create(CultureInfo.InvariantCulture, $"left: {_contextMenu.X}px; top: {_contextMenu.Y}px;");

    private bool HasClipboardFields => _clipboardFields.Count > 0;

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_lastFields, Fields))
        {
            _fields.Clear();
            _fields.AddRange((Fields ?? []).Select(Clone));
            _selectedFieldUuids.RemoveWhere(uuid => _fields.All(field => field.Uuid != uuid));
            _lastFields = Fields;
        }
    }

    private RenderFragment RenderPageOverlay(SigningDocumentPage page) => builder =>
    {
        var sequence = 0;
        builder.OpenElement(sequence++, "div");
        builder.AddAttribute(sequence++, "class", "tm-pdf-template-designer__page-surface");
        builder.AddAttribute(sequence++, "data-page-key", GetPageKey(page));
        builder.AddAttribute(sequence++, "onmousedown", EventCallback.Factory.Create<MouseEventArgs>(this, args => HandlePagePointerDownAsync(page, args)));
        builder.AddAttribute(sequence++, "onmousemove", EventCallback.Factory.Create<MouseEventArgs>(this, args => HandlePagePointerMoveAsync(page, args)));
        builder.AddAttribute(sequence++, "onmouseup", EventCallback.Factory.Create<MouseEventArgs>(this, args => HandlePagePointerUpAsync(page, args)));
        builder.AddAttribute(sequence++, "oncontextmenu", EventCallback.Factory.Create<MouseEventArgs>(this, args => OpenPageContextMenu(page, args)));
        builder.AddEventPreventDefaultAttribute(sequence++, "oncontextmenu", true);
        builder.AddAttribute(sequence++, "ondragover", EventCallback.Factory.Create<DragEventArgs>(this, _ => Task.CompletedTask));
        builder.AddEventPreventDefaultAttribute(sequence++, "ondragover", true);
        builder.AddAttribute(sequence++, "ondrop", EventCallback.Factory.Create<DragEventArgs>(this, args => HandlePaletteDropAsync(page, args)));
        builder.AddEventPreventDefaultAttribute(sequence++, "ondrop", true);
        builder.AddElementReferenceCapture(sequence++, reference => _pageSurfaceRefs[GetPageKey(page)] = reference);

        foreach (var field in _fields)
        {
            foreach (var area in GetAreasForPage(field, page))
            {
                var currentField = field;
                var currentArea = area;
                builder.OpenComponent<TmSigningFieldOverlay>(sequence++);
                builder.AddAttribute(sequence++, "Field", currentField);
                builder.AddAttribute(sequence++, "Area", currentArea);
                builder.AddAttribute(sequence++, "Selected", _selectedFieldUuids.Contains(currentField.Uuid));
                builder.AddAttribute(sequence++, "Draggable", !Disabled && !_drawType.HasValue);
                builder.AddAttribute(sequence++, "Editable", !Disabled && _selectedFieldUuids.Contains(currentField.Uuid));
                builder.AddAttribute(sequence++, "ReadOnly", Disabled);
                builder.AddAttribute(sequence++, "OnClick", EventCallback.Factory.Create<TmSigningFieldOverlayPointerEventArgs>(this, args => SelectFieldAsync(args, false)));
                builder.AddAttribute(sequence++, "OnStartMove", EventCallback.Factory.Create<TmSigningFieldOverlayPointerEventArgs>(this, StartMoveAsync));
                builder.AddAttribute(sequence++, "OnStartResize", EventCallback.Factory.Create<TmSigningFieldOverlayResizeEventArgs>(this, StartResizeAsync));
                builder.AddAttribute(sequence++, "OnContextMenu", EventCallback.Factory.Create<TmSigningFieldOverlayPointerEventArgs>(this, OpenFieldContextMenuAsync));
                builder.AddAttribute(sequence++, "data-field-uuid", currentField.Uuid);
                builder.AddAttribute(sequence++, "data-area-uuid", currentArea.Uuid);
                builder.CloseComponent();
            }
        }

        if (_drawState?.PageKey == GetPageKey(page))
        {
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "tm-pdf-template-designer__draft");
            builder.AddAttribute(sequence++, "style", GetAreaStyle(_drawState.Area));
            builder.CloseElement();
        }

        var selectedAreas = _fields
            .Where(field => _selectedFieldUuids.Contains(field.Uuid))
            .SelectMany(field => GetAreasForPage(field, page))
            .ToArray();

        if (selectedAreas.Length > 1)
        {
            builder.OpenElement(sequence++, "div");
            builder.AddAttribute(sequence++, "class", "tm-pdf-template-designer__selection-bounds");
            builder.AddAttribute(sequence++, "style", GetRectangleStyle(SigningGeometryHelper.GetSelectionRectangle(selectedAreas)));
            builder.CloseElement();
        }

        builder.CloseElement();
    };

    private Task SelectFieldTypeAsync(SigningFieldType type)
    {
        if (Disabled)
        {
            return Task.CompletedTask;
        }

        _drawType = type;
        _contextMenu = null;
        return Task.CompletedTask;
    }

    private Task StartPaletteDrag(SigningFieldType type)
    {
        if (Disabled)
        {
            return Task.CompletedTask;
        }

        _dragType = type;
        _drawType = null;
        _contextMenu = null;
        return Task.CompletedTask;
    }

    private Task EndPaletteDrag()
    {
        _dragType = null;
        return Task.CompletedTask;
    }

    private async Task HandlePaletteDropAsync(SigningDocumentPage page, DragEventArgs args)
    {
        if (Disabled || _dragType is not { } type)
        {
            return;
        }

        var point = await ToPointAsync(page, args);
        var area = CreateDefaultAreaAtPoint(page, type, point.X, point.Y);
        var field = CreateField(type, area);
        _fields.Add(field);
        _selectedFieldUuids.Clear();
        _selectedFieldUuids.Add(field.Uuid);
        _dragType = null;
        _drawType = null;
        await NotifyFieldsChangedAsync();
    }

    private async Task HandlePagePointerDownAsync(SigningDocumentPage page, MouseEventArgs args)
    {
        if (Disabled)
        {
            return;
        }

        var point = await ToPointAsync(page, args);
        if (_drawType.HasValue)
        {
            _drawState = new DrawState(page, point.X, point.Y, new SigningFieldArea
            {
                Uuid = Guid.NewGuid().ToString("N"),
                AttachmentUuid = page.AttachmentUuid,
                Page = page.PageIndex,
                X = point.X,
                Y = point.Y
            });
            return;
        }

        _selectedFieldUuids.Clear();
        _drawState = new DrawState(page, point.X, point.Y, new SigningFieldArea
        {
            Uuid = "selection",
            AttachmentUuid = page.AttachmentUuid,
            Page = page.PageIndex,
            X = point.X,
            Y = point.Y
        }, isSelectionBox: true);
        await Task.CompletedTask;
    }

    private async Task HandlePagePointerMoveAsync(SigningDocumentPage page, MouseEventArgs args)
    {
        if (_drawState is null || _drawState.PageKey != GetPageKey(page))
        {
            return;
        }

        var point = await ToPointAsync(page, args);
        _drawState.Area = CreateAreaFromPoints(_drawState.Page, _drawState.StartX, _drawState.StartY, point.X, point.Y, _drawState.Area.Uuid);
    }

    private async Task HandlePagePointerUpAsync(SigningDocumentPage page, MouseEventArgs args)
    {
        if (_drawState is null || _drawState.PageKey != GetPageKey(page))
        {
            return;
        }

        await HandlePointerUpAsync(args);
    }

    private async Task HandlePointerMoveAsync(MouseEventArgs args)
    {
        if (_moveState is not null)
        {
            var deltaX = (args.ClientX - _moveState.StartClientX) / DefaultPageWidth;
            var deltaY = (args.ClientY - _moveState.StartClientY) / DefaultPageHeight;
            ApplyMove(deltaX, deltaY);
            return;
        }

        if (_resizeState is not null)
        {
            var deltaX = (args.ClientX - _resizeState.StartClientX) / DefaultPageWidth;
            var deltaY = (args.ClientY - _resizeState.StartClientY) / DefaultPageHeight;
            ApplyResize(deltaX, deltaY);
            await NotifyFieldsChangedAsync();
        }
    }

    private async Task HandlePointerUpAsync(MouseEventArgs args)
    {
        if (_drawState is not null)
        {
            var state = _drawState;
            _drawState = null;

            if (state.Area.Width < MinWidth || state.Area.Height < MinHeight)
            {
                return;
            }

            if (state.IsSelectionBox)
            {
                SelectFieldsInRectangle(state.Page, state.Area);
                return;
            }

            if (_drawType.HasValue)
            {
                var field = CreateField(_drawType.Value, state.Area);
                _fields.Add(field);
                _selectedFieldUuids.Clear();
                _selectedFieldUuids.Add(field.Uuid);
                await NotifyFieldsChangedAsync();
            }
        }

        if (_moveState is not null)
        {
            _moveState = null;
            await NotifyFieldsChangedAsync();
        }

        _resizeState = null;
    }

    private async Task HandleDesignerKeyDownAsync(KeyboardEventArgs args)
    {
        if (Disabled || args.Key is not "Delete" || _selectedFieldUuids.Count == 0)
        {
            return;
        }

        await DeleteSelectedAsync();
    }

    private async Task SelectFieldAsync(TmSigningFieldOverlayPointerEventArgs args, bool append)
    {
        if (args.Field is null)
        {
            return;
        }

        var useAppend = append || args.MouseEventArgs.CtrlKey || args.MouseEventArgs.MetaKey;
        if (!useAppend)
        {
            _selectedFieldUuids.Clear();
        }

        if (useAppend && _selectedFieldUuids.Contains(args.Field.Uuid))
        {
            _selectedFieldUuids.Remove(args.Field.Uuid);
        }
        else
        {
            _selectedFieldUuids.Add(args.Field.Uuid);
        }

        _contextMenu = null;
        await Task.CompletedTask;
    }

    private Task StartMoveAsync(TmSigningFieldOverlayPointerEventArgs args)
    {
        if (Disabled || args.Field is null || args.Area is null)
        {
            return Task.CompletedTask;
        }

        if (args.MouseEventArgs.CtrlKey || args.MouseEventArgs.MetaKey)
        {
            return Task.CompletedTask;
        }

        if (!_selectedFieldUuids.Contains(args.Field.Uuid))
        {
            _selectedFieldUuids.Clear();
            _selectedFieldUuids.Add(args.Field.Uuid);
        }

        var selected = _fields
            .Where(field => _selectedFieldUuids.Contains(field.Uuid))
            .SelectMany(field => field.Areas.Select(area => new AreaSnapshot(field.Uuid, area.Uuid, Clone(area))))
            .ToArray();

        _moveState = new MoveState(args.MouseEventArgs.ClientX, args.MouseEventArgs.ClientY, selected);
        return Task.CompletedTask;
    }

    private Task StartResizeAsync(TmSigningFieldOverlayResizeEventArgs args)
    {
        if (Disabled || args.Field is null || args.Area is null)
        {
            return Task.CompletedTask;
        }

        _selectedFieldUuids.Clear();
        _selectedFieldUuids.Add(args.Field.Uuid);
        _resizeState = new ResizeState(
            args.Field.Uuid,
            args.Area.Uuid,
            Clone(args.Area),
            args.Handle,
            args.MouseEventArgs.ClientX,
            args.MouseEventArgs.ClientY);
        return Task.CompletedTask;
    }

    private void ApplyMove(double deltaX, double deltaY)
    {
        if (_moveState is null)
        {
            return;
        }

        foreach (var snapshot in _moveState.Areas)
        {
            var field = _fields.FirstOrDefault(item => item.Uuid == snapshot.FieldUuid);
            var area = field?.Areas.FirstOrDefault(item => item.Uuid == snapshot.AreaUuid);
            if (area is null)
            {
                continue;
            }

            CopyInto(area, SigningGeometryHelper.Move(snapshot.Area, deltaX, deltaY, MinWidth, MinHeight));
        }
    }

    private void ApplyResize(double deltaX, double deltaY)
    {
        if (_resizeState is null)
        {
            return;
        }

        var field = _fields.FirstOrDefault(item => item.Uuid == _resizeState.FieldUuid);
        var area = field?.Areas.FirstOrDefault(item => item.Uuid == _resizeState.AreaUuid);
        if (area is null)
        {
            return;
        }

        CopyInto(area, SigningGeometryHelper.Resize(_resizeState.Area, _resizeState.Handle, deltaX, deltaY, MinWidth, MinHeight));
    }

    private void SelectFieldsInRectangle(SigningDocumentPage page, SigningFieldArea selection)
    {
        _selectedFieldUuids.Clear();
        foreach (var field in _fields)
        {
            if (GetAreasForPage(field, page).Any(area => Intersects(area, selection)))
            {
                _selectedFieldUuids.Add(field.Uuid);
            }
        }
    }

    private async Task HandleEditorFieldChangedAsync(SigningField field)
    {
        var index = _fields.FindIndex(item => item.Uuid == field.Uuid);
        if (index >= 0)
        {
            _fields[index] = Clone(field);
            await NotifyFieldsChangedAsync();
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (_selectedFieldUuids.Count == 0)
        {
            return;
        }

        _fields.RemoveAll(field => _selectedFieldUuids.Contains(field.Uuid));
        _selectedFieldUuids.Clear();
        _contextMenu = null;
        await NotifyFieldsChangedAsync();
    }

    private Task OpenFieldContextMenuAsync(TmSigningFieldOverlayPointerEventArgs args)
    {
        if (args.Field is null)
        {
            return Task.CompletedTask;
        }

        if (!_selectedFieldUuids.Contains(args.Field.Uuid))
        {
            _selectedFieldUuids.Clear();
            _selectedFieldUuids.Add(args.Field.Uuid);
        }

        _contextMenu = new ContextMenuState(
            SelectedFieldCount > 1 ? ContextMenuKind.Selection : ContextMenuKind.Field,
            args.MouseEventArgs.ClientX,
            args.MouseEventArgs.ClientY,
            args.Field.Uuid,
            args.Area?.AttachmentUuid,
            args.Area?.Page ?? 0,
            null,
            null);
        return Task.CompletedTask;
    }

    private async Task OpenPageContextMenu(SigningDocumentPage page, MouseEventArgs args)
    {
        var point = await ToPointAsync(page, args);
        _contextMenu = new ContextMenuState(
            ContextMenuKind.Page,
            args.ClientX,
            args.ClientY,
            null,
            page.AttachmentUuid,
            page.PageIndex,
            point.X,
            point.Y);
    }

    private Task CopyContextFieldAsync()
    {
        var field = _fields.FirstOrDefault(item => item.Uuid == _contextMenu?.FieldUuid);
        _clipboardFields.Clear();
        if (field is not null)
        {
            _clipboardFields.Add(Clone(field));
            _clipboardStatus = Loc["TmPdfTemplateDesigner_FieldCopied"];
        }

        _contextMenu = null;
        return Task.CompletedTask;
    }

    private Task CopySelectionAsync()
    {
        _clipboardFields.Clear();
        _clipboardFields.AddRange(_fields
            .Where(item => _selectedFieldUuids.Contains(item.Uuid))
            .Select(Clone));

        if (_clipboardFields.Count > 0)
        {
            _clipboardStatus = _clipboardFields.Count == 1
                ? Loc["TmPdfTemplateDesigner_FieldCopied"]
                : Loc["TmPdfTemplateDesigner_SelectionCopied"];
        }

        _contextMenu = null;
        return Task.CompletedTask;
    }

    private async Task DeleteContextFieldAsync()
    {
        if (_contextMenu?.FieldUuid is null)
        {
            return;
        }

        _fields.RemoveAll(field => field.Uuid == _contextMenu.FieldUuid);
        _selectedFieldUuids.Remove(_contextMenu.FieldUuid);
        _contextMenu = null;
        await NotifyFieldsChangedAsync();
    }

    private async Task PasteFieldAsync()
    {
        if (_clipboardFields.Count == 0 || _contextMenu is null)
        {
            return;
        }

        var clones = _clipboardFields.Select(Clone).ToList();
        var allAreas = clones.SelectMany(field => field.Areas).ToArray();
        var sourceBounds = allAreas.Length > 0
            ? SigningGeometryHelper.GetSelectionRectangle(allAreas)
            : new SigningRectangle(0, 0, MinWidth, MinHeight);
        var targetX = _contextMenu.DocumentX ?? Clamp(sourceBounds.X + 0.03 + sourceBounds.Width / 2, 0, 1);
        var targetY = _contextMenu.DocumentY ?? Clamp(sourceBounds.Y + 0.03 + sourceBounds.Height / 2, 0, 1);
        var desiredX = Clamp(targetX - sourceBounds.Width / 2, 0, Math.Max(0, 1 - sourceBounds.Width));
        var desiredY = Clamp(targetY - sourceBounds.Height / 2, 0, Math.Max(0, 1 - sourceBounds.Height));
        var deltaX = desiredX - sourceBounds.X;
        var deltaY = desiredY - sourceBounds.Y;

        foreach (var clone in clones)
        {
            clone.Uuid = Guid.NewGuid().ToString("N");
            clone.Name = $"{clone.Name} copy".Trim();
            foreach (var area in clone.Areas)
            {
                area.Uuid = Guid.NewGuid().ToString("N");
                area.AttachmentUuid = _contextMenu.AttachmentUuid;
                area.Page = _contextMenu.PageIndex;
                area.X = Clamp(area.X + deltaX, 0, Math.Max(0, 1 - area.Width));
                area.Y = Clamp(area.Y + deltaY, 0, Math.Max(0, 1 - area.Height));
            }
        }

        _fields.AddRange(clones);
        _selectedFieldUuids.Clear();
        foreach (var clone in clones)
        {
            _selectedFieldUuids.Add(clone.Uuid);
        }

        _contextMenu = null;
        _clipboardStatus = clones.Count == 1
            ? Loc["TmPdfTemplateDesigner_FieldPasted"]
            : Loc["TmPdfTemplateDesigner_SelectionPasted"];
        await NotifyFieldsChangedAsync();
    }

    private async Task CopyFieldToAllPagesAsync(SigningField field)
    {
        var target = _fields.FirstOrDefault(item => item.Uuid == field.Uuid);
        var sourceArea = target?.Areas.FirstOrDefault();
        if (target is null || sourceArea is null)
        {
            return;
        }

        foreach (var page in Documents.Where(page => page.AttachmentUuid == sourceArea.AttachmentUuid))
        {
            if (target.Areas.Any(area => area.AttachmentUuid == page.AttachmentUuid && area.Page == page.PageIndex))
            {
                continue;
            }

            var copy = Clone(sourceArea);
            copy.Uuid = Guid.NewGuid().ToString("N");
            copy.Page = page.PageIndex;
            copy.AttachmentUuid = page.AttachmentUuid;
            target.Areas.Add(copy);
        }

        await NotifyFieldsChangedAsync();
    }

    private async Task DetectFieldsAsync()
    {
        if (OnDetectFields is null)
        {
            return;
        }

        _isDetecting = true;
        _detectionError = null;

        try
        {
            var detected = await OnDetectFields.Invoke();
            _fields.AddRange(detected.Select(Clone));
            await NotifyFieldsChangedAsync();
        }
        catch (Exception ex)
        {
            _detectionError = ex.Message;
        }
        finally
        {
            _isDetecting = false;
        }
    }

    private async Task NotifyFieldsChangedAsync()
    {
        var materialized = _fields.Select(Clone).ToArray();
        _lastFields = materialized;
        if (FieldsChanged.HasDelegate)
        {
            await FieldsChanged.InvokeAsync(materialized);
        }
    }

    private SigningField CreateField(SigningFieldType type, SigningFieldArea area)
    {
        return new SigningField
        {
            Uuid = Guid.NewGuid().ToString("N"),
            SubmitterUuid = SelectedSubmitterUuid ?? SubmitterRoles.FirstOrDefault()?.Uuid,
            Name = GetFieldTypeLabel(type),
            Type = type,
            Required = type is SigningFieldType.Signature or SigningFieldType.Initials,
            Areas = [SigningGeometryHelper.Clamp(area, MinWidth, MinHeight)]
        };
    }

    private IEnumerable<SigningFieldArea> GetAreasForPage(SigningField field, SigningDocumentPage page)
    {
        return field.Areas.Where(area => area.AttachmentUuid == page.AttachmentUuid && area.Page == page.PageIndex);
    }

    private async Task<(double X, double Y)> ToPointAsync(SigningDocumentPage page, MouseEventArgs args)
    {
        if (_pageSurfaceRefs.TryGetValue(GetPageKey(page), out var reference))
        {
            try
            {
                _jsModule ??= await JS.InvokeAsync<IJSObjectReference>("import", "./_content/Tempo.Blazor/js/pdf-template-designer.js");
                if (_jsModule is null)
                {
                    return ToPointFromOffset(page, args);
                }

                var rect = await _jsModule.InvokeAsync<PageSurfaceRect>("getElementRect", reference);
                if (rect is { Width: > 0, Height: > 0 } && (args.ClientX != 0 || args.ClientY != 0))
                {
                    return (
                        Clamp((args.ClientX - rect.Left) / rect.Width, 0, 1),
                        Clamp((args.ClientY - rect.Top) / rect.Height, 0, 1));
                }
            }
            catch (JSException)
            {
                // Fall back to event offsets when JavaScript is unavailable in tests or prerendering.
            }
            catch (InvalidOperationException)
            {
                // Fall back to event offsets when JavaScript is unavailable in tests or prerendering.
            }
        }

        return ToPointFromOffset(page, args);
    }

    private static (double X, double Y) ToPointFromOffset(SigningDocumentPage page, MouseEventArgs args)
    {
        var width = page.Width > 0 ? page.Width : DefaultPageWidth;
        var height = page.Height > 0 ? page.Height : DefaultPageHeight;
        return (
            Clamp(args.OffsetX / width, 0, 1),
            Clamp(args.OffsetY / height, 0, 1));
    }

    private static SigningFieldArea CreateAreaFromPoints(SigningDocumentPage page, double startX, double startY, double endX, double endY, string uuid)
    {
        var x = Math.Min(startX, endX);
        var y = Math.Min(startY, endY);
        var width = Math.Abs(endX - startX);
        var height = Math.Abs(endY - startY);

        return new SigningFieldArea
        {
            Uuid = uuid,
            AttachmentUuid = page.AttachmentUuid,
            Page = page.PageIndex,
            X = x,
            Y = y,
            Width = width,
            Height = height
        };
    }

    private static SigningFieldArea CreateDefaultAreaAtPoint(SigningDocumentPage page, SigningFieldType type, double x, double y)
    {
        var (width, height) = GetDefaultAreaSize(type);
        return new SigningFieldArea
        {
            Uuid = Guid.NewGuid().ToString("N"),
            AttachmentUuid = page.AttachmentUuid,
            Page = page.PageIndex,
            X = Clamp(x - width / 2, 0, 1 - width),
            Y = Clamp(y - height / 2, 0, 1 - height),
            Width = width,
            Height = height
        };
    }

    private static (double Width, double Height) GetDefaultAreaSize(SigningFieldType type)
    {
        return type switch
        {
            SigningFieldType.Signature => (0.34, 0.065),
            SigningFieldType.Initials => (0.16, 0.065),
            SigningFieldType.Date or SigningFieldType.DateNow => (0.22, 0.055),
            SigningFieldType.Number => (0.2, 0.055),
            SigningFieldType.Checkbox => (0.08, 0.05),
            SigningFieldType.Radio or SigningFieldType.Select or SigningFieldType.Multiple => (0.32, 0.06),
            SigningFieldType.File or SigningFieldType.Image or SigningFieldType.Stamp => (0.26, 0.07),
            SigningFieldType.Phone => (0.28, 0.055),
            _ => (0.3, 0.055)
        };
    }

    private static bool Intersects(SigningFieldArea a, SigningFieldArea b)
    {
        return a.X < b.X + b.Width
            && a.X + a.Width > b.X
            && a.Y < b.Y + b.Height
            && a.Y + a.Height > b.Y;
    }

    private static string GetAreaStyle(SigningFieldArea area)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"left: {area.X * 100}%; top: {area.Y * 100}%; width: {area.Width * 100}%; height: {area.Height * 100}%;");
    }

    private static string GetRectangleStyle(SigningRectangle rectangle)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"left: {rectangle.X * 100}%; top: {rectangle.Y * 100}%; width: {rectangle.Width * 100}%; height: {rectangle.Height * 100}%;");
    }

    private string GetPaletteItemClass(SigningFieldType type)
    {
        return _drawType == type
            ? "tm-pdf-template-designer__palette-item tm-pdf-template-designer__palette-item--active"
            : "tm-pdf-template-designer__palette-item";
    }

    private string GetFieldTypeLabel(SigningFieldType type)
    {
        return type switch
        {
            SigningFieldType.Text => Loc["TmSigning_Field_Text"],
            SigningFieldType.Signature => Loc["TmSigning_Field_Signature"],
            SigningFieldType.Initials => Loc["TmSigning_Field_Initials"],
            SigningFieldType.Date => Loc["TmSigning_Field_Date"],
            SigningFieldType.DateNow => Loc["TmSigning_Field_Date"],
            SigningFieldType.Number => Loc["TmSigning_Field_Number"],
            SigningFieldType.Checkbox => Loc["TmSigning_Field_Checkbox"],
            SigningFieldType.Radio => Loc["TmSigning_Field_Radio"],
            SigningFieldType.Select => Loc["TmSigning_Field_Select"],
            SigningFieldType.Multiple => Loc["TmSigning_Field_Multiple"],
            SigningFieldType.File => Loc["TmSigning_Field_File"],
            SigningFieldType.Image => Loc["TmSigning_Field_Image"],
            SigningFieldType.Stamp => Loc["TmSigning_Field_Stamp"],
            SigningFieldType.Phone => Loc["TmSigning_Field_Phone"],
            SigningFieldType.Verification => Loc["TmSigning_Field_Verification"],
            SigningFieldType.Kba => Loc["TmSigning_Field_Kba"],
            SigningFieldType.Payment => Loc["TmSigning_Field_Payment"],
            SigningFieldType.Cells => Loc["TmSigning_Field_Cells"],
            SigningFieldType.Heading => Loc["TmSigning_Field_Heading"],
            SigningFieldType.Strikethrough => Loc["TmSigning_Field_Strikethrough"],
            _ => type.ToString()
        };
    }

    private static string GetIconName(SigningFieldType type)
    {
        return type switch
        {
            SigningFieldType.Signature or SigningFieldType.Initials => "edit",
            SigningFieldType.Date or SigningFieldType.DateNow => "calendar",
            SigningFieldType.Number or SigningFieldType.Payment => "hash",
            SigningFieldType.Checkbox => "check-square",
            SigningFieldType.Radio => "circle-dot",
            SigningFieldType.Select or SigningFieldType.Multiple => "list",
            SigningFieldType.File => "paperclip",
            SigningFieldType.Image => "image",
            SigningFieldType.Stamp => "stamp",
            SigningFieldType.Phone => "phone",
            SigningFieldType.Heading => "heading",
            SigningFieldType.Strikethrough => "strikethrough",
            _ => "type"
        };
    }

    private static string GetPageKey(SigningDocumentPage page) => $"{page.AttachmentUuid}:{page.PageIndex}";

    private static string GetPageElementId(SigningDocumentPage page) => $"tm-pdf-template-designer-page-{page.AttachmentUuid}-{page.PageIndex}";

    private static void AddClass(List<string> classes, bool condition, string cssClass)
    {
        if (condition)
        {
            classes.Add(cssClass);
        }
    }

    private static double Clamp(double value, double min, double max) => Math.Min(Math.Max(value, min), max);

    private static void CopyInto(SigningFieldArea target, SigningFieldArea source)
    {
        target.X = source.X;
        target.Y = source.Y;
        target.Width = source.Width;
        target.Height = source.Height;
        target.CellWidth = source.CellWidth;
        target.OptionUuid = source.OptionUuid;
    }

    private static SigningField Clone(SigningField field)
    {
        return new SigningField
        {
            Uuid = field.Uuid,
            SubmitterUuid = field.SubmitterUuid,
            Name = field.Name,
            Title = field.Title,
            Description = field.Description,
            Type = field.Type,
            Required = field.Required,
            ReadOnly = field.ReadOnly,
            Prefillable = field.Prefillable,
            DefaultValue = field.DefaultValue,
            Preferences = Clone(field.Preferences),
            Validation = field.Validation is null ? null : Clone(field.Validation),
            Conditions = field.Conditions.Select(Clone).ToList(),
            Options = field.Options.Select(Clone).ToList(),
            Areas = field.Areas.Select(Clone).ToList()
        };
    }

    private static SigningFieldPreferences Clone(SigningFieldPreferences preferences)
    {
        return new SigningFieldPreferences
        {
            Color = preferences.Color,
            Align = preferences.Align,
            Format = preferences.Format,
            FontFamily = preferences.FontFamily,
            FontSize = preferences.FontSize,
            WithSignatureId = preferences.WithSignatureId,
            WithLogo = preferences.WithLogo,
            ReasonFieldUuid = preferences.ReasonFieldUuid,
            Formula = preferences.Formula,
            Currency = preferences.Currency,
            Price = preferences.Price,
            PriceId = preferences.PriceId,
            PaymentLinkId = preferences.PaymentLinkId,
            AdditionalSettings = new Dictionary<string, object?>(preferences.AdditionalSettings)
        };
    }

    private static SigningFieldValidation Clone(SigningFieldValidation validation)
    {
        return new SigningFieldValidation
        {
            Pattern = validation.Pattern,
            Message = validation.Message,
            Min = validation.Min,
            Max = validation.Max,
            Step = validation.Step
        };
    }

    private static SigningFieldCondition Clone(SigningFieldCondition condition)
    {
        return new SigningFieldCondition
        {
            FieldUuid = condition.FieldUuid,
            Action = condition.Action,
            Value = condition.Value,
            Operation = condition.Operation
        };
    }

    private static SigningFieldOption Clone(SigningFieldOption option)
    {
        return new SigningFieldOption
        {
            Uuid = option.Uuid,
            Value = option.Value
        };
    }

    private static SigningFieldArea Clone(SigningFieldArea area)
    {
        return new SigningFieldArea
        {
            Uuid = area.Uuid,
            AttachmentUuid = area.AttachmentUuid,
            Page = area.Page,
            X = area.X,
            Y = area.Y,
            Width = area.Width,
            Height = area.Height,
            CellWidth = area.CellWidth,
            OptionUuid = area.OptionUuid
        };
    }

    private enum ContextMenuKind
    {
        Page,
        Field,
        Selection
    }

    private sealed record ContextMenuState(
        ContextMenuKind Kind,
        double X,
        double Y,
        string? FieldUuid,
        string? AttachmentUuid,
        int PageIndex,
        double? DocumentX,
        double? DocumentY);

    private sealed record AreaSnapshot(string FieldUuid, string AreaUuid, SigningFieldArea Area);

    private sealed record MoveState(double StartClientX, double StartClientY, IReadOnlyList<AreaSnapshot> Areas);

    private sealed record ResizeState(string FieldUuid, string AreaUuid, SigningFieldArea Area, SigningResizeHandle Handle, double StartClientX, double StartClientY);

    private sealed class PageSurfaceRect
    {
        public double Left { get; set; }

        public double Top { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }
    }

    private sealed class DrawState
    {
        public DrawState(SigningDocumentPage page, double startX, double startY, SigningFieldArea area, bool isSelectionBox = false)
        {
            Page = page;
            StartX = startX;
            StartY = startY;
            Area = area;
            IsSelectionBox = isSelectionBox;
        }

        public SigningDocumentPage Page { get; }

        public string PageKey => GetPageKey(Page);

        public double StartX { get; }

        public double StartY { get; }

        public SigningFieldArea Area { get; set; }

        public bool IsSelectionBox { get; }
    }
}
