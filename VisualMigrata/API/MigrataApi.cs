using System;

namespace VisualMigrata.API;

public interface IVisualMigrataApi
{
    // New additions for backend control
    void SetEngineStatus(string status, bool isOnline);
    void UpdateTableHeaders(string col1, string col2, string col3, string col4, string col5);

    // Existing methods
    void UpdateGlobalStats(float totalPopulation, int activeMigrations);
    void UpdateSimulationData(GruisNodeData[] nodes, GruisMigrationData[] migrations);
    void UpdateLabelsJSON(string jsonPayload);
}

public static class MigrataBridge
{
    public static IVisualMigrataApi? ViewLayer { get; set; }

    public static event Action<uint, float>? OnInjectPopulationRequested;
    public static event Action<uint, uint, float>? OnForceMigrationRequested;
    public static event Action<uint, float>? OnSetActivityRequested;

    public static void RequestPopulationInjection(uint nodeId, float amount) => OnInjectPopulationRequested?.Invoke(nodeId, amount);
    public static void RequestForceMigration(uint sourceId, uint targetId, float volume) => OnForceMigrationRequested?.Invoke(sourceId, targetId, volume);
    public static void RequestSetActivity(uint nodeId, float activityLevel) => OnSetActivityRequested?.Invoke(nodeId, activityLevel);
}