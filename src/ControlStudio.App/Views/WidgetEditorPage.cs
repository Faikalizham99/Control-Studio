using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Accessibility;

namespace ControlStudio.App.Views;

internal sealed class WidgetEditorPage : ContentPage
{
    private readonly IWidgetEditorStore studio;
    private readonly Func<Task<string?>> choosePhotoSource;
    private readonly ILogger logger;
    private WidgetDesign draft;
    private Guid? selected;
    private readonly HashSet<string> ownedPhotos = [];
    private readonly string dragScope = Guid.NewGuid().ToString("N");
    private readonly Dictionary<Guid, WidgetLayerRow> rows = [];
    private readonly WidgetCanvasView canvas;
    private readonly ProfileNameEntry name = new() { MaxLength = 60, Placeholder = "My widget", BackgroundColor = Colors.Transparent, FontSize = 17, HeightRequest = 44 };
    private readonly WidgetSegments sizes;
    private readonly VerticalStackLayout form = new() { Padding = new Thickness(16, 8, 16, 28), Spacing = 14 };
    private readonly WidgetLayerList layerList = new();
    private readonly VerticalStackLayout precise = new() { Spacing = 6, IsVisible = false };
    private readonly ScrollView scroll = new();
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
    private readonly List<(string Title, Slider Slider, Label Label)> sliders = [];
    private bool updating, busy, saved, departed, closing;
    private Window? observedWindow;
    private bool Foreground => EditorPageActivity.IsForeground(this);
    private bool CanEdit => Foreground && !busy && !saved && !departed && !background.IsBusy && !canvas.IsInteracting && !layerList.IsReordering;

    public WidgetEditorPage(IWidgetEditorStore studio, IColourPicker colourPicker, ILogger logger, WidgetDesign? original = null, WidgetSize initialSize = WidgetSize.Small, Func<Task<string?>>? choosePhotoSource = null)
    {
        this.studio = studio; this.logger = logger;
        this.choosePhotoSource = choosePhotoSource ?? (() => DisplayActionSheetAsync("Add photo layer", "Cancel", null, "Photo Library", "Files"));
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
        canvas.InteractionChanged += (_, _) => { if (canvas.IsInteracting) CloseTools(); RefreshActions(); };
        sizes = new WidgetSegments(["Small", "Medium", "Large", "XL"], "EditorWidgetSize", index =>
        {
            if (!CanEdit) return;
            CloseTools(); draft = draft with { Size = WidgetDesignRules.SupportedSizes[index], CanvasDimensions = null };
            ApplyCanvasSizing(); RefreshControls(); UpdateCanvas();
        });
        sizes.Update(Array.IndexOf(WidgetDesignRules.SupportedSizes.ToArray(), draft.Size));
        background = new ColourPickerView(colourPicker, "WidgetBackground", "Background"); background.SetSelection(draft.BackgroundHex);
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
            SettingsControls.Text("XL requires iOS 27 or later. Changing size keeps layer positions proportionally, but photo crops may change.", 12, secondary: true)
        } }));
        if (original is not null && !WidgetDesignRules.IsSupported(original.Size))
            form.Children.Add(SettingsControls.Text("This design uses a retired layout. Review the new proportions, then save and assign an Extra Large slot. Cancel keeps your saved design unchanged.", 14, secondary: true));

        resetTool = Tool(StudioSymbol.Refresh, "Reset selected photo", () => EditLayer(layer => WidgetLayerEdits.Reset(layer, draft.Size, draft.CanvasDimensions)));
        rotateTool = Tool(StudioSymbol.RotateLeft, "Rotate selected photo", () => ToggleTool(rotateMenu));
        flipTool = Tool(StudioSymbol.Flip, "Flip selected photo", () => ToggleTool(flipMenu));
        opacityTool = Tool(StudioSymbol.Opacity, "Adjust photo opacity", () => ToggleTool(opacityMenu));
        blendTool = Tool(StudioSymbol.Blend, "Blend selected photo", () =>
        {
            var show = !blendMenu.IsVisible; CloseTools();
            if (show && draft.Layers.FirstOrDefault(x => x.Id == selected) is { } layer) blendMenu.Show(layer);
        });
        blendTool.AutomationId = "WidgetBlendTool";
        deleteTool = Tool(StudioSymbol.Delete, "Delete selected photo", () => _ = RunAsync(RemoveLayerAsync), destructive: true);
        tools = new WidgetHorizontalStrip([resetTool, rotateTool, flipTool, blendTool, opacityTool, deleteTool], "WidgetEditorToolbar", logger);
        foreach (var panel in new VisualElement[] { rotateMenu, flipMenu, opacityMenu, blendMenu })
            panel.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(IsVisible)) RefreshToolSelection(); };
        rotateMenu.Children.Add(MenuAction("Rotate Left", StudioSymbol.RotateLeft, () => EditLayer(layer => WidgetLayerEdits.Rotate(layer, -1, draft.Size, draft.CanvasDimensions), closeTools: false)));
        rotateMenu.Children.Add(MenuAction("Rotate Right", StudioSymbol.RotateRight, () => EditLayer(layer => WidgetLayerEdits.Rotate(layer, 1, draft.Size, draft.CanvasDimensions), closeTools: false)));
        flipMenu.Children.Add(MenuAction("Flip horizontally", StudioSymbol.Flip, () => EditLayer(layer => layer with { FlipHorizontal = !layer.FlipHorizontal }, closeTools: false)));
        flipMenu.Children.Add(MenuAction("Flip vertically", StudioSymbol.Flip, () => EditLayer(layer => layer with { FlipVertical = !layer.FlipVertical }, closeTools: false)));
        opacity.SetDynamicResource(Slider.MinimumTrackColorProperty, "AccentText"); SemanticProperties.SetDescription(opacity, "Selected photo opacity");
        opacity.ValueChanged += (_, args) => { if (!updating && CanEdit) EditLayer(layer => layer with { Opacity = args.NewValue }, closeTools: false, changesPlacement: false); };
        opacityMenu.Children.Add(opacityCaption); opacityMenu.Children.Add(opacity);
        form.Children.Add(Card(new VerticalStackLayout { Spacing = 12, Children =
        {
            SettingsControls.Text("Live preview", 22, bold: true), SettingsControls.Text("Pinch to resize • Drag past the widget edges to crop", 12, secondary: true),
            sizingCaption,
            SettingsControls.Text("The outlined widget is the saved area. Swipe empty workspace sideways to see beyond its edges. The faint outside photo is not saved.", 12, secondary: true),
            new WidgetCanvasViewport(canvas, logger), tools, rotateMenu, flipMenu, opacityMenu, blendMenu
        } }));

        addPhoto = AsyncAction("Add photo layer", StudioSymbol.Photo, "AddWidgetPhoto", PickPhotoAsync);
        var layerHeading = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var layerLabel = SettingsControls.Text("Layers", 21, bold: true); layerLabel.VerticalOptions = LayoutOptions.Center;
        layerHeading.Add(layerLabel); layerHeading.Add(layersCount, 1);
        layersCount.VerticalTextAlignment = TextAlignment.Center;
        form.Children.Add(Card(new VerticalStackLayout { Spacing = 12, Children = { layerHeading, addPhoto, layerList } }));
        precise.Children.Add(selectedTitle);
        precise.Children.Add(SettingsControls.Text("Full photo keeps the entire image visible. Turn it off to crop inside the layer frame. Moving past the widget edges hides content in either mode.", 12, secondary: true));
        var fitLabel = SettingsControls.Text("Show full photo", 14); fitLabel.VerticalOptions = LayoutOptions.Center;
        SemanticProperties.SetDescription(fullPhoto, "Show the full photo without cropping inside its layer");
        fullPhoto.Toggled += (_, args) => { if (!updating && CanEdit) EditLayer(layer => layer with { FitPhoto = args.Value }, closeTools: false); };
        var fitRow = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8 };
        fitRow.Add(fitLabel); fitRow.Add(fullPhoto, 1); precise.Children.Add(fitRow);
        precise.Children.Add(MenuAction("Centre on widget", StudioSymbol.Widgets, () => EditLayer(WidgetLayerEdits.Center, closeTools: false)));
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
        scroll.Content = form; layout.Add(ClipboardEditorControls.Header(Title, back, cancel)); layout.Add(scroll, 0, 1);
        Content = new WidgetEditorSurface(layout, () => CanEdit && selected is not null, ClearLayerSelection,
            canvas, tools, rotateMenu, flipMenu, opacityMenu, blendMenu, layerList, precise, background);
        selected = draft.Layers.LastOrDefault()?.Id; RefreshBackground(); RefreshLayers();
    }
    private static Border Card(View content) => ClipboardEditorControls.Surface(content, 22, 14);
    private WidgetToolButton Tool(StudioSymbol symbol, string title, Action action, bool destructive = false)
    {
        var button = new WidgetToolButton(symbol, title, destructive); button.Clicked += (_, _) => { if (CanEdit) action(); }; return button;
    }
    private void RefreshToolSelection()
    {
        resetTool.IsSelected = deleteTool.IsSelected = false;
        rotateTool.IsSelected = rotateMenu.IsVisible; flipTool.IsSelected = flipMenu.IsVisible;
        blendTool.IsSelected = blendMenu.IsVisible; opacityTool.IsSelected = opacityMenu.IsVisible;
        var active = rotateMenu.IsVisible ? rotateTool : flipMenu.IsVisible ? flipTool : blendMenu.IsVisible ? blendTool : opacityMenu.IsVisible ? opacityTool : null;
        if (active is not null) tools.Reveal(active);
    }
    private ClipboardEditorActionButton MenuAction(string text, StudioSymbol symbol, Action action)
    {
        var button = new ClipboardEditorActionButton(text, ClipboardEditorControls.Icon(symbol, size: 18), "Widget" + text.Replace(" ", ""));
        button.Clicked += (_, _) => { if (CanEdit) action(); }; return button;
    }
    private ClipboardEditorActionButton AsyncAction(string text, StudioSymbol symbol, string id, Func<Task> action)
    {
        var button = new ClipboardEditorActionButton(text, ClipboardEditorControls.Icon(symbol, size: 18), id, accentCaption: true);
        button.Clicked += async (_, _) => await RunAsync(action); return button;
    }
    private void CloseTools() { rotateMenu.IsVisible = flipMenu.IsVisible = opacityMenu.IsVisible = blendMenu.IsVisible = false; RefreshToolSelection(); }
    private void ToggleTool(VisualElement panel) { var show = !panel.IsVisible; CloseTools(); panel.IsVisible = show; }
    private void SelectLayer(Guid id)
    {
        if (busy || saved || departed || layerList.IsReordering) return;
        if (selected != id) CloseTools(); selected = id; RefreshLayers();
    }
    private void ClearLayerSelection()
    {
        if (!CanEdit || selected is null) return;
        CloseTools(); precise.IsVisible = false; selected = null; name.Unfocus(); RefreshLayers();
    }
    private void ToggleLayerLock(Guid id)
    {
        if (!CanEdit) return;
        var layer = draft.Layers.FirstOrDefault(x => x.Id == id); if (layer is null) return;
        if (selected == id) { CloseTools(); precise.IsVisible = false; }
        ReplaceLayer(layer with { Locked = !layer.Locked }); RefreshLayers();
        try { if (Foreground) SemanticScreenReader.Default.Announce(layer.Name + (layer.Locked ? " unlocked." : " locked. Unlock in Layers to move or resize.")); }
        catch (Exception ex) { logger.LogDebug("Widget lock announcement unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
    }
    private void ReplaceLayer(WidgetLayer layer) => draft = draft with { Layers = draft.Layers.Select(x => x.Id == layer.Id ? layer : x).ToArray() };
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
        slider.ValueChanged += (_, args) => { if (!updating && CanEdit) ApplySlider(index, args.NewValue); };
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
        layerList.CancelReorder();
        layersCount.Text = $"{draft.Layers.Count} {(draft.Layers.Count == 1 ? "layer" : "layers")}";
        foreach (var id in rows.Keys.Where(id => !draft.Layers.Any(x => x.Id == id)).ToArray())
        { layerList.Items.Remove(rows[id]); rows[id].ReleaseSource(); rows.Remove(id); }
        var index = 0;
        foreach (var layer in draft.Layers.Reverse())
        {
            if (!rows.TryGetValue(layer.Id, out var row))
            {
                row = new WidgetLayerRow(layer, studio.AssetPath(layer.AssetKey), dragScope, SelectLayer,
                    (id, visible) =>
                    {
                        if (!CanEdit) return;
                        var current = draft.Layers.FirstOrDefault(x => x.Id == id); if (current is null) return;
                        ReplaceLayer(current with { Visible = visible }); RefreshLayers();
                    }, id => { if (CanEdit) { selected = id; RefreshLayers(); _ = RunAsync(LayerMenuAsync); } },
                    ReorderLayers, ToggleLayerLock);
                rows.Add(layer.Id, row); layerList.Items.Add(row);
            }
            var currentIndex = layerList.Items.IndexOf(row);
            if (currentIndex != index) { layerList.Items.RemoveAt(currentIndex); layerList.Items.Insert(index, row); }
            row.Update(layer, selected == layer.Id); index++;
        }
        RefreshControls(); RefreshActions(); UpdateCanvas();
    }
    private void ReorderLayers(Guid source, Guid target)
    {
        if (!CanEdit) return;
        draft = draft with { Layers = WidgetLayerEdits.Reorder(draft.Layers, source, target) }; RefreshLayers();
    }
    private void RefreshControls()
    {
        updating = true;
        try
        {
            var layer = draft.Layers.FirstOrDefault(x => x.Id == selected);
            blendMenu.Update(layer);
            if (layer is not null) blendTool.Describe("Blend", layer.Name + ", " + WidgetBlending.Name(layer.BlendMode));
            if (layer is null) { precise.IsVisible = false; opacityCaption.Text = "Photo opacity"; return; }
            selectedTitle.Text = layer.Name + " · Size, position & crop"; opacityCaption.Text = $"Photo opacity · {layer.Opacity:P0}"; opacity.Value = layer.Opacity;
            opacityTool.Describe("Photo opacity", $"{layer.Opacity:P0}");
            fullPhoto.IsToggled = layer.FitPhoto;
            var values = new[] { layer.Frame.X, layer.Frame.Y, layer.Frame.Width, layer.Frame.Height, layer.Zoom, layer.CropX, layer.CropY };
            for (var i = 0; i < sliders.Count; i++)
            {
                sliders[i].Slider.Value = values[i]; sliders[i].Label.Text = i == 4 ? $"{sliders[i].Title} · {values[i]:0.00}×" : $"{sliders[i].Title} · {values[i]:P0}";
            }
            sliders[0].Slider.IsEnabled = sliders[1].Slider.IsEnabled = true;
            sliders[4].Slider.IsEnabled = !layer.FitPhoto;
            var bounds = WidgetDesignRules.Canvas(draft); var width = layer.Frame.Width * bounds.Width; var height = layer.Frame.Height * bounds.Height;
            var placement = WidgetPhotoGeometry.Image(layer, width, height);
            sliders[5].Slider.IsEnabled = !layer.FitPhoto && placement.Width > width + .001;
            sliders[6].Slider.IsEnabled = !layer.FitPhoto && placement.Height > height + .001;
        }
        finally { updating = false; }
    }
    private async Task LayerMenuAsync()
    {
        var layer = draft.Layers.FirstOrDefault(x => x.Id == selected); if (layer is null) return;
        var index = draft.Layers.ToList().FindIndex(x => x.Id == layer.Id);
        var choices = new List<string> { "Rename", layer.Locked ? "Unlock photo" : "Lock photo", "Deselect photo" };
        if (!layer.Locked) choices.AddRange(["Size, position & crop", "Centre on widget", layer.FitPhoto ? "Crop to frame" : "Show full photo"]);
        if (index < draft.Layers.Count - 1) choices.Add("Move forward"); if (index > 0) choices.Add("Move backward");
        var choice = await DisplayActionSheetAsync(layer.Name, "Cancel", "Delete layer", choices.ToArray());
        if (!Foreground || departed) return;
        switch (choice)
        {
            case "Lock photo": ReplaceLayer(layer with { Locked = true }); precise.IsVisible = false; break;
            case "Unlock photo": ReplaceLayer(layer with { Locked = false }); break;
            case "Deselect photo": selected = null; precise.IsVisible = false; break;
            case "Rename":
                var title = await DisplayPromptAsync("Layer name", "Use up to 40 characters.", initialValue: layer.Name, maxLength: 40);
                if (!Foreground || departed || title is null) return;
                var changed = layer with { Name = title.Trim() }; var next = draft with { Layers = draft.Layers.Select(x => x.Id == layer.Id ? changed : x).ToArray() };
                WidgetDesignRules.Validate(next); draft = next; break;
            case "Size, position & crop":
                precise.IsVisible = true;
                var animate = false;
#if IOS
                animate = !UIKit.UIAccessibility.IsReduceMotionEnabled;
#endif
                await scroll.ScrollToAsync(precisionCard, ScrollToPosition.Start, animate);
                break;
            case "Move forward": MoveLayer(1); break;
            case "Centre on widget": ReplaceLayer(WidgetLayerEdits.Center(layer)); break;
            case "Show full photo": if (!layer.Locked) ReplaceLayer(layer with { FitPhoto = true }); break;
            case "Crop to frame": if (!layer.Locked) ReplaceLayer(layer with { FitPhoto = false }); break;
            case "Move backward": MoveLayer(-1); break;
            case "Delete layer": await RemoveLayerAsync(); break;
        }
        RefreshLayers();
    }
    private void MoveLayer(int delta)
    {
        var layers = draft.Layers.ToList(); var index = layers.FindIndex(x => x.Id == selected); var target = index + delta;
        if (index >= 0 && target >= 0 && target < layers.Count) draft = draft with { Layers = WidgetLayerEdits.Reorder(layers, layers[index].Id, layers[target].Id) };
    }
    internal Task AddPhotoAsync() => RunAsync(PickPhotoAsync);
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
    private async Task RemoveLayerAsync()
    {
        var layer = draft.Layers.FirstOrDefault(x => x.Id == selected); if (layer is null) return;
        if (!await DisplayAlertAsync("Delete photo layer?", "Remove this layer from your widget draft? Your original photo is kept.", "Delete", "Cancel") || !Foreground || departed) return;
        CloseTools(); draft = draft with { Layers = draft.Layers.Where(x => x.Id != layer.Id).ToArray() }; selected = draft.Layers.LastOrDefault()?.Id; RefreshLayers();
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
    private void SetTransparent(bool value) { draft = draft with { TransparentBackground = value }; RefreshBackground(); UpdateCanvas(); }
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
        if (!draft.TransparentBackground && !background.CommitPendingHex()) { ShowStatus("Enter a valid background colour before saving."); return; }
        name.Unfocus(); draft = draft with { Name = name.Text?.Trim() ?? "", PreserveCanvas = true };
        // Export owns its own compositor on a worker thread. Release the preview's
        // buffers first rather than retaining two eight-layer compositions at once.
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
        var canInteract = !busy && !departed && !background.IsBusy && !canvas.IsInteracting;
        var canNavigate = canInteract && !layerList.IsReordering;
        form.IsEnabled = !busy && !saved && !departed && !background.IsBusy;
        // The list must stay enabled until its native drag recognizer finishes.
        layerList.IsEnabled = canInteract && !saved;
        save.IsEnabled = sizes.IsEnabled = name.IsEnabled = background.IsEnabled = canNavigate && !saved;
        canvas.IsEnabled = !layerList.IsReordering;
        back.IsEnabled = cancel.IsEnabled = canNavigate;
        addPhoto.IsEnabled = canNavigate && !saved && draft.Layers.Count < WidgetDesignRules.MaximumLayers;
        var layer = draft.Layers.FirstOrDefault(x => x.Id == selected);
        opacityTool.IsEnabled = deleteTool.IsEnabled = blendTool.IsEnabled = blendMenu.IsEnabled = tools.IsEnabled = canNavigate && !saved && layer is not null;
        resetTool.IsEnabled = rotateTool.IsEnabled = flipTool.IsEnabled = canNavigate && !saved && layer is { Locked: false };
        precise.IsEnabled = canNavigate && !saved && layer is { Locked: false };
    }
    private async Task RunAsync(Func<Task> operation)
    {
        if (!CanEdit) return;
        CloseTools(); busy = true; RefreshActions();
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
    private async void WindowActivated(object? sender, EventArgs args) { RefreshActions(); if (Foreground && !saved && !busy) ResumeCanvas(); if (saved && !busy) await TryCloseSavedAsync(); }
    private void WindowDeactivated(object? sender, EventArgs args) { layerList.CancelReorder(); canvas.EndInteraction(true); canvas.SuspendComposition(); CloseTools(); RefreshActions(); }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (observedWindow != Window)
        {
            StopObservingWindow(); observedWindow = Window;
            if (observedWindow is not null) { observedWindow.Activated += WindowActivated; observedWindow.Deactivated += WindowDeactivated; }
        }
        RefreshActions(); if (Foreground && !saved && !busy) ResumeCanvas(); if (saved && !busy) await TryCloseSavedAsync();
    }
    private void StopObservingWindow()
    {
        if (observedWindow is not null) { observedWindow.Activated -= WindowActivated; observedWindow.Deactivated -= WindowDeactivated; }
        observedWindow = null;
    }
    protected override void OnDisappearing()
    {
        layerList.CancelReorder(); canvas.EndInteraction(true); CloseTools(); RefreshActions();
        // A native picker can cover this page without removing it from MAUI navigation.
        // Keep activation recovery subscribed until this editor actually leaves the stack.
        base.OnDisappearing();
    }
    protected override bool OnBackButtonPressed() => busy || background.IsBusy || canvas.IsInteracting || layerList.IsReordering || base.OnBackButtonPressed();
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (!Navigation.NavigationStack.Contains(this))
        {
            departed = true; layerList.CancelReorder(); StopObservingWindow(); RefreshActions();
            canvas.ReleaseSources(); foreach (var row in rows.Values) row.ReleaseSource(); if (!busy) _ = CleanupAsync();
        }
    }
}
