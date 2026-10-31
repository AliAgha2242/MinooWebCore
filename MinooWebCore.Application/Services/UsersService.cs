using System.Security.Cryptography;
using MinooWebCore.Data;
using MinooWebCore.Data.Models;
using MinooWebCore.Infrastructure.Services.Contract;

namespace MinooWebCore.Application.Services;

public sealed class UsersService : IUsersService
{
    private readonly IGenericRepository<UserModel> _repository;
    private readonly IDecriptServices _decryptService;

    public UsersService(IDecriptServices decryptService, IGenericRepository<UserModel> repository)
    {
        _decryptService = decryptService;
        _repository = repository;
    }

    public async Task<UserModel?> GetAsync(int userId)
    {
        var users = await _repository.FindAsync(x => x.UserId == userId);
        var user = users.FirstOrDefault();
        if (user is not null)
            user.UserPassword = await _decryptService.Decript(user.UserPassword);

        return user;
    }

    public async Task<UserModel?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        var users = await _repository.FindAsync(
            x => x.UserName == username,
            cancellationToken);

        var user = users.FirstOrDefault();
        if (user is not null)
            user.UserPassword = await _decryptService.Decript(user.UserPassword);

        return user;
    }

    public async Task<string> ResetPass(UserModel user, CancellationToken cancellationToken = default)
    {
        var newPassword = RandomNumberGenerator.GetInt32(1_000_000, 10_000_000).ToString();
        user.UserPassword = await _decryptService.Encript(newPassword);
        _repository.Update(user);
        await _repository.SaveChangesAsync(cancellationToken);
        return newPassword;
    }
}