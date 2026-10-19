namespace ControlStudio.Core.Actions;

public interface IActionPlatform
{
    Task<bool> OpenUrlAsync(Uri uri, CancellationToken cancellationToken);
    Task CopyTextAsync(string text, CancellationToken cancellationToken);
}

public sealed record ActionExecutionResult(string Message);
public interface IActionExecutor
{
    Task<ActionExecutionResult> ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken = default);
}

public sealed class ActionExecutor(IActionValidator validator, IActionPlatform platform, ICounterRepository counter) : IActionExecutor
{
    public async Task<ActionExecutionResult> ExecuteAsync(ActionDefinition action, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        validator.Validate(action);
        try
        {
            switch (action.Parameters)
            {
                case OpenUrlParameters url:
                    if (!await platform.OpenUrlAsync(ActionValidator.ValidateWebUrl(url.Url), cancellationToken))
                        throw new ActionExecutionException("iOS could not open this link. Check the address and try again.");
                    return new("Link opened.");
                case CopyTextParameters copy:
                    await platform.CopyTextAsync(copy.Text, cancellationToken);
                    return new("Text copied to the clipboard.");
                case IncrementCounterParameters:
                    var snapshot = await counter.IncrementAsync(cancellationToken);
                    return new(snapshot.IsShared
                        ? $"Counter {snapshot.Value}. {(snapshot.RefreshRequested ? "Widget refresh requested." : "Saved to shared storage.")}"
                        : $"Counter {snapshot.Value}. Saved on this iPhone; App Group sharing is unavailable.");
                default:
                    throw new ActionValidationException("This action type is unavailable.");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (ActionExecutionException) { throw; }
        catch (CounterStorageException) { throw; }
        catch (Exception) { throw new ActionExecutionException("The action could not finish. Check its settings and try again."); }
    }
}
