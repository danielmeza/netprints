namespace NetPrints.Testing.Ui.Screenplay;

/// <summary>Something an actor can do, such as driving the editor (<see cref="UseNetPrints"/>).</summary>
public interface IAbility;

/// <summary>
/// A business-level step ("open the project", "compile"). Tasks call page objects; they never
/// locate UI elements themselves.
/// </summary>
public interface ITask
{
    string Description { get; }

    Task PerformAsAsync(Actor actor, CancellationToken cancellationToken);
}

/// <summary>A question about the state of the system, answered through page objects.</summary>
public interface IQuestion<T>
{
    string Description { get; }

    Task<T> AnsweredByAsync(Actor actor, CancellationToken cancellationToken);
}

/// <summary>The Screenplay actor: abilities, tasks, questions and a journal for diagnostics.</summary>
public sealed class Actor(string name)
{
    private readonly Dictionary<Type, IAbility> abilities = [];
    private readonly List<string> journal = [];

    public string Name { get; } = name;

    /// <summary>What the actor did, in order ("performed …", "asked …", "failed …").</summary>
    public IReadOnlyList<string> Journal => journal;

    public static Actor Named(string name) => new(name);

    public Actor WhoCan(params IAbility[] newAbilities)
    {
        foreach (var ability in newAbilities)
        {
            abilities[ability.GetType()] = ability;
        }

        return this;
    }

    public bool Can<TAbility>() where TAbility : IAbility => abilities.Values.OfType<TAbility>().Any();

    public TAbility Using<TAbility>() where TAbility : IAbility =>
        abilities.Values.OfType<TAbility>().FirstOrDefault()
        ?? throw new InvalidOperationException($"{Name} does not have the ability {typeof(TAbility).Name}.");

    public async Task AttemptsToAsync(ITask task, CancellationToken cancellationToken)
    {
        try
        {
            await task.PerformAsAsync(this, cancellationToken);
            journal.Add($"performed: {task.Description}");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            journal.Add($"failed: {task.Description}: {e.Message}");
            throw new ScreenplayException($"{Name} could not {task.Description}.", e);
        }
    }

    public async Task AttemptsToAsync(CancellationToken cancellationToken, params ITask[] tasks)
    {
        foreach (var task in tasks)
        {
            await AttemptsToAsync(task, cancellationToken);
        }
    }

    public async Task<T> AsksForAsync<T>(IQuestion<T> question, CancellationToken cancellationToken)
    {
        try
        {
            var answer = await question.AnsweredByAsync(this, cancellationToken);
            journal.Add($"asked: {question.Description} = {answer}");
            return answer;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            journal.Add($"failed to answer: {question.Description}: {e.Message}");
            throw new ScreenplayException($"{Name} could not find out {question.Description}.", e);
        }
    }

    public override string ToString() => Name;
}

public sealed class ScreenplayException(string message, Exception inner) : Exception(message, inner);
