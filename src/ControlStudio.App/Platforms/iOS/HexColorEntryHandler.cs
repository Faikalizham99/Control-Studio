using Microsoft.Maui.Handlers;
using Microsoft.Maui.Platform;
using UIKit;

namespace ControlStudio.App.Platforms.iOS;

public sealed class HexColorEntryHandler : EntryHandler
{
    protected override void ConnectHandler(MauiTextField platformView)
    {
        base.ConnectHandler(platformView);
        platformView.BorderStyle = UITextBorderStyle.None;
        platformView.BackgroundColor = UIColor.Clear;
        platformView.AutocorrectionType = UITextAutocorrectionType.No;
        platformView.SpellCheckingType = UITextSpellCheckingType.No;
        platformView.AutocapitalizationType = UITextAutocapitalizationType.AllCharacters;
    }
}
