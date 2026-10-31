using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinooWebCore.Data.Models;
using MinooWebCore.Persistence.StaticConfiguration;
using MinooWebCore.Services.Wcf;
using MinooWebCore.Services.Wcf.Contracts;
using StackExchange.Redis;

namespace MinooWebCore.Controllers;

[ApiController]
[Authorize]
[Route("api/mapping")]
public sealed class MappingController : ControllerBase
{
    private static readonly TimeSpan MappingInterval = TimeSpan.FromHours(1);
    private const string EndpointPath = "/Minoo.Services.SanadMali/SanadMaliWebService.svc";

    private readonly IMinooConfiguration _minooConfiguration;
    private readonly IWcfClient _wcfClient;
    private readonly IDatabase _redis;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MappingController> _logger;

    public MappingController(
        IMinooConfiguration minooConfiguration,
        IWcfClient wcfClient,
        IConnectionMultiplexer redis,
        IConfiguration configuration,
        ILogger<MappingController> logger)
    {
        _minooConfiguration = minooConfiguration;
        _wcfClient = wcfClient;
        _redis = redis.GetDatabase();
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("servers")]
    public async Task<ActionResult<IReadOnlyList<MappingServerStatus>>> GetServers()
    {
        var servers = _minooConfiguration.GetAllSerevr();

        var statuses = await Task.WhenAll(servers.Select(async server =>
        {
            var ttl = await _redis.KeyTimeToLiveAsync(GetRateLimitKey(server));
            return new MappingServerStatus(
                server.VahedeTejariSN,
                server.ServerName,
                server.DatabaseName,
                ttl is null,
                ttl is null ? 0 : Math.Max(0, (long)Math.Ceiling(ttl.Value.TotalSeconds)));
        }));

        return Ok(statuses);
    }

    [HttpPost("execute")]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<IReadOnlyList<MappingExecutionResult>>> Execute(
        [FromBody] ExecuteMappingRequest request)
    {
        if (request.ServerIds is null || request.ServerIds.Count == 0)
            return BadRequest(new { message = "حداقل یک سرور را انتخاب کنید." });

        var serviceUserId = _configuration["Mapping:ServiceUserId"];
        var serviceUserPassword = _configuration["Mapping:ServiceUserPassword"];

        if (string.IsNullOrWhiteSpace(serviceUserId) ||
            string.IsNullOrWhiteSpace(serviceUserPassword))
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "نام کاربری و رمز سرویس WCF در تنظیمات Mapping وارد نشده است." });
        }

        var configuredServers = _minooConfiguration.GetAllSerevr();
        var requestedIds = request.ServerIds.Distinct().ToHashSet();
        var selectedServers = configuredServers
            .Where(server => requestedIds.Contains(server.VahedeTejariSN))
            .ToList();

        if (selectedServers.Count != requestedIds.Count)
            return BadRequest(new { message = "یک یا چند سرور انتخاب‌شده در تنظیمات معتبر نیست." });

        var callSynchronous = _configuration.GetValue("Mapping:CallSynchronous", false);

        var results = await Task.WhenAll(selectedServers.Select(server =>
            ExecuteForServerAsync(server, serviceUserId, serviceUserPassword, callSynchronous)));

        return Ok(results);
    }

    private async Task<MappingExecutionResult> ExecuteForServerAsync(
        ServerInfo server,
        string serviceUserId,
        string serviceUserPassword,
        bool callSynchronous)
    {
        if (string.IsNullOrWhiteSpace(server.ServerName))
            return new MappingExecutionResult(server.VahedeTejariSN, server.ServerName ?? "",
                "Failed", "نام سرور در AppConfig.json خالی است.");

        var key = GetRateLimitKey(server);
        var acquired = await _redis.StringSetAsync(
            key,
            Guid.NewGuid().ToString("N"),
            MappingInterval,
            When.NotExists);

        if (!acquired)
        {
            var ttl = await _redis.KeyTimeToLiveAsync(key);
            var remaining = ttl is null ? 0 : Math.Max(0, (long)Math.Ceiling(ttl.Value.TotalSeconds));
            return new MappingExecutionResult(server.VahedeTejariSN, server.ServerName,
                "Limited", $"برای این سرور قبلاً درخواست ثبت شده است. زمان باقی‌مانده: {remaining} ثانیه.",
                remaining);
        }

        try
        {
            var endpoint = new UriBuilder(Uri.UriSchemeHttp, server.ServerName)
            {
                Path = EndpointPath
            }.Uri.AbsoluteUri;

            var serviceRequest = new ServiceRequest
            {
                ServiceName = "NegashtWebService",
                ServiceCommand = "NegashtCaller",
                ConsumerUsername = serviceUserId,
                ConsumerPassword = serviceUserPassword
            };

            // The middleware's dispatcher expects the command name as the first serialized argument.
            serviceRequest.SetArguments("NegashtCaller", serviceUserId, serviceUserPassword, callSynchronous);

            var response = await _wcfClient.InvokeAsync<IBaseService, ServiceResponse>(
                endpoint,
                service => service.RunAsync(serviceRequest));

            var success = response.ErrorCode == 0 &&
                          response.ServiceResult is bool serviceResult &&
                          serviceResult;

            var responseMessage = success
                ? "درخواست نگاشت با موفقیت به سرویس ارسال شد."
                : response.ErrorCode != 0
                    ? $"سرویس WCF خطا برگرداند (کد {response.ErrorCode})."
                    : "سرویس WCF اجرای نگاشت را تأیید نکرد.";

            return new MappingExecutionResult(
                server.VahedeTejariSN,
                server.ServerName,
                success ? "Success" : "Failed",
                responseMessage);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception,
                "WCF mapping execution failed for server {ServerName} (SN: {ServerId}).",
                server.ServerName,
                server.VahedeTejariSN);

            // The one-hour cooldown is retained for failed attempts as well.
            return new MappingExecutionResult(server.VahedeTejariSN, server.ServerName,
                "Failed", "ارتباط با سرویس WCF ناموفق بود. محدودیت یک‌ساعته برای این تلاش حفظ شد.");
        }
    }

    private static RedisKey GetRateLimitKey(ServerInfo server) =>
        $"minoo:mapping:cooldown:{server.VahedeTejariSN.ToString(CultureInfo.InvariantCulture)}";
}

public sealed class ExecuteMappingRequest
{
    public List<decimal> ServerIds { get; set; } = [];
}

public sealed record MappingServerStatus(
    decimal ServerId,
    string ServerName,
    string DatabaseName,
    bool CanExecute,
    long RemainingSeconds);

public sealed record MappingExecutionResult(
    decimal ServerId,
    string ServerName,
    string Status,
    string Message,
    long? RemainingSeconds = null);
