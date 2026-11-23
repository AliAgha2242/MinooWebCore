using Microsoft.Extensions.Logging;
using MinooWebCore.Infrastructure.Dapper;
using MinooWebCore.Infrastructure.Services.Contract;

namespace MinooWebCore.Application.Services;

public sealed class MenuAccessService : IMenuAccessService
{
    private const int CompAppLocationKeyId = 2814;

    private const string AccessQuery = """
        SELECT CAST(
            CASE WHEN EXISTS
            (
                SELECT 1
                FROM CompAppLocation AS cal
                INNER JOIN LocUser AS lu
                    ON lu.CompAppLocationKeyID = cal.CompAppLocationKeyID
                INNER JOIN LocUserAccess AS lua
                    ON lua.LocUserKeyID = lu.LocUserKeyID
                INNER JOIN AppAccess AS aa
                    ON aa.AppAccessKeyID = lua.AppAccessKeyID
                INNER JOIN Users AS u
                    ON u.UsersKeyID = lu.UsersKeyID
                WHERE cal.CompAppLocationKeyID = @CompAppLocationKeyId
                  AND u.UserID = @UserId
                  AND aa.Name COLLATE Latin1_General_100_CI_AS
                      = @ObjectName COLLATE Latin1_General_100_CI_AS
            )
            THEN 1 ELSE 0 END AS int);
        """;

    private readonly IDapperRepository _dapper;
    private readonly ILogger<MenuAccessService> _logger;

    public MenuAccessService(
        IDapperRepository dapper,
        ILogger<MenuAccessService> logger)
    {
        _dapper = dapper;
        _logger = logger;
    }

    public async Task<bool> HasAccessAsync(
        int userId,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        if (userId <= 0 || string.IsNullOrWhiteSpace(objectName))
            return false;

        try
        {
            var result = await _dapper.QueryFirstOrDefaultAsync<int>(
                AccessQuery,
                new
                {
                    CompAppLocationKeyId,
                    UserId = userId,
                    ObjectName = objectName.Trim()
                },
                cancellationToken: cancellationToken);

            return result == 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Menu access lookup failed for user {UserId} and menu {ObjectName}. Access is denied.",
                userId,
                objectName);

            return false;
        }
    }
}
