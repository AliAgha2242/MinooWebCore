namespace MinooWebCore.Data.Models;

public sealed class ReportDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string? SqlQuery { get; set; }
    public string? ExcelFileName { get; set; }
    public byte[]? ExcelContent { get; set; }
    public bool NeedsPagination { get; set; }
    public int? PageSize { get; set; }
    public int SoftwareGroupId { get; set; }
    public int ReportLevelId { get; set; }
    // Legacy field retained for backward compatibility; new reports use ServerTargets.
    public decimal? SpecificServerVahedeTejariSN { get; set; }
    public bool UseDefaultServer { get; set; } = true;
    public ICollection<ReportDefinitionServer> ServerTargets { get; set; } = new List<ReportDefinitionServer>();
    public SoftwareGroup SoftwareGroup { get; set; } = null!;
    public ReportLevel ReportLevel { get; set; } = null!;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? DeletedAtUtc { get; set; }

    public ICollection<ReportParameterDefinition> Parameters { get; set; } = new List<ReportParameterDefinition>();
}


public sealed class ReportDefinitionServer
{
    public int ReportDefinitionId { get; set; }
    public decimal VahedeTejariSN { get; set; }
    public ReportDefinition Report { get; set; } = null!;
}

public sealed class SoftwareGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<ReportDefinition> Reports { get; set; } = new List<ReportDefinition>();
}

public sealed class ReportLevel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<ReportDefinition> Reports { get; set; } = new List<ReportDefinition>();
}
