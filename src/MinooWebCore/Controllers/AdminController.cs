using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MinooWebCore.Data;
using MinooWebCore.Data.Models;

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

    public AdminController(
        IGenericRepository<ReportDefinition> reportRepository,
        IGenericRepository<ReportParameterDefinition> parameterRepository)
    {
        _reportRepository = reportRepository;
        _parameterRepository = parameterRepository;
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
    public IActionResult Index(string section = CreateReportSection)
    {
        PrepareAdminView(CreateReportSection);
        ViewBag.IsEdit = false;
        return View(new CreateReportInput());
    }

    [HttpGet]
    public async Task<IActionResult> ManageReports(CancellationToken cancellationToken)
    {
        PrepareAdminView(ManageReportsSection);
        var reports = await _reportRepository.FindAsync(x => !x.IsDeleted, cancellationToken);
        return View("Reports", reports.OrderByDescending(x => x.CreatedAtUtc).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> CreateReport(CreateReportInput input, CancellationToken cancellationToken)
    {
        PrepareAdminView(CreateReportSection);
        ViewBag.IsEdit = false;

        if (!ValidateInput(input, allowExistingExcel: false))
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
            CreatedAtUtc = DateTime.UtcNow
        };

        await _reportRepository.AddAsync(report, cancellationToken);
        AddParameterEntities(report.Id, input.Parameters);
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

        PrepareAdminView(CreateReportSection);
        ViewBag.IsEdit = true;
        return View("Index", input);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<IActionResult> UpdateReport(CreateReportInput input, CancellationToken cancellationToken)
    {
        PrepareAdminView(CreateReportSection);
        ViewBag.IsEdit = true;

        var report = (await _reportRepository.FindAsync(
            x => x.Id == input.ReportId && !x.IsDeleted, cancellationToken)).FirstOrDefault();
        if (report is null)
            return NotFound();

        if (!ValidateInput(input, allowExistingExcel: true, hasExistingExcel: report.ExcelContent is { Length: > 0 }))
        {
            input.ExistingExcelFileName = report.ExcelFileName;
            return View("Index", input);
        }

        report.Name = input.ReportName.Trim();
        report.SourceType = input.Source;
        report.SqlQuery = input.Source == "Sql" ? input.SqlQuery.Trim() : null;
        report.NeedsPagination = input.NeedsPagination;
        report.PageSize = input.NeedsPagination ? input.PageSize : null;
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

        AddParameterEntities(report.Id, input.Parameters);
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
        if (string.IsNullOrWhiteSpace(input.ReportName))
            ModelState.AddModelError(nameof(input.ReportName), "نام گزارش را وارد کنید.");

        if (input.Source is not ("Excel" or "Sql"))
            ModelState.AddModelError(nameof(input.Source), "منبع گزارش را انتخاب کنید.");

        if (input.NeedsPagination && (!input.PageSize.HasValue || input.PageSize.Value <= 0))
            ModelState.AddModelError(nameof(input.PageSize), "تعداد رکورد در هر صفحه باید عددی بزرگ‌تر از صفر باشد.");

        if (input.Source == "Sql" && string.IsNullOrWhiteSpace(input.SqlQuery))
            ModelState.AddModelError(nameof(input.SqlQuery), "Query گزارش را وارد کنید.");

        if (input.Source == "Excel")
        {
            if (input.ExcelFile is null || input.ExcelFile.Length == 0)
            {
                if (!allowExistingExcel || !hasExistingExcel)
                    ModelState.AddModelError(nameof(input.ExcelFile), "فایل اکسل را انتخاب کنید.");
            }
            else if (!Path.GetExtension(input.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(nameof(input.ExcelFile), "فعلاً فقط فایل‌های Excel با پسوند .xlsx پشتیبانی می‌شوند.");
            }
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

        var allowedParameterTypes = new[] { "Text", "Number", "Date", "Checkbox", "RadioButton", "ListBox" };
        var parameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < input.Parameters.Count; index++)
        {
            var parameter = input.Parameters[index];
            if (string.IsNullOrWhiteSpace(parameter.Name) ||
                !Regex.IsMatch(parameter.Name, "^[A-Za-z_][A-Za-z0-9_]*$"))
                ModelState.AddModelError($"Parameters[{index}].Name", "نام فنی باید با حرف یا _ شروع شود و فقط شامل حروف انگلیسی، عدد و _ باشد.");
            else if (!parameterNames.Add(parameter.Name.Trim()))
                ModelState.AddModelError($"Parameters[{index}].Name", "نام فنی پارامترها باید یکتا باشد.");

            if (string.IsNullOrWhiteSpace(parameter.DisplayName))
                ModelState.AddModelError($"Parameters[{index}].DisplayName", "عنوان پارامتر الزامی است.");
            if (!allowedParameterTypes.Contains(parameter.Type))
                ModelState.AddModelError($"Parameters[{index}].Type", "نوع پارامتر معتبر نیست.");

            if (parameter.Type == "ListBox")
            {
                if (string.IsNullOrWhiteSpace(parameter.DataSource))
                    ModelState.AddModelError($"Parameters[{index}].DataSource", "Query منبع لیست‌باکس الزامی است.");
                if (string.IsNullOrWhiteSpace(parameter.DisplayNameField))
                    ModelState.AddModelError($"Parameters[{index}].DisplayNameField", "نام ستون نمایشی الزامی است.");
                if (string.IsNullOrWhiteSpace(parameter.ValueField))
                    ModelState.AddModelError($"Parameters[{index}].ValueField", "نام ستون مقدار الزامی است.");
            }
            if (parameter.Type == "RadioButton" && string.IsNullOrWhiteSpace(parameter.Options))
                ModelState.AddModelError($"Parameters[{index}].Options", "گزینه‌های رادیوباتن را وارد کنید.");
        }

        return ModelState.IsValid;
    }

    private async void AddParameterEntities(int reportId, IReadOnlyList<ReportParameterInput> parameters)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            var item = parameters[index];
            await _parameterRepository.AddAsync(new ReportParameterDefinition
            {
                ReportDefinitionId = reportId,
                Name = item.Name.Trim(),
                DisplayName = item.DisplayName.Trim(),
                Type = item.Type,
                DataSource = item.Type == "ListBox" ? item.DataSource : null,
                DisplayNameField = item.Type == "ListBox" ? item.DisplayNameField : null,
                ValueField = item.Type == "ListBox" ? item.ValueField : null,
                Options = item.Type == "RadioButton" ? item.Options : null,
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
        public string DataSource { get; set; } = string.Empty;
        public string DisplayNameField { get; set; } = string.Empty;
        public string ValueField { get; set; } = string.Empty;
        public string Options { get; set; } = string.Empty;
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

    public sealed class CreateReportInput
    {
        public string ReportName { get; set; } = string.Empty;
        public string Source { get; set; } = "Excel";
        public bool NeedsPagination { get; set; }
        public int? PageSize { get; set; }
        public IFormFile? ExcelFile { get; set; }
        public string SqlQuery { get; set; } = string.Empty;
        public List<ReportParameterInput> Parameters { get; set; } = [];

    }

    public sealed class ReportParameterInput
    {
        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string Type { get; set; } = "Text";
        public string DataSource { get; set; } = string.Empty;
        public string DisplayNameField { get; set; } = string.Empty;
        public string ValueField { get; set; } = string.Empty;
        public string Options { get; set; } = string.Empty;
    }
}
