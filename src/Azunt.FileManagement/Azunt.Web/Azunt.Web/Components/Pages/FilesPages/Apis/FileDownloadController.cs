using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

using Azunt.FileManagement;

// Open XML SDK
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using SS = DocumentFormat.OpenXml.Spreadsheet;

namespace Azunt.Apis.Files
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileDownloadController : ControllerBase
    {
        private readonly IFileRepository _repository;
        private readonly IFileStorageService _fileStorage;

        public FileDownloadController(IFileRepository repository, IFileStorageService fileStorage)
        {
            _repository = repository;
            _fileStorage = fileStorage;
        }

        /// <summary>
        /// 파일업로드 리스트 엑셀 다운로드 (Open XML SDK)
        /// GET /api/FileDownload/ExcelDown
        /// </summary>
        [HttpGet("ExcelDown")]
        public async Task<IActionResult> ExcelDown()
        {
            var items = await _repository.GetAllAsync();
            if (items is null || !items.Any())
                return NotFound("No file records found.");

            using var stream = new MemoryStream();

            using (var doc = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook, true))
            {
                // Workbook
                var wbPart = doc.AddWorkbookPart();
                wbPart.Workbook = new Workbook();

                // Styles
                var styles = wbPart.AddNewPart<WorkbookStylesPart>();
                styles.Stylesheet = BuildStylesheet();
                styles.Stylesheet.Save();

                // Worksheet
                var wsPart = wbPart.AddNewPart<WorksheetPart>();
                var sheetData = new SheetData();

                // Columns (B~F)
                var columns = new Columns(
                    new SS.Column { Min = 2, Max = 2, Width = 14, CustomWidth = true }, // B: Id
                    new SS.Column { Min = 3, Max = 3, Width = 28, CustomWidth = true }, // C: Name
                    new SS.Column { Min = 4, Max = 4, Width = 24, CustomWidth = true }, // D: Created
                    new SS.Column { Min = 5, Max = 5, Width = 12, CustomWidth = true }, // E: Active
                    new SS.Column { Min = 6, Max = 6, Width = 22, CustomWidth = true }  // F: CreatedBy
                );

                var ws = new Worksheet();
                ws.Append(columns);
                ws.Append(sheetData);

                // Layout: start at B2
                const int startRow = 2;
                const int startCol = 2; // B

                // Header
                var headerRow = new Row { RowIndex = (uint)startRow };
                var headers = new[] { "Id", "Name", "Created", "Active", "CreatedBy" };
                for (int i = 0; i < headers.Length; i++)
                {
                    headerRow.Append(CreateTextCell(ToRef(startCol + i, startRow), headers[i], styleIndex: 2)); // 2: header style
                }
                sheetData.Append(headerRow);

                // Data
                var currentRow = startRow + 1;
                foreach (var m in items)
                {
                    var row = new Row { RowIndex = (uint)currentRow };

                    // B: Id (text로 기록)
                    row.Append(CreateTextCell(ToRef(startCol + 0, currentRow), m.Id.ToString()));

                    // C: Name
                    row.Append(CreateTextCell(ToRef(startCol + 1, currentRow), m.Name ?? string.Empty));

                    // D: Created (로컬, yyyy-MM-dd HH:mm:ss)
                    var createdText = m.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
                    row.Append(CreateTextCell(ToRef(startCol + 2, currentRow), createdText));

                    // E: Active (nullable → bool 변환)
                    row.Append(CreateBooleanCell(ToRef(startCol + 3, currentRow), m.Active ?? false));

                    // F: CreatedBy
                    row.Append(CreateTextCell(ToRef(startCol + 4, currentRow), m.CreatedBy ?? string.Empty));

                    sheetData.Append(row);
                    currentRow++;
                }

                // Conditional Formatting (Active 컬럼: E3:E{lastRow})
                var lastRow = currentRow - 1;
                if (lastRow >= startRow + 1)
                {
                    var sqref = $"{ToRef(startCol + 3, startRow + 1)}:{ToRef(startCol + 3, lastRow)}"; // E3:E{lastRow}
                    var cf = BuildThreeColorScaleConditionalFormatting(sqref);
                    ws.Append(cf);
                }

                wsPart.Worksheet = ws;
                wsPart.Worksheet.Save();

                // Sheets
                var sheets = new Sheets();
                sheets.Append(new Sheet
                {
                    Id = wbPart.GetIdOfPart(wsPart),
                    SheetId = 1U,
                    Name = "Files"
                });
                wbPart.Workbook.Append(sheets);
                wbPart.Workbook.Save();
            }

            var bytes = stream.ToArray();
            var fileName = $"{DateTime.Now:yyyyMMddHHmmss}_Files.xlsx";
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        /// <summary>
        /// 파일 단일 다운로드
        /// GET /api/FileDownload/{fileName}
        /// </summary>
        [HttpGet("{fileName}")]
        public async Task<IActionResult> Download(string fileName)
        {
            try
            {
                var stream = await _fileStorage.DownloadAsync(fileName);
                return File(stream, "application/octet-stream", fileName);
            }
            catch (FileNotFoundException)
            {
                return NotFound($"FileEntity not found: {fileName}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Download error: {ex.Message}");
            }
        }

        // =========================
        // Stylesheet
        // 0: 기본
        // 1: 본문(얇은 테두리)
        // 2: 헤더(흰 글꼴 + 진한 파랑 배경 + 테두리)
        // =========================
        private static Stylesheet BuildStylesheet()
        {
            // Fonts
            var fonts = new Fonts { Count = 2U };
            fonts.Append(new Font(new FontSize { Val = 11 }, new SS.Color { Theme = 1 }, new FontName { Val = "Calibri" })); // 0
            fonts.Append(new Font(new Bold(), new SS.Color { Rgb = "FFFFFFFF" }, new FontSize { Val = 11 }, new FontName { Val = "Calibri" })); // 1

            // Fills
            var fills = new Fills { Count = 4U };
            fills.Append(new Fill(new PatternFill { PatternType = PatternValues.None }));    // 0
            fills.Append(new Fill(new PatternFill { PatternType = PatternValues.Gray125 })); // 1
            fills.Append(new Fill( // Header DarkBlue
                new PatternFill(
                    new ForegroundColor { Rgb = "FF0B3D91" },
                    new BackgroundColor { Indexed = 64U }
                )
                { PatternType = PatternValues.Solid })); // 2
            fills.Append(new Fill( // Body light gray (옵션)
                new PatternFill(
                    new ForegroundColor { Rgb = "FFF5F5F5" },
                    new BackgroundColor { Indexed = 64U }
                )
                { PatternType = PatternValues.Solid })); // 3

            // Borders
            var borders = new Borders { Count = 2U };
            borders.Append(new Border()); // 0: none
            borders.Append(new Border(    // 1: thin
                new LeftBorder { Style = BorderStyleValues.Thin },
                new RightBorder { Style = BorderStyleValues.Thin },
                new TopBorder { Style = BorderStyleValues.Thin },
                new BottomBorder { Style = BorderStyleValues.Thin },
                new DiagonalBorder()
            ));

            // CellFormats
            var cfs = new CellFormats { Count = 3U };

            // 0: 기본
            cfs.Append(new CellFormat());

            // 1: 본문(테두리)
            cfs.Append(new CellFormat
            {
                BorderId = 1U,
                ApplyBorder = true
                // 필요시 FillId=3U로 본문 배경(연회색) 적용 가능
            });

            // 2: 헤더(흰 글꼴 + 파랑 배경 + 테두리)
            cfs.Append(new CellFormat
            {
                FontId = 1U,
                FillId = 2U,
                BorderId = 1U,
                ApplyFont = true,
                ApplyFill = true,
                ApplyBorder = true,
                Alignment = new Alignment { Horizontal = HorizontalAlignmentValues.Left, Vertical = VerticalAlignmentValues.Center }
            });

            return new Stylesheet(fonts, fills, borders, cfs);
        }

        // =========================
        // Conditional Formatting (Three Color Scale on Active column)
        // =========================
        private static ConditionalFormatting BuildThreeColorScaleConditionalFormatting(string sqref)
        {
            // 3색 스케일: Min → Red, Percentile 50 → White, Max → Green
            var rule = new ConditionalFormattingRule
            {
                Type = ConditionalFormatValues.ColorScale,
                Priority = 1
            };

            var colorScale = new ColorScale();

            // ConditionalFormatValueObject (cfvo)
            colorScale.Append(new SS.ConditionalFormatValueObject { Type = SS.ConditionalFormatValueObjectValues.Min });
            colorScale.Append(new SS.ConditionalFormatValueObject { Type = SS.ConditionalFormatValueObjectValues.Percentile, Val = "50" });
            colorScale.Append(new SS.ConditionalFormatValueObject { Type = SS.ConditionalFormatValueObjectValues.Max });

            // 색상: Red, White, Green
            colorScale.Append(new SS.Color { Rgb = "FFFF0000" });
            colorScale.Append(new SS.Color { Rgb = "FFFFFFFF" });
            colorScale.Append(new SS.Color { Rgb = "FF00B050" });

            rule.Append(colorScale);

            var cf = new ConditionalFormatting
            {
                SequenceOfReferences = new ListValue<StringValue> { InnerText = sqref }
            };
            cf.Append(rule);

            return cf;
        }

        // =========================
        // Cell Helpers
        // =========================
        private static Cell CreateTextCell(string cellRef, string text, uint styleIndex = 1) =>
            new Cell
            {
                CellReference = cellRef,
                DataType = CellValues.InlineString,
                StyleIndex = styleIndex,
                InlineString = new InlineString(new SS.Text(text ?? string.Empty))
            };

        private static Cell CreateBooleanCell(string cellRef, bool value, uint styleIndex = 1) =>
            new Cell
            {
                CellReference = cellRef,
                DataType = CellValues.Boolean,
                StyleIndex = styleIndex,
                CellValue = new CellValue(value ? "1" : "0")
            };

        // =========================
        // Address helpers
        // =========================
        private static string ToRef(int colIndex, int rowIndex) => $"{ToColName(colIndex)}{rowIndex}";
        private static string ToColName(int index)
        {
            var dividend = index;
            var columnName = string.Empty;
            while (dividend > 0)
            {
                var modulo = (dividend - 1) % 26;
                columnName = (char)('A' + modulo) + columnName;
                dividend = (dividend - modulo) / 26;
            }
            return columnName;
        }
    }
}
