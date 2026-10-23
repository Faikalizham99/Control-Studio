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
    private readonly WidgetCanvasView canvas;
    private readonly ProfileNameEntry name = new() { MaxLength = 60, Placeholder = "Widget name", BackgroundColor = Colors.Transparent, FontSize = 15, HeightRequest = 44 };
    private readonly Picker size = new() { Title = "Widget size", ItemsSource = new[] { "Small", "Medium", "Large", "Extra Large" } };
    private readonly Picker extraLargeLayout = new() { Title = "Extra Large layout", ItemsSource = new[] { "Portrait · iPhone/iPadOS 27+", "Landscape · iPad" } };
    private readonly VerticalStackLayout form = new() { Padding = new Thickness(16, 12, 16, 28), Spacing = 16 };
    private readonly VerticalStackLayout layerList = new() { Spacing = 8 };
    private readonly VerticalStackLayout controls = new() { Spacing = 6 };
    private readonly Label status = SettingsControls.Text("", 14, secondary: true);
    private readonly Label layersTitle = SettingsControls.Text("", 19, bold: true);
    private readonly Label selectedTitle = SettingsControls.Text("", 19, bold: true);
    private readonly ClipboardEditorActionButton save = new("Save widget", new ProfileGlyph(ProfileSymbol.Check), "SaveWidgetDesign", primary: true);
    private readonly ClipboardEditorActionButton cancel = new("Cancel", null, "CancelWidgetDesign", accentCaption: true);
    private readonly ClipboardEditorActionButton back = new("", new ProfileGlyph(ProfileSymbol.Back), "BackFromWidgetEditor");
    private readonly ClipboardEditorActionButton addPhoto;
    private readonly ClipboardEditorActionButton raise, lower, remove;
    private readonly Switch shown = new();
    private readonly ColourPickerView background;
    private readonly List<(string Title, Slider Slider, Label Label)> sliders = [];
    private bool updating, busy, saved, departed, active, closing;
    private Window? observedWindow;
    private bool Foreground => active && UIKit.UIApplication.SharedApplication.ApplicationState == UIKit.UIApplicationState.Active;

    public WidgetEditorPage(WidgetStudioService studio, AppleColorPicker colourPicker, ILogger logger, WidgetDesign? original = null, WidgetSize initialSize = WidgetSize.Small)
    {
        this.studio = studio; this.logger = logger; draft = original ?? (WidgetDesignRules.New() with { Size = initialSize });
        Title = original is null ? "New widget" : "Edit widget"; AutomationId = "WidgetEditorPage";
        SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this); NavigationPage.SetHasNavigationBar(this, false);
        name.Text = draft.Name; size.SelectedIndex = Math.Min(3, (int)draft.Size);
        extraLargeLayout.SelectedIndex = draft.Size == WidgetSize.ExtraLarge ? 1 : 0;
        name.SetAppThemeColor(Entry.TextColorProperty, Color.FromArgb("#16171C"), Colors.White);
        size.SetAppThemeColor(Picker.TextColorProperty, Color.FromArgb("#16171C"), Colors.White);
        extraLargeLayout.SetAppThemeColor(Picker.TextColorProperty, Color.FromArgb("#16171C"), Colors.White);
        SemanticProperties.SetDescription(name, "Widget name"); SemanticProperties.SetDescription(size, "Widget size");
        SemanticProperties.SetDescription(extraLargeLayout, "Extra Large layout and device compatibility");
        canvas = new WidgetCanvasView(studio.Repository.AssetPath, id => { selected = id; RefreshLayers(); });
        var layoutField = ClipboardEditorControls.Field(extraLargeLayout, new Thickness(12, 0));
        layoutField.IsVisible = size.SelectedIndex == 3;
        void ChangeSize()
        {
            if (size.SelectedIndex < 0 || busy) return;
            layoutField.IsVisible = size.SelectedIndex == 3;
            draft = draft with { Size = size.SelectedIndex == 3
                ? (extraLargeLayout.SelectedIndex == 1 ? WidgetSize.ExtraLarge : WidgetSize.ExtraLargePortrait)
                : (WidgetSize)size.SelectedIndex };
            RefreshControls(); UpdateCanvas();
        }
        size.SelectedIndexChanged += (_, _) => ChangeSize();
        extraLargeLayout.SelectedIndexChanged += (_, _) => ChangeSize();
        form.Children.Add(ClipboardEditorControls.Card(new VerticalStackLayout { Spacing = 10, Children =
        {
            SettingsControls.Text("Make it your widget.", 25, bold: true),
            ClipboardEditorControls.Field(name, new Thickness(12, 0)), ClipboardEditorControls.Field(size, new Thickness(12, 0)),
            layoutField,
            SettingsControls.Text("Extra Large Portrait works on iPhone and iPad with iOS/iPadOS 27 or later. Landscape is for iPad. You can design either here; changing size keeps layer positions proportionally, but photo crops may change.", 12, secondary: true)
        } }));
        form.Children.Add(SettingsControls.Text("Live preview", 19, bold: true)); form.Children.Add(canvas);
        form.Children.Add(SettingsControls.Text("Preview proportions vary by device. Tap a photo to select it, then use the controls to position, resize and crop it.", 12, secondary: true));
        addPhoto = Action("Add photo layer", StudioSymbol.Photo, "AddWidgetPhoto", PickPhotoAsync);
        raise = Action("Raise layer", StudioSymbol.LayerUp, "RaiseWidgetLayer", () => MoveAsync(1));
        lower = Action("Lower layer", StudioSymbol.LayerDown, "LowerWidgetLayer", () => MoveAsync(-1));
        remove = Action("Remove layer", StudioSymbol.Delete, "RemoveWidgetLayer", RemoveLayerAsync);
        form.Children.Add(ClipboardEditorControls.Card(new VerticalStackLayout { Spacing = 12, Children =
        {
            layersTitle, SettingsControls.Text("Photos are optional layers. New photos go on top; reorder layers to change what overlaps.", 13, secondary: true),
            addPhoto, layerList, Row(raise, lower), remove
        } }));
        var visibleRow = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        visibleRow.Add(SettingsControls.Text("Show this layer", 14)); visibleRow.Add(shown, 1);
        shown.SetDynamicResource(Switch.OnColorProperty, "Accent"); SemanticProperties.SetDescription(shown, "Show selected photo layer");
        shown.Toggled += (_, args) => { if (!updating) { ChangeLayer(layer => layer with { Visible = args.Value }); RefreshLayers(); } };
        controls.Children.Add(selectedTitle); controls.Children.Add(visibleRow);
        controls.Children.Add(SettingsControls.Text("Reduce width or height to leave room for movement. Zoom and crop adjust the photo inside its frame; zoom in for more crop movement.", 12, secondary: true));
        AddSlider("Horizontal position", 0, 1); AddSlider("Vertical position", 0, 1);
        AddSlider("Width", .05, 1); AddSlider("Height", .05, 1);
        AddSlider("Photo zoom", 1, 4); AddSlider("Crop horizontally", 0, 1); AddSlider("Crop vertically", 0, 1);
        form.Children.Add(ClipboardEditorControls.Card(controls));
        background = new ColourPickerView(colourPicker, "WidgetBackground", "Background"); background.SetSelection(draft.BackgroundHex);
        background.ColourChanged += (_, args) => { draft = draft with { BackgroundHex = args.Hex }; UpdateCanvas(); };
        background.BusyChanged += (_, _) => RefreshActions();
        form.Children.Add(ClipboardEditorControls.Card(new VerticalStackLayout { Spacing = 10, Children = { SettingsControls.Text("Background colour", 19, bold: true), background } }));
        status.IsVisible = false; form.Children.Add(status); form.Children.Add(save);
        form.Children.Add(SettingsControls.Text("Save, then open Widget slots in Widgets and assign this design to a matching slot. Add Studio 1–10 in that size on your Home Screen. Editing a design updates every slot using it; changing its size empties its old slots. iOS schedules updates.", 13, secondary: true));
        save.Clicked += async (_, _) => await RunAsync(SaveAsync);
        cancel.Clicked += async (_, _) => await ExitAsync();
        back.Clicked += async (_, _) => await ExitAsync();
        var layout = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        layout.Add(ClipboardEditorControls.Header(Title, back, cancel)); layout.Add(new ScrollView { Content = form }, 0, 1); Content = layout;
        selected = draft.Layers.LastOrDefault()?.Id; RefreshLayers();
    }
    private ClipboardEditorActionButton Action(string text, StudioSymbol symbol, string id, Func<Task> action)
    {
        var button = new ClipboardEditorActionButton(text, ClipboardEditorControls.Icon(symbol, size: 18), id);
        button.Clicked += async (_, _) => await RunAsync(action); return button;
    }
    private static Grid Row(View first, View second)
    {
        var row = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) } };
        row.Add(first); row.Add(second, 1); return row;
    }
    private void AddSlider(string title, double min, double max)
    {
        var label = SettingsControls.Text(title, 13);
        var slider = new Slider { Minimum = min, Maximum = max, MinimumHeightRequest = 44 };
        slider.SetDynamicResource(Slider.MinimumTrackColorProperty, "AccentText");
        SemanticProperties.SetDescription(slider, title + " for selected photo");
        var index = sliders.Count; sliders.Add((title, slider, label)); controls.Children.Add(label); controls.Children.Add(slider);
        slider.ValueChanged += (_, args) => { if (!updating) ApplySlider(index, args.NewValue); };
    }
    private void ApplySlider(int index, double value) => ChangeLayer(layer =>
    {
        var frame = layer.Frame;
        return index switch
        {
            0 => layer with { Frame = frame with { X = Math.Min(value, 1 - frame.Width) } },
            1 => layer with { Frame = frame with { Y = Math.Min(value, 1 - frame.Height) } },
            2 => layer with { Frame = frame with { Width = value, X = Math.Min(frame.X, 1 - value) } },
            3 => layer with { Frame = frame with { Height = value, Y = Math.Min(frame.Y, 1 - value) } },
            4 => layer with { Zoom = value }, 5 => layer with { CropX = value }, 6 => layer with { CropY = value }, _ => layer
        };
    });
    private void ChangeLayer(Func<WidgetLayer, WidgetLayer> transform)
    {
        if (selected is null || busy) return;
        draft = draft with { Layers = draft.Layers.Select(x => x.Id == selected ? transform(x) : x).ToArray() };
        RefreshControls(); UpdateCanvas();
    }
    private void UpdateCanvas() => canvas.Update(draft, selected);
    private void RefreshLayers()
    {
        layerList.Children.Clear(); layersTitle.Text = $"Layers · {draft.Layers.Count}/{WidgetDesignRules.MaximumLayers}";
        foreach (var layer in draft.Layers.Reverse())
        {
            var label = $"{(selected == layer.Id ? "✓ " : "")}{layer.Name}{(!layer.Visible ? " · Hidden" : "")}";
            var button = new ClipboardEditorActionButton(label, null, "SelectWidgetLayer" + layer.Id.ToString("N"), accentCaption: selected == layer.Id);
            button.Describe($"{layer.Name}{(!layer.Visible ? ", hidden" : "")}{(selected == layer.Id ? ", selected" : "")}");
            button.Clicked += (_, _) => { if (!busy) { selected = layer.Id; RefreshLayers(); } }; layerList.Children.Add(button);
        }
        RefreshControls(); RefreshActions(); UpdateCanvas();
    }
    private void RefreshControls()
    {
        updating = true;
        try
        {
            var layer = draft.Layers.FirstOrDefault(x => x.Id == selected); controls.IsVisible = layer is not null;
            if (layer is null) return;
            selectedTitle.Text = layer.Name;
            shown.IsToggled = layer.Visible;
            sliders[0].Slider.Maximum = Math.Max(.001, 1 - layer.Frame.Width);
            sliders[1].Slider.Maximum = Math.Max(.001, 1 - layer.Frame.Height);
            var values = new[] { layer.Frame.X, layer.Frame.Y, layer.Frame.Width, layer.Frame.Height, layer.Zoom, layer.CropX, layer.CropY };
            for (var i = 0; i < sliders.Count; i++)
            {
                var control = sliders[i]; control.Slider.Value = values[i];
                control.Label.Text = i == 4 ? $"{control.Title} · {values[i]:0.00}×" : $"{control.Title} · {values[i] * 100:0}%";
            }
            // Full-width/height layers cannot move along that axis; keep disabled labels readable.
            sliders[0].Slider.IsEnabled = layer.Frame.Width < 1; sliders[1].Slider.IsEnabled = layer.Frame.Height < 1;
            var canvasSize = WidgetDesignRules.Canvas(draft.Size);
            var width = layer.Frame.Width * canvasSize.Width; var height = layer.Frame.Height * canvasSize.Height;
            var placement = WidgetPhotoGeometry.Image(layer, width, height);
            sliders[5].Slider.IsEnabled = placement.Width > width + .001; sliders[6].Slider.IsEnabled = placement.Height > height + .001;
        }
        finally { updating = false; }
    }
    private async Task PickPhotoAsync()
    {
        if (draft.Layers.Count >= WidgetDesignRules.MaximumLayers) return;
        var choice = await DisplayActionSheetAsync("Add photo layer", "Cancel", null, "Photo Library", "Files");
        if (departed || choice is not ("Photo Library" or "Files")) return;
        var layer = await studio.PickPhotoAsync(choice == "Photo Library"); if (layer is null) return;
        ownedPhotos.Add(layer.AssetKey);
        if (departed) { await CleanupAsync(); return; }
        var ordinal = 1; while (draft.Layers.Any(x => x.Name == "Photo " + ordinal)) ordinal++;
        layer = layer with { Name = "Photo " + ordinal };
        draft = draft with { Layers = draft.Layers.Append(layer).ToArray() }; selected = layer.Id; RefreshLayers();
    }
    private Task MoveAsync(int delta)
    {
        var layers = draft.Layers.ToList(); var index = layers.FindIndex(x => x.Id == selected); var target = index + delta;
        if (index >= 0 && target >= 0 && target < layers.Count) { (layers[index], layers[target]) = (layers[target], layers[index]); draft = draft with { Layers = layers }; RefreshLayers(); }
        return Task.CompletedTask;
    }
    private async Task RemoveLayerAsync()
    {
        var layer = draft.Layers.FirstOrDefault(x => x.Id == selected); if (layer is null) return;
        if (!await DisplayAlertAsync("Remove photo layer?", "Remove this layer from your widget draft? Your original photo is kept.", "Remove", "Cancel") || departed) return;
        draft = draft with { Layers = draft.Layers.Where(x => x.Id != selected).ToArray() }; selected = draft.Layers.LastOrDefault()?.Id; RefreshLayers();
        if (ownedPhotos.Contains(layer.AssetKey))
        {
            try { await studio.Repository.RemoveDraftPhotosAsync([layer.AssetKey]); ownedPhotos.Remove(layer.AssetKey); }
            catch (Exception ex) { logger.LogWarning("Removed widget draft photo cleanup failed. ErrorType={ErrorType}", ex.GetType().Name); }
        }
    }
    private async Task SaveAsync()
    {
        if (!background.CommitPendingHex()) { ShowStatus("Enter a valid background colour before saving."); return; }
        name.Unfocus(); draft = draft with { Name = name.Text?.Trim() ?? "" };
        var result = await studio.SaveAsync(draft); draft = result.Design; saved = true;
        await CleanupAsync();
        if (!Foreground || departed) return;
        if (!result.Published) await DisplayAlertAsync("Saved on this device", "Your design is saved, but the Home Screen update could not be published. Check App Group signing in Settings, then tap Refresh widgets in My Widgets.", "OK");
        if (Foreground && !departed) await CloseSavedAsync();
    }
    private void RefreshActions()
    {
        var canNavigate = !busy && !departed && !background.IsBusy;
        var enabled = canNavigate && !saved;
        form.IsEnabled = save.IsEnabled = enabled; back.IsEnabled = cancel.IsEnabled = canNavigate;
        addPhoto.IsEnabled = enabled && draft.Layers.Count < WidgetDesignRules.MaximumLayers;
        var index = draft.Layers.ToList().FindIndex(x => x.Id == selected);
        remove.IsEnabled = enabled && index >= 0; raise.IsEnabled = enabled && index >= 0 && index < draft.Layers.Count - 1;
        lower.IsEnabled = enabled && index > 0;
    }
    private async Task RunAsync(Func<Task> operation)
    {
        if (busy || saved || departed || background.IsBusy) return;
        busy = true; RefreshActions();
        try { await operation(); }
        catch (Exception ex) { logger.LogWarning("Widget editor operation failed. ErrorType={ErrorType}", ex.GetType().Name); if (!departed) ShowStatus(ex is IOException ? ex.Message : "The widget could not be updated. Try again."); }
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
        if (busy) return;
        await TryCloseSavedAsync();
    }
    private async Task TryCloseSavedAsync()
    {
        try { await CloseSavedAsync(); }
        catch (Exception ex) { logger.LogWarning("Widget editor dismissal failed. ErrorType={ErrorType}", ex.GetType().Name); ShowStatus("Your widget is saved. Tap Back to return."); }
    }
    private async void WindowActivated(object? sender, EventArgs args) { if (saved && !busy) await TryCloseSavedAsync(); }
    protected override async void OnAppearing()
    {
        base.OnAppearing(); active = true;
        if (observedWindow != Window) { StopObservingWindow(); observedWindow = Window; if (observedWindow is not null) observedWindow.Activated += WindowActivated; }
        if (saved && !busy) await TryCloseSavedAsync();
    }
    private void StopObservingWindow()
    {
        if (observedWindow is not null) observedWindow.Activated -= WindowActivated;
        observedWindow = null;
    }
    protected override void OnDisappearing() { active = false; StopObservingWindow(); base.OnDisappearing(); }
    protected override bool OnBackButtonPressed() => busy || background.IsBusy || base.OnBackButtonPressed();
    protected override void OnNavigatedFrom(NavigatedFromEventArgs args)
    {
        base.OnNavigatedFrom(args);
        if (!Navigation.NavigationStack.Contains(this)) { departed = true; if (!busy) _ = CleanupAsync(); }
    }
}
