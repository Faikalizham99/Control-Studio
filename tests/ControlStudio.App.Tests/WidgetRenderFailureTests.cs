using ControlStudio.App.Rendering;
using Xunit;

namespace ControlStudio.App.Tests;

public sealed class WidgetRenderFailureTests
{
    [Fact]
    public void ExpectedActionableErrorsAreRetained()
    {
        const string message = "A photo is missing. Remove its layer or choose it again.";
        Assert.Equal(message, WidgetRenderFailure.Describe(new IOException(message), WidgetRenderStage.Photo));
    }
    [Fact]
    public void NativeErrorsIdentifyTheStageAndTypeWithoutCopyingPrivateContentOrBlamingPhotos()
    {
        foreach (var stage in Enum.GetValues<WidgetRenderStage>())
        {
            var description = WidgetRenderFailure.Describe(new InvalidOperationException("/private/photo.png secret layer content"), stage);
            Assert.Contains("InvalidOperationException", description); Assert.Contains("Reopen the editor", description);
            Assert.DoesNotContain("/private", description); Assert.DoesNotContain("secret", description); Assert.DoesNotContain("choosing the photo again", description);
        }
        Assert.Contains("creating the preview image", WidgetRenderFailure.Describe(new Exception(), WidgetRenderStage.Snapshot));
        Assert.Contains("image checkpoint", WidgetRenderFailure.Describe(new ObjectDisposedException("private raster"), WidgetRenderStage.Checkpoint));
    }
    [Fact]
    public void NativeExceptionIdentifiersCanBeReportedWithoutCopyingArbitraryNativeText()
    {
        Assert.Contains("Exception/NSInvalidArgumentException", WidgetRenderFailure.Describe(new Exception(), WidgetRenderStage.Snapshot, "NSInvalidArgumentException"));
        foreach (var text in new[] { "/private/photo.png", "layer content", new string('x', 65) })
            Assert.DoesNotContain(text, WidgetRenderFailure.Describe(new Exception(), WidgetRenderStage.Snapshot, text));
    }
}
