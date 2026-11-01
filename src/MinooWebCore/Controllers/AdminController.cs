using System.IO.Compression;
using System.Xml.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MinooWebCore.Controllers;

[Authorize]
public sealed class AdminController : Controller
{
    private const string CreateReportSection = "create-report";

    private static readonly (string Key, string Title, string Description, string Icon)[] MenuItems =
    [
        (CreateReportSection, "ایجاد گزارش", "تعریف گزارش جدید و انتخاب منبع داده", "▥")
    ];

    [HttpGet]
    public IActionResult Index(string section = CreateReportSection)
    {
        ViewBag.MenuItems = MenuItems;
        ViewBag.SelectedSection = CreateReportSection;
        ViewBag.SelectedTitle = MenuItems[0].Title;
        ViewBag.SelectedDescription = MenuItems[0].Description;
        return View(new CreateReportInput());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public IActionResult CreateReport(CreateReportInput input)
    {
        ViewBag.MenuItems = MenuItems;
        ViewBag.SelectedSection = CreateReportSection;
        ViewBag.SelectedTitle = MenuItems[0].Title;
        ViewBag.SelectedDescription = MenuItems[0].Description;

        if (string.IsNullOrWhiteSpace(input.ReportName))
            ModelState.AddModelError(nameof(input.ReportName), "نام گزارش را وارد کنید.");

        if (input.Source is not ("Excel" or "Sql"))
            ModelState.AddModelError(nameof(input.Source), "منبع گزارش را انتخاب کنید.");

        if (input.Source == "Sql")
            return View("Index", input);

        if (input.ExcelFile is null || input.ExcelFile.Length == 0)
            ModelState.AddModelError(nameof(input.ExcelFile), "فایل اکسل را انتخاب کنید.");
        else if (!Path.GetExtension(input.ExcelFile.FileName).Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
            ModelState.AddModelError(nameof(input.ExcelFile), "فعلاً فقط فایل‌های Excel با پسوند .xlsx پشتیبانی می‌شوند.");

        if (ModelState.IsValid)
        {
            try
            {
                var validationError = ValidateExcelFile(input.ExcelFile!);
                if (validationError is not null)
                    ModelState.AddModelError(nameof(input.ExcelFile), validationError);
                else
                    ViewBag.SuccessMessage = "ساختار فایل اکسل معتبر است و می‌توانید به مرحله ثبت گزارش بروید.";
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

        return View("Index", input);
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
        public IFormFile? ExcelFile { get; set; }
    }
}
