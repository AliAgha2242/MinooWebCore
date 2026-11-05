using System.Security.Cryptography;
using MinooWebCore.Data;
using MinooWebCore.Data.Models;
using MinooWebCore.Infrastructure.Dapper;
using MinooWebCore.Infrastructure.Services.Contract;

namespace MinooWebCore.Application.Services;

public sealed class UsersService : IUsersService
{
    private readonly IGenericRepository<UserModel> _repository;
    private readonly IDecriptServices _decryptService;
    private readonly IDapperRepository _dapperRepository;

    public UsersService(
        IDecriptServices decryptService,
        IGenericRepository<UserModel> repository,
        IDapperRepository dapperRepository)
    {
        _decryptService = decryptService;
        _repository = repository;
        _dapperRepository = dapperRepository;
    }

    public async Task<UserModel?> GetAsync(int userId)
    {
        var users = await _repository.FindAsync(x => x.UserId == userId);
        var user = users.FirstOrDefault();
        if (user is not null)
            user.UserPassword = await _decryptService.Decript(user.UserPassword);
        return user;
    }

    public async Task<UserModel?> GetByUsernameAsync(string UserID, CancellationToken cancellationToken = default)
    {
        List<UserModel?>? users;
        if (string.IsNullOrWhiteSpace(UserID))
            return null;
        try
        {
            users = await _repository.FindAsync(x => x.UserId.ToString() == UserID, cancellationToken);
        }
        catch (Exception ex)
        {

            throw;
        }
        // Login name is Users.UserCode (string), not the numeric UserId column.
        var user = users.FirstOrDefault();
        if (user is not null)
        {
            user.UserPassword = await _decryptService.Decript(user.UserPassword);
            user.VahedeTejariSn = (await _dapperRepository.QueryAsync<decimal>(
                "select (select top 1 VahedeTejariSN from paVahedeTejari where CompanyID = Company.CompanyID) from Users join Company on Company.CompanyKeyId = Users.defaultCompanyKeyId where Users.UsersKeyID = @UsersKeyId",
                new { UsersKeyId = user.UsersKeyId },
                cancellationToken: cancellationToken)).FirstOrDefault();
        }

        return user;
    }

    public async Task<string> ResetPass(UserModel user, CancellationToken cancellationToken = default)
    {
        var newPassword = RandomNumberGenerator.GetInt32(1_000_000, 10_000_000).ToString();
        user.UserPassword = await _decryptService.Encript(newPassword);
        _repository.Update(user);
        //await _repository.SaveChangesAsync(cancellationToken);
        return newPassword;
    }
}