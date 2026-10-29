namespace MinooWebCore.Services;

/// <summary>
/// Demo-only provider. Replace with your real API or database-backed authentication.
/// </summary>
public sealed class AuthenticateService : IAuthenticateService
{
    public Task<bool> ValidateCredentialsAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        // Intentionally demo-only credentials; never use this provider in production.
        var valid = string.Equals(username, "admin", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(password, "Minoo@123", StringComparison.Ordinal);
        return Task.FromResult(valid);
    }
}
