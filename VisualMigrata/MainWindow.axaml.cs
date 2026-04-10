using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Text;

namespace VisualMigrata;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Event handler for the UI button. Triggers the generation and injection
    /// of 3D node data and 2D overlay JSON definitions into the GruisEngine via the MapControl.
    /// </summary>
    private void OnLoadMockDataClicked(object? sender, RoutedEventArgs e)
    {
        InjectDemoData();
    }

    /// <summary>
    /// Generates simulation nodes, migration arcs, and customized JSON UI layouts,
    /// identical to the C++ 'injectDemoData()' logic.
    /// </summary>
    private void InjectDemoData()
    {
        var rnd = new Random(1337);
        int count = 40;
        float extent = 200.0f;

        var nodes = new GruisNodeData[count];
        var migrations = new List<GruisMigrationData>();
        
        var jsonBuilder = new StringBuilder();
        jsonBuilder.Append("{\"nodes\": [");

        // 1. Generate Simulated Nodes & Construct JSON in parallel
        for (int i = 1; i <= count; i++)
        {
            float pop = 5.0f + (float)rnd.NextDouble() * 15.0f;
            bool isHub = pop > 15.0f;

            // Define node structural data
            nodes[i - 1] = new GruisNodeData
            {
                Id = (uint)i,
                PosX = ((float)rnd.NextDouble() - 0.5f) * extent * 2.0f,
                PosZ = ((float)rnd.NextDouble() - 0.5f) * extent * 2.0f,
                PosY = 0.5f,
                Population = pop,
                ActivityLevel = 0.5f + (float)rnd.NextDouble() * 0.5f,
                Name = isHub ? $"HUB-{i}" : $"Sector-{i}",
                Status = isHub ? "OPERATIONAL" : "ACTIVE"
            };

            // 2. Generate JSON UI layout specifically mapped to this Node's ID
            jsonBuilder.Append($"{{\"id\": {i}, \"elements\": [");

            if (isHub)
            {
                // High Population HUB layout (Custom bolding and colors)
                jsonBuilder.Append($"{{\"type\": \"text\", \"content\": \"HUB-{i} COMMANDDDDDDDDD\", \"x\": 30, \"y\": 60, \"size\": 64, \"color\": \"#EFB122\", \"weight\": \"bold\"}},");
                jsonBuilder.Append($"{{\"type\": \"text\", \"content\": \"STATUS: CRITICAL\", \"x\": 30, \"y\": 140, \"size\": 32, \"color\": \"#FF0000\", \"weight\": \"bold\"}},");
                jsonBuilder.Append($"{{\"type\": \"text\", \"content\": \"POPULATION: {(int)pop}M\", \"x\": 30, \"y\": 190, \"size\": 24, \"color\": \"#FFFFFF\", \"weight\": \"normal\"}}");
            }
            else
            {
                // Standard Sector layout
                jsonBuilder.Append($"{{\"type\": \"text\", \"content\": \"Sector-{i}\", \"x\": 30, \"y\": 60, \"size\": 48, \"color\": \"#00FFFF\", \"weight\": \"bold\"}},");
                jsonBuilder.Append($"{{\"type\": \"text\", \"content\": \"STATUS: OPTIMAL\", \"x\": 30, \"y\": 120, \"size\": 24, \"color\": \"#FFFFFF\", \"weight\": \"normal\"}}");
            }

            jsonBuilder.Append("]}");
            
            // Add comma separator between node JSON objects, except for the last one
            if (i < count) jsonBuilder.Append(",");
        }
        jsonBuilder.Append("]}");

        // 3. Generate Simulated Migration Arcs
        for (int i = 0; i < count; i++)
        {
            int targetIdx = rnd.Next(count);
            if (i != targetIdx)
            {
                migrations.Add(new GruisMigrationData
                {
                    SourceId = nodes[i].Id,
                    TargetId = nodes[targetIdx].Id,
                    Volume = nodes[i].Population * 0.5f
                });
            }
        }

        // 4. Inject 3D layout data to the Avalonia control wrapper.
        // NOTE: GruisMapControl.UpdateSimulation handles pinning (fixed blocks or GCHandle) 
        // to prevent garbage collection moving the memory during the P/Invoke call.
        MapControl.UpdateSimulation(nodes, migrations.ToArray());

        // 5. Inject the dynamically baked UI layout instructions
        MapControl.UpdateLabelsJSON(jsonBuilder.ToString());
    }
}