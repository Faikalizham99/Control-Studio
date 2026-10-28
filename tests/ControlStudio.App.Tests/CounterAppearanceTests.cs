using ControlStudio.App.Views;
using ControlStudio.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui.ApplicationModel;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class CounterAppearanceTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private static AppearancePreferences Preferences(Publisher publisher, TestPreferences storage) => new(publisher, NullLogger<AppearancePreferences>.Instance, storage);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void StartupPublishesSavedThemeAndAccentAndReopenedPreferencesRetainThem(int theme)
    {
        var storage = new TestPreferences(); storage.Set("appearance", theme); storage.Set("accent", "00ffd2");
        var publisher = new Publisher(); var preferences = Preferences(publisher, storage);
        preferences.Attach(host.App);
        Assert.Equal((theme, "#00FFD2"), Assert.Single(publisher.Values));
        Assert.Equal(theme switch { 1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.Unspecified }, host.App.UserAppTheme);
        preferences.SetTheme((theme + 1) % 3); preferences.SetAccent("#AF52DE");
        var reopened = Preferences(new Publisher(), new TestPreferences(storage.Values));
        Assert.Equal((theme + 1) % 3, reopened.ThemeIndex); Assert.Equal("#AF52DE", reopened.AccentHex);
    }

    [Fact]
    public void SliderPreviewsStayLocalUntilCommitEvenDuringThemeChangesAndActivation()
    {
        var publisher = new Publisher(); var preferences = Preferences(publisher, new TestPreferences()); preferences.Attach(host.App);
        publisher.Values.Clear();
        for (var i = 0; i < 100; i++) preferences.SetAccent($"#{i:X6}", persist: false);
        Assert.Empty(publisher.Values);
        preferences.SetTheme(2); preferences.SynchronizeCounterAppearance();
        Assert.All(publisher.Values, value => Assert.Equal((2, AppearanceValues.DefaultAccent), value));
        preferences.SetAccent(preferences.AccentHex);
        Assert.Equal((2, "#000063"), publisher.Values[^1]);
    }

    [Fact]
    public void PublicationFailureCannotBreakAppAppearanceAndCanRetryTheSavedChoice()
    {
        var storage = new TestPreferences(); var publisher = new Publisher { Fails = true };
        var preferences = Preferences(publisher, storage); preferences.Attach(host.App);
        preferences.SetTheme(2); Assert.True(preferences.SetAccent("#FFFFFF"));
        Assert.Equal(AppTheme.Dark, host.App.UserAppTheme); Assert.Equal(Colors.White, host.App.Resources["Accent"]);
        Assert.Equal("#FFFFFF", storage.Get("accent", "")); Assert.Equal(2, storage.Get("appearance", 0));
        publisher.Fails = false; preferences.SynchronizeCounterAppearance();
        Assert.Equal((2, "#FFFFFF"), Assert.Single(publisher.Values));
    }

    [Fact]
    public void InvalidSettingsFallBackAndProfileChangesDoNotPublishWidgetAppearance()
    {
        var storage = new TestPreferences(); storage.Set("appearance", 7); storage.Set("accent", "invalid");
        var publisher = new Publisher(); var preferences = Preferences(publisher, storage); preferences.Attach(host.App);
        Assert.Equal((0, AppearanceValues.DefaultAccent), Assert.Single(publisher.Values)); publisher.Values.Clear();
        Assert.False(preferences.SetAccent("not a colour")); Assert.Throws<ArgumentOutOfRangeException>(() => preferences.SetTheme(-1));
        preferences.SaveProfile("Name", "photo"); preferences.SetCustomColourEnabled(false);
        Assert.Empty(publisher.Values);
    }

    [Fact]
    public void ForegroundRetryCannotThrowWhenLocalPreferenceReadsAreTemporarilyUnavailable()
    {
        var storage = new TestPreferences(); var publisher = new Publisher(); var preferences = Preferences(publisher, storage);
        preferences.Attach(host.App); publisher.Values.Clear(); storage.FailReads = true;
        preferences.SynchronizeCounterAppearance(); Assert.Empty(publisher.Values);
        storage.FailReads = false; preferences.SynchronizeCounterAppearance();
        Assert.Equal((0, AppearanceValues.DefaultAccent), Assert.Single(publisher.Values));
    }

    [Theory]
    [InlineData("profile_name")] [InlineData("profile_photo")]
    public void FailedProfileWritesDoNotPublishUncommittedStateOrPhotoOwnership(string failedKey)
    {
        var storage = new TestPreferences(); var preferences = Preferences(new Publisher(), storage);
        preferences.SaveProfile("Original", "original-photo");
        var changes = new List<string?>(); preferences.PropertyChanged += (_, args) => changes.Add(args.PropertyName);
        storage.FailWriteKey = failedKey;
        Assert.Throws<IOException>(() => preferences.SaveProfile("Changed", "draft-photo"));
        Assert.Equal("Original", preferences.DisplayName); Assert.Equal("original-photo", preferences.PhotoKey);
        Assert.Equal("original-photo", storage.Get("profile_photo", "")); Assert.Empty(changes);
        storage.FailWriteKey = null;
        Assert.True(preferences.SaveProfile("Changed", "draft-photo"));
        var reopened = Preferences(new Publisher(), storage);
        Assert.Equal("Changed", reopened.DisplayName); Assert.Equal("draft-photo", reopened.PhotoKey);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BothPreviewsMatchThemeAndExactAccentWithReadableIconWells(bool medium)
    {
        var preferences = Preferences(new Publisher(), new TestPreferences()); preferences.Attach(host.App);
        var preview = new CounterWidgetPreview(medium); host.Attach(preview);
        var frame = Assert.IsType<Border>(Assert.IsType<WidgetPreviewFrame>(preview.Content).Content);
        var content = Assert.IsType<Grid>(frame.Content);
        var labels = Assert.IsType<VerticalStackLayout>(content.Children[0]);
        var brand = Assert.IsType<Grid>(labels.Children[0]); var headerWell = Assert.IsType<Border>(brand.Children[0]);
        var wells = medium ? new[] { headerWell, Assert.IsType<Border>(content.Children[1]) } : new[] { headerWell };
        foreach (var theme in new[] { 1, 2 })
        foreach (var hex in AppearanceValues.Presets.Concat(["#000000", "#FFFFFF", "#00FFD2", "#FFFF00"]))
        {
            preferences.SetTheme(theme); preferences.SetAccent(hex);
            Assert.Equal(Color.FromArgb(theme == 2 ? "#1C1C1E" : "#FFFFFF"), frame.BackgroundColor);
            foreach (var well in wells)
            {
                var icon = Assert.IsType<StudioGlyph>(well.Content);
                Assert.Equal(hex, AppearanceColors.ToRgbHex(icon.Tint));
                Assert.True(AppearanceColors.Contrast(icon.Tint, well.BackgroundColor) >= 4.5);
            }
            foreach (var label in labels.Children.OfType<Label>().Append(Assert.IsType<Label>(brand.Children[1])))
                Assert.True(AppearanceColors.Contrast(label.TextColor, frame.BackgroundColor) >= 4.5);
        }
    }

    public void Dispose() => host.Dispose();
    private sealed class Publisher : ICounterAppearancePublisher
    {
        public bool Fails;
        public List<(int Theme, string Accent)> Values = [];
        public void Publish(int themeIndex, string accentHex)
        {
            if (Fails) throw new IOException("Shared container unavailable");
            Values.Add((themeIndex, accentHex));
        }
    }
}
