namespace MinooWebCore.Infrastructure.Services.Contract;

public interface IMenuAccessService
{
    Task<bool> HasAccessAsync(
        int userId,
        string objectName,
        CancellationToken cancellationToken = default);
}
