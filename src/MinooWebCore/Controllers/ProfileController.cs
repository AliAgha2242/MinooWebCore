using System.Security.Claims;
using MinooWebCore.Infrastructure.Services.Contract;
using MinooWebCore.Models;
using MinooWebCore.Services.Redis;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MinooWebCore.Controllers;

[Authorize]
public sealed class ProfileController : Controller
{
    private const int MaxFavoriteApplications = 50;
    private readonly IUsersService _usersService;
    private readonly IRedisService _redis;

    public ProfileController(IUsersService usersService, IRedisService redis)
    {
        _usersService = usersService;
        _redis = redis;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!TryGetUserId(out var userId))
            return Forbid();
        var user = await _usersService.GetAsync(userId);
        if (user is null)
            return Forbid();
        ViewBag.UserName = user.UserName;
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> FavoriteApps()
    {
        if (!TryGetUserId(out var userId))
            return Forbid();
        return Ok(await GetFavoriteAppsAsync(userId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveFavoriteApp([FromBody] SaveFavoriteApplicationRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Forbid();

        var name = request.Name?.Trim();
        var urlText = request.Url?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80)
            return BadRequest(new { message = "نام اپلیکیشن باید بین ۱ تا ۸۰ کاراکتر باشد." });

        if (string.IsNullOrWhiteSpace(urlText) || urlText.Length > 2048 ||
            !Uri.TryCreate(urlText, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            return BadRequest(new { message = "لینک معتبر با پروتکل http یا https وارد کنید." });

        var apps = await GetFavoriteAppsAsync(userId);
        if (request.Id is Guid id)
        {
            var existing = apps.FirstOrDefault(app => app.Id == id);
            if (existing is null)
                return NotFound(new { message = "اپلیکیشن موردنظر پیدا نشد." });
            existing.Name = name;
            existing.Url = uri.AbsoluteUri;
        }
        else
        {
            if (apps.Count >= MaxFavoriteApplications)
                return BadRequest(new { message = $"حداکثر {MaxFavoriteApplications} اپلیکیشن می‌توانید ذخیره کنید." });
            apps.Add(new FavoriteApplication { Id = Guid.NewGuid(), Name = name, Url = uri.AbsoluteUri });
        }

        await _redis.SetAsync(GetFavoriteApplicationsKey(userId), apps);
        return Ok(apps);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteFavoriteApp([FromBody] DeleteFavoriteApplicationRequest request)
    {
        if (!TryGetUserId(out var userId))
            return Forbid();
        var apps = await GetFavoriteAppsAsync(userId);
        if (apps.RemoveAll(app => app.Id == request.Id) == 0)
            return NotFound(new { message = "اپلیکیشن موردنظر پیدا نشد." });
        await _redis.SetAsync(GetFavoriteApplicationsKey(userId), apps);
        return Ok(apps);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Forbid();
        var user = await _usersService.GetAsync(userId);
        if (user is null)
            return Forbid();
        var newPassword = await _usersService.ResetPass(user, cancellationToken);
        TempData["NewPassword"] = newPassword;
        TempData["Success"] = "رمز عبور با موفقیت بازنشانی شد.";
        return RedirectToAction(nameof(Index));
    }

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private async Task<List<FavoriteApplication>> GetFavoriteAppsAsync(int userId) =>
        await _redis.GetAsync<List<FavoriteApplication>>(GetFavoriteApplicationsKey(userId)) ?? [];

    private static string GetFavoriteApplicationsKey(int userId) =>
        $"minoo:user:{userId}:favorite-applications";
}