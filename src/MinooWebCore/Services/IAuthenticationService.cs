namespace MinooWebCore.Services;

public interface IAuthenticateService
{
    Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default);
}
