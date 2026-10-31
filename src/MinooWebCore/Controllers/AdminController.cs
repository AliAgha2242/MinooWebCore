using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MinooWebCore.Controllers;

[Authorize]
[Route("Admin")]
public sealed class AdminController : Controller
{
    private static readonly (string Key, string Title, string Description, string Icon)[] MenuItems =
    [
        ("dashboard", "نمای کلی", "خلاصه وضعیت پنل مدیریت", "▦"),
        ("users", "مدیریت کاربران", "کاربران و وضعیت حساب‌ها", "♙"),
        ("software", "گروه‌ها و نرم‌افزارها", "مدیریت گروه‌ها و برنامه‌های قابل نمایش", "▤"),
        ("access", "نقش‌ها و دسترسی‌ها", "تنظیم دسترسی کاربران به بخش‌ها", "⚿"),
        ("database", "پایگاه داده", "تنظیمات و ابزارهای داده", "◈"),
        ("reports", "گزارش‌ها و رویدادها", "گزارش فعالیت‌ها و رخدادهای سامانه", "▥"),
        ("settings", "تنظیمات سامانه", "تنظیمات عمومی Minoo Web Core", "⚙")
    ];

    [HttpGet("")]
    public IActionResult Index(string section = "dashboard")
    {
        var selected = MenuItems.FirstOrDefault(item => item.Key == section);
        if (selected == default)
            selected = MenuItems[0];

        ViewBag.MenuItems = MenuItems;
        ViewBag.SelectedSection = selected.Key;
        ViewBag.SelectedTitle = selected.Title;
        ViewBag.SelectedDescription = selected.Description;
        return View();
    }
}
