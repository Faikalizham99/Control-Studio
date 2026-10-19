using System.Text.Json.Serialization;

namespace ControlStudio.Core.Actions;

public enum ActionCategory { Finance, Productivity, Utilities, Applications, Learning, Personal }
public enum ActionIcon { Link, Copy, Counter, Star, Book, Bolt }
public enum ActionColor { Blue, Purple, Green, Orange, Pink, Slate }

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(OpenUrlParameters), "openUrl")]
[JsonDerivedType(typeof(CopyTextParameters), "copyText")]
[JsonDerivedType(typeof(IncrementCounterParameters), "incrementCounter")]
public abstract record ActionParameters;
public sealed record OpenUrlParameters(string Url) : ActionParameters;
public sealed record CopyTextParameters(string Text) : ActionParameters;
public sealed record IncrementCounterParameters : ActionParameters;

public interface IActionDefinition
{
    Guid Id { get; }
    string Name { get; }
    ActionParameters Parameters { get; }
}

public sealed record ActionDefinition(
    Guid Id, string Name, ActionParameters Parameters,
    ActionCategory Category = ActionCategory.Personal,
    ActionIcon Icon = ActionIcon.Bolt,
    ActionColor Color = ActionColor.Blue,
    bool IsFavorite = false, int SortOrder = 0) : IActionDefinition;

public sealed record DashboardDocument(int SchemaVersion, List<ActionDefinition> Actions);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow)]
[JsonSerializable(typeof(DashboardDocument))]
public partial class ActionJsonContext : JsonSerializerContext;

public interface IActionRepository
{
    Task<IReadOnlyList<ActionDefinition>> ReadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ActionDefinition action, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task MoveAsync(Guid id, int offset, CancellationToken cancellationToken = default);
}

public interface IActionValidator { void Validate(ActionDefinition action); }
public sealed class ActionValidationException(string message) : Exception(message);
public sealed class ActionExecutionException(string message) : Exception(message);
