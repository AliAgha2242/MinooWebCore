namespace MinooWebCore.Services;

public interface IAuthenticateService
{
    Task<AuthenticatedUser?> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default);
}