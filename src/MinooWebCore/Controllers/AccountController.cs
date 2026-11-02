using System.Security.Claims;
using MinooWebCore.Models;
using MinooWebCore.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MinooWebCore.Controllers;

public sealed class AccountController : Controller
{
    private readonly IAuthenticateService _authenticationService;
    private readonly IReportsSsoTicketService _reportsSso;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        IAuthenticateService authenticationService,
        IReportsSsoTicketService reportsSso,
        IConfiguration configuration,
        ILogger<AccountController> logger)
    {
        _authenticationService = authenticationService;
        _reportsSso = reportsSso;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);
            return RedirectToAction("Index", "Home");
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var authenticatedUser = await _authenticationService.ValidateCredentialsAsync(
            model.Username, model.Password, cancellationToken);

        if (authenticatedUser is null)
        {
            ModelState.AddModelError(string.Empty, "نام کاربری یا رمز عبور صحیح نیست.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, authenticatedUser.UserId.ToString()),
            new(ClaimTypes.Name, authenticatedUser.UserName),
            new("UserCode", authenticatedUser.UserCode),
            new(ClaimTypes.Role, "User")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);
        var properties = new AuthenticationProperties
        {
            IsPersistent = model.RememberMe,
            AllowRefresh = true,
            IssuedUtc = DateTimeOffset.UtcNow
        };

        if (model.RememberMe)
            properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(14);

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);

        _logger.LogInformation("A user signed in successfully.");
        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            return Redirect(model.ReturnUrl);

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> LaunchReports(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userCode = User.FindFirstValue("UserCode");
        var userName = User.Identity?.Name;
        var reportsBaseUrl = _configuration["Sso:Reports:BaseUrl"];

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(userCode) ||
            string.IsNullOrWhiteSpace(userName))
            return Forbid();

        if (!Uri.TryCreate(reportsBaseUrl, UriKind.Absolute, out var baseUri) ||
            (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            _logger.LogError("The Reports base URL must be an absolute HTTP or HTTPS URL.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                "سامانه گزارشات هنوز در تنظیمات درگاه مادر تعریف نشده است.");
        }

        var ticket = await _reportsSso.IssueAsync(
            new ReportsSsoIdentity(userId, userCode, userName), cancellationToken);

        ViewBag.ReportsConsumeUrl = new Uri(baseUri.AbsoluteUri.TrimEnd('/') + "/sso/consume").ToString();
        ViewBag.SsoTicket = ticket;
        Response.Headers["Cache-Control"] = "no-store, no-cache";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public IActionResult ForgotPassword() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ForgotPassword(string? usernameOrEmail)
    {
        TempData["Info"] = "این بخش آماده اتصال به فرایند بازیابی رمز عبور شماست.";
        return RedirectToAction(nameof(ForgotPassword));
    }

    public IActionResult AccessDenied() => View();
}