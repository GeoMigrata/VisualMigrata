using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Text;
using VisualMigrata.API;

namespace VisualMigrata;

public partial class MainWindow : Window, IVisualMigrataApi
{
    // --- STANDALONE MOCK STATE ---
    private GruisNodeData[]? _mockNodes;
    private List<GruisMigrationData>? _mockMigrations;
    private float _mockTotalPop = 0f;

    public MainWindow()
    {
        InitializeComponent();
        
        // Register this active window as the view layer endpoint for the Simulation Core
        MigrataBridge.ViewLayer = this;
    }

    // =========================================================================
    // UI TO SIMULATION (OUTBOUND)
    // =========================================================================

    private void OnInjectPopulationClicked(object? sender, RoutedEventArgs e)
    {
        uint targetNodeId = (uint)(TargetNodeInput.Value ?? 1m);
        float injectionAmount = (float)(PopulationAmountInput.Value ?? 0m);

        if (injectionAmount > 0)
        {
            // 1. Fire the command to the GeoMigrata simulation bridge (for when it's connected)
            MigrataBridge.RequestPopulationInjection(targetNodeId, injectionAmount);

            // 2.[STANDALONE TEST]: Apply the data locally if we are using the Mock Layout
            if (_mockNodes != null)
            {
                ApplyLocalPopulationInjection(targetNodeId, injectionAmount);
            }
        }
    }

    private void OnLoadMockDataClicked(object? sender, RoutedEventArgs e)
    {
        InjectDemoData();
    }

    // =========================================================================
    // SIMULATION TO UI (INBOUND API IMPLEMENTATION)
    // =========================================================================

    public void UpdateGlobalStats(float totalPopulation, int activeMigrations)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TotalPopulationText.Text = $"{totalPopulation:F1} M";
            ActiveMigrationsText.Text = activeMigrations.ToString();
        });
    }

    public void UpdateSimulationData(GruisNodeData[] nodes, GruisMigrationData[] migrations)
    {
        MapControl.UpdateSimulation(nodes, migrations);
    }

    public void UpdateLabelsJSON(string jsonPayload)
    {
        MapControl.UpdateLabelsJSON(jsonPayload);
    }

    // =========================================================================
    // LOCAL DEMO LOGIC & STATE MANAGEMENT
    // =========================================================================

    private void ApplyLocalPopulationInjection(uint nodeId, float amount)
    {
        if (_mockNodes == null || _mockMigrations == null) return;

        bool nodeFound = false;

        // Find the node and mutate its population
        for (int i = 0; i < _mockNodes.Length; i++)
        {
            if (_mockNodes[i].Id == nodeId)
            {
                _mockNodes[i].Population += amount;
                _mockTotalPop += amount;
                nodeFound = true;
                break;
            }
        }

        if (nodeFound)
        {
            // Regenerate the JSON with the updated Population text
            string newJson = GenerateMockJson(_mockNodes);

            // Push the updated Data and JSON back to the GruisEngine View Layer
            UpdateSimulationData(_mockNodes, _mockMigrations.ToArray());
            UpdateLabelsJSON(newJson);
            UpdateGlobalStats(_mockTotalPop, _mockMigrations.Count);
        }
    }

    private void InjectDemoData()
    {
        var rnd = new Random(1337);
        int count = 40;
        float extent = 200.0f;
        
        // Reset state
        _mockTotalPop = 0f;
        _mockNodes = new GruisNodeData[count];
        _mockMigrations = new List<GruisMigrationData>();

        for (int i = 1; i <= count; i++)
        {
            float pop = 5.0f + (float)rnd.NextDouble() * 15.0f;
            _mockTotalPop += pop;

            _mockNodes[i - 1] = new GruisNodeData
            {
                Id = (uint)i,
                PosX = ((float)rnd.NextDouble() - 0.5f) * extent * 2.0f,
                PosZ = ((float)rnd.NextDouble() - 0.5f) * extent * 2.0f,
                PosY = 0.5f,
                Population = pop,
                ActivityLevel = 0.5f + (float)rnd.NextDouble() * 0.5f,
                Name = pop > 15.0f ? $"HUB-{i}" : $"Sector-{i}",
                Status = pop > 15.0f ? "OPERATIONAL" : "ACTIVE"
            };
        }

        for (int i = 0; i < count; i++)
        {
            int targetIdx = rnd.Next(count);
            if (i != targetIdx)
            {
                _mockMigrations.Add(new GruisMigrationData
                {
                    SourceId = _mockNodes[i].Id,
                    TargetId = _mockNodes[targetIdx].Id,
                    Volume = _mockNodes[i].Population * 0.5f
                });
            }
        }

        // Bake the UI layouts
        string newJson = GenerateMockJson(_mockNodes);

        // Send to Engine
        UpdateSimulationData(_mockNodes, _mockMigrations.ToArray());
        UpdateLabelsJSON(newJson);
        UpdateGlobalStats(_mockTotalPop, _mockMigrations.Count);
    }

    private string GenerateMockJson(GruisNodeData[] nodes)
    {
        var jsonBuilder = new StringBuilder();
        jsonBuilder.Append("{\"nodes\":[");

        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];

            // Dynamically upgrade standard sectors to HUBs if population is injected high enough
            bool isHub = node.Population > 15.0f;
            string nodeName = isHub ? $"HUB-{node.Id}" : $"Sector-{node.Id}";
            string nodeStatus = isHub ? "CRITICAL" : "OPTIMAL";

            jsonBuilder.Append($"{{\"id\": {node.Id}, \"elements\":[");
            if (isHub)
            {
                jsonBuilder.Append($"{{\"type\": \"text\", \"content\": \"{nodeName} COMMAND\", \"x\": 30, \"y\": 60, \"size\": 64, \"color\": \"#EFB122\", \"weight\": \"bold\"}},");
                jsonBuilder.Append($"{{\"type\": \"text\", \"content\": \"STATUS: {nodeStatus}\", \"x\": 30, \"y\": 140, \"size\": 32, \"color\": \"#FF0000\", \"weight\": \"bold\"}},");
                jsonBuilder.Append($"{{\"type\": \"text\", \"content\": \"POPULATION: {(int)node.Population}M\", \"x\": 30, \"y\": 190, \"size\": 24, \"color\": \"#FFFFFF\", \"weight\": \"normal\"}}");
            }
            else
            {
                jsonBuilder.Append($"{{\"type\": \"text\", \"content\": \"{nodeName}\", \"x\": 30, \"y\": 60, \"size\": 48, \"color\": \"#00FFFF\", \"weight\": \"bold\"}},");
                jsonBuilder.Append($"{{\"type\": \"text\", \"content\": \"STATUS: {nodeStatus}\", \"x\": 30, \"y\": 120, \"size\": 24, \"color\": \"#FFFFFF\", \"weight\": \"normal\"}}");
            }

            jsonBuilder.Append("]}");
            if (i < nodes.Length - 1) jsonBuilder.Append(",");
        }
        jsonBuilder.Append("]}");

        return jsonBuilder.ToString();
    }
}