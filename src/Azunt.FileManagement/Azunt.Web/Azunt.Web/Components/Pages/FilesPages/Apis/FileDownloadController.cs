using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using Azunt.FileManagement;
using System.Threading.Tasks;
using System.Linq;
using System;

namespace Azunt.Apis.Files;

//[Authorize(Roles = "Administrators")]
[Route("api/[controller]")]
[ApiController]
public class FileDownloadController : ControllerBase
{
    private readonly IFileRepository _repository;

    public FileDownloadController(IFileRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// 파일업로드 리스트 엑셀 다운로드
    /// GET /api/FileDownload/ExcelDown
    /// </summary>
    [HttpGet("ExcelDown")]
    public async Task<IActionResult> ExcelDown()
    {
        var items = await _repository.GetAllAsync();

        if (!items.Any())
        {
            return NotFound("No file records found.");
        }

        using var package = new ExcelPackage();
        var sheet = package.Workbook.Worksheets.Add("Files");

        // 데이터 바인딩
        var range = sheet.Cells["B2"].LoadFromCollection(
            items.Select(m => new
            {
                m.Id,
                m.Name,
                Created = m.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
                m.Active,
                m.CreatedBy
            }),
            PrintHeaders: true
        );

        // 스타일 설정
        var header = sheet.Cells["B2:F2"];
        sheet.DefaultColWidth = 22;
        range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
        range.Style.Fill.PatternType = ExcelFillStyle.Solid;
        range.Style.Fill.BackgroundColor.SetColor(Color.WhiteSmoke);
        range.Style.Border.BorderAround(ExcelBorderStyle.Medium);

        header.Style.Font.Bold = true;
        header.Style.Font.Color.SetColor(Color.White);
        header.Style.Fill.BackgroundColor.SetColor(Color.DarkBlue);

        // 조건부 서식 (예: Active 컬럼 색상 강조)
        var activeCol = range.Offset(1, 3, items.Count(), 1); // Active = 4th column (0-indexed)
        var rule = activeCol.ConditionalFormatting.AddThreeColorScale();
        rule.LowValue.Color = Color.Red;
        rule.MiddleValue.Color = Color.White;
        rule.HighValue.Color = Color.Green;

        // 파일 다운로드 반환
        var content = package.GetAsByteArray();
        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"{DateTime.Now:yyyyMMddHHmmss}_Files.xlsx");
    }
}