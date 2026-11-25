using Microsoft.Extensions.Logging;
using MinooWebCore.Infrastructure.Dapper;
using MinooWebCore.Infrastructure.Services.Contract;

namespace MinooWebCore.Application.Services;

public sealed class MenuAccessService : IMenuAccessService
{
    private const int CompAppLocationKeyId = 2814;

    private const string AccessQuery = """
        SELECT CAST(CASE WHEN EXISTS
        (
            SELECT 1
            FROM LocUserAccess lua
            INNER JOIN LocUser lu ON lu.LocUserKeyID = lua.LocUserKeyID
            INNER JOIN AppGroup ag ON ag.AppGroupKeyID = lua.AppGroupKeyID
            INNER JOIN AppAccess aa ON aa.ApplicationKeyID = ag.ApplicationKeyID
            INNER JOIN LocConnection lc ON lc.CompAppLocationKeyID = lu.CompAppLocationKeyID
            INNER JOIN Server s ON s.ServerKeyID = lc.ServerKeyID
            INNER JOIN GroupAccess ga ON ga.AppAccessKeyID = aa.AppAccessKeyID
                AND ga.AppGroupKeyID = ag.AppGroupKeyID
            INNER JOIN Users u ON u.UsersKeyID = lu.UsersKeyID
            WHERE u.UserID = @UserCode
              AND ag.ApplicationKeyID = 22
              AND aa.Name = @ObjectName
        ) THEN 1 ELSE 0 END AS int);
        """;

    private const string AuthorizedServersQuery = """
        SELECT DISTINCT s.ServerName
        FROM LocUserAccess lua
        INNER JOIN LocUser lu ON lu.LocUserKeyID = lua.LocUserKeyID
        INNER JOIN AppGroup ag ON ag.AppGroupKeyID = lua.AppGroupKeyID
        INNER JOIN AppAccess aa ON aa.ApplicationKeyID = ag.ApplicationKeyID
        INNER JOIN LocConnection lc ON lc.CompAppLocationKeyID = lu.CompAppLocationKeyID
        INNER JOIN Server s ON s.ServerKeyID = lc.ServerKeyID
        INNER JOIN GroupAccess ga ON ga.AppAccessKeyID = aa.AppAccessKeyID
            AND ga.AppGroupKeyID = ag.AppGroupKeyID
        INNER JOIN Users u ON u.UsersKeyID = lu.UsersKeyID
        WHERE u.UserID = @UserCode
          AND ag.ApplicationKeyID = 22
          AND aa.Name = @AccessName
        ORDER BY s.ServerName;
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
        string userCode,
        string objectName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userCode) || string.IsNullOrWhiteSpace(objectName))
            return false;

        try
        {
            var result = await _dapper.QueryFirstOrDefaultAsync<int>(
                AccessQuery,
                new { UserCode = userCode.Trim(), ObjectName = objectName.Trim() },
                cancellationToken: cancellationToken);
            return result == 1;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Menu access lookup failed for user {UserCode} and menu {ObjectName}. Access is denied.", userCode, objectName);
            return false;
        }
    }

    public async Task<IReadOnlyList<string>> GetAuthorizedServerNamesAsync(
        string userCode,
        string accessName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userCode) || string.IsNullOrWhiteSpace(accessName))
            return [];

        try
        {
            var names = await _dapper.QueryAsync<string>(
                AuthorizedServersQuery,
                new { UserCode = userCode.Trim(), AccessName = accessName.Trim() },
                cancellationToken: cancellationToken);
            return names.Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Authorized server lookup failed for user {UserCode} and access {AccessName}.", userCode, accessName);
            return [];
        }
    }
}
