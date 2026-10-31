using MinooWebCore.Data.Models;

namespace MinooWebCore.Infrastructure.Services.Contract;

public interface IUsersService
{
    Task<UserModel?> GetAsync(int userId);
    Task<UserModel?> GetByUsernameAsync(string UserId, CancellationToken cancellationToken = default);
    Task<string> ResetPass(UserModel user, CancellationToken cancellationToken = default);
}