using System.Reflection;
using ControlStudio.App.Views;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Xunit;

namespace ControlStudio.App.Tests;

[Collection("MAUI controls")]
public sealed class ProfileEditorSaveTests : IDisposable
{
    private readonly MauiTestContext host = new();
    private readonly TestPreferences storage = new();
    private readonly Photos photos = new();
    private AppearancePreferences Preferences() => new(new Publisher(), NullLogger<AppearancePreferences>.Instance, storage);
    private NavigationPage Open(ProfileEditorPage page)
    {
        var navigation = (NavigationPage)Activator.CreateInstance(typeof(NavigationPage), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [false, new ContentPage()], null)!;
        host.App.Windows[0].Page = navigation;
        if (!host.App.Windows[0].IsActivated) ((IWindow)host.App.Windows[0]).Activated();
        return navigation;
    }
    private static IEnumerable<Element> Descendants(Element element)
    {
        yield return element;
        if (element is IVisualTreeElement tree)
            foreach (var child in tree.GetVisualChildren().OfType<Element>())
                foreach (var nested in Descendants(child)) yield return nested;
    }
    private static Button Button(Page page, string id) => Descendants(page).OfType<Button>().Single(x => x.AutomationId == id);
    private static void Name(Page page, string text) => Descendants(page).OfType<Entry>().Single(x => x.AutomationId == "ProfileName").Text = text;

    [Theory]
    [InlineData("profile_name")] [InlineData("profile_photo")]
    public async Task FailedSaveKeepsDraftRetryableAndCancelRemovesOnlyItsImportedPhoto(string failedKey)
    {
        var preferences = Preferences(); preferences.SaveProfile("Original", "original-photo");
        var page = new ProfileEditorPage(preferences, photos, _ => Task.FromResult<string?>("Files"));
        var navigation = Open(page); await navigation.PushAsync(page);
        await page.ChoosePhotoAsync(); Name(page, "Changed"); storage.FailWriteKey = failedKey;
        await page.SaveAsync();
        Assert.Equal("Original", preferences.DisplayName); Assert.Equal("original-photo", preferences.PhotoKey);
        Assert.Empty(photos.Removed); Assert.Contains(page, navigation.Navigation.NavigationStack);
        Assert.True(Button(page, "SaveProfile").IsEnabled); Assert.True(Button(page, "ChooseProfilePhoto").IsEnabled);
        Assert.True(Button(page, "ProfileBack").IsEnabled); Assert.True(Button(page, "ProfileCancel").IsEnabled);
        Assert.Contains(Descendants(page).OfType<Label>(), label => label.IsVisible && label.Text == "Your profile could not be saved. Please try again.");
        await navigation.PopAsync();
        Assert.Equal("draft-photo", Assert.Single(photos.Removed));
        Assert.Equal("original-photo", storage.Get("profile_photo", ""));
    }

    [Fact]
    public async Task FailedSaveCanRetryAndSuccessfulSaveKeepsNewPhotoAcrossDeparture()
    {
        var preferences = Preferences(); preferences.SaveProfile("Original", "original-photo");
        var page = new ProfileEditorPage(preferences, photos, _ => Task.FromResult<string?>("Photo Library"));
        var navigation = Open(page); await navigation.PushAsync(page); await page.ChoosePhotoAsync(); Name(page, "Changed");
        storage.FailWrites = true; await page.SaveAsync(); storage.FailWrites = false; await page.SaveAsync();
        Assert.DoesNotContain(page, navigation.Navigation.NavigationStack);
        Assert.Equal("original-photo", Assert.Single(photos.Removed));
        var reopened = Preferences(); Assert.Equal("Changed", reopened.DisplayName); Assert.Equal("draft-photo", reopened.PhotoKey);
        Assert.False(Button(page, "SaveProfile").IsEnabled); Assert.False(Button(page, "ProfileBack").IsEnabled);
    }

    [Fact]
    public async Task DismissalFailureAfterCommitAllowsBackWithoutRewritingPreferencesOrDeletingSavedPhoto()
    {
        var preferences = Preferences(); preferences.SaveProfile("Original", "original-photo"); var attempts = 0;
        Task Close() => ++attempts == 1 ? Task.FromException(new IOException("navigation failed")) : Task.CompletedTask;
        var page = new ProfileEditorPage(preferences, photos, _ => Task.FromResult<string?>("Files"), Close);
        var navigation = Open(page); await navigation.PushAsync(page); await page.ChoosePhotoAsync(); Name(page, "Changed");
        await page.SaveAsync();
        Assert.Equal("draft-photo", preferences.PhotoKey); Assert.Equal("original-photo", Assert.Single(photos.Removed));
        Assert.False(Button(page, "SaveProfile").IsEnabled); Assert.False(Button(page, "ChooseProfilePhoto").IsEnabled);
        Assert.True(Button(page, "ProfileBack").IsEnabled); Assert.True(Button(page, "ProfileCancel").IsEnabled);
        storage.FailWrites = true;
        ((IButtonController)Button(page, "ProfileBack")).SendClicked(); Assert.Equal(2, attempts);
        await navigation.PopAsync();
        Assert.Equal("original-photo", Assert.Single(photos.Removed)); Assert.Equal("draft-photo", storage.Get("profile_photo", ""));
    }

    [Fact]
    public async Task RapidSaveWhileDismissalIsPendingCannotCommitOrCloseTwice()
    {
        var completion = new TaskCompletionSource(); var calls = 0;
        Task Close() { calls++; return completion.Task; }
        var preferences = Preferences(); var page = new ProfileEditorPage(preferences, photos, null, Close);
        var navigation = Open(page); await navigation.PushAsync(page); Name(page, "Valid name");
        var saving = page.SaveAsync(); Assert.False(Button(page, "SaveProfile").IsEnabled); Assert.False(Button(page, "ProfileBack").IsEnabled);
        await page.SaveAsync(); Assert.Equal(1, calls); completion.SetResult(); await saving;
        Assert.True(Button(page, "ProfileBack").IsEnabled); Assert.False(Button(page, "SaveProfile").IsEnabled);
    }

    [Fact]
    public async Task InvalidNameDoesNotWriteOrDiscardTheDraftPhoto()
    {
        var preferences = Preferences(); preferences.SaveProfile("Original", "original-photo");
        var page = new ProfileEditorPage(preferences, photos, _ => Task.FromResult<string?>("Files"));
        var navigation = Open(page); await navigation.PushAsync(page); await page.ChoosePhotoAsync(); Name(page, " ");
        await page.SaveAsync(); Assert.Equal("original-photo", preferences.PhotoKey); Assert.Empty(photos.Removed);
        Assert.True(Button(page, "SaveProfile").IsEnabled); Assert.Contains(page, navigation.Navigation.NavigationStack);
    }

    public void Dispose() => host.Dispose();
    private sealed class Publisher : ICounterAppearancePublisher { public void Publish(int themeIndex, string accentHex) { } }
    private sealed class Photos : IProfilePhotoStore
    {
        public List<string> Removed { get; } = [];
        public string? GetPath(string key) => string.IsNullOrEmpty(key) ? null : "/private/" + key + ".jpg";
        public Task<string?> PickAsync(bool fromLibrary) => Task.FromResult<string?>("draft-photo");
        public Task<string?> AdjustAsync(string key) => Task.FromResult<string?>("draft-photo");
        public void Remove(string key) => Removed.Add(key);
    }
}
