using System.Security.Cryptography;
using System.Text;
using MinooWebCore.Services.Redis;

namespace MinooWebCore.Services;

public sealed record ReportsSsoIdentity(string UserId, string UserCode, string UserName);

public interface IReportsSsoTicketService
{
    Task<string> IssueAsync(ReportsSsoIdentity user, CancellationToken cancellationToken = default);
    Task<ReportsSsoIdentity?> RedeemAsync(string ticket);
}

public sealed class ReportsSsoTicketService(IRedisService redis, IConfiguration configuration)
    : IReportsSsoTicketService
{
    private const string KeyPrefix = "minoo:sso:reports:ticket:";

    public async Task<string> IssueAsync(
        ReportsSsoIdentity user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(user.UserId) ||
            string.IsNullOrWhiteSpace(user.UserCode) ||
            string.IsNullOrWhiteSpace(user.UserName))
            throw new ArgumentException("اطلاعات هویتی کاربر برای صدور بلیت کامل نیست.", nameof(user));

        var ticket = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var lifetime = Math.Clamp(configuration.GetValue("Sso:TicketLifetimeSeconds", 60), 30, 180);
        await redis.SetAsync(Key(ticket), user, TimeSpan.FromSeconds(lifetime));
        return ticket;
    }

    public Task<ReportsSsoIdentity?> RedeemAsync(string ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 256)
            return Task.FromResult<ReportsSsoIdentity?>(null);
        return redis.GetAndDeleteAsync<ReportsSsoIdentity>(Key(ticket));
    }

    private static string Key(string ticket)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ticket));
        return KeyPrefix + Convert.ToHexString(hash);
    }
}
