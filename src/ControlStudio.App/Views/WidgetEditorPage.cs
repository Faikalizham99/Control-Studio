using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Accessibility;

namespace ControlStudio.App.Views;

internal sealed class WidgetEditorPage : ContentPage
{
    private readonly IWidgetEditorStore studio;
    private readonly Func<Task<string?>> choosePhotoSource;
    private readonly Func<Task<string?>> chooseLayerType;
    private readonly Func<string, string[], Task<string?>>? chooseLayerAction;
    private readonly ILogger logger;
    private WidgetDesign draft;
    private Guid? selected;
    private readonly HashSet<string> ownedPhotos = [];
    private readonly string dragScope = Guid.NewGuid().ToString("N");
    private readonly Dictionary<Guid, WidgetLayerRow> rows = [];
    private readonly WidgetCanvasView canvas;
    private readonly ProfileNameEntry name = new() { MaxLength = 60, Placeholder = "My widget", BackgroundColor = Colors.Transparent, FontSize = 17, HeightRequest = 44 };
    private readonly WidgetSegments sizes;
    private readonly WidgetEditorForm form = new() { Padding = new Thickness(16, 8, 16, 28), Spacing = 14 };
    private readonly WidgetLayerList layerList = new();
    private readonly VerticalStackLayout precise = new() { Spacing = 6, IsVisible = false };
    private readonly WidgetRotationControls rotation = new("PrecisionRotation");
    private readonly WidgetEditorScrollView scroll = new();
    private readonly VerticalStackLayout adjustments = new() { Spacing = 12, IsVisible = false, AutomationId = "WidgetAdjustments" };
    private readonly Border photoSettings;
    private readonly Label selectionHint = SettingsControls.Text("Tap a layer to select it. Add layers below.", 12, secondary: true);
    private readonly Border precisionCard;
    private readonly Label status = SettingsControls.Text("", 14, secondary: true);
    private readonly Label layersCount = SettingsControls.Text("", 13, secondary: true);
    private readonly Label selectedTitle = SettingsControls.Text("", 16, bold: true);
    private readonly Label backgroundStatus = SettingsControls.Text("", 12);
    private readonly Label backgroundMode = SettingsControls.Text("", 14, secondary: true);
    private readonly Label backgroundHelp = SettingsControls.Text("", 13, secondary: true);
    private readonly Label opacityCaption = SettingsControls.Text("Photo opacity", 14, bold: true);
    private readonly Label sizingCaption = SettingsControls.Text("", 12, secondary: true);
    private readonly Slider opacity = new() { Minimum = 0, Maximum = 1, MinimumHeightRequest = 44 };
    private readonly Switch fullPhoto = SettingsControls.RowSwitch("WidgetFullPhoto");
    private readonly ClipboardEditorActionButton save = new("Save widget", new ProfileGlyph(ProfileSymbol.Check), "SaveWidgetDesign", primary: true);
    private readonly ClipboardEditorActionButton cancel = new("Cancel", null, "CancelWidgetDesign", accentCaption: true);
    private readonly ClipboardEditorActionButton back = new("", new ProfileGlyph(ProfileSymbol.Back), "BackFromWidgetEditor");
    private readonly ClipboardEditorActionButton addPhoto;
    private readonly ClipboardEditorActionButton solid;
    private readonly WidgetToolButton resetTool, rotateTool, flipTool, opacityTool, deleteTool, blendTool;
    private readonly WidgetHorizontalStrip tools;
    private readonly WidgetBlendPanel blendMenu;
    private readonly VerticalStackLayout rotateMenu = new() { Spacing = 8, IsVisible = false };
    private readonly VerticalStackLayout flipMenu = new() { Spacing = 8, IsVisible = false };
    private readonly VerticalStackLayout opacityMenu = new() { Spacing = 4, IsVisible = false };
    private readonly ColourPickerView background;
    private readonly WidgetClockControls clockControls;
    private readonly List<View> photoOnlyPrecision = [];
    private readonly List<(string Title, Slider Slider, Label Label)> sliders = [];
    private bool updating, busy, saved, departed, closing;
    private Window? observedWindow;
    private bool Foreground => EditorPageActivity.IsForeground(this);
    private bool CanEdit => Foreground && !busy && !saved && !departed && !background.IsBusy && !clockControls.IsBusy && !canvas.IsInteracting && !layerList.IsReordering;

    public WidgetEditorPage(IWidgetEditorStore studio, IColourPicker colourPicker, ILogger logger, WidgetDesign? original = null, WidgetSize initialSize = WidgetSize.Small, Func<Task<string?>>? choosePhotoSource = null, Func<Task<string?>>? chooseLayerType = null, Func<string, string[], Task<string?>>? chooseLayerAction = null)
    {
        this.studio = studio; this.logger = logger;
        this.choosePhotoSource = choosePhotoSource ?? (() => DisplayActionSheetAsync("Add photo layer", "Cancel", null, "Photo Library", "Files"));
        this.chooseLayerType = chooseLayerType ?? (() => DisplayActionSheetAsync("Add layer", "Cancel", null, "Photo", "Clock", "Date", "Text"));
        this.chooseLayerAction = chooseLayerAction;
        draft = WidgetDesignRules.ForEditing(original ?? (WidgetDesignRules.New() with { Size = initialSize, TransparentBackground = OperatingSystem.IsIOSVersionAtLeast(26) }));
        ApplyCanvasSizing();
        Title = original is null ? "New widget" : "Edit widget"; AutomationId = "WidgetEditorPage"; WidgetStudioStyle.Page(this);
        name.Text = draft.Name; name.SetAppThemeColor(Entry.TextColorProperty, Color.FromArgb("#16171C"), Colors.White);
        SemanticProperties.SetDescription(name, "Widget name");
        canvas = new WidgetCanvasView(studio.AssetPath, SelectLayer, layer =>
        {
            if (saved || departed || busy) return;
            ReplaceLayer(layer); RefreshControls(); UpdateCanvas();
        }, ClearLayerSelection);
        canvas.RenderFailed += (_, message) => ShowStatus(message);
        blendMenu = new WidgetBlendPanel(mode =>
        {
            if (!CanEdit) return;
            EditLayer(layer => layer with { BlendMode = mode }, closeTools: false, changesPlacement: false);
        });
        blendMenu.LayoutChanging += (_, _) => scroll.PreserveViewport();
        blendMenu.PositionChanging += view => scroll.PreserveAnchor(view);
        canvas.InteractionChanged += (_, _) => { if (canvas.IsInteracting) ClosePhotoTools(); RefreshActions(); };
        sizes = new WidgetSegments(["Small", "Medium", "Large", "XL"], "EditorWidgetSize", index =>
        {
            if (!CanEdit) return;
            CloseTools(); draft = draft with { Size = WidgetDesignRules.SupportedSizes[index], CanvasDimensions = null };
            ApplyCanvasSizing(); RefreshControls(); UpdateCanvas();
        });
        sizes.Update(Array.IndexOf(WidgetDesignRules.SupportedSizes.ToArray(), draft.Size));
        background = new ColourPickerView(colourPicker, "WidgetBackground", "Background"); background.SetSelection(draft.BackgroundHex);
        clockControls = new(colourPicker) { CanInteract = () => CanEdit };
        clockControls.BusyChanged += (_, _) => RefreshActions();
        clockControls.LayoutChanging += (_, _) => scroll.PreserveViewport();
        clockControls.PositionChanging += view => scroll.PreserveAnchor(view);
        clockControls.PanelChanged += (_, _) => ClosePhotoTools();
        clockControls.InvalidColour += (_, _) => ShowStatus("Enter a valid text colour before continuing.");
        clockControls.DeleteRequested += (_, _) => { if (CanEdit) _ = RunAsync(RemoveLayerAsync); };
        clockControls.CentreHorizontallyRequested += (_, _) =>
        {
            if (CanEdit && CommitClockColour()) EditLayer(WidgetLayerEdits.CenterHorizontally, closeTools: false);
        };
        clockControls.NudgeRequested += NudgeLayer;
        clockControls.RotationRequested += RotateLayer;
        clockControls.Changed += changed =>
        {
            if (saved || departed || canvas.IsInteracting || layerList.IsReordering) return;
            var current = draft.Layers.FirstOrDefault(x => x.Id == changed.Id && WidgetTextLayers.IsText(x)); if (current is null) return;
            ReplaceLayer(current with { Clock = changed.Clock, Date = changed.Date, Text = changed.Text, Opacity = changed.Opacity }); RefreshControls();
            if (Foreground) UpdateCanvas(); else canvas.SuspendComposition();
        };
        layerList.CanStart = () => CanEdit;
        layerList.InteractionChanged += (_, _) => RefreshActions();
        layerList.Reordered += ReorderLayers;
        background.ColourChanged += (_, args) => { draft = draft with { BackgroundHex = args.Hex }; UpdateCanvas(); };
        background.BusyChanged += (_, _) => RefreshActions();
        backgroundStatus.SetDynamicResource(Label.TextColorProperty, "AccentText");
        backgroundStatus.HorizontalTextAlignment = TextAlignment.End; backgroundStatus.VerticalTextAlignment = TextAlignment.Center;
        var nameHeading = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) } };
        var nameLabel = SettingsControls.Text("Widget name", 14, secondary: true); nameLabel.VerticalOptions = LayoutOptions.Center;
        nameHeading.Add(nameLabel); nameHeading.Add(backgroundStatus, 1);
        form.Children.Add(Card(new VerticalStackLayout { Spacing = 10, Children =
        {
            nameHeading, ClipboardEditorControls.Field(name, new Thickness(12, 0)), SettingsControls.Text("Widget size", 14, secondary: true), sizes,
            SettingsControls.Text("XL requires iOS 27 or later. Changing size keeps layer positions proportionally, but photo crops may change.", 12, secondary: true),
            sizingCaption
        } }));
        if (original is not null && !WidgetDesignRules.IsSupported(original.Size))
            form.Children.Add(SettingsControls.Text("This design uses a retired layout. Review the new proportions, then save and assign an Extra Large slot. Cancel keeps your saved design unchanged.", 14, secondary: true));

        resetTool = Tool(StudioSymbol.Refresh, "Reset selected photo", () => EditLayer(layer => WidgetLayerEdits.Reset(layer, draft.Size, draft.CanvasDimensions)));
        rotateTool = Tool(StudioSymbol.RotateLeft, "Rotate selected photo", () => ToggleTool(rotateMenu));
        flipTool = Tool(StudioSymbol.Flip, "Flip selected photo", () => ToggleTool(flipMenu));
        opacityTool = Tool(StudioSymbol.Opacity, "Adjust photo opacity", () => ToggleTool(opacityMenu));
        blendTool = Tool(StudioSymbol.Blend, "Blend selected photo", () => ToggleTool(blendMenu));
        blendTool.AutomationId = "WidgetBlendTool";
        deleteTool = Tool(StudioSymbol.Delete, "Delete selected photo", () => _ = RunAsync(RemoveLayerAsync), destructive: true);
        tools = new WidgetHorizontalStrip([resetTool, rotateTool, flipTool, blendTool, opacityTool, deleteTool], "WidgetEditorToolbar");
        foreach (var panel in new VisualElement[] { rotateMenu, flipMenu, opacityMenu, blendMenu })
            panel.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(IsVisible)) RefreshToolSelection(); };
        rotateMenu.Children.Add(MenuAction("Rotate Left", StudioSymbol.RotateLeft, () => EditLayer(layer => WidgetLayerEdits.Rotate(layer, -1, draft.Size, draft.CanvasDimensions), closeTools: false)));
        rotateMenu.Children.Add(MenuAction("Rotate Right", StudioSymbol.RotateRight, () => EditLayer(layer => WidgetLayerEdits.Rotate(layer, 1, draft.Size, draft.CanvasDimensions), closeTools: false)));
        flipMenu.Children.Add(MenuAction("Flip horizontally", StudioSymbol.Flip, () => EditLayer(layer => layer with { FlipHorizontal = !layer.FlipHorizontal }, closeTools: false)));
        flipMenu.Children.Add(MenuAction("Flip vertically", StudioSymbol.Flip, () => EditLayer(layer => layer with { FlipVertical = !layer.FlipVertical }, closeTools: false)));
        opacity.SetDynamicResource(Slider.MinimumTrackColorProperty, "AccentText"); SemanticProperties.SetDescription(opacity, "Selected photo opacity");
        opacity.ValueChanged += (_, args) => { if (!updating && CanEdit) EditLayer(layer => layer with { Opacity = args.NewValue }, closeTools: false, changesPlacement: false); };
        opacityMenu.Children.Add(opacityCaption); opacityMenu.Children.Add(opacity);
        photoSettings = Card(new VerticalStackLayout { Spacing = 12, Children = { rotateMenu, flipMenu, opacityMenu, blendMenu } });
        photoSettings.IsVisible = false; adjustments.Children.Add(photoSettings); adjustments.Children.Add(clockControls);
        var previewTitle = SettingsControls.Text("Live preview", 22, bold: true); previewTitle.MaxLines = 1;
        SemanticProperties.SetHeadingLevel(previewTitle, SemanticHeadingLevel.Level2);
        selectionHint.VerticalTextAlignment = TextAlignment.Center; selectionHint.HorizontalTextAlignment = TextAlignment.Center; selectionHint.MaxLines = 1;
        var toolbarSlot = new Grid { Children = { tools, clockControls.Toolbar, selectionHint } };
        // Let the width-sized canvas request its complete height. A constrained star
        // row would shrink tall widgets instead of letting the page scroll.
        var previewBody = new Grid { RowSpacing = 8, RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(64) } };
        previewBody.Add(previewTitle); previewBody.Add(new WidgetCanvasViewport(canvas, logger), 0, 1); previewBody.Add(toolbarSlot, 0, 2);
        var preview = Card(previewBody); preview.AutomationId = "WidgetEditorLivePreview";
        form.Children.Add(preview); form.Children.Add(adjustments);
        addPhoto = AsyncAction("Add layer", StudioSymbol.Plus, "AddWidgetPhoto", ChooseLayerAsync);
        var layerHeading = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var layerLabel = SettingsControls.Text("Layers", 21, bold: true); layerLabel.VerticalOptions = LayoutOptions.Center;
        layerHeading.Add(layerLabel); layerHeading.Add(layersCount, 1);
        layersCount.VerticalTextAlignment = TextAlignment.Center;
        form.Children.Add(Card(new VerticalStackLayout { Spacing = 12, Children = { layerHeading, addPhoto, layerList } }));
        precise.Children.Add(selectedTitle);
        var photoHelp = SettingsControls.Text("Full photo keeps the entire image visible. Turn it off to crop inside the layer frame. Moving past the widget edges hides content in either mode.", 12, secondary: true);
        precise.Children.Add(photoHelp); photoOnlyPrecision.Add(photoHelp);
        var fitLabel = SettingsControls.Text("Show full photo", 14); fitLabel.VerticalOptions = LayoutOptions.Center;
        SemanticProperties.SetDescription(fullPhoto, "Show the full photo without cropping inside its layer");
        fullPhoto.Toggled += (_, args) => { if (!updating && CanEdit && draft.Layers.Any(x => x.Id == selected && x.Kind == WidgetLayerKind.Photo)) EditLayer(layer => layer with { FitPhoto = args.Value }, closeTools: false); };
        var fitRow = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8 };
        fitRow.Add(fitLabel); fitRow.Add(fullPhoto, 1); precise.Children.Add(fitRow);
        photoOnlyPrecision.Add(fitRow);
        precise.Children.Add(MenuAction("Centre on widget", StudioSymbol.Widgets, () => EditLayer(WidgetLayerEdits.Center, closeTools: false)));
        precise.Children.Add(MenuAction("Centre horizontally", StudioSymbol.CenterHorizontal, () => EditLayer(WidgetLayerEdits.CenterHorizontally, closeTools: false)));
        var nudge = new WidgetLayerNudgeControls("PrecisionPositionNudge");
        nudge.PositionChanging += view => scroll.PreserveAnchor(view);
        nudge.MoveRequested += direction => { if (precise.IsVisible) NudgeLayer(direction); };
        precise.Children.Add(nudge);
        rotation.PositionChanging += view => scroll.PreserveAnchor(view);
        rotation.Changed += degrees => { if (precise.IsVisible) RotateLayer(degrees); };
        precise.Children.Add(rotation);
        AddSlider("Horizontal position", -WidgetDesignRules.MaximumLayerOffset, WidgetDesignRules.MaximumLayerOffset);
        AddSlider("Vertical position", -WidgetDesignRules.MaximumLayerOffset, WidgetDesignRules.MaximumLayerOffset);
        AddSlider("Width", WidgetDesignRules.MinimumLayerSize, WidgetDesignRules.MaximumLayerSize);
        AddSlider("Height", WidgetDesignRules.MinimumLayerSize, WidgetDesignRules.MaximumLayerSize);
        AddSlider("Photo zoom", 1, 4); AddSlider("Crop horizontally", 0, 1); AddSlider("Crop vertically", 0, 1);
        precise.Children.Add(MenuAction("Done", StudioSymbol.Check, () => precise.IsVisible = false));
        // Hide the entire card when precision controls are closed, including its padding.
        precisionCard = Card(precise); precisionCard.SetBinding(IsVisibleProperty, new Binding(nameof(IsVisible), source: precise)); form.Children.Add(precisionCard);

        var chooseBackground = AsyncAction("Background", StudioSymbol.Widgets, "ChooseWidgetBackground", ChooseBackgroundAsync);
        solid = new ClipboardEditorActionButton("Use solid colour", ClipboardEditorControls.Icon(StudioSymbol.Opacity), "SolidWidgetBackground", accentCaption: true);
        solid.Clicked += (_, _) => { if (CanEdit) SetTransparent(false); };
        var backgroundHeading = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var backgroundLabel = SettingsControls.Text("Background", 21, bold: true); backgroundLabel.VerticalOptions = LayoutOptions.Center;
        chooseBackground.VerticalOptions = LayoutOptions.Center;
        backgroundHeading.Add(backgroundLabel); backgroundHeading.Add(chooseBackground, 1);
        form.Children.Add(Card(new VerticalStackLayout { Spacing = 10, Children = { backgroundHeading, backgroundMode, backgroundHelp, solid, background } }));
        status.IsVisible = false; form.Children.Add(status); form.Children.Add(save);
        form.Children.Add(SettingsControls.Text("Save, then open Widget slots in My Widgets and assign this design to a matching slot. Add Control Studio → Studio 1–10 on your Home Screen. iOS schedules updates.", 12, secondary: true));
        save.Clicked += async (_, _) => await RunAsync(SaveAsync);
        cancel.Clicked += async (_, _) => await ExitAsync(); back.Clicked += async (_, _) => await ExitAsync();
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        scroll.Content = form; scroll.AutomationId = "WidgetEditorSettingsScroll";
        form.Arranged += (_, _) => scroll.CompleteReflow();
        layout.Add(ClipboardEditorControls.Header(Title, back, cancel)); layout.Add(scroll, 0, 1);
        Content = new WidgetEditorSurface(layout, () => CanEdit && selected is not null, ClearLayerSelection,
            canvas, tools, clockControls.Toolbar, rotateMenu, flipMenu, opacityMenu, blendMenu, layerList, precise, background, clockControls);
        selected = draft.Layers.LastOrDefault()?.Id; RefreshBackground(); RefreshLayers();
    }
    private static Border Card(View content) => ClipboardEditorControls.Surface(content, 22, 14);
    private WidgetToolButton Tool(StudioSymbol symbol, string title, Action action, bool destructive = false)
    {
        var button = new WidgetToolButton(symbol, title, destructive); button.Clicked += (_, _) => { if (CanEdit) { scroll.PreserveAnchor(button); action(); } }; return button;
    }
    private void RefreshToolSelection()
    {
        resetTool.IsSelected = deleteTool.IsSelected = false;
        rotateTool.IsSelected = rotateMenu.IsVisible; flipTool.IsSelected = flipMenu.IsVisible;
        blendTool.IsSelected = blendMenu.IsVisible; opacityTool.IsSelected = opacityMenu.IsVisible;
        var active = rotateMenu.IsVisible ? rotateTool : flipMenu.IsVisible ? flipTool : blendMenu.IsVisible ? blendTool : opacityMenu.IsVisible ? opacityTool : null;
        if (photoSettings is not null) photoSettings.IsVisible = active is not null;
        adjustments.IsVisible = active is not null || clockControls.IsVisible;
    }
    private ClipboardEditorActionButton MenuAction(string text, StudioSymbol symbol, Action action)
    {
        var button = new ClipboardEditorActionButton(text, ClipboardEditorControls.Icon(symbol, size: 18), "Widget" + text.Replace(" ", ""));
        button.Clicked += (_, _) => { if (CanEdit) { scroll.PreserveAnchor(button); action(); } }; return button;
    }
    private ClipboardEditorActionButton AsyncAction(string text, StudioSymbol symbol, string id, Func<Task> action)
    {
        var button = new ClipboardEditorActionButton(text, ClipboardEditorControls.Icon(symbol, size: 18), id, accentCaption: true);
        button.Clicked += async (_, _) => { if (CanEdit) { scroll.PreserveAnchor(button); await RunAsync(action); } }; return button;
    }
    private void ClosePhotoTools() { scroll.PreserveViewport(); rotateMenu.IsVisible = flipMenu.IsVisible = opacityMenu.IsVisible = blendMenu.IsVisible = false; RefreshToolSelection(); }
    private void CloseTools() { ClosePhotoTools(); clockControls.ClosePanel(); }
    private void ToggleTool(VisualElement panel)
    {
        var show = !panel.IsVisible;
        scroll.PreserveViewport(); clockControls.ClosePanel();
        // Mount the destination first so switching tools never collapses every
        // panel between two native layout passes.
        if (show)
        {
            if (panel == blendMenu && draft.Layers.FirstOrDefault(x => x.Id == selected) is { } layer) blendMenu.Show(layer);
            else panel.IsVisible = true;
        }
        foreach (var other in new VisualElement[] { rotateMenu, flipMenu, opacityMenu, blendMenu })
            if (other != panel || !show) other.IsVisible = false;
        RefreshToolSelection();
    }
    private void SelectLayer(Guid id)
    {
        if (busy || saved || departed || layerList.IsReordering) return;
        if (!CommitClockColour()) return;
        if (selected != id) CloseTools(); selected = id; RefreshLayers();
    }
    private void ClearLayerSelection()
    {
        if (!CanEdit || selected is null || !CommitClockColour()) return;
        CloseTools(); precise.IsVisible = false; selected = null; name.Unfocus(); RefreshLayers();
    }
    private void ToggleLayerLock(Guid id)
    {
        if (!CanEdit) return;
        if (!CommitClockColour()) return;
        var layer = draft.Layers.FirstOrDefault(x => x.Id == id); if (layer is null) return;
        // Locking placement does not change the selected layer or close its
        // appearance tools. Keep precision controls mounted, but disabled.
        ReplaceLayer(layer with { Locked = !layer.Locked }); RefreshLayerState(id);
        try { if (Foreground) SemanticScreenReader.Default.Announce(layer.Name + (layer.Locked ? " unlocked." : " locked. Unlock in Layers to move or resize.")); }
        catch (Exception ex) { logger.LogDebug("Widget lock announcement unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
    }
    private void ReplaceLayer(WidgetLayer layer) => draft = draft with { Layers = draft.Layers.Select(x => x.Id == layer.Id ? layer : x).ToArray() };
    private void NudgeLayer(WidgetMoveDirection direction)
    {
        if (CanEdit && CommitClockColour())
            EditLayer(layer => WidgetLayerEdits.Nudge(layer, direction, draft.Size, draft.CanvasDimensions), closeTools: false);
    }
    private void RotateLayer(double degrees)
    {
        if (CanEdit && CommitClockColour()) EditLayer(layer => WidgetLayerRotation.Set(layer, degrees), closeTools: false);
    }
    private void EditLayer(Func<WidgetLayer, WidgetLayer> transform, bool closeTools = true, bool changesPlacement = true)
    {
        var layer = draft.Layers.FirstOrDefault(x => x.Id == selected); if (layer is null || (changesPlacement && layer.Locked)) return;
        if (closeTools) CloseTools(); var changed = transform(layer); if (changed == layer) return;
        ReplaceLayer(changed);
        if (layer.BlendMode != changed.BlendMode && rows.TryGetValue(layer.Id, out var row)) row.Update(changed, true);
        RefreshControls(); UpdateCanvas();
    }
    private void AddSlider(string title, double minimum, double maximum)
    {
        var label = SettingsControls.Text(title, 13); var slider = new Slider { Minimum = minimum, Maximum = maximum, MinimumHeightRequest = 44 };
        slider.SetDynamicResource(Slider.MinimumTrackColorProperty, "AccentText"); SemanticProperties.SetDescription(slider, title + " for selected photo");
        var index = sliders.Count; sliders.Add((title, slider, label)); precise.Children.Add(label); precise.Children.Add(slider);
        slider.ValueChanged += (_, args) => { if (!updating && CanEdit && slider.IsEnabled) ApplySlider(index, args.NewValue); };
    }
    private void ApplySlider(int index, double value) => EditLayer(layer =>
    {
        var f = layer.Frame;
        return index switch
        {
            0 => WidgetLayerEdits.Move(layer, value - f.X, 0),
            1 => WidgetLayerEdits.Move(layer, 0, value - f.Y),
            2 => layer.FitPhoto ? WidgetLayerEdits.Scale(layer, value / f.Width) : layer with { Frame = f with { Width = value } },
            3 => layer.FitPhoto ? WidgetLayerEdits.Scale(layer, value / f.Height) : layer with { Frame = f with { Height = value } },
            4 => layer with { Zoom = value }, 5 => layer with { CropX = value }, 6 => layer with { CropY = value }, _ => layer
        };
    }, closeTools: false);
    private void ApplyCanvasSizing()
    {
        var sizing = studio.CanvasSizing(draft.Size);
        if (sizing is not null && WidgetCanvasSizes.Valid(draft.Size, sizing.Canvas) && (sizing.Measured || draft.CanvasDimensions is null))
            draft = draft with { CanvasDimensions = sizing.Canvas };
        var bounds = WidgetDesignRules.Canvas(draft);
        var measured = sizing?.Measured == true && sizing.Canvas == draft.CanvasDimensions;
        sizingCaption.Text = $"{bounds.Width:0.#} × {bounds.Height:0.#} pt · " + (measured ? "Device widget size" : "Estimated size");
        if (!measured) sizingCaption.Text += ". After this size refreshes on your Home Screen, reopen the editor to use its measured size.";
    }
    private void ResumeCanvas()
    {
        canvas.EndInteraction(true); ApplyCanvasSizing(); RefreshControls(); UpdateCanvas();
    }
    private void UpdateCanvas() => canvas.Update(draft, selected);
    private void RefreshLayers()
    {
        scroll.PreserveViewport();
        layerList.CancelReorder();
        layersCount.Text = $"{draft.Layers.Count} / {WidgetDesignRules.MaximumLayers} layers";
        foreach (var id in rows.Keys.Where(id => !draft.Layers.Any(x => x.Id == id)).ToArray())
        { layerList.Items.Remove(rows[id]); rows[id].ReleaseSource(); rows.Remove(id); }
        var index = 0;
        foreach (var layer in draft.Layers.Reverse())
        {
            if (!rows.TryGetValue(layer.Id, out var row))
            {
                row = new WidgetLayerRow(layer, layer.Kind == WidgetLayerKind.Photo ? studio.AssetPath(layer.AssetKey) : null, dragScope, SelectLayer,
                    (id, visible) =>
                    {
                        if (!CanEdit) return;
                        var current = draft.Layers.FirstOrDefault(x => x.Id == id); if (current is null) return;
                        ReplaceLayer(current with { Visible = visible }); RefreshLayerState(id);
                    }, id => { if (CanEdit && CommitClockColour()) _ = RunAsync(() => LayerMenuAsync(id)); },
                    ReorderLayers, ToggleLayerLock);
                row.ActionStarting += (_, _) => scroll.PreserveAnchor(row);
                rows.Add(layer.Id, row); layerList.Items.Add(row);
            }
            var currentIndex = layerList.Items.IndexOf(row);
            if (currentIndex != index) { layerList.Items.RemoveAt(currentIndex); layerList.Items.Insert(index, row); }
            row.Update(layer, selected == layer.Id); index++;
        }
        RefreshControls(); RefreshActions(); UpdateCanvas();
    }
    private void RefreshLayerState(Guid id)
    {
        if (rows.TryGetValue(id, out var row) && draft.Layers.FirstOrDefault(x => x.Id == id) is { } layer) row.Update(layer, selected == id);
        if (selected == id) RefreshControls();
        RefreshActions(); UpdateCanvas();
    }
    private void ReorderLayers(Guid source, Guid target)
    {
        if (!CanEdit) return;
        scroll.CancelAnchor();
        draft = draft with { Layers = WidgetLayerEdits.Reorder(draft.Layers, source, target) }; RefreshLayers();
    }
    private void RefreshControls()
    {
        scroll.PreserveViewport();
        updating = true;
        try
        {
            var layer = draft.Layers.FirstOrDefault(x => x.Id == selected);
            clockControls.Update(layer);
            rotation.Update(layer);
            var isPhoto = layer?.Kind == WidgetLayerKind.Photo;
            tools.IsVisible = isPhoto;
            selectionHint.IsVisible = layer is null;
            RefreshToolSelection();
            blendMenu.Update(isPhoto ? layer : null);
            foreach (var view in photoOnlyPrecision) view.IsVisible = isPhoto;
            if (layer is not null) blendTool.Describe("Blend", layer.Name + ", " + WidgetBlending.Name(layer.BlendMode));
            if (layer is null) { precise.IsVisible = false; opacityCaption.Text = "Photo opacity"; return; }
            selectedTitle.Text = layer.Name + (isPhoto ? " · Size, position & crop" : " · Size & position"); opacityCaption.Text = $"Photo opacity · {layer.Opacity:P0}"; opacity.Value = layer.Opacity;
            opacityTool.Describe("Photo opacity", $"{layer.Opacity:P0}");
            fullPhoto.IsToggled = layer.FitPhoto;
            var values = new[] { layer.Frame.X, layer.Frame.Y, layer.Frame.Width, layer.Frame.Height, layer.Zoom, layer.CropX, layer.CropY };
            for (var i = 0; i < sliders.Count; i++)
            {
                sliders[i].Slider.Value = values[i]; sliders[i].Label.Text = i == 4 ? $"{sliders[i].Title} · {values[i]:0.00}×" : $"{sliders[i].Title} · {values[i]:P0}";
                sliders[i].Slider.IsVisible = sliders[i].Label.IsVisible = i < 4 || isPhoto;
            }
            sliders[0].Slider.IsEnabled = sliders[1].Slider.IsEnabled = true;
            sliders[4].Slider.IsEnabled = isPhoto && !layer.FitPhoto;
            var bounds = WidgetDesignRules.Canvas(draft); var width = layer.Frame.Width * bounds.Width; var height = layer.Frame.Height * bounds.Height;
            var placement = WidgetPhotoGeometry.Image(layer, width, height);
            sliders[5].Slider.IsEnabled = isPhoto && !layer.FitPhoto && placement.Width > width + .001;
            sliders[6].Slider.IsEnabled = isPhoto && !layer.FitPhoto && placement.Height > height + .001;
        }
        finally { updating = false; }
    }
    private async Task LayerMenuAsync(Guid id)
    {
        var layer = draft.Layers.FirstOrDefault(x => x.Id == id); if (layer is null) return;
        var index = draft.Layers.ToList().FindIndex(x => x.Id == layer.Id);
        var choices = new List<string> { "Rename", layer.Locked ? "Unlock layer" : "Lock layer" };
        if (draft.Layers.Count < WidgetDesignRules.MaximumLayers) choices.Insert(1, "Duplicate layer");
        if (selected == id) choices.Add("Deselect layer");
        if (!layer.Locked)
        {
            choices.Add(layer.Kind == WidgetLayerKind.Photo ? "Size, position & crop" : "Size & position"); choices.Add("Centre on widget");
            if (layer.Kind == WidgetLayerKind.Photo) choices.Add(layer.FitPhoto ? "Crop to frame" : "Show full photo");
        }
        if (index < draft.Layers.Count - 1) choices.Add("Move forward"); if (index > 0) choices.Add("Move backward");
        var choice = chooseLayerAction is null ? await DisplayActionSheetAsync(layer.Name, "Cancel", "Delete layer", choices.ToArray())
            : await chooseLayerAction(layer.Name, choices.ToArray());
        if (!Foreground || departed) return;
        if (choice is null or "Cancel") return;
        if (rows.TryGetValue(id, out var row)) scroll.PreserveAnchor(row);
        switch (choice)
        {
            case "Duplicate layer": DuplicateLayer(id); return;
            case "Lock layer": ReplaceLayer(layer with { Locked = true }); RefreshLayerState(id); return;
            case "Unlock layer": ReplaceLayer(layer with { Locked = false }); RefreshLayerState(id); return;
            case "Deselect layer": CloseTools(); selected = null; precise.IsVisible = false; break;
            case "Rename":
                var title = await DisplayPromptAsync("Layer name", "Use up to 40 characters.", initialValue: layer.Name, maxLength: 40);
                if (!Foreground || departed || title is null) return;
                if (rows.TryGetValue(id, out var renamedRow)) scroll.PreserveAnchor(renamedRow);
                var changed = layer with { Name = title.Trim() }; var next = draft with { Layers = draft.Layers.Select(x => x.Id == layer.Id ? changed : x).ToArray() };
                WidgetDesignRules.Validate(next); draft = next; RefreshLayerState(id); return;
            case "Size, position & crop":
            case "Size & position":
                scroll.PreserveViewport();
                if (selected != id) { CloseTools(); selected = id; }
                precise.IsVisible = true;
                break;
            case "Move forward": MoveLayer(id, 1); break;
            case "Centre on widget": ReplaceLayer(WidgetLayerEdits.Center(layer)); break;
            case "Show full photo": if (!layer.Locked) ReplaceLayer(layer with { FitPhoto = true }); break;
            case "Crop to frame": if (!layer.Locked) ReplaceLayer(layer with { FitPhoto = false }); break;
            case "Move backward": MoveLayer(id, -1); break;
            case "Delete layer": await RemoveLayerAsync(id); return;
        }
        RefreshLayers();
    }
    private void DuplicateLayer(Guid id)
    {
        var (updated, copy) = WidgetLayerEdits.Duplicate(draft, id);
        rows.TryGetValue(id, out var sourceRow);
        CloseTools(); precise.IsVisible = false; draft = updated; selected = copy.Id;
        RefreshLayers();
        if (sourceRow is not null && rows.TryGetValue(copy.Id, out var copyRow)) scroll.TransferAnchor(sourceRow, copyRow);
        ShowStatus(copy.Name + " created. The copy is selected.");
    }
    private void MoveLayer(Guid id, int delta)
    {
        scroll.CancelAnchor();
        var layers = draft.Layers.ToList(); var index = layers.FindIndex(x => x.Id == id); var target = index + delta;
        if (index >= 0 && target >= 0 && target < layers.Count) draft = draft with { Layers = WidgetLayerEdits.Reorder(layers, layers[index].Id, layers[target].Id) };
    }
    internal Task AddPhotoAsync() => RunAsync(PickPhotoAsync);
    internal Task AddLayerAsync() => RunAsync(ChooseLayerAsync);
    private async Task ChooseLayerAsync()
    {
        if (!CommitClockColour() || draft.Layers.Count >= WidgetDesignRules.MaximumLayers) return;
        name.Unfocus(); var choice = await chooseLayerType();
        if (!Foreground || departed) return;
        if (choice == "Photo") await PickPhotoAsync();
        else if (choice is "Clock" or "Date" or "Text")
        {
            if (choice != "Text" && OperatingSystem.IsIOS() && !OperatingSystem.IsIOSVersionAtLeast(18)) { ShowStatus("Live text layers require iOS 18 or later."); return; }
            var ordinal = 1; while (draft.Layers.Any(x => x.Name == choice + " " + ordinal)) ordinal++;
            var colour = Application.Current?.Resources.TryGetValue("Accent", out var accent) == true && accent is Color value ? AppearanceColors.ToRgbHex(value) : "#007AFF";
            var layer = choice switch { "Date" => WidgetDates.New("Date " + ordinal, colour), "Text" => WidgetTexts.New("Text " + ordinal, colour), _ => WidgetClocks.New("Clock " + ordinal, colour) };
            draft = draft with { Layers = draft.Layers.Append(layer).ToArray() }; selected = layer.Id; RefreshLayers();
        }
    }
    private bool CommitClockColour()
    {
        if (clockControls.CommitPendingColour()) return true;
        ShowStatus("Enter a valid text colour before continuing."); return false;
    }
    private async Task PickPhotoAsync()
    {
        if (draft.Layers.Count >= WidgetDesignRules.MaximumLayers) return;
        name.Unfocus();
        var choice = await choosePhotoSource();
        if (!Foreground || departed || choice is not ("Photo Library" or "Files")) return;
        var layer = await studio.PickPhotoAsync(choice == "Photo Library"); if (layer is null) return;
        ownedPhotos.Add(layer.AssetKey); if (departed) { await CleanupAsync(); return; }
        var ordinal = 1; while (draft.Layers.Any(x => x.Name == "Photo " + ordinal)) ordinal++;
        layer = WidgetLayerEdits.Reset(layer with { Name = "Photo " + ordinal }, draft.Size, draft.CanvasDimensions);
        draft = draft with { Layers = draft.Layers.Append(layer).ToArray() }; selected = layer.Id; RefreshLayers();
    }
    private Task RemoveLayerAsync() => RemoveLayerAsync(selected);
    private async Task RemoveLayerAsync(Guid? id)
    {
        var layer = draft.Layers.FirstOrDefault(x => x.Id == id); if (layer is null) return;
        if (!await DisplayAlertAsync("Delete layer?", layer.Kind == WidgetLayerKind.Photo ? "Remove this layer from your widget draft? Your original photo is kept." : "Remove this " + WidgetTextLayers.Name(layer) + " from your widget draft?", "Delete", "Cancel") || !Foreground || departed) return;
        if (rows.TryGetValue(layer.Id, out var row)) scroll.PreserveAnchor(row);
        if (selected == layer.Id) CloseTools();
        draft = draft with { Layers = draft.Layers.Where(x => x.Id != layer.Id).ToArray() };
        if (selected == layer.Id) selected = draft.Layers.LastOrDefault()?.Id;
        RefreshLayers();
        if (ownedPhotos.Contains(layer.AssetKey) && !draft.Layers.Any(x => x.AssetKey == layer.AssetKey))
        {
            try { await studio.RemoveDraftPhotosAsync([layer.AssetKey]); ownedPhotos.Remove(layer.AssetKey); }
            catch (Exception ex) { logger.LogWarning("Removed widget draft photo cleanup failed. ErrorType={ErrorType}", ex.GetType().Name); }
        }
    }
    private async Task ChooseBackgroundAsync()
    {
        var choice = await DisplayActionSheetAsync("Widget background", "Cancel", null, "Transparent (Default)", "Solid colour");
        if (!Foreground || departed) return;
        if (choice == "Transparent (Default)")
        {
            if (!OperatingSystem.IsIOSVersionAtLeast(26)) { ShowStatus("Transparent Home Screen widgets require iOS 26 or later."); return; }
            SetTransparent(true);
        }
        else if (choice == "Solid colour") SetTransparent(false);
    }
    private void SetTransparent(bool value) { scroll.PreserveViewport(); draft = draft with { TransparentBackground = value }; RefreshBackground(); UpdateCanvas(); }
    private void RefreshBackground()
    {
        backgroundStatus.Text = "Background: " + (draft.TransparentBackground ? "Transparent" : "Solid colour");
        backgroundMode.Text = draft.TransparentBackground ? "Transparent (Default)" : "Solid colour";
        backgroundHelp.Text = draft.TransparentBackground
            ? "No background fill. The checkerboard marks transparent areas and is not included in your widget. Home Screen appearance also follows iOS widget styling."
            : "A solid colour behind your layers. Choose a preset or enter a custom colour.";
        background.IsVisible = !draft.TransparentBackground; solid.IsVisible = draft.TransparentBackground;
    }
    private async Task SaveAsync()
    {
        if (!CommitClockColour()) return;
        if (!draft.TransparentBackground && !background.CommitPendingHex()) { ShowStatus("Enter a valid background colour before saving."); return; }
        name.Unfocus(); draft = draft with { Name = name.Text?.Trim() ?? "", PreserveCanvas = true };
        ShowStatus("Saving widget…");
        // Export owns its own compositor on a worker thread. Release the preview's
        // buffers first rather than retaining two full compositions at once.
        canvas.SuspendComposition();
        var result = await studio.SaveAsync(draft); draft = result.Design; saved = true; await CleanupAsync();
        if (!Foreground || departed) return;
        if (!result.Published) await DisplayAlertAsync("Saved on this device", "Your design is saved, but the Home Screen update could not be published. Check App Group signing in Settings, then tap Refresh widgets in My Widgets.", "OK");
        if (Foreground && !departed) await CloseSavedAsync();
    }
    private void RefreshActions()
    {
        // Keep the canvas enabled during a gesture; disabling its parent would cancel native recognition.
        // Availability follows operation state. Native picker dismissal can briefly report an
        // inactive application; persisting that snapshot in IsEnabled can lock the whole editor.
        // Execution still checks the current navigation owner and MAUI window activation.
        var canInteract = !busy && !departed && !background.IsBusy && !clockControls.IsBusy && !canvas.IsInteracting;
        var canNavigate = canInteract && !layerList.IsReordering;
        // The canvas lives inside this form. Disabling its ancestor during a
        // native pinch/drag would cancel the gesture that just started.
        form.IsEnabled = !busy && !saved && !departed && !background.IsBusy && !clockControls.IsBusy;
        // The list must stay enabled until its native drag recognizer finishes.
        layerList.IsEnabled = canInteract && !saved;
        save.IsEnabled = sizes.IsEnabled = name.IsEnabled = background.IsEnabled = canNavigate && !saved;
        // Keep operation guards explicit without disabling an active gesture.
        canvas.IsEnabled = !busy && !saved && !departed && !background.IsBusy && !clockControls.IsBusy && !layerList.IsReordering;
        back.IsEnabled = cancel.IsEnabled = canNavigate;
        addPhoto.IsEnabled = canNavigate && !saved && draft.Layers.Count < WidgetDesignRules.MaximumLayers;
        addPhoto.Describe(draft.Layers.Count >= WidgetDesignRules.MaximumLayers
            ? $"Add layer. Limit of {WidgetDesignRules.MaximumLayers} layers reached. Remove a layer to add another."
            : $"Add layer. {draft.Layers.Count} of {WidgetDesignRules.MaximumLayers} layers used.");
        var layer = draft.Layers.FirstOrDefault(x => x.Id == selected);
        clockControls.SetAvailable(canNavigate && !saved && layer is not null && WidgetTextLayers.IsText(layer));
        opacityTool.IsEnabled = deleteTool.IsEnabled = blendTool.IsEnabled = blendMenu.IsEnabled = tools.IsEnabled = canNavigate && !saved && layer?.Kind == WidgetLayerKind.Photo;
        resetTool.IsEnabled = rotateTool.IsEnabled = flipTool.IsEnabled = rotateMenu.IsEnabled = flipMenu.IsEnabled = canNavigate && !saved && layer is { Kind: WidgetLayerKind.Photo, Locked: false };
        precise.IsEnabled = canNavigate && !saved && layer is { Locked: false };
    }
    private async Task RunAsync(Func<Task> operation)
    {
        if (!CanEdit) return;
        busy = true; RefreshActions();
        try { await operation(); }
        catch (Exception ex)
        { logger.LogWarning("Widget editor operation failed. ErrorType={ErrorType}", ex.GetType().Name); if (!departed) ShowStatus(ex is IOException ? ex.Message : "The widget could not be updated. Try again."); }
        finally
        {
            busy = false; RefreshActions();
            if (departed) await CleanupAsync();
            else if (!saved && Foreground) UpdateCanvas();
        }
    }
    private void ShowStatus(string message)
    {
        scroll.PreserveViewport();
        status.Text = message; status.IsVisible = true;
        try { if (Foreground) SemanticScreenReader.Default.Announce(message); }
        catch (Exception ex) { logger.LogDebug("Widget announcement unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
    }
    private async Task CleanupAsync()
    {
        try { await studio.RemoveDraftPhotosAsync(ownedPhotos); ownedPhotos.Clear(); }
        catch (Exception ex) { logger.LogWarning("Widget draft cleanup failed. ErrorType={ErrorType}", ex.GetType().Name); }
    }
    private async Task CloseSavedAsync()
    {
        if (closing || departed || !Foreground) return; closing = true;
        try { await Navigation.PopAsync(); } finally { closing = false; }
    }
    private async Task ExitAsync()
    {
        if (!saved) { await RunAsync(() => Navigation.PopAsync()); return; }
        if (!busy) await TryCloseSavedAsync();
    }
    private async Task TryCloseSavedAsync()
    {
        try { await CloseSavedAsync(); }
        catch (Exception ex) { logger.LogWarning("Widget editor dismissal failed. ErrorType={ErrorType}", ex.GetType().Name); ShowStatus("Your widget is saved. Tap Back to return."); }
    }
    private void SuspendEditor()
    {
        scroll.CancelAnchor(); layerList.CancelReorder(); canvas.EndInteraction(true); canvas.SuspendComposition();
        blendMenu.SetForeground(false);
        clockControls.ResetPressedState();
        foreach (var tool in new[] { resetTool, rotateTool, flipTool, opacityTool, deleteTool, blendTool }) tool.Scale = 1;
        RefreshActions();
    }
    private async void WindowActivated(object? sender, EventArgs args) { TimeZoneInfo.ClearCachedData(); RefreshActions(); blendMenu.SetForeground(Foreground); if (Foreground && !saved && !busy) ResumeCanvas(); if (saved && !busy) await TryCloseSavedAsync(); }
    private void WindowDeactivated(object? sender, EventArgs args) => SuspendEditor();
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (observedWindow != Window)
        {
            StopObservingWindow(); observedWindow = Window;
            if (observedWindow is not null) { observedWindow.Activated += WindowActivated; observedWindow.Deactivated += WindowDeactivated; }
        }
        RefreshActions(); blendMenu.SetForeground(Foreground); if (Foreground && !saved && !busy) ResumeCanvas(); if (saved && !busy) await TryCloseSavedAsync();
    }
    private void StopObservingWindow()
    {
        if (observedWindow is not null) { observedWindow.Activated -= WindowActivated; observedWindow.Deactivated -= WindowDeactivated; }
        observedWindow = null;
    }
    protected override void OnDisappearing()
    {
        SuspendEditor();
        // A native picker can cover this page without removing it from MAUI navigation.
        // Keep activation recovery subscribed until this editor actually leaves the stack.
        base.OnDisappearing();
    }
    protected override bool OnBackButtonPressed() => busy || background.IsBusy || clockControls.IsBusy || canvas.IsInteracting || layerList.IsReordering || base.OnBackButtonPressed();
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (!Navigation.NavigationStack.Contains(this))
        {
            departed = true; layerList.CancelReorder(); StopObservingWindow(); RefreshActions(); scroll.ReleaseViewport();
            canvas.ReleaseSources(); foreach (var row in rows.Values) row.ReleaseSource(); if (!busy) _ = CleanupAsync();
        }
    }
}
