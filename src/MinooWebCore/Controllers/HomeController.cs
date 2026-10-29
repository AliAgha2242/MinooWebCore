using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MinooWebCore.Controllers;

[Authorize]
public sealed class HomeController : Controller
{
    // Temporary sample data. Software entries can later come from a database or API.
    private static readonly Dictionary<string, string[]> SoftwareGroups = new()
    {
        ["گروه نرم افزاری اطلاعات پایه"] = new[] { "برنامه یک", "برنامه دو", "برنامه سه" },
        ["گروه نرم افزاری انبار و برنامه ریزی"] = new[] { "برنامه یک", "برنامه دو", "برنامه سه" },
        ["گروه نرم افزاری گزارشات مبتنی بر وب"] = new[] { "برنامه یک", "برنامه دو", "برنامه سه" },
        ["گروه نرم افزاری مالی"] = new[] { "برنامه یک", "برنامه دو", "برنامه سه" },
        ["گروه نرم افزاری توسعه دهنگان"] = new[] { "برنامه یک", "برنامه دو", "برنامه سه" }
    };

    public IActionResult Index()
    {
        ViewBag.SoftwareGroups = SoftwareGroups;
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
        {
            return BadRequest();
        }

        TempData["SelectedGroup"] = groupName;
        TempData["SelectedSoftware"] = softwareName;
        return RedirectToAction(nameof(Index));
    }

    [AllowAnonymous]
    public IActionResult Error() => View();
}
