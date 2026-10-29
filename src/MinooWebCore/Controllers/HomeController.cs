using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MinooWebCore.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    // Temporary in-code list; this can later come from a database or API.
    private static readonly string[] SoftwareGroups =
    {
        "گروه نرم افزاری اطلاعات پایه",
        "گروه نرم افزاری انبار و برنامه ریزی",
        "گروه نرم افزاری گزارشات مبتنی بر وب",
        "گروه نرم افزاری مالی",
        "گروه نرم افزاری توسعه دهنگان"
    };

    public IActionResult Index()
    {
        ViewBag.SoftwareGroups = SoftwareGroups;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult SelectGroup(string groupName)
    {
        if (string.IsNullOrWhiteSpace(groupName) ||
            !SoftwareGroups.Contains(groupName, StringComparer.Ordinal))
        {
            return BadRequest();
        }

        TempData["SelectedGroup"] = groupName;
        return RedirectToAction(nameof(Index));
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
