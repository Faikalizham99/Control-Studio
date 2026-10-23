using ControlStudio.Core;

namespace ControlStudio.App.Views;

public sealed class AboutPage : ContentPage
{
    public AboutPage()
    {
        Title = "Widget & Shortcuts setup"; SafeAreaEdges = SafeAreaEdges.All; DashboardStyle.Page(this);
        Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 18, Children = {
            SettingsControls.Text("Everything stays local", 24, bold: true),
            SettingsControls.Text("No account, backend or analytics. Your profile, appearance and dashboard are stored privately on this device. Photos are chosen from Photo Library or Files, edited locally, and never uploaded.", 16),
            SettingsControls.Text("Widget & Shortcuts", 22, bold: true),
            SettingsControls.Text($"Configured App Group: {AppGroupConfiguration.Identifier}", 14),
            SettingsControls.Text("The app and both extensions must be signed to allow this exact group. A provider group can be shared with other apps signed using the same profile.", 16),
            SettingsControls.Text("Open the app once, then add Control Studio from the Home Screen widget picker. In Apple's Shortcuts app, search for Increment Control Studio Counter. Both extensions must be preserved and signed with matching App Group permissions.", 16),
            SettingsControls.Text("If shared storage is unavailable, the app uses a separate local counter. Local values are not automatically merged into the shared counter when signing changes. iOS decides when widgets refresh.", 16),
            SettingsControls.Text("Build your own widget", 22, bold: true),
            SettingsControls.Text("In Studio → My Widgets, tap +, name your design and choose Small, Medium, Large or Extra Large. Extra Large offers Portrait for iPhone/iPadOS 27+ and Landscape for iPad. Add optional photo layers, arrange and crop them, then Save widget. Open Widget slots, choose the size, and assign your design to slot 1–10. On your Home Screen, add Control Studio → Studio 1–10 in that same size and layout. Each size has its own assignments. Remove old custom widgets once and replace them with the new slots. Your saved designs and the Small/Medium counter widgets are kept.", 16),
            SettingsControls.Text("Widget designs and source photos stay on this device. Only the saved preview and its design details are shared with the widget extension through the configured App Group. If publication fails, the design remains saved locally; check signing and tap Refresh widgets in My Widgets. Clipboard items stay in private storage and write to the system clipboard only when you choose Copy. Avoid storing passwords here.", 16),
            SettingsControls.Text($"Control Studio · {AppInfo.Current.VersionString} ({AppInfo.Current.BuildString})", 13, true)
        } } };
    }
}
