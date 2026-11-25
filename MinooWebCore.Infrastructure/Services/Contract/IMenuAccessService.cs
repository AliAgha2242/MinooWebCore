namespace MinooWebCore.Infrastructure.Services.Contract;

public interface IMenuAccessService
{
    Task<bool> HasAccessAsync(
        string userCode,
        string objectName,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetAuthorizedServerNamesAsync(
        string userCode,
        string accessName,
        CancellationToken cancellationToken = default);
}
