// --- START OF FILE MigrataApi.cs ---
using System;

namespace VisualMigrata.API;

/// <summary>
/// A generic container allowing the backend to format table cells dynamically.
/// </summary>
public class TableCellData
{
    public string Text { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#FFFFFF";
    public bool IsBold { get; set; } = false;
}

public interface IVisualMigrataApi
{
    void SetEngineStatus(string status, bool isOnline);
    
    // NEW: The backend passes an array of headers, and a 2D array of rows/columns.
    void UpdateDynamicTable(string[] headers, TableCellData[][] rows);

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