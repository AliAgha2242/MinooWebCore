namespace MinooWebCore.Data.Models;

public sealed class ParameterType
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public bool RequiresDataSource { get; set; }
    public bool RequiresOptions { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<ReportParameterDefinition> Parameters { get; set; } = new List<ReportParameterDefinition>();
}
