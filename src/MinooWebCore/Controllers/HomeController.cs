using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinooWebCore.Infrastructure.Services.Contract;
using MinooWebCore.Models;
using MinooWebCore.Services.Redis;

namespace MinooWebCore.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    private static readonly Dictionary<string, string[]> SoftwareGroups = new()
    {
        ["گروه نرم افزاری گزارشات مبتنی بر وب"] = ["گزارشات"],
        ["گروه نرم افزاری مالی"] = ["اجرا نگاشت"]
    };

    private readonly IRedisService _redis;
    private readonly IMenuAccessService _menuAccessService;

    public HomeController(IRedisService redis, IMenuAccessService menuAccessService)
    {
        _redis = redis;
        _menuAccessService = menuAccessService;
    }

    public async Task<IActionResult> Index()
    {
        if (!TryGetUserId(out var userId, out var userCode))
            return Forbid();

        var softwareGroups = SoftwareGroups.ToDictionary(
            group => group.Key,
            group => group.Value,
            StringComparer.Ordinal);

        var canRunMapping = await _menuAccessService.HasAccessAsync(
            userCode,
            "RunNegasht",
            HttpContext.RequestAborted);

        if (!canRunMapping)
        {
            const string financeGroup = "گروه نرم افزاری مالی";
            if (softwareGroups.TryGetValue(financeGroup, out var applications))
            {
                softwareGroups[financeGroup] = applications
                    .Where(application => !string.Equals(application, "اجرا نگاشت", StringComparison.Ordinal))
                    .ToArray();
            }
        }

        ViewBag.SoftwareGroups = softwareGroups;
        ViewBag.FavoriteApplications =
            await _redis.GetAsync<List<FavoriteApplication>>(GetFavoriteApplicationsKey(userId))
            ?? [];

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SelectSoftware(string groupName, string softwareName)
    {
        if (string.IsNullOrWhiteSpace(groupName) ||
            !SoftwareGroups.TryGetValue(groupName, out var softwareList) ||
            string.IsNullOrWhiteSpace(softwareName) ||
            !softwareList.Contains(softwareName, StringComparer.Ordinal))
            return BadRequest();

        TempData["SelectedGroup"] = groupName;
        TempData["SelectedSoftware"] = softwareName;
        return RedirectToAction(nameof(Index));
    }

    [AllowAnonymous]
    public IActionResult Error() => View();

    private bool TryGetUserId(out int userId, out string userCode)
    {
        userCode = User.FindFirstValue("UserCode") ?? string.Empty;
        return int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId) && userId > 0 && !string.IsNullOrWhiteSpace(userCode);
    }

    private static string GetFavoriteApplicationsKey(int userId) =>
        $"minoo:user:{userId}:favorite-applications";
}
