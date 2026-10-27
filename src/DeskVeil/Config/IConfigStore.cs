namespace DeskVeil.Config;

public interface IConfigStore
{
    /// <summary>The in-memory configuration. Mutate it, then call <see cref="Save"/>.</summary>
    DeskVeilConfig Config { get; }

    void Save();
}
