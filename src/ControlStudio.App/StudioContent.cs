using ControlStudio.Core;
using ControlStudio.Core.Actions;

namespace ControlStudio.App;

internal enum ClipboardKind { Text, Photo, Link, Snippet }

internal static class StudioContent
{
    public static IReadOnlyList<ActionDefinition> ClipboardItems(IEnumerable<ActionDefinition> actions, ClipboardKind kind = ClipboardKind.Text) =>
        actions.Where(action => Kind(action.Parameters) == kind).OrderBy(action => action.SortOrder).ToArray();

    public static ClipboardKind? Kind(ActionParameters parameters) => parameters switch
    {
        CopyTextParameters => ClipboardKind.Text, PhotoParameters => ClipboardKind.Photo,
        OpenUrlParameters => ClipboardKind.Link, SnippetParameters => ClipboardKind.Snippet, _ => null
    };

    public static string LanguageName(SnippetLanguage language) => language switch
    {
        SnippetLanguage.PlainText => "Plain text", SnippetLanguage.JavaScript => "JavaScript", SnippetLanguage.TypeScript => "TypeScript",
        SnippetLanguage.Json => "JSON", SnippetLanguage.Html => "HTML", SnippetLanguage.Css => "CSS", SnippetLanguage.CSharp => "C#",
        SnippetLanguage.Sql => "SQL", SnippetLanguage.Xml => "XML", SnippetLanguage.Cpp => "C++", _ => language.ToString()
    };

    public static ActionDefinition Draft(ActionDefinition? original, ClipboardKind kind, string? name, string? content,
        SnippetLanguage language = SnippetLanguage.PlainText)
    {
        if (original is not null && Kind(original.Parameters) != kind)
            throw new ArgumentException("An item's type cannot change while editing.", nameof(original));
        ActionParameters parameters = kind switch
        {
            ClipboardKind.Text => new CopyTextParameters(content ?? ""),
            ClipboardKind.Link => new OpenUrlParameters((content ?? "").Trim()),
            ClipboardKind.Photo => new PhotoParameters(content ?? ""),
            ClipboardKind.Snippet => new SnippetParameters(content ?? "", language),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
        return original is null
            ? new ActionDefinition(Guid.NewGuid(), (name ?? "").Trim(), parameters, Icon: kind == ClipboardKind.Link ? ActionIcon.Link : ActionIcon.Copy)
            : original with { Name = (name ?? "").Trim(), Parameters = parameters };
    }

    public static ActionDefinition ClipboardDraft(ActionDefinition? original, string? name, string? text)
    {
        return Draft(original, ClipboardKind.Text, name, text);
    }

    public static CounterSnapshot? WidgetSnapshot(CounterSnapshot snapshot)
    {
        snapshot.Validate();
        // The widget reads shared storage; a private fallback is not its value.
        return snapshot.IsShared ? snapshot : null;
    }
}
