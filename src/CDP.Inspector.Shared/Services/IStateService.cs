#nullable enable

namespace CdpInspectorApp.Services;

/// <summary>
/// Defines a contract for the service managing application state save/restore operations.
/// </summary>
public interface IStateService
{
    /// <summary>
    /// Registers a state provider to participate in load/save lifecycles.
    /// </summary>
    /// <param name="provider">The state provider.</param>
    void RegisterProvider(IStateProvider provider);

    /// <summary>
    /// Unregisters a state provider.
    /// </summary>
    /// <param name="stateKey">The state key to unregister.</param>
    void UnregisterProvider(string stateKey);

    /// <summary>
    /// Saves all registered state providers' states to a persistent store.
    /// </summary>
    void Save();

    /// <summary>
    /// Loads all registered state providers' states from the persistent store.
    /// </summary>
    void Load();
}
