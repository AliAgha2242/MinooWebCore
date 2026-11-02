using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinooWebCore.Services;

namespace MinooWebCore.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/sso/reports")]
public sealed class ReportsSsoController(
    IReportsSsoTicketService tickets,
    IConfiguration configuration,
    ILogger<ReportsSsoController> logger) : ControllerBase
{
    public sealed record RedeemRequest(string Ticket);

    [HttpPost("redeem")]
    [RequestSizeLimit(4096)]
    public async Task<IActionResult> Redeem(
        [FromBody] RedeemRequest? request,
        [FromHeader(Name = "X-Reports-Client-Secret")] string? suppliedSecret,
        CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store, no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";

        var expectedSecret = configuration["Sso:Reports:ClientSecret"];
        if (!SecretsMatch(expectedSecret, suppliedSecret))
            return Unauthorized();

        if (request is null || string.IsNullOrWhiteSpace(request.Ticket))
            return BadRequest();

        var user = await tickets.RedeemAsync(request.Ticket);
        if (user is null)
            return Unauthorized();

        logger.LogInformation("A one-time Reports SSO ticket was redeemed.");
        return Ok(user);
    }

    private static bool SecretsMatch(string? expected, string? supplied)
    {
        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(supplied))
            return false;
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var suppliedBytes = Encoding.UTF8.GetBytes(supplied);
        return expectedBytes.Length == suppliedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes);
    }
}
