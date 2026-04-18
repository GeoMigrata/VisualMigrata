namespace VisualMigrata;

// Add this strongly-typed class to hold the row data
public class NodeViewModel
{
    public uint Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string PopulationStr { get; set; } = string.Empty;
    public string ActivityStr { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusColor { get; set; } = string.Empty;
}