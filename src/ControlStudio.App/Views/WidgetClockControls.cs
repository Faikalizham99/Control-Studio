using ControlStudio.Core.Widgets;

namespace ControlStudio.App.Views;

internal enum WidgetClockTool { Time, Font, Colour, Size, Position, Opacity }

internal sealed class WidgetClockControls : ContentView
{
    private readonly WidgetSegments format, alignment;
    private readonly WidgetClockFontPicker fonts;
    private readonly WidgetDateFormatPicker dates;
    private readonly Switch showWeekday = SettingsControls.RowSwitch("DateShowWeekday");
    private readonly WidgetSegments weekdayStyle;
    private readonly View dateFormat;
    private readonly View clockTime;
    private readonly View textContent;
    private readonly Editor content = new()
    {
        Placeholder = "Type your text here…", FontSize = 16, HeightRequest = 144, AutoSize = EditorAutoSizeOption.Disabled,
        BackgroundColor = Colors.Transparent, IsTextPredictionEnabled = true, IsSpellCheckEnabled = true, AutomationId = "WidgetTextContent"
    };
    private readonly Label contentCount = SettingsControls.Text("", 12, secondary: true);
    private readonly Label formatHeading = SettingsControls.Text("Time", 19, bold: true);
    private readonly Label sizeHelp = SettingsControls.Text("", 12, secondary: true);
    private readonly Label positionHelp = SettingsControls.Text("", 12, secondary: true);
    private readonly Switch seconds = SettingsControls.RowSwitch("ClockShowSeconds");
    private readonly Switch amPm = SettingsControls.RowSwitch("ClockShowAmPm");
    private readonly Grid amPmRow;
    private readonly Slider size = new() { Minimum = .2, Maximum = 1, AutomationId = "ClockTextSize" };
    private readonly Slider opacity = new() { Minimum = 0, Maximum = 1, AutomationId = "ClockOpacity" };
    private readonly Label sizeLabel = SettingsControls.Text("Text size", 14), opacityLabel = SettingsControls.Text("Opacity", 14);
    private readonly Label notice = SettingsControls.Text("", 12, secondary: true);
    private readonly WidgetTextColourControls colour;
    private readonly ClipboardEditorActionButton centre = new("Centre horizontally", ClipboardEditorControls.Icon(StudioSymbol.CenterHorizontal, size: 18), "CentreClockHorizontally", accentCaption: true);
    private readonly WidgetLayerNudgeControls nudge = new("ClockPositionNudge");
    private readonly WidgetRotationControls rotation = new("ClockRotation");
    private readonly Dictionary<WidgetClockTool, (WidgetToolButton Button, View Panel)> tools = [];
    private readonly WidgetToolButton delete;
    private WidgetLayer? layer;
    private bool updating, available = true;
    public WidgetHorizontalStrip Toolbar { get; }
    public WidgetClockTool? ActiveTool { get; private set; }
    public Func<bool>? CanInteract { get; set; }
    public bool IsBusy => colour.IsBusy;
    public event EventHandler? BusyChanged;
    public event Action<WidgetLayer>? Changed;
    public event EventHandler? CentreHorizontallyRequested;
    public event Action<WidgetMoveDirection>? NudgeRequested;
    public event Action<double>? RotationRequested;
    public event EventHandler? DeleteRequested;
    public event EventHandler? PanelChanged;
    public event EventHandler? LayoutChanging;
    public event Action<View>? PositionChanging;
    public event EventHandler? InvalidColour;
    private bool CanUse => available && IsEnabled && !IsBusy && layer is not null && (CanInteract?.Invoke() ?? true);
    private bool IsDate => layer?.Kind == WidgetLayerKind.Date;
    private bool IsCustomText => layer?.Kind == WidgetLayerKind.Text;
    private WidgetClockOptions? TextStyle => layer is null ? null : WidgetTextLayers.Style(layer);
    public WidgetClockControls(IColourPicker picker)
    {
        AutomationId = "WidgetClockControls"; IsVisible = false;
        format = new(["12-hour", "24-hour"], "ClockFormat", index => Edit(WidgetClockTool.Time, x => x with { TwentyFourHour = index == 1 }));
        fonts = new(font => Edit(WidgetClockTool.Font, x => WidgetClockWeights.ChangeFont(x, font)),
            weight => { if (TextStyle is { } clock && WidgetClockWeights.Available(clock.Font).Contains(weight)) Edit(WidgetClockTool.Font, x => x with { Weight = weight }); });
        fonts.PositionChanging += view => PositionChanging?.Invoke(view);
        dates = new(value => EditDate(date => WidgetDates.ChangeFormat(date, value)));
        dates.PositionChanging += view => PositionChanging?.Invoke(view);
        weekdayStyle = new(["Short", "Full"], "DateWeekdayStyle", SelectWeekdayStyle) { AutomationId = "DateWeekdayStyle" };
        showWeekday.Toggled += (_, args) => EditDate(date => date with { ShowWeekday = args.Value }, showWeekday);
        SemanticProperties.SetDescription(showWeekday, "Show weekday in the date");
        SemanticProperties.SetHint(weekdayStyle, "Short uses Sun; Full uses Sunday. Enable Show weekday to change this style.");
        var weekdayRow = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var weekdayLabel = SettingsControls.Text("Show weekday", 14); weekdayLabel.VerticalOptions = LayoutOptions.Center;
        weekdayRow.Add(weekdayLabel); weekdayRow.Add(showWeekday, 1);
        dateFormat = new VerticalStackLayout { Spacing = 12, Children = { weekdayRow, SettingsControls.Text("Weekday style", 14, bold: true), weekdayStyle, dates } };
        content.SetAppThemeColor(Editor.TextColorProperty, Color.FromArgb("#16171C"), Color.FromArgb("#F5F5F7"));
        content.SetAppThemeColor(Editor.PlaceholderColorProperty, Color.FromArgb("#737B8B"), Color.FromArgb("#9198A7"));
        SemanticProperties.SetDescription(content, "Text layer content");
        SemanticProperties.SetHint(content, $"Up to {WidgetTexts.MaximumLength} characters. New lines are supported.");
        content.TextChanged += (_, args) =>
        {
            if (updating || !CanUse || ActiveTool != WidgetClockTool.Time || layer?.Text is not { } text) return;
            var normalized = WidgetTexts.NormalizeContent(args.NewTextValue);
            if (content.Text != normalized)
            {
                updating = true;
                try { content.Text = normalized; } finally { updating = false; }
            }
            PositionChanging?.Invoke(content);
            Publish(layer with { Text = text with { Content = normalized } });
        };
        textContent = new VerticalStackLayout { Spacing = 8, Children = { ClipboardEditorControls.Field(content, 10), contentCount,
            SettingsControls.Text("Use new lines for separate lines. Text wraps inside its frame and scales down when needed. A blank layer displays nothing.", 12, secondary: true) } };
        alignment = new(["Left", "Centre", "Right"], "ClockAlignment", index => Edit(WidgetClockTool.Position, x => x with { Alignment = (WidgetClockAlignment)index }));
        seconds.Toggled += (_, args) => Edit(WidgetClockTool.Time, x => x with { ShowSeconds = args.Value });
        amPm.Toggled += (_, args) => { if (layer?.Clock?.TwentyFourHour == false) Edit(WidgetClockTool.Time, x => x with { ShowAmPm = args.Value }); };
        size.ValueChanged += (_, args) => Edit(WidgetClockTool.Size, x => x with { FontScale = args.NewValue });
        opacity.ValueChanged += (_, args) => { if (!updating && CanUse && ActiveTool == WidgetClockTool.Opacity) Publish(layer! with { Opacity = args.NewValue }); };
        foreach (var slider in new[] { size, opacity }) { slider.MinimumHeightRequest = 44; slider.SetDynamicResource(Slider.MinimumTrackColorProperty, "AccentText"); }
        SemanticProperties.SetDescription(size, "Clock text size within its frame"); SemanticProperties.SetDescription(opacity, "Clock opacity");
        SemanticProperties.SetDescription(seconds, "Show clock seconds");
        SemanticProperties.SetDescription(amPm, "Show AM/PM");
        centre.Describe("Centre clock horizontally in the widget, keeping its vertical position and size");
        centre.Clicked += (_, _) => { if (!updating && CanUse && ActiveTool == WidgetClockTool.Position && layer is { Locked: false }) CentreHorizontallyRequested?.Invoke(this, EventArgs.Empty); };
        nudge.PositionChanging += view => PositionChanging?.Invoke(view);
        nudge.MoveRequested += direction =>
        {
            if (!updating && CanUse && ActiveTool == WidgetClockTool.Position && layer is { Locked: false }) NudgeRequested?.Invoke(direction);
        };
        rotation.PositionChanging += view => PositionChanging?.Invoke(view);
        rotation.Changed += degrees =>
        {
            if (!updating && CanUse && ActiveTool == WidgetClockTool.Position && layer is { Locked: false }) RotationRequested?.Invoke(degrees);
        };
        colour = new(picker) { CanInteract = () => CanUse && ActiveTool == WidgetClockTool.Colour };
        colour.StyleChanged += style => { if (!updating && layer is not null) Publish(WidgetTextLayers.WithStyle(layer, style)); };
        colour.BusyChanged += (_, _) => BusyChanged?.Invoke(this, EventArgs.Empty);
        colour.PositionChanging += view => PositionChanging?.Invoke(view);
        colour.LayoutChanging += (_, _) => LayoutChanging?.Invoke(this, EventArgs.Empty);
        var row = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var label = SettingsControls.Text("Show seconds", 14); label.VerticalOptions = LayoutOptions.Center; row.Add(label); row.Add(seconds, 1);
        amPmRow = new Grid { ColumnSpacing = 8, IsVisible = false, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        var amPmLabel = SettingsControls.Text("Show AM/PM", 14); amPmLabel.VerticalOptions = LayoutOptions.Center; amPmRow.Add(amPmLabel); amPmRow.Add(amPm, 1);
        var panels = new VerticalStackLayout { Spacing = 10, Children = { notice } };
        void Add(WidgetClockTool tool, StudioSymbol symbol, string title, params View[] controls)
        {
            var body = new VerticalStackLayout { Spacing = 12 };
            var heading = tool == WidgetClockTool.Time ? formatHeading : SettingsControls.Text(title, 19, bold: true); SemanticProperties.SetHeadingLevel(heading, SemanticHeadingLevel.Level2);
            body.Children.Add(heading);
            foreach (var control in controls) body.Children.Add(control);
            var panel = ClipboardEditorControls.Surface(body, 22, 14);
            panel.AutomationId = "ClockPanel" + tool; panel.IsVisible = false; panels.Children.Add(panel);
            var button = new WidgetToolButton(symbol, "Clock " + title.ToLowerInvariant(), automationId: "ClockTool" + tool);
            button.Clicked += (_, _) => Toggle(tool); tools.Add(tool, (button, panel));
        }
        clockTime = new VerticalStackLayout { Spacing = 12, Children = { format, row, amPmRow,
            SettingsControls.Text("Time follows this iPhone. iOS may simplify it while the display is dimmed.", 12, secondary: true) } };
        Add(WidgetClockTool.Time, StudioSymbol.Clock, "Time", clockTime, dateFormat, textContent);
        Add(WidgetClockTool.Font, StudioSymbol.Font, "Font", fonts);
        Add(WidgetClockTool.Colour, StudioSymbol.Palette, "Colour", colour);
        Add(WidgetClockTool.Size, StudioSymbol.Expand, "Size", sizeLabel, size,
            sizeHelp);
        Add(WidgetClockTool.Position, StudioSymbol.CenterHorizontal, "Position", centre, nudge, rotation,
            positionHelp,
            SettingsControls.Text("Text alignment", 14, bold: true), alignment);
        Add(WidgetClockTool.Opacity, StudioSymbol.Opacity, "Opacity", opacityLabel, opacity);
        delete = new WidgetToolButton(StudioSymbol.Delete, "Delete clock layer", destructive: true, automationId: "ClockToolDelete");
        delete.Clicked += (_, _) => { if (CanUse && ValidateColour()) DeleteRequested?.Invoke(this, EventArgs.Empty); };
        Toolbar = new WidgetHorizontalStrip(tools.Values.Select(x => (View)x.Button).Append(delete), "ClockEditorToolbar") { IsVisible = false };
        Content = panels;
    }
    private bool ValidateColour()
    {
        if (CommitPendingColour()) return true;
        InvalidColour?.Invoke(this, EventArgs.Empty); return false;
    }
    private void Toggle(WidgetClockTool tool)
    {
        if (!CanUse || !ValidateColour()) return;
        PositionChanging?.Invoke(tools[tool].Button);
        SetPanel(ActiveTool == tool ? null : tool);
    }
    private void SetPanel(WidgetClockTool? tool)
    {
        if (tool != WidgetClockTool.Time || !IsCustomText) content.Unfocus();
        var changed = ActiveTool != tool; ActiveTool = tool;
        if (changed) LayoutChanging?.Invoke(this, EventArgs.Empty);
        if (tool is { } next) tools[next].Panel.IsVisible = true;
        foreach (var (key, item) in tools) { if (key != tool) item.Panel.IsVisible = false; item.Button.IsSelected = key == tool; }
        IsVisible = layer is not null && tool is not null;
        if (changed)
        {
            PanelChanged?.Invoke(this, EventArgs.Empty);
        }
    }
    public void ClosePanel() { if (!IsBusy) SetPanel(null); }
    public void ResetPressedState() { foreach (var item in tools.Values) item.Button.Scale = 1; delete.Scale = 1; }
    public void SetAvailable(bool value)
    {
        available = value; IsEnabled = value; Toolbar.IsEnabled = value;
        centre.IsEnabled = nudge.IsEnabled = value && layer is { Locked: false };
    }
    private void Edit(WidgetClockTool tool, Func<WidgetClockOptions, WidgetClockOptions> edit)
    {
        if (!updating && CanUse && ActiveTool == tool && TextStyle is { } options) Publish(WidgetTextLayers.WithStyle(layer!, edit(options)));
    }
    private void EditDate(Func<WidgetDateOptions, WidgetDateOptions> edit, View? anchor = null)
    {
        if (updating || !CanUse || ActiveTool != WidgetClockTool.Time || layer?.Date is not { } date) return;
        if (anchor is not null) PositionChanging?.Invoke(anchor);
        Publish(layer with { Date = edit(date) });
    }
    private void SelectWeekdayStyle(int index)
    {
        if (layer?.Date is { } date && WidgetDates.IncludesWeekday(date))
            EditDate(value => value with { WeekdayStyle = (WidgetWeekdayStyle)index }, weekdayStyle);
    }
    private void Publish(WidgetLayer value) { layer = value; Changed?.Invoke(value); }
    public bool CommitPendingColour()
    {
        if (layer is null || colour.CommitPendingHex()) return true;
        SetPanel(WidgetClockTool.Colour); return false;
    }
    public void Update(WidgetLayer? value)
    {
        LayoutChanging?.Invoke(this, EventArgs.Empty);
        updating = true;
        try
        {
            var next = value is not null && WidgetTextLayers.IsText(value) ? value : null;
            var changedLayer = next?.Id != layer?.Id; layer = next;
            Toolbar.IsVisible = layer is not null;
            if (changedLayer) SetPanel(null);
            IsVisible = layer is not null && ActiveTool is not null;
            centre.IsEnabled = nudge.IsEnabled = available && layer is { Locked: false };
            rotation.Update(layer);
            if (TextStyle is not { } clock) return;
            var kind = WidgetTextLayers.Name(layer!);
            formatHeading.Text = IsCustomText ? "Content" : IsDate ? "Date format" : "Time";
            clockTime.IsVisible = !IsDate && !IsCustomText; dateFormat.IsVisible = IsDate; textContent.IsVisible = IsCustomText;
            if (layer!.Text is { } text)
            {
                if (content.Text != text.Content) content.Text = text.Content;
                contentCount.Text = $"{text.Content.Length} / {WidgetTexts.MaximumLength}";
            }
            if (layer!.Date is { } date)
            {
                showWeekday.IsToggled = WidgetDates.IncludesWeekday(date);
                weekdayStyle.Update((int)date.WeekdayStyle); weekdayStyle.IsEnabled = showWeekday.IsToggled;
                dates.Update(date);
            }
            fonts.SetSample(IsCustomText ? "Hello" : IsDate ? "25 Oct" : "12:34:56");
            tools[WidgetClockTool.Time].Button.SetSymbol(IsCustomText ? StudioSymbol.Text : IsDate ? StudioSymbol.Calendar : StudioSymbol.Clock);
            SemanticProperties.SetDescription(size, IsCustomText ? "Text size within its frame" : layer.Kind + " text size within its frame");
            SemanticProperties.SetDescription(opacity, layer.Kind + " opacity");
            centre.Describe("Centre " + kind + " horizontally in the widget, keeping its vertical position and size");
            sizeHelp.Text = "Text size within the layer. Pinch the preview to resize the whole " + kind + " around its centre.";
            positionHelp.Text = "Drag the " + kind + " to move it. The layer menu also offers precise size and position controls.";
            notice.Text = !layer.Visible ? "This " + kind + " is hidden. Show it in Layers to see it in the preview." : layer.Opacity == 0 ? "This " + kind + " has 0% opacity. Increase opacity to see it." : "";
            if (IsCustomText && string.IsNullOrWhiteSpace(layer.Text!.Content)) notice.Text = "This text layer is blank. Enter text in Content to see it in the preview.";
            notice.IsVisible = notice.Text.Length > 0;
            format.Update(clock.TwentyFourHour ? 1 : 0); fonts.Update(clock.Font, clock.Weight); alignment.Update((int)clock.Alignment);
            seconds.IsToggled = clock.ShowSeconds; size.Value = clock.FontScale; opacity.Value = layer.Opacity;
            amPm.IsToggled = clock.ShowAmPm; amPmRow.IsVisible = !IsDate && !IsCustomText && !clock.TwentyFourHour;
            sizeLabel.Text = $"Text size · {clock.FontScale:P0}"; opacityLabel.Text = $"Opacity · {layer.Opacity:P0}";
            colour.Update(clock, layer.Id);
            foreach (var (key, item) in tools) item.Button.Describe(layer.Kind + " " + (key == WidgetClockTool.Time ? IsCustomText ? "content" : IsDate ? "format" : "time" : key.ToString().ToLowerInvariant()), layer.Name);
            delete.Describe("Delete " + kind + " layer", layer.Name);
        }
        finally { updating = false; }
    }
}
