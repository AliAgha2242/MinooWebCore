using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinooWebCore.Data;
using MinooWebCore.Data.Models;
using MinooWebCore.Persistence.StaticConfiguration;

namespace MinooWebCore.Controllers;

[Authorize]
public sealed class AdminController : Controller
{
    private const string CreateReportSection = "create-report";
    private const string ManageReportsSection = "manage-reports";

    private static readonly (string Key, string Title, string Description, string Icon)[] MenuItems =
    [
        (CreateReportSection, "ایجاد گزارش", "تعریف گزارش جدید و انتخاب منبع داده", "▥"),
        (ManageReportsSection, "مدیریت گزارش‌ها", "مشاهده، ویرایش و حذف گزارش‌های ثبت‌شده", "☷")
    ];

    private readonly IGenericRepository<ReportDefinition> _reportRepository;
    private readonly IGenericRepository<ReportParameterDefinition> _parameterRepository;
    private readonly IGenericRepository<SoftwareGroup> _softwareGroupRepository;
    private readonly IGenericRepository<ReportLevel> _reportLevelRepository;
    private readonly IGenericRepository<ReportDefinitionServer> _reportServerRepository;
    private readonly IMinooConfiguration _minooConfiguration;

    public AdminController(
        IGenericRepository<ReportDefinition> reportRepository,
        IGenericRepository<ReportParameterDefinition> parameterRepository,
        IGenericRepository<SoftwareGroup> softwareGroupRepository,
        IGenericRepository<ReportLevel> reportLevelRepository,
        IGenericRepository<ReportDefinitionServer> reportServerRepository,
        IMinooConfiguration minooConfiguration)
    {
        _reportRepository = reportRepository;
        _parameterRepository = parameterRepository;
        _softwareGroupRepository = softwareGroupRepository;
        _reportLevelRepository = reportLevelRepository;
        _reportServerRepository = reportServerRepository;
        _minooConfiguration = minooConfiguration;
    }

    private async Task PrepareReportFormAsync(CancellationToken cancellationToken)
    {
        PrepareAdminView(CreateReportSection);
        ViewBag.SoftwareGroups = (await _softwareGroupRepository.FindAsync(x => x.IsActive, cancellationToken))
            .OrderBy(x => x.Name).ToList();
        ViewBag.ReportLevels = (await _reportLevelRepository.FindAsync(x => x.IsActive, cancellationToken))
            .OrderBy(x => x.Id).ToList();

        try
        {
            ViewBag.AvailableServers = _minooConfiguration.GetAllSerevr()
                .OrderBy(x => x.ServerName).ThenBy(x => x.DatabaseName).ToList();
        }
        catch
        {
            ViewBag.AvailableServers = new List<ServerInfo>();
            ViewBag.ServerConfigurationError = "دریافت فهرست سرورها از تنظیمات سامانه امکان‌پذیر نیست.";
        }
    }

    private async Task<bool> ValidateReportMetadataAsync(CreateReportInput input, CancellationToken cancellationToken)
    {
        var groups = await _softwareGroupRepository.FindAsync(
            x => x.Id == input.SoftwareGroupId && x.IsActive, cancellationToken);
        if (groups.Count == 0)
            ModelState.AddModelError(nameof(input.SoftwareGroupId), "گروه نرم‌افزاری معتبر را انتخاب کنید.");

        var levels = await _reportLevelRepository.FindAsync(
            x => x.Id == input.ReportLevelId && x.IsActive, cancellationToken);
        if (levels.Count == 0)
            ModelState.AddModelError(nameof(input.ReportLevelId), "سطح گزارش معتبر را انتخاب کنید.");

        input.SelectedServerVahedeTejariSNs ??= [];
        if (!string.Equals(input.Source, "Sql", StringComparison.OrdinalIgnoreCase) || input.UseDefaultServer)
        {
            input.UseDefaultServer = true;
            input.SelectedServerVahedeTejariSNs.Clear();
        }
        else
        {
            input.SelectedServerVahedeTejariSNs = input.SelectedServerVahedeTejariSNs
                .Distinct()
                .ToList();

            if (input.SelectedServerVahedeTejariSNs.Count == 0)
            {
                ModelState.AddModelError(nameof(input.SelectedServerVahedeTejariSNs), "برای اجرای چندسروره، حداقل یک سرور انتخاب کنید.");
            }
            else
            {
                try
                {
                    var configuredServerIds = _minooConfiguration.GetAllSerevr()
                        .Select(x => x.VahedeTejariSN)
                        .ToHashSet();
                    if (input.SelectedServerVahedeTejariSNs.Any(id => !configuredServerIds.Contains(id)))
                        ModelState.AddModelError(nameof(input.SelectedServerVahedeTejariSNs), "یک یا چند سرور انتخاب‌شده در تنظیمات سامانه وجود ندارد.");
                }
                catch
                {
                    ModelState.AddModelError(nameof(input.SelectedServerVahedeTejariSNs), "فهرست سرورها در دسترس نیست؛ لطفاً تنظیمات سرورها را بررسی کنید.");
                }
            }
        }

        return ModelState.IsValid;
    }

    private void PrepareAdminView(string section = CreateReportSection)
    {
        var selected = MenuItems.FirstOrDefault(x => x.Key == section);
        if (string.IsNullOrEmpty(selected.Key))
            selected = MenuItems[0];

        ViewBag.MenuItems = MenuItems;
        ViewBag.SelectedSection = selected.Key;
        ViewBag.SelectedTitle = selected.Title;
        ViewBag.SelectedDescription = selected.Description;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string section = CreateReportSection, CancellationToken cancellationToken = default)
    {
        await PrepareReportFormAsync(cancellationToken);
        ViewBag.IsEdit = false;
        return View(new CreateReportInput());
    }

    [HttpGet]
    public async Task<IActionResult> ManageReports(CancellationToken cancellationToken)
    {
        PrepareAdminView(ManageReportsSection);
        var reports = await _reportRepository.FindAsync(x => !x.IsDeleted, cancellationToken);
        ViewBag.SoftwareGroupNames = (await _softwareGroupRepository.GetAllAsync(cancellationToken))
            .ToDictionary(x => x.Id, x => x.Name);
        ViewBag.ReportLevelNames = (await _reportLevelRepository.GetAllAsync(cancellationToken))
            .ToDictionary(x => x.Id, x => x.Name);
        try
        {
            var serverLookup = _minooConfiguration.GetAllSerevr()
                .GroupBy(x => x.VahedeTejariSN)
                .ToDictionary(x => x.Key, x => x.First().ServerName + " · " + x.First().DatabaseName);
            ViewBag.ServerNames = serverLookup;

            var reportIds = reports.Select(x => x.Id).ToArray();
            var targets = reportIds.Length == 0
                ? new List<ReportDefinitionServer?>()
                : await _reportServerRepository.FindAsync(x => reportIds.Contains(x.ReportDefinitionId), cancellationToken)
                    ?? new List<ReportDefinitionServer?>();
            ViewBag.ReportServerNames = targets
                .Where(x => x is not null)
                .GroupBy(x => x!.ReportDefinitionId)
                .ToDictionary(
                    group => group.Key,
                    group => string.Join("، ", group.Select(target =>
                        serverLookup.TryGetValue(target!.VahedeTejariSN, out var name)
                            ? name
                            : target.VahedeTejariSN.ToString(System.Globalization.CultureInfo.InvariantCulture))));
        }
        catch
        {
            ViewBag.ServerNames = new Dictionary<decimal, string>();
            ViewBag.ReportServerNames = new Dictionary<int, string>();
        }
        return View("Reports", reports.OrderByDescending(x => x.CreatedAtUtc).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> CreateReport(CreateReportInput input, CancellationToken cancellationToken)
    {
        await PrepareReportFormAsync(cancellationToken);
        ViewBag.IsEdit = false;

        if (!await ValidateReportMetadataAsync(input, cancellationToken) ||
            !ValidateInput(input, allowExistingExcel: false))
            return View("Index", input);

        byte[]? excelContent = null;
        string? excelFileName = null;
        if (input.Source == "Excel")
        {
            await using var stream = new MemoryStream();
            await input.ExcelFile!.CopyToAsync(stream, cancellationToken);
            excelContent = stream.ToArray();
            excelFileName = Path.GetFileName(input.ExcelFile.FileName);
        }

        var report = new ReportDefinition
        {
            Name = input.ReportName.Trim(),
            SourceType = input.Source,
            SqlQuery = input.Source == "Sql" ? input.SqlQuery.Trim() : null,
            ExcelFileName = excelFileName,
            ExcelContent = excelContent,
            NeedsPagination = input.NeedsPagination,
            PageSize = input.NeedsPagination ? input.PageSize : null,
            SoftwareGroupId = input.SoftwareGroupId,
            ReportLevelId = input.ReportLevelId,
            UseDefaultServer = input.UseDefaultServer,
            ServerTargets = input.SelectedServerVahedeTejariSNs.Select(id => new ReportDefinitionServer { VahedeTejariSN = id }).ToList(),
            CreatedAtUtc = DateTime.UtcNow
        };

        AddParameterEntities(report, input.Parameters);
        await _reportRepository.AddAsync(report, cancellationToken);
        try
        {
            await _reportRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(input.ReportName), "گزارشی با این نام قبلاً ثبت شده است یا ذخیره‌سازی با خطا مواجه شد.");
            return View("Index", input);
        }

        TempData["AdminSuccessMessage"] = "گزارش با موفقیت ذخیره شد.";
        return RedirectToAction(nameof(ManageReports));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var report = (await _reportRepository.FindAsync(x => x.Id == id && !x.IsDeleted, cancellationToken)).FirstOrDefault();
        if (report is null)
            return NotFound();

        var parameters = await _parameterRepository.FindAsync(x => x.ReportDefinitionId == id, cancellationToken);
        var input = new CreateReportInput
        {
            ReportId = report.Id,
            ReportName = report.Name,
            Source = report.SourceType,
            NeedsPagination = report.NeedsPagination,
            PageSize = report.PageSize,
            SoftwareGroupId = report.SoftwareGroupId,
            ReportLevelId = report.ReportLevelId,
            UseDefaultServer = report.UseDefaultServer,
            SelectedServerVahedeTejariSNs = (await _reportServerRepository.FindAsync(x => x.ReportDefinitionId == id, cancellationToken) ?? [])
                .Where(x => x is not null).Select(x => x!.VahedeTejariSN).ToList(),
            SqlQuery = report.SqlQuery ?? string.Empty,
            ExistingExcelFileName = report.ExcelFileName,
            Parameters = parameters.OrderBy(x => x.SortOrder).Select(x => new ReportParameterInput
            {
                Name = x.Name,
                DisplayName = x.DisplayName,
                Type = x.Type,
                DataSource = x.DataSource ?? string.Empty,
                DisplayNameField = x.DisplayNameField ?? string.Empty,
                ValueField = x.ValueField ?? string.Empty,
                Options = x.Options ?? string.Empty
            }).ToList()
        };

        await PrepareReportFormAsync(cancellationToken);
        ViewBag.IsEdit = true;
        return View("Index", input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(21 * 1024 * 1024)]
    public async Task<IActionResult> UpdateReport(CreateReportInput input, CancellationToken cancellationToken)
    {
        await PrepareReportFormAsync(cancellationToken);
        ViewBag.IsEdit = true;

        var report = (await _reportRepository.FindAsync(
            x => x.Id == input.ReportId && !x.IsDeleted, cancellationToken)).FirstOrDefault();
        if (report is null)
            return NotFound();

        if (!await ValidateReportMetadataAsync(input, cancellationToken) ||
            !ValidateInput(input, allowExistingExcel: true, hasExistingExcel: report.ExcelContent is { Length: > 0 }))
        {
            input.ExistingExcelFileName = report.ExcelFileName;
            return View("Index", input);
        }

        report.Name = input.ReportName.Trim();
        report.SourceType = input.Source;
        report.SqlQuery = input.Source == "Sql" ? input.SqlQuery.Trim() : null;
        report.NeedsPagination = input.NeedsPagination;
        report.PageSize = input.NeedsPagination ? input.PageSize : null;
        report.SoftwareGroupId = input.SoftwareGroupId;
        report.ReportLevelId = input.ReportLevelId;
        report.UseDefaultServer = input.UseDefaultServer;
        report.UpdatedAtUtc = DateTime.UtcNow;

        if (input.Source == "Sql")
        {
            report.ExcelFileName = null;
            report.ExcelContent = null;
        }
        else if (input.ExcelFile is not null && input.ExcelFile.Length > 0)
        {
            await using var stream = new MemoryStream();
            await input.ExcelFile.CopyToAsync(stream, cancellationToken);
            report.ExcelContent = stream.ToArray();
            report.ExcelFileName = Path.GetFileName(input.ExcelFile.FileName);
        }

        var oldParameters = await _parameterRepository.FindAsync(
            x => x.ReportDefinitionId == report.Id, cancellationToken);
        foreach (var parameter in oldParameters)
            _parameterRepository.Remove(parameter);

        var oldServerTargets = await _reportServerRepository.FindAsync(
            x => x.ReportDefinitionId == report.Id, cancellationToken);
        foreach (var target in oldServerTargets ?? [])
            if (target is not null)
                _reportServerRepository.Remove(target);

        if (!input.UseDefaultServer)
        {
            foreach (var serverId in input.SelectedServerVahedeTejariSNs.Distinct())
                await _reportServerRepository.AddAsync(new ReportDefinitionServer
                {
                    ReportDefinitionId = report.Id,
                    VahedeTejariSN = serverId
                }, cancellationToken);
        }

        AddParameterEntities(report, input.Parameters);
        _reportRepository.Update(report);

        try
        {
            await _reportRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError(nameof(input.ReportName), "نام گزارش تکراری است یا ذخیره‌سازی با خطا مواجه شد.");
            input.ExistingExcelFileName = report.ExcelFileName;
            return View("Index", input);
        }

        TempData["AdminSuccessMessage"] = "تغییرات گزارش با موفقیت ذخیره شد.";
        return RedirectToAction(nameof(ManageReports));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteReport(int id, CancellationToken cancellationToken)
    {
        var report = (await _reportRepository.FindAsync(
            x => x.Id == id && !x.IsDeleted, cancellationToken)).FirstOrDefault();
        if (report is null)
            return NotFound();

        report.IsDeleted = true;
        report.DeletedAtUtc = DateTime.UtcNow;
        report.UpdatedAtUtc = DateTime.UtcNow;
        _reportRepository.Update(report);
        await _reportRepository.SaveChangesAsync(cancellationToken);

        TempData["AdminSuccessMessage"] = "گزارش حذف شد. اطلاعات آن به‌صورت Soft Delete در دیتابیس باقی مانده است.";
        return RedirectToAction(nameof(ManageReports));
    }

    private bool ValidateInput(CreateReportInput input, bool allowExistingExcel, bool hasExistingExcel = false)
    {
        input.Parameters ??= [];

        if (string.IsNullOrWhiteSpace(input.ReportName))
            ModelState.AddModelError(nameof(input.ReportName), "نام گزارش را وارد کنید.");
        else if (input.ReportName.Trim().Length > 150)
            ModelState.AddModelError(nameof(input.ReportName), "نام گزارش نباید بیشتر از ۱۵۰ کاراکتر باشد.");

        if (input.Source is not ("Excel" or "Sql"))
            ModelState.AddModelError(nameof(input.Source), "منبع گزارش معتبر نیست.");

        if (input.NeedsPagination && (!input.PageSize.HasValue || input.PageSize.Value is < 1 or > 50000))
            ModelState.AddModelError(nameof(input.PageSize), "تعداد رکورد در هر صفحه باید بین ۱ تا ۵۰٬۰۰۰ باشد.");

        if (input.Source == "Sql")
        {
            if (string.IsNullOrWhiteSpace(input.SqlQuery))
                ModelState.AddModelError(nameof(input.SqlQuery), "Query گزارش را وارد کنید.");
            else if (input.SqlQuery.Length > 100000)
                ModelState.AddModelError(nameof(input.SqlQuery), "طول Query نباید بیشتر از ۱۰۰٬۰۰۰ کاراکتر باشد.");
        }

        if (input.Source == "Excel")
        {
            if (input.ExcelFile is null || input.ExcelFile.Length == 0)
            {
                if (!allowExistingExcel || !hasExistingExcel)
                    ModelState.AddModelError(nameof(input.ExcelFile), "فایل اکسل را انتخاب کنید.");
            }
            else if (input.ExcelFile.Length > 20L * 1024 * 1024)
                ModelState.AddModelError(nameof(input.ExcelFile), "حجم فایل اکسل نباید بیشتر از ۲۰ مگابایت باشد.");
            else if (!Path.GetExtension(input.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
                ModelState.AddModelError(nameof(input.ExcelFile), "فقط فایل‌های Excel با پسوند .xlsx پشتیبانی می‌شوند.");
            else
            {
                try
                {
                    var validationError = ValidateExcelFile(input.ExcelFile);
                    if (validationError is not null)
                        ModelState.AddModelError(nameof(input.ExcelFile), validationError);
                }
                catch (InvalidDataException)
                {
                    ModelState.AddModelError(nameof(input.ExcelFile), "فایل انتخاب‌شده یک فایل Excel معتبر نیست یا ساختار آن قابل خواندن نیست.");
                }
                catch (Exception)
                {
                    ModelState.AddModelError(nameof(input.ExcelFile), "هنگام بررسی فایل اکسل خطایی رخ داد.");
                }
            }
        }

        if (input.Parameters.Count > 30)
            ModelState.AddModelError(nameof(input.Parameters), "حداکثر ۳۰ پارامتر برای هر گزارش مجاز است.");

        var allowedParameterTypes = new HashSet<string>(new[] { "Text", "Number", "Date", "Time", "DateTime", "Checkbox", "RadioButton", "ListBox", "ComboBox" }, StringComparer.Ordinal);
        var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < input.Parameters.Count; index++)
        {
            var parameter = input.Parameters[index];
            var prefix = $"Parameters[{index}]";
            parameter.Name ??= string.Empty;
            parameter.DisplayName ??= string.Empty;
            parameter.Type ??= string.Empty;

            if (string.IsNullOrWhiteSpace(parameter.Name) || parameter.Name.Length > 64 ||
                !Regex.IsMatch(parameter.Name, "^[A-Za-z_][A-Za-z0-9_]*$"))
                ModelState.AddModelError($"{prefix}.Name", "نام فنی باید حداکثر ۶۴ کاراکتر و با حرف انگلیسی یا _ شروع شود؛ فقط حروف، عدد و _ مجاز است.");
            else if (!parameterNames.Add(parameter.Name.Trim()))
                ModelState.AddModelError($"{prefix}.Name", "نام فنی پارامترها باید یکتا باشد.");

            if (string.IsNullOrWhiteSpace(parameter.DisplayName) || parameter.DisplayName.Trim().Length > 100)
                ModelState.AddModelError($"{prefix}.DisplayName", "عنوان پارامتر الزامی است و نباید بیشتر از ۱۰۰ کاراکتر باشد.");

            if (!allowedParameterTypes.Contains(parameter.Type))
                ModelState.AddModelError($"{prefix}.Type", "نوع پارامتر معتبر نیست.");

            if (parameter.Type is "Date" or "DateTime")
            {
                if (parameter.CalendarType is not ("Persian" or "Gregorian")) ModelState.AddModelError($"{prefix}.CalendarType", "تقویم تاریخ معتبر نیست.");
                if (parameter.DateFormat is not ("yyyyMMdd" or "yyyy/MM/dd" or "yyyy-MM-dd")) ModelState.AddModelError($"{prefix}.DateFormat", "فرمت تاریخ معتبر نیست.");
            }
            if (parameter.Type is "Time" or "DateTime")
                if (parameter.TimeFormat is not ("HH:mm" or "HH:mm:ss")) ModelState.AddModelError($"{prefix}.TimeFormat", "فرمت ساعت معتبر نیست.");
            if (parameter.Type == "DateTime" && parameter.DateTimeSeparator is not (" " or "T")) ModelState.AddModelError($"{prefix}.DateTimeSeparator", "جداکننده تاریخ و ساعت معتبر نیست.");

            if (parameter.Type is "ListBox" or "ComboBox")
            {
                if (string.IsNullOrWhiteSpace(parameter.DataSource) || parameter.DataSource.Length > 10000)
                    ModelState.AddModelError($"{prefix}.DataSource", "Query منبع لیست‌باکس الزامی است و حداکثر ۱۰٬۰۰۰ کاراکتر می‌تواند باشد.");
                if (!IsValidFieldIdentifier(parameter.DisplayNameField))
                    ModelState.AddModelError($"{prefix}.DisplayNameField", "نام ستون نمایشی معتبر نیست.");
                if (!IsValidFieldIdentifier(parameter.ValueField))
                    ModelState.AddModelError($"{prefix}.ValueField", "نام ستون مقدار معتبر نیست.");
            }

            if (parameter.Type == "RadioButton")
            {
                var options = (parameter.Options ?? string.Empty)
                    .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (options.Length == 0)
                    ModelState.AddModelError($"{prefix}.Options", "حداقل یک گزینه برای رادیوباتن وارد کنید.");
                else if (options.Length > 50 || options.Any(option => option.Length > 100) ||
                         options.Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Length)
                    ModelState.AddModelError($"{prefix}.Options", "گزینه‌ها باید یکتا باشند؛ حداکثر ۵۰ گزینه و هر گزینه حداکثر ۱۰۰ کاراکتر.");
            }
        }

        return ModelState.IsValid;
    }

    private static bool IsValidFieldIdentifier(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= 128 &&
        Regex.IsMatch(value, "^[A-Za-z_][A-Za-z0-9_]*$");

    private static void AddParameterEntities(ReportDefinition report, IReadOnlyList<ReportParameterInput> parameters)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            var item = parameters[index];
            report.Parameters.Add(new ReportParameterDefinition
            {
                Name = item.Name.Trim(),
                DisplayName = item.DisplayName.Trim(),
                Type = item.Type,
                ParameterTypeId = item.Type switch { "Text" => 1, "Number" => 2, "Date" => 3, "ListBox" => 5, "RadioButton" => 6, "ComboBox" => 7, "Checkbox" => 8, "Time" => 9, "DateTime" => 10, _ => 1 },
                CalendarType = item.Type is "Date" or "DateTime" ? item.CalendarType : null,
                DateFormat = item.Type is "Date" or "DateTime" ? item.DateFormat : null,
                TimeFormat = item.Type is "Time" or "DateTime" ? item.TimeFormat : null,
                DateTimeSeparator = item.Type == "DateTime" ? item.DateTimeSeparator : null,
                DataSource = item.Type is "ListBox" or "ComboBox" ? item.DataSource?.Trim() : null,
                DisplayNameField = item.Type is "ListBox" or "ComboBox" ? item.DisplayNameField?.Trim() : null,
                ValueField = item.Type is "ListBox" or "ComboBox" ? item.ValueField?.Trim() : null,
                Options = item.Type == "RadioButton" ? item.Options?.Trim() : null,
                SortOrder = index
            });
        }
    }

    public sealed class CreateReportInput
    {
        public int? ReportId { get; set; }
        public string ReportName { get; set; } = string.Empty;
        public string Source { get; set; } = "Excel";
        public bool NeedsPagination { get; set; }
        public int? PageSize { get; set; }
        public int SoftwareGroupId { get; set; }
        public int ReportLevelId { get; set; }
        public bool UseDefaultServer { get; set; } = true;
        public List<decimal> SelectedServerVahedeTejariSNs { get; set; } = [];
        public IFormFile? ExcelFile { get; set; }
        public string ExistingExcelFileName { get; set; } = string.Empty;
        public string SqlQuery { get; set; } = string.Empty;
        public List<ReportParameterInput> Parameters { get; set; } = [];
    }

    public sealed class ReportParameterInput
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = "Text";

        // These fields are required only for their matching parameter type.
        // Nullable declarations prevent MVC's implicit [Required] validation
        // from rejecting parameters that do not use these fields.
        public string? DataSource { get; set; }
        public string? DisplayNameField { get; set; }
        public string? ValueField { get; set; }
        public string? Options { get; set; }
        public string CalendarType { get; set; } = "Gregorian";
        public string DateFormat { get; set; } = "yyyy-MM-dd";
        public string TimeFormat { get; set; } = "HH:mm:ss";
        public string? DateTimeSeparator { get; set; }
    }

    private static string? ValidateExcelFile(IFormFile file)
    {
        using var stream = file.OpenReadStream();
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);

        var workbookEntry = archive.GetEntry("xl/workbook.xml")
            ?? throw new InvalidDataException("Workbook not found.");

        XDocument workbook;
        using (var workbookStream = workbookEntry.Open())
            workbook = XDocument.Load(workbookStream);

        XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";

        var sheets = workbook.Root?.Element(main + "sheets")?.Elements(main + "sheet").ToList() ?? [];
        if (sheets.Count != 1)
            return "فایل اکسل باید دقیقاً یک شیت داشته باشد.";

        var relationshipId = (string?)sheets[0].Attribute(relationships + "id");
        if (string.IsNullOrWhiteSpace(relationshipId))
            return "اطلاعات شیت فایل اکسل معتبر نیست.";

        var relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels")
            ?? throw new InvalidDataException("Workbook relationships not found.");

        XDocument relationshipsDocument;
        using (var relationshipsStream = relationshipsEntry.Open())
            relationshipsDocument = XDocument.Load(relationshipsStream);

        var target = relationshipsDocument.Root?
            .Elements(packageRelationships + "Relationship")
            .FirstOrDefault(element => (string?)element.Attribute("Id") == relationshipId)?
            .Attribute("Target")?.Value;

        if (string.IsNullOrWhiteSpace(target))
            return "مسیر شیت داخل فایل اکسل پیدا نشد.";

        var worksheetPath = NormalizeWorksheetPath(target);
        var worksheetEntry = archive.GetEntry(worksheetPath)
            ?? throw new InvalidDataException("Worksheet not found.");

        XDocument worksheet;
        using (var worksheetStream = worksheetEntry.Open())
            worksheet = XDocument.Load(worksheetStream);

        var rows = worksheet.Root?
            .Element(main + "sheetData")?
            .Elements(main + "row")
            .ToList() ?? [];

        var firstRow = rows.FirstOrDefault();
        if (firstRow is null)
            return "شیت اکسل خالی است و ردیف عنوان ستون‌ها وجود ندارد.";

        var sharedStrings = ReadSharedStrings(archive, main);
        var cells = firstRow.Elements(main + "c").ToList();
        if (cells.Count == 0)
            return "ردیف اول اکسل باید شامل عنوان ستون‌ها باشد.";

        var headers = new SortedDictionary<int, string>();
        foreach (var cell in cells)
        {
            var reference = (string?)cell.Attribute("r");
            var columnIndex = GetColumnIndex(reference);
            if (columnIndex <= 0)
                continue;

            var cellType = (string?)cell.Attribute("t");
            var value = cellType switch
            {
                "inlineStr" => string.Concat(cell.Element(main + "is")?.Descendants(main + "t").Select(node => node.Value) ?? []),
                "s" => ReadSharedString(cell, main, sharedStrings),
                _ => cell.Element(main + "v")?.Value ?? string.Empty
            };

            headers[columnIndex] = value.Trim();
        }

        var lastColumn = rows
            .SelectMany(row => row.Elements(main + "c"))
            .Where(cell => cell.Element(main + "v") is not null
                || cell.Element(main + "is") is not null
                || cell.Element(main + "f") is not null)
            .Select(cell => GetColumnIndex((string?)cell.Attribute("r")))
            .DefaultIfEmpty(0)
            .Max();

        if (lastColumn == 0)
            return "ردیف اول اکسل باید شامل عنوان ستون‌ها باشد.";

        for (var column = 1; column <= lastColumn; column++)
        {
            if (!headers.TryGetValue(column, out var header) || string.IsNullOrWhiteSpace(header))
                return "تمام ستون‌های اکسل باید در ردیف اول عنوان داشته باشند.";
        }

        return null;
    }

    private static List<string> ReadSharedStrings(ZipArchive archive, XNamespace main)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
            return [];

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Root?.Elements(main + "si")
            .Select(item => string.Concat(item.Descendants(main + "t").Select(text => text.Value)))
            .ToList() ?? [];
    }

    private static string ReadSharedString(XElement cell, XNamespace main, IReadOnlyList<string> sharedStrings)
    {
        var indexText = cell.Element(main + "v")?.Value;
        return int.TryParse(indexText, out var index) && index >= 0 && index < sharedStrings.Count
            ? sharedStrings[index]
            : string.Empty;
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrWhiteSpace(cellReference))
            return 0;

        var column = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character))
                break;

            column = (column * 26) + (char.ToUpperInvariant(character) - 'A' + 1);
        }

        return column;
    }

    private static string NormalizeWorksheetPath(string target)
    {
        var segments = new List<string>();
        var path = target.Replace('\\', '/');
        if (!path.StartsWith('/'))
            path = "xl/" + path;

        foreach (var segment in path.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "..")
            {
                if (segments.Count > 0)
                    segments.RemoveAt(segments.Count - 1);
            }
            else if (segment != ".")
            {
                segments.Add(segment);
            }
        }

        return string.Join('/', segments);
    }

    //public sealed class CreateReportInput
    //{
    //    public string ReportName { get; set; } = string.Empty;
    //    public string Source { get; set; } = "Excel";
    //    public bool NeedsPagination { get; set; }
    //    public int? PageSize { get; set; }
    //    public IFormFile? ExcelFile { get; set; }
    //    public string SqlQuery { get; set; } = string.Empty;
    //    public List<ReportParameterInput> Parameters { get; set; } = [];

    //}

    //public sealed class ReportParameterInput
    //{
    //    public string Name { get; set; } = string.Empty;
    //    public string DisplayName { get; set; } = string.Empty;
    //    public string Type { get; set; } = "Text";
    //    public string DataSource { get; set; } = string.Empty;
    //    public string DisplayNameField { get; set; } = string.Empty;
    //    public string ValueField { get; set; } = string.Empty;
    //    public string Options { get; set; } = string.Empty;
    //}
}
