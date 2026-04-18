using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using VisualMigrata.API;

namespace VisualMigrata;

public partial class MainWindow : Window, IVisualMigrataApi
{
    private GruisNodeData[]? _mockNodes;
    private List<GruisMigrationData>? _mockMigrations;
    private float _mockTotalPop = 0f;
    private DispatcherTimer? _mockSimTimer; // Timer for standalone tick logic

    public MainWindow()
    {
        InitializeComponent();
        MigrataBridge.ViewLayer = this;

        // Hide the mock button if built for Release
#if !DEBUG
        ResetDemoBtn.IsVisible = false;
#else
        // Standalone UI Mock Feature: Initialize a background timer to "drain" and recycle migrations 
        _mockSimTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _mockSimTimer.Tick += MockSimulationTick;
        _mockSimTimer.Start();
#endif
    }

    // =========================================================================
    // UI TO SIMULATION BUTTON HANDLERS
    // =========================================================================

    private void OnInjectPopulationClicked(object? sender, RoutedEventArgs e)
    {
        uint targetNodeId = (uint)(TargetNodeInput.Value ?? 1m);
        float amount = (float)(PopulationAmountInput.Value ?? 0m);

        if (amount > 0)
        {
            MigrataBridge.RequestPopulationInjection(targetNodeId, amount);
            if (_mockNodes != null) ApplyLocalPopulationInjection(targetNodeId, amount);
        }
    }

    private void OnForceMigrationClicked(object? sender, RoutedEventArgs e)
    {
        uint sourceId = (uint)(MigrateSourceInput.Value ?? 1m);
        uint targetId = (uint)(MigrateTargetInput.Value ?? 2m);
        float volume = (float)(MigrateVolumeInput.Value ?? 0m);

        if (sourceId != targetId && volume > 0)
        {
            MigrataBridge.RequestForceMigration(sourceId, targetId, volume);
            if (_mockNodes != null) ApplyLocalForceMigration(sourceId, targetId, volume);
        }
    }

    private void OnSetActivityClicked(object? sender, RoutedEventArgs e)
    {
        uint nodeId = (uint)(ActivityNodeInput.Value ?? 1m);
        float activity = (float)(ActivityLevelInput.Value ?? 1.0m);

        MigrataBridge.RequestSetActivity(nodeId, activity);
        if (_mockNodes != null) ApplyLocalActivityChange(nodeId, activity);
    }

    private void OnToggleTableClicked(object? sender, RoutedEventArgs e)
    {
        DataTableOverlay.IsVisible = !DataTableOverlay.IsVisible;
        if (DataTableOverlay.IsVisible) RefreshDataTable();
    }

    private void OnLoadMockDataClicked(object? sender, RoutedEventArgs e) => InjectDemoData();

    // =========================================================================
    // DATA TABLE BINDING LOGIC
    // =========================================================================

    private void RefreshDataTable()
    {
        if (_mockNodes == null) return;

        var viewData = _mockNodes.Select(n => new NodeViewModel
        {
            Id = n.Id,
            Name = n.Name,
            PopulationStr = $"{n.Population:F1} M",
            ActivityStr = $"{n.ActivityLevel:F2}",
            Status = n.Status,
            StatusColor = n.Status == "CRITICAL" ? "#FF0000" : "#00FF00"
        }).ToList();

        NodeTableItems.ItemsSource = viewData;
    }

    // =========================================================================
    // INBOUND API IMPLEMENTATION
    // =========================================================================
    
    public void SetEngineStatus(string status, bool isOnline)
    {
        Dispatcher.UIThread.Post(() =>
        {
            EngineStatusText.Text = status;
            // Green if online, Red if offline
            EngineStatusText.Foreground = isOnline ? 
                Avalonia.Media.Brushes.Lime : Avalonia.Media.Brushes.Red;
        });
    }

    public void UpdateTableHeaders(string col1, string col2, string col3, string col4, string col5)
    {
        Dispatcher.UIThread.Post(() =>
        {
            HeaderCol0.Text = col1;
            HeaderCol1.Text = col2;
            HeaderCol2.Text = col3;
            HeaderCol3.Text = col4;
            HeaderCol4.Text = col5;
        });
    }

    public void UpdateGlobalStats(float totalPopulation, int activeMigrations)
    {
        Dispatcher.UIThread.Post(() =>
        {
            TotalPopulationText.Text = $"{totalPopulation:F1} M";
            ActiveMigrationsText.Text = activeMigrations.ToString();
        });
    }

    public void UpdateSimulationData(GruisNodeData[] nodes, GruisMigrationData[] migrations) => MapControl.UpdateSimulation(nodes, migrations);
    
    public void UpdateLabelsJSON(string jsonPayload) => MapControl.UpdateLabelsJSON(jsonPayload);

    // =========================================================================
    // STANDALONE LOCAL MOCK LOGIC
    // =========================================================================

    /// <summary>
    /// Simulates the background engine tick by slowly draining migration volumes.
    /// When they reach 0, they are recycled out of the active flow.
    /// </summary>
    private void MockSimulationTick(object? sender, EventArgs e)
    {
        if (_mockMigrations == null || _mockMigrations.Count == 0) return;

        bool migrationsChanged = false;

        // Iterate backwards because we are modifying and removing items from the list
        for (int i = _mockMigrations.Count - 1; i >= 0; i--)
        {
            var m = _mockMigrations[i];
            m.Volume -= 0.15f; // Drain rate

            if (m.Volume <= 0)
            {
                _mockMigrations.RemoveAt(i);
            }
            else
            {
                _mockMigrations[i] = m;
            }
            migrationsChanged = true;
        }

        if (migrationsChanged && _mockNodes != null)
        {
            // Sync the updated arrays back to the visualizer
            UpdateSimulationData(_mockNodes, _mockMigrations.ToArray());
            UpdateGlobalStats(_mockTotalPop, _mockMigrations.Count);
        }
    }

    private void ApplyLocalPopulationInjection(uint nodeId, float amount)
    {
        if (_mockNodes == null) return;
        bool found = false;

        for (int i = 0; i < _mockNodes.Length; i++)
        {
            if (_mockNodes[i].Id == nodeId)
            {
                _mockNodes[i].Population += amount;
                _mockTotalPop += amount;
                found = true; break;
            }
        }
        if (found) RefreshView();
    }

    private void ApplyLocalForceMigration(uint sourceId, uint targetId, float volume)
    {
        if (_mockNodes == null || _mockMigrations == null) return;

        int srcIdx = -1, tgtIdx = -1;
        for (int i = 0; i < _mockNodes.Length; i++)
        {
            if (_mockNodes[i].Id == sourceId) srcIdx = i;
            if (_mockNodes[i].Id == targetId) tgtIdx = i;
        }

        if (srcIdx != -1 && tgtIdx != -1)
        {
            float actualVol = Math.Min(_mockNodes[srcIdx].Population, volume);
            _mockNodes[srcIdx].Population -= actualVol;
            _mockNodes[tgtIdx].Population += actualVol;

            _mockMigrations.Add(new GruisMigrationData { SourceId = sourceId, TargetId = targetId, Volume = actualVol });
            RefreshView();
        }
    }

    private void ApplyLocalActivityChange(uint nodeId, float activity)
    {
        if (_mockNodes == null) return;
        bool found = false;

        for (int i = 0; i < _mockNodes.Length; i++)
        {
            if (_mockNodes[i].Id == nodeId)
            {
                _mockNodes[i].ActivityLevel = activity;
                found = true; break;
            }
        }
        if (found) RefreshView();
    }

    private void RefreshView()
    {
        if (_mockNodes == null || _mockMigrations == null) return;
        
        UpdateSimulationData(_mockNodes, _mockMigrations.ToArray());
        UpdateLabelsJSON(GenerateMockJson(_mockNodes));
        UpdateGlobalStats(_mockTotalPop, _mockMigrations.Count);

        if (DataTableOverlay.IsVisible) RefreshDataTable();
    }

    private void InjectDemoData()
    {
        SetEngineStatus("OFFLINE (TEST)", false);
        
        var rnd = new Random(1337);
        int count = 40;
        float extent = 200.0f;
        
        _mockTotalPop = 0f;
        _mockNodes = new GruisNodeData[count];
        _mockMigrations = new List<GruisMigrationData>();

        for (int i = 1; i <= count; i++)
        {
            float pop = 5.0f + (float)rnd.NextDouble() * 15.0f;
            _mockTotalPop += pop;

            _mockNodes[i - 1] = new GruisNodeData
            {
                Id = (uint)i, PosX = ((float)rnd.NextDouble() - 0.5f) * extent * 2.0f, PosZ = ((float)rnd.NextDouble() - 0.5f) * extent * 2.0f, PosY = 0.5f,
                Population = pop, ActivityLevel = 0.5f + (float)rnd.NextDouble() * 0.5f,
                Name = pop > 15.0f ? $"HUB-{i}" : $"Sector-{i}", Status = pop > 15.0f ? "OPERATIONAL" : "ACTIVE"
            };
        }

        for (int i = 0; i < count; i++)
        {
            int targetIdx = rnd.Next(count);
            if (i != targetIdx)
            {
                _mockMigrations.Add(new GruisMigrationData { SourceId = _mockNodes[i].Id, TargetId = _mockNodes[targetIdx].Id, Volume = _mockNodes[i].Population * 0.5f });
            }
        }
        RefreshView();
    }

    private string GenerateMockJson(GruisNodeData[] nodes)
    {
        var jsonBuilder = new StringBuilder();
        jsonBuilder.Append("{\"nodes\":[");

        for (int i = 0; i < nodes.Length; i++)
        {
            var node = nodes[i];
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
