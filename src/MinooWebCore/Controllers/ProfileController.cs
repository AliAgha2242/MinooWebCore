using System.Security.Claims;
using MinooWebCore.Infrastructure.Services.Contract;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MinooWebCore.Controllers;

[Authorize]
public sealed class ProfileController : Controller
{
    private readonly IUsersService _usersService;

    public ProfileController(IUsersService usersService) => _usersService = usersService;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Forbid();

        var user = await _usersService.GetAsync(userId);
        if (user is null)
            return Forbid();

        ViewBag.UserName = user.UserName;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Forbid();

        var user = await _usersService.GetAsync(userId);
        if (user is null)
            return Forbid();

        var newPassword = await _usersService.ResetPass(user, cancellationToken);
        TempData["NewPassword"] = newPassword;
        TempData["Success"] = "رمز عبور با موفقیت بازنشانی شد.";
        return RedirectToAction(nameof(Index));
    }
}