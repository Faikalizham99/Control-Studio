namespace ControlStudio.Core.Actions;

public sealed class ActionValidator : IActionValidator
{
    public void Validate(ActionDefinition action)
    {
        if (action is null || action.Id == Guid.Empty)
            throw new ActionValidationException("This action needs a valid identifier.");
        if (string.IsNullOrWhiteSpace(action.Name) || action.Name.Length > 60 || action.Name.Any(char.IsControl))
            throw new ActionValidationException("Enter a name of 1–60 characters without control characters.");
        if (!Enum.IsDefined(action.Category) || !Enum.IsDefined(action.Icon) || !Enum.IsDefined(action.Color) || action.SortOrder < 0)
            throw new ActionValidationException("Choose a supported category, icon and color.");
        switch (action.Parameters)
        {
            case OpenUrlParameters url:
                ValidateWebUrl(url.Url);
                break;
            case CopyTextParameters copy when !string.IsNullOrWhiteSpace(copy.Text) && copy.Text.Length <= 10_000 && !copy.Text.Contains('\0'):
            case IncrementCounterParameters:
                break;
            case CopyTextParameters:
                throw new ActionValidationException("Enter text to copy, up to 10,000 characters, without null characters.");
            case PhotoParameters photo when Guid.TryParseExact(photo.AssetKey, "N", out _):
                break;
            case PhotoParameters:
                throw new ActionValidationException("Choose a photo before saving.");
            case SnippetParameters snippet when Enum.IsDefined(snippet.Language) && !string.IsNullOrWhiteSpace(snippet.Code) && snippet.Code.Length <= 50_000 && !snippet.Code.Contains('\0'):
                break;
            case SnippetParameters:
                throw new ActionValidationException("Choose a language and enter code, up to 50,000 characters, without null characters.");
            default:
                throw new ActionValidationException("Choose a supported action type.");
        }
    }

    public static Uri ValidateWebUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 2048 || value.Any(c => char.IsControl(c) || char.IsWhiteSpace(c)) ||
            value.Contains('\\') || !Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ActionValidationException("Enter a complete http:// or https:// link without spaces or embedded credentials.");
        return uri;
    }
}
