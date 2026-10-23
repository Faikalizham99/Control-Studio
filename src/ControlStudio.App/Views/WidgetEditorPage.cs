using ControlStudio.App.Platforms.iOS;
using ControlStudio.Core.Widgets;
using Microsoft.Extensions.Logging;

namespace ControlStudio.App.Views;

internal sealed class WidgetEditorPage : ContentPage
{
    private readonly WidgetStudioService studio;
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
    private readonly VerticalStackLayout layerList = new() { Spacing = 8 };
    private readonly VerticalStackLayout precise = new() { Spacing = 6, IsVisible = false };
    private readonly ScrollView scroll = new();
    private readonly Border precisionCard;
    private readonly Label status = SettingsControls.Text("", 14, secondary: true);
    private readonly Label layersCount = SettingsControls.Text("", 13, secondary: true);
    private readonly Label selectedTitle = SettingsControls.Text("", 16, bold: true);
    private readonly Label backgroundStatus = SettingsControls.Text("", 12);
    private readonly Label backgroundMode = SettingsControls.Text("", 14, secondary: true);
    private readonly Label backgroundHelp = SettingsControls.Text("", 13, secondary: true);
    private readonly Label opacityCaption = SettingsControls.Text("Opacity", 11);
    private readonly Slider opacity = new() { Minimum = 0, Maximum = 1, MinimumHeightRequest = 44 };
    private readonly ClipboardEditorActionButton save = new("Save widget", new ProfileGlyph(ProfileSymbol.Check), "SaveWidgetDesign", primary: true);
    private readonly ClipboardEditorActionButton cancel = new("Cancel", null, "CancelWidgetDesign", accentCaption: true);
    private readonly ClipboardEditorActionButton back = new("", new ProfileGlyph(ProfileSymbol.Back), "BackFromWidgetEditor");
    private readonly ClipboardEditorActionButton addPhoto;
    private readonly ClipboardEditorActionButton solid;
    private readonly ClipboardIconButton resetTool, rotateTool, flipTool, opacityTool, deleteTool;
    private readonly VerticalStackLayout rotateMenu = new() { Spacing = 8, IsVisible = false };
    private readonly VerticalStackLayout flipMenu = new() { Spacing = 8, IsVisible = false };
    private readonly VerticalStackLayout opacityMenu = new() { Spacing = 4, IsVisible = false };
    private readonly ColourPickerView background;
    private readonly List<(string Title, Slider Slider, Label Label)> sliders = [];
    private bool updating, busy, saved, departed, active, closing;
    private Window? observedWindow;
    private bool Foreground => active && UIKit.UIApplication.SharedApplication.ApplicationState == UIKit.UIApplicationState.Active;
    private bool CanEdit => Foreground && !busy && !saved && !departed && !background.IsBusy && !canvas.IsInteracting;

    public WidgetEditorPage(WidgetStudioService studio, AppleColorPicker colourPicker, ILogger logger, WidgetDesign? original = null, WidgetSize initialSize = WidgetSize.Small)
    {
        this.studio = studio; this.logger = logger;
        draft = WidgetDesignRules.ForEditing(original ?? (WidgetDesignRules.New() with { Size = initialSize, TransparentBackground = OperatingSystem.IsIOSVersionAtLeast(26) }));
        Title = original is null ? "New widget" : "Edit widget"; AutomationId = "WidgetEditorPage"; WidgetStudioStyle.Page(this);
        name.Text = draft.Name; name.SetAppThemeColor(Entry.TextColorProperty, Color.FromArgb("#16171C"), Colors.White);
        SemanticProperties.SetDescription(name, "Widget name");
        canvas = new WidgetCanvasView(studio.Repository.AssetPath, SelectLayer, layer =>
        {
            if (saved || departed || busy) return;
            ReplaceLayer(layer); RefreshControls(); UpdateCanvas();
        });
        canvas.InteractionChanged += (_, _) => { if (canvas.IsInteracting) CloseTools(); RefreshActions(); };
        sizes = new WidgetSegments(["Small", "Medium", "Large", "XL"], "EditorWidgetSize", index =>
        {
            if (!CanEdit) return;
            CloseTools(); draft = draft with { Size = WidgetDesignRules.SupportedSizes[index] }; RefreshControls(); UpdateCanvas();
        });
        sizes.Update(Array.IndexOf(WidgetDesignRules.SupportedSizes.ToArray(), draft.Size));
        background = new ColourPickerView(colourPicker, "WidgetBackground", "Background"); background.SetSelection(draft.BackgroundHex);
        background.ColourChanged += (_, args) => { draft = draft with { BackgroundHex = args.Hex }; UpdateCanvas(); };
        background.BusyChanged += (_, _) => RefreshActions();
        backgroundStatus.SetDynamicResource(Label.TextColorProperty, "AccentText");
        backgroundStatus.HorizontalTextAlignment = TextAlignment.End; backgroundStatus.VerticalTextAlignment = TextAlignment.Center;
        var nameHeading = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) } };
        nameHeading.Add(SettingsControls.Text("Widget name", 14, secondary: true)); nameHeading.Add(backgroundStatus, 1);
        form.Children.Add(Card(new VerticalStackLayout { Spacing = 10, Children =
        {
            nameHeading, ClipboardEditorControls.Field(name, new Thickness(12, 0)), SettingsControls.Text("Widget size", 14, secondary: true), sizes,
            SettingsControls.Text("XL requires iOS 27 or later. Changing size keeps layer positions proportionally, but photo crops may change.", 12, secondary: true)
        } }));
        if (original is not null && !WidgetDesignRules.IsSupported(original.Size))
            form.Children.Add(SettingsControls.Text("This design uses a retired layout. Review the new proportions, then save and assign an Extra Large slot. Cancel keeps your saved design unchanged.", 14, secondary: true));

        resetTool = Tool(StudioSymbol.Refresh, "Reset selected photo", () => EditLayer(layer => WidgetLayerEdits.Reset(layer, draft.Size)));
        rotateTool = Tool(StudioSymbol.RotateLeft, "Rotate selected photo", () => ToggleTool(rotateMenu));
        flipTool = Tool(StudioSymbol.Flip, "Flip selected photo", () => ToggleTool(flipMenu));
        opacityTool = Tool(StudioSymbol.Opacity, "Adjust photo opacity", () => ToggleTool(opacityMenu));
        deleteTool = Tool(StudioSymbol.Delete, "Delete selected photo", () => _ = RunAsync(RemoveLayerAsync), destructive: true);
        var tools = new Grid { ColumnSpacing = 4 };
        var toolViews = new[] { ToolCell(resetTool, "Reset"), ToolCell(rotateTool, "Rotate"), ToolCell(flipTool, "Flip"), ToolCell(opacityTool, opacityCaption), ToolCell(deleteTool, "Delete", true) };
        for (var i = 0; i < toolViews.Length; i++) { tools.ColumnDefinitions.Add(new(GridLength.Star)); tools.Add(toolViews[i], i); }
        rotateMenu.Children.Add(MenuAction("Rotate Left", StudioSymbol.RotateLeft, () => EditLayer(layer => WidgetLayerEdits.Rotate(layer, -1), closeTools: false)));
        rotateMenu.Children.Add(MenuAction("Rotate Right", StudioSymbol.RotateRight, () => EditLayer(layer => WidgetLayerEdits.Rotate(layer, 1), closeTools: false)));
        flipMenu.Children.Add(MenuAction("Flip horizontally", StudioSymbol.Flip, () => EditLayer(layer => layer with { FlipHorizontal = !layer.FlipHorizontal }, closeTools: false)));
        flipMenu.Children.Add(MenuAction("Flip vertically", StudioSymbol.Flip, () => EditLayer(layer => layer with { FlipVertical = !layer.FlipVertical }, closeTools: false)));
        opacity.SetDynamicResource(Slider.MinimumTrackColorProperty, "AccentText"); SemanticProperties.SetDescription(opacity, "Selected photo opacity");
        opacity.ValueChanged += (_, args) => { if (!updating && CanEdit) EditLayer(layer => layer with { Opacity = args.NewValue }, closeTools: false); };
        opacityMenu.Children.Add(SettingsControls.Text("Photo opacity", 14)); opacityMenu.Children.Add(opacity);
        form.Children.Add(Card(new VerticalStackLayout { Spacing = 12, Children =
        {
            SettingsControls.Text("Live preview", 22, bold: true), SettingsControls.Text("Pinch to resize • Drag to move • Tap a layer to select it", 12, secondary: true),
            canvas, tools, rotateMenu, flipMenu, opacityMenu
        } }));

        addPhoto = AsyncAction("Add photo layer", StudioSymbol.Photo, "AddWidgetPhoto", PickPhotoAsync);
        var layerHeading = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        layerHeading.Add(SettingsControls.Text("Layers", 21, bold: true)); layerHeading.Add(layersCount, 1);
        layersCount.VerticalTextAlignment = TextAlignment.Center;
        form.Children.Add(Card(new VerticalStackLayout { Spacing = 12, Children = { layerHeading, addPhoto, layerList } }));
        precise.Children.Add(selectedTitle);
        precise.Children.Add(SettingsControls.Text("Adjust width and height independently. Zoom and crop position the photo inside its frame.", 12, secondary: true));
        AddSlider("Horizontal position", 0, 1); AddSlider("Vertical position", 0, 1); AddSlider("Width", .05, 1); AddSlider("Height", .05, 1);
        AddSlider("Photo zoom", 1, 4); AddSlider("Crop horizontally", 0, 1); AddSlider("Crop vertically", 0, 1);
        precise.Children.Add(MenuAction("Done", StudioSymbol.Check, () => precise.IsVisible = false));
        // Hide the entire card when precision controls are closed, including its padding.
        precisionCard = Card(precise); precisionCard.SetBinding(IsVisibleProperty, new Binding(nameof(IsVisible), source: precise)); form.Children.Add(precisionCard);

        var chooseBackground = AsyncAction("Background", StudioSymbol.Widgets, "ChooseWidgetBackground", ChooseBackgroundAsync);
        solid = new ClipboardEditorActionButton("Use solid colour", ClipboardEditorControls.Icon(StudioSymbol.Opacity), "SolidWidgetBackground", accentCaption: true);
        solid.Clicked += (_, _) => { if (CanEdit) SetTransparent(false); };
        var backgroundHeading = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        backgroundHeading.Add(SettingsControls.Text("Background", 21, bold: true)); backgroundHeading.Add(chooseBackground, 1);
        form.Children.Add(Card(new VerticalStackLayout { Spacing = 10, Children = { backgroundHeading, backgroundMode, backgroundHelp, solid, background } }));
        status.IsVisible = false; form.Children.Add(status); form.Children.Add(save);
        form.Children.Add(SettingsControls.Text("Save, then open Widget slots in My Widgets and assign this design to a matching slot. Add Control Studio → Studio 1–10 on your Home Screen. iOS schedules updates.", 12, secondary: true));
        save.Clicked += async (_, _) => await RunAsync(SaveAsync);
        cancel.Clicked += async (_, _) => await ExitAsync(); back.Clicked += async (_, _) => await ExitAsync();
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        scroll.Content = form; layout.Add(ClipboardEditorControls.Header(Title, back, cancel)); layout.Add(scroll, 0, 1); Content = layout;
        selected = draft.Layers.LastOrDefault()?.Id; RefreshBackground(); RefreshLayers();
    }
    private static Border Card(View content) => ClipboardEditorControls.Surface(content, 22, 14);
    private ClipboardIconButton Tool(StudioSymbol symbol, string title, Action action, bool destructive = false)
    {
        var button = new ClipboardIconButton(symbol, title, destructive); button.Clicked += (_, _) => { if (CanEdit) action(); }; return button;
    }
    private static View ToolCell(View button, string text, bool destructive = false) => ToolCell(button, SettingsControls.Text(text, 11), destructive);
    private static View ToolCell(View button, Label label, bool destructive = false)
    {
        label.HorizontalTextAlignment = TextAlignment.Center; label.MaxLines = 2;
        if (destructive) label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#B5223E"), Color.FromArgb("#FF8096"));
        button.HorizontalOptions = LayoutOptions.Center;
        return new VerticalStackLayout { Spacing = 4, Children = { button, label } };
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
    private void CloseTools() { rotateMenu.IsVisible = flipMenu.IsVisible = opacityMenu.IsVisible = false; }
    private void ToggleTool(VisualElement panel) { var show = !panel.IsVisible; CloseTools(); panel.IsVisible = show; }
    private void SelectLayer(Guid id)
    {
        if (busy || saved || departed) return;
        if (selected != id) CloseTools(); selected = id; RefreshLayers();
    }
    private void ReplaceLayer(WidgetLayer layer) => draft = draft with { Layers = draft.Layers.Select(x => x.Id == layer.Id ? layer : x).ToArray() };
    private void EditLayer(Func<WidgetLayer, WidgetLayer> transform, bool closeTools = true)
    {
        var layer = draft.Layers.FirstOrDefault(x => x.Id == selected); if (layer is null) return;
        if (closeTools) CloseTools(); ReplaceLayer(transform(layer)); RefreshControls(); UpdateCanvas();
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
            0 => layer with { Frame = f with { X = Math.Min(value, 1 - f.Width) } },
            1 => layer with { Frame = f with { Y = Math.Min(value, 1 - f.Height) } },
            2 => layer with { Frame = f with { Width = value, X = Math.Min(f.X, 1 - value) } },
            3 => layer with { Frame = f with { Height = value, Y = Math.Min(f.Y, 1 - value) } },
            4 => layer with { Zoom = value }, 5 => layer with { CropX = value }, 6 => layer with { CropY = value }, _ => layer
        };
    }, closeTools: false);
    private void UpdateCanvas() => canvas.Update(draft, selected);
    private void RefreshLayers()
    {
        layersCount.Text = $"{draft.Layers.Count} {(draft.Layers.Count == 1 ? "layer" : "layers")}";
        foreach (var id in rows.Keys.Where(id => !draft.Layers.Any(x => x.Id == id)).ToArray())
        { layerList.Children.Remove(rows[id]); rows[id].ReleaseSource(); rows.Remove(id); }
        var index = 0;
        foreach (var layer in draft.Layers.Reverse())
        {
            if (!rows.TryGetValue(layer.Id, out var row))
            {
                row = new WidgetLayerRow(layer, studio.Repository.AssetPath(layer.AssetKey), dragScope, SelectLayer,
                    (id, visible) =>
                    {
                        if (!CanEdit) return;
                        var current = draft.Layers.FirstOrDefault(x => x.Id == id); if (current is null) return;
                        ReplaceLayer(current with { Visible = visible }); RefreshLayers();
                    }, id => { if (CanEdit) { selected = id; RefreshLayers(); _ = RunAsync(LayerMenuAsync); } },
                    (source, target) => { if (CanEdit) { draft = draft with { Layers = WidgetLayerEdits.Reorder(draft.Layers, source, target) }; RefreshLayers(); } });
                rows.Add(layer.Id, row); layerList.Children.Add(row);
            }
            var currentIndex = layerList.Children.IndexOf(row);
            if (currentIndex != index) { layerList.Children.RemoveAt(currentIndex); layerList.Children.Insert(index, row); }
            row.Update(layer, selected == layer.Id); index++;
        }
        RefreshControls(); RefreshActions(); UpdateCanvas();
    }
    private void RefreshControls()
    {
        updating = true;
        try
        {
            var layer = draft.Layers.FirstOrDefault(x => x.Id == selected);
            if (layer is null) { precise.IsVisible = false; opacityCaption.Text = "Opacity"; return; }
            selectedTitle.Text = layer.Name + " · Size, position & crop"; opacityCaption.Text = $"Opacity · {layer.Opacity:P0}"; opacity.Value = layer.Opacity;
            sliders[0].Slider.Maximum = Math.Max(.001, 1 - layer.Frame.Width); sliders[1].Slider.Maximum = Math.Max(.001, 1 - layer.Frame.Height);
            var values = new[] { layer.Frame.X, layer.Frame.Y, layer.Frame.Width, layer.Frame.Height, layer.Zoom, layer.CropX, layer.CropY };
            for (var i = 0; i < sliders.Count; i++)
            {
                sliders[i].Slider.Value = values[i]; sliders[i].Label.Text = i == 4 ? $"{sliders[i].Title} · {values[i]:0.00}×" : $"{sliders[i].Title} · {values[i]:P0}";
            }
            sliders[0].Slider.IsEnabled = layer.Frame.Width < 1; sliders[1].Slider.IsEnabled = layer.Frame.Height < 1;
            var bounds = WidgetDesignRules.Canvas(draft.Size); var width = layer.Frame.Width * bounds.Width; var height = layer.Frame.Height * bounds.Height;
            var placement = WidgetPhotoGeometry.Image(layer, width, height);
            sliders[5].Slider.IsEnabled = placement.Width > width + .001; sliders[6].Slider.IsEnabled = placement.Height > height + .001;
        }
        finally { updating = false; }
    }
    private async Task LayerMenuAsync()
    {
        var layer = draft.Layers.FirstOrDefault(x => x.Id == selected); if (layer is null) return;
        var index = draft.Layers.ToList().FindIndex(x => x.Id == layer.Id);
        var choices = new List<string> { "Rename", "Size, position & crop" };
        if (index < draft.Layers.Count - 1) choices.Add("Move forward"); if (index > 0) choices.Add("Move backward");
        var choice = await DisplayActionSheetAsync(layer.Name, "Cancel", "Delete layer", choices.ToArray());
        if (!Foreground || departed) return;
        switch (choice)
        {
            case "Rename":
                var title = await DisplayPromptAsync("Layer name", "Use up to 40 characters.", initialValue: layer.Name, maxLength: 40);
                if (!Foreground || departed || title is null) return;
                var changed = layer with { Name = title.Trim() }; var next = draft with { Layers = draft.Layers.Select(x => x.Id == layer.Id ? changed : x).ToArray() };
                WidgetDesignRules.Validate(next); draft = next; break;
            case "Size, position & crop":
                precise.IsVisible = true;
                await scroll.ScrollToAsync(precisionCard, ScrollToPosition.Start, !UIKit.UIAccessibility.IsReduceMotionEnabled);
                break;
            case "Move forward": MoveLayer(1); break;
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
    private async Task PickPhotoAsync()
    {
        if (draft.Layers.Count >= WidgetDesignRules.MaximumLayers) return;
        var choice = await DisplayActionSheetAsync("Add photo layer", "Cancel", null, "Photo Library", "Files");
        if (!Foreground || departed || choice is not ("Photo Library" or "Files")) return;
        var layer = await studio.PickPhotoAsync(choice == "Photo Library"); if (layer is null) return;
        ownedPhotos.Add(layer.AssetKey); if (departed) { await CleanupAsync(); return; }
        var ordinal = 1; while (draft.Layers.Any(x => x.Name == "Photo " + ordinal)) ordinal++;
        layer = WidgetLayerEdits.Reset(layer with { Name = "Photo " + ordinal }, draft.Size);
        draft = draft with { Layers = draft.Layers.Append(layer).ToArray() }; selected = layer.Id; RefreshLayers();
    }
    private async Task RemoveLayerAsync()
    {
        var layer = draft.Layers.FirstOrDefault(x => x.Id == selected); if (layer is null) return;
        if (!await DisplayAlertAsync("Delete photo layer?", "Remove this layer from your widget draft? Your original photo is kept.", "Delete", "Cancel") || !Foreground || departed) return;
        CloseTools(); draft = draft with { Layers = draft.Layers.Where(x => x.Id != layer.Id).ToArray() }; selected = draft.Layers.LastOrDefault()?.Id; RefreshLayers();
        if (ownedPhotos.Contains(layer.AssetKey) && !draft.Layers.Any(x => x.AssetKey == layer.AssetKey))
        {
            try { await studio.Repository.RemoveDraftPhotosAsync([layer.AssetKey]); ownedPhotos.Remove(layer.AssetKey); }
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
        name.Unfocus(); draft = draft with { Name = name.Text?.Trim() ?? "" };
        var result = await studio.SaveAsync(draft); draft = result.Design; saved = true; await CleanupAsync();
        if (!Foreground || departed) return;
        if (!result.Published) await DisplayAlertAsync("Saved on this device", "Your design is saved, but the Home Screen update could not be published. Check App Group signing in Settings, then tap Refresh widgets in My Widgets.", "OK");
        if (Foreground && !departed) await CloseSavedAsync();
    }
    private void RefreshActions()
    {
        // Keep the canvas enabled during a gesture; disabling its parent would cancel native recognition.
        var canNavigate = Foreground && !busy && !departed && !background.IsBusy && !canvas.IsInteracting;
        form.IsEnabled = Foreground && !busy && !saved && !departed && !background.IsBusy;
        save.IsEnabled = sizes.IsEnabled = layerList.IsEnabled = canNavigate && !saved;
        back.IsEnabled = cancel.IsEnabled = canNavigate;
        addPhoto.IsEnabled = canNavigate && !saved && draft.Layers.Count < WidgetDesignRules.MaximumLayers;
        var hasLayer = draft.Layers.Any(x => x.Id == selected);
        resetTool.IsEnabled = rotateTool.IsEnabled = flipTool.IsEnabled = opacityTool.IsEnabled = deleteTool.IsEnabled = canNavigate && !saved && hasLayer;
    }
    private async Task RunAsync(Func<Task> operation)
    {
        if (!CanEdit || !Foreground) return;
        CloseTools(); busy = true; RefreshActions();
        try { await operation(); }
        catch (Exception ex)
        { logger.LogWarning("Widget editor operation failed. ErrorType={ErrorType}", ex.GetType().Name); if (!departed) ShowStatus(ex is IOException ? ex.Message : "The widget could not be updated. Try again."); }
        finally { busy = false; RefreshActions(); if (departed) await CleanupAsync(); }
    }
    private void ShowStatus(string message)
    {
        status.Text = message; status.IsVisible = true;
        try { if (Foreground) SemanticScreenReader.Default.Announce(message); }
        catch (Exception ex) { logger.LogDebug("Widget announcement unavailable. ErrorType={ErrorType}", ex.GetType().Name); }
    }
    private async Task CleanupAsync()
    {
        try { await studio.Repository.RemoveDraftPhotosAsync(ownedPhotos); ownedPhotos.Clear(); }
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
    private async void WindowActivated(object? sender, EventArgs args) { RefreshActions(); if (saved && !busy) await TryCloseSavedAsync(); }
    private void WindowDeactivated(object? sender, EventArgs args) { canvas.EndInteraction(true); CloseTools(); RefreshActions(); }
    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true;
        if (observedWindow != Window)
        {
            StopObservingWindow(); observedWindow = Window;
            if (observedWindow is not null) { observedWindow.Activated += WindowActivated; observedWindow.Deactivated += WindowDeactivated; }
        }
        RefreshActions(); if (saved && !busy) await TryCloseSavedAsync();
    }
    private void StopObservingWindow()
    {
        if (observedWindow is not null) { observedWindow.Activated -= WindowActivated; observedWindow.Deactivated -= WindowDeactivated; }
        observedWindow = null;
    }
    protected override void OnDisappearing()
    { canvas.EndInteraction(true); CloseTools(); active = false; RefreshActions(); StopObservingWindow(); base.OnDisappearing(); }
    protected override bool OnBackButtonPressed() => busy || background.IsBusy || canvas.IsInteracting || base.OnBackButtonPressed();
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (!Navigation.NavigationStack.Contains(this))
        {
            departed = true; canvas.ReleaseSources(); foreach (var row in rows.Values) row.ReleaseSource(); if (!busy) _ = CleanupAsync();
        }
    }
}
