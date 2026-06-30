#nullable enable

using System.Text.Json.Nodes;

namespace CdpInspectorApp.Services;

/// <summary>
/// Defines a contract for components that participate in the application state save/restore lifecycle.
/// </summary>
public interface IStateProvider
{
    /// <summary>
    /// Unique key identifying the state managed by this provider.
    /// </summary>
    string StateKey { get; }

    /// <summary>
    /// Saves the current state of the component as a JSON node.
    /// </summary>
    /// <returns>A JsonNode representing the saved state, or null if there is no state to save.</returns>
    JsonNode? SaveState();

    /// <summary>
    /// Restores the state of the component from the provided JSON node.
    /// </summary>
    /// <param name="stateNode">The saved JSON node, or null if no state is available.</param>
    void LoadState(JsonNode? stateNode);
}
