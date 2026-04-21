# VisualMigrata Integration Documentation

## 1. Architecture Overview
**VisualMigrata** acts as the decoupled View Layer for your spatial simulation project. It consists of two main components:
1. **GruisEngine (C++):** Hardware-accelerated OpenGL 3D rendering core.
2. **Avalonia UI (C#):** A hardware-accelerated 2D UI overlay and application host.

To ensure strict separation of concerns, the backend simulation engine (`GeoMigrata`) **never interacts directly with the UI or the 3D engine**. Instead, all communication flows through a thread-safe static API layer called `MigrataBridge`.

---

## 2. The Communication Bridge (`MigrataBridge`)
The `VisualMigrata.API.MigrataBridge` is the central nervous system of the application.

* **ViewLayer:** The Avalonia Window registers itself here. The backend uses this reference to push data *to* the UI.
* **Events:** The backend subscribes to static events here to listen for commands *from* the UI.

---

## 3. Inbound API: Sending Data to the UI (Backend -> UI)
The backend simulation core pushes updates to the visualization layer by calling methods on `MigrataBridge.ViewLayer`. These methods automatically handle thread-safety, meaning your backend can call them from background calculation threads without crashing the UI.

### 3.1. Core Simulation Data
Updates the 3D OpenGL viewport with positional and migration data.
```csharp
// Push spatial arrays to the 3D Engine
MigrataBridge.ViewLayer?.UpdateSimulationData(GruisNodeData[] nodes, GruisMigrationData[] migrations);

// Push JSON string to generate custom 3D text billboards
MigrataBridge.ViewLayer?.UpdateLabelsJSON(string jsonPayload);
```

### 3.2. Command Center Statistics
Updates the UI overlay panel on the left side of the screen.
```csharp
// Set the Engine Status (e.g., "ONLINE", true = Green, false = Red)
MigrataBridge.ViewLayer?.SetEngineStatus("ONLINE", true);

// Update global running counters
MigrataBridge.ViewLayer?.UpdateGlobalStats(totalPopulation: 566.4f, activeMigrations: 12);
```

### 3.3. Dynamic Data Table
The central "Global Node Overview" table is 100% dynamic. The backend dictates how many columns exist, the headers, the text content, and even the text color/weight.

```csharp
// 1. Define your dynamic headers
string[] headers = new string[] { "ID", "NODE NAME", "POPULATION", "CUSTOM METRIC" };

// 2. Build your rows using TableCellData
TableCellData[][] rows = new TableCellData[][]
{
    new TableCellData[] {
        new TableCellData { Text = "1" },
        new TableCellData { Text = "Sector-Alpha" },
        new TableCellData { Text = "31.5 M", ColorHex = "#EFB122", IsBold = true }, // Custom Yellow + Bold
        new TableCellData { Text = "Safe", ColorHex = "#00FF00" }                 // Custom Green
    }
};

// 3. Push to UI
MigrataBridge.ViewLayer?.UpdateDynamicTable(headers, rows);
```

---

## 4. Outbound API: Listening for UI Commands (UI -> Backend)
When a user clicks a button in the "Simulation Tools" panel, `VisualMigrata` fires a C# event. Your backend should subscribe to these events during startup to respond to user input.

```csharp
// Subscribe to UI Commands
MigrataBridge.OnInjectPopulationRequested += HandlePopulationInjection;
MigrataBridge.OnForceMigrationRequested += HandleForceMigration;
MigrataBridge.OnSetActivityRequested += HandleSetActivity;

// --- Handlers ---
private void HandlePopulationInjection(uint targetNodeId, float amount)
{
    Console.WriteLine($"UI requested {amount}M injected into Node {targetNodeId}");
    // Apply logic to backend engine...
}

private void HandleForceMigration(uint sourceId, uint targetId, float volume)
{
    Console.WriteLine($"UI requested {volume}M flow from {sourceId} to {targetId}");
    // Apply logic to backend engine...
}

private void HandleSetActivity(uint nodeId, float activityLevel)
{
    Console.WriteLine($"UI requested Node {nodeId} activity set to {activityLevel}");
    // Apply logic to backend engine...
}
```

---

## 5. Structs & Data Types

### `GruisNodeData` (Struct)
Defines the 3D physical representation of a node.
* `Id` (uint): Unique identifier.
* `PosX`, `PosY`, `PosZ` (float): Spatial coordinates in the 3D world.
* `Population` (float): Drives the scale/intensity of the node visualization.
* `ActivityLevel` (float): Range `0.0` - `1.0`. Drives visual pulsing speed/brightness.
* `Name` (string[32]): Internal name (max 32 chars).
* `Status` (string[16]): Internal status (max 16 chars).

### `GruisMigrationData` (Struct)
Defines a 3D arc flowing between two nodes.
* `SourceId` (uint): ID of the emitting node.
* `TargetId` (uint): ID of the receiving node.
* `Volume` (float): Controls the thickness/brightness of the physical arc.

### `TableCellData` (Class)
Defines a single cell in the dynamic Data Table overlay.
* `Text` (string): The text to display.
* `ColorHex` (string): Hex color code (e.g., `"#FFFFFF"`, `"#FF0000"`). Default is White.
* `IsBold` (bool): If true, renders the text with a heavy font weight.

---

## 6. Quick Start: Connecting the Backend
Here is a minimal complete example of how a standalone `GeoMigrata` background service connects to the View Layer.

```csharp
using VisualMigrata.API;
using System.Threading;

public class GeoMigrataEngine
{
    public void Start()
    {
        // 1. Subscribe to UI Commands
        MigrataBridge.OnInjectPopulationRequested += (id, vol) => Inject(id, vol);
        MigrataBridge.OnForceMigrationRequested += (src, tgt, vol) => Migrate(src, tgt, vol);

        // 2. Wait for the UI layer to finish initializing
        while (MigrataBridge.ViewLayer == null)
        {
            Thread.Sleep(100);
        }

        // 3. Mark Engine as Online
        MigrataBridge.ViewLayer.SetEngineStatus("ONLINE", true);

        // 4. Start Background Simulation Loop
        var simThread = new Thread(SimulationLoop);
        simThread.IsBackground = true;
        simThread.Start();
    }

    private void SimulationLoop()
    {
        while (true)
        {
            // Calculate simulation step...
            
            // Push updates to UI
            MigrataBridge.ViewLayer?.UpdateSimulationData(_activeNodes, _activeMigrations);
            MigrataBridge.ViewLayer?.UpdateGlobalStats(_totalPop, _activeMigrations.Length);
            
            Thread.Sleep(100); // 10 ticks per second
        }
    }
}
```