using System;

namespace VisualMigrata.API;

/// <summary>
/// Interface implemented by VisualMigrata to receive data from the headless GeoMigrata simulator.
/// </summary>
public interface IVisualMigrataApi
{
    void UpdateGlobalStats(float totalPopulation, int activeMigrations);
    void UpdateSimulationData(GruisNodeData[] nodes, GruisMigrationData[] migrations);
    void UpdateLabelsJSON(string jsonPayload);
}

/// <summary>
/// Static bridge that the background GeoMigrata Simulator uses to push data, 
/// and the Avalonia UI uses to fire commands back to the simulator.
/// </summary>
public static class MigrataBridge
{
    // The headless simulation core will call methods on this reference
    public static IVisualMigrataApi? ViewLayer { get; set; }

    // --- TWO-WAY COMMUNICATION EVENTS ---
    
    // Fired by the UI when the user requests population injection.
    // The headless GeoMigrata simulator should subscribe to this event.
    public static event Action<uint, float>? OnInjectPopulationRequested;

    /// <summary>
    /// Invoked by the UI to send a command to the Simulation Core.
    /// </summary>
    public static void RequestPopulationInjection(uint nodeId, float amount)
    {
        OnInjectPopulationRequested?.Invoke(nodeId, amount);
    }
}