using MinooWebCore.Infrastructure.Services.Contract;

namespace MinooWebCore.Services;

public sealed class AuthenticateService : IAuthenticateService
{
    private readonly IUsersService _usersService;

    public AuthenticateService(IUsersService usersService)
    {
        _usersService = usersService;
    }

    public async Task<AuthenticatedUser?> ValidateCredentialsAsync(
        string username,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await _usersService.GetByUsernameAsync(username, cancellationToken);
        if (user is null || string.IsNullOrEmpty(user.UserPassword))
            return null;

        return string.Equals(user.UserPassword, password, StringComparison.Ordinal)
            ? new AuthenticatedUser(user.UserId, user.UserName)
            : null;
    }
}

public sealed record AuthenticatedUser(int UserId, string UserName);