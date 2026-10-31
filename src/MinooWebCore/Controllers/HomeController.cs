using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MinooWebCore.Models;
using MinooWebCore.Services.Redis;

namespace MinooWebCore.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    private static readonly Dictionary<string, string[]> SoftwareGroups = new()
    {
        ["گروه نرم افزاری گزارشات مبتنی بر وب"] = ["برنامه یک", "برنامه دو", "برنامه سه"],
        ["گروه نرم افزاری مالی"] = ["اجرا نگاشت", "برنامه دو", "برنامه سه"]
    };

    private readonly IRedisService _redis;

    public HomeController(IRedisService redis) => _redis = redis;

    public async Task<IActionResult> Index()
    {
        ViewBag.SoftwareGroups = SoftwareGroups;
        if (!TryGetUserId(out var userId))
            return Forbid();

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

    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out userId);

    private static string GetFavoriteApplicationsKey(int userId) =>
        $"minoo:user:{userId}:favorite-applications";
}