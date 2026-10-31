namespace MinooWebCore.Models;

public sealed class FavoriteApplication
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public sealed class SaveFavoriteApplicationRequest
{
    public Guid? Id { get; set; }
    public string? Name { get; set; }
    public string? Url { get; set; }
}

public sealed class DeleteFavoriteApplicationRequest
{
    public Guid Id { get; set; }
}