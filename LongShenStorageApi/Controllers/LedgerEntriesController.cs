using Microsoft.AspNetCore.Mvc;
using LongShenStorageApi.Data;
using LongShenStorageApi.Models;

namespace LongShenStorageApi.Controllers;

/// <summary>
/// 台账查询与报表
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class LedgerEntriesController : ControllerBase
{
    private readonly SqlServerRepository _repo;

    public LedgerEntriesController(SqlServerRepository repo) => _repo = repo;

    /// <summary>获取所有台账（最近100条）</summary>
    [HttpGet]
    public ActionResult<List<LedgerEntry>> GetAll()
    {
        return Ok(_repo.LoadLedger().Take(100).ToList());
    }

    /// <summary>查询台账（支持多条件筛选）</summary>
    [HttpPost("query")]
    public ActionResult<List<LedgerEntry>> Query([FromBody] LedgerQueryRequest query)
    {
        var all = _repo.LoadLedger();
        var filtered = all.AsEnumerable();

        if (query.Type.HasValue)
            filtered = filtered.Where(e => e.Type == query.Type.Value);
        if (!string.IsNullOrWhiteSpace(query.PalletNumber))
            filtered = filtered.Where(e => e.PalletNumber.Contains(query.PalletNumber, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query.WorkOrder))
            filtered = filtered.Where(e => e.WorkOrder.Contains(query.WorkOrder, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(query.OperatorName))
            filtered = filtered.Where(e => e.OperatorName.Contains(query.OperatorName, StringComparison.OrdinalIgnoreCase));
        if (query.StartTime.HasValue)
            filtered = filtered.Where(e => e.Timestamp >= query.StartTime.Value);
        if (query.EndTime.HasValue)
            filtered = filtered.Where(e => e.Timestamp <= query.EndTime.Value);

        return Ok(filtered.OrderByDescending(e => e.Timestamp).ToList());
    }

    /// <summary>获取报表CSV内容</summary>
    [HttpPost("report")]
    public ActionResult<object> GetReport([FromBody] ReportRequest request)
    {
        var state = _repo.Load();
        var csvLines = request.ReportType switch
        {
            "库存清单" => BuildInventoryReport(state),
            "操作记录" => BuildOperationReport(state),
            _ => BuildLedgerReport(state)
        };
        return Ok(new { csvLines });
    }

    private static List<string> BuildLedgerReport(AppState state)
    {
        var lines = new List<string> { "时间,类型,操作人员,托盘号,工装号,项目号,型号,工单号,电解槽编号,组件节数,客户名称,货位,说明" };
        lines.AddRange(state.Ledger.OrderByDescending(e => e.Timestamp).Select(e =>
            $"{e.Timestamp:yyyy-MM-dd HH:mm:ss},{e.Type},{e.OperatorName},{e.PalletNumber},{e.ToolingNumber},{e.ProjectNumber},{e.ModelType},{e.WorkOrder},{e.CellNumber},{e.ComponentSections},{e.CustomerName},{e.SlotCode},{e.ActionDescription}"));
        return lines;
    }

    private static List<string> BuildInventoryReport(AppState state)
    {
        var lines = new List<string> { "托盘号,工装号,项目号,型号,工单号,电解槽编号,组件节数,客户名称,入库时间,货位,操作人员,备注" };
        lines.AddRange(state.Inventory.OrderBy(i => i.SlotCode).Select(i =>
            $"{i.PalletNumber},{i.ToolingNumber},{i.ProjectNumber},{i.ModelType},{i.WorkOrder},{i.CellNumber},{i.ComponentSections},{i.CustomerName},{i.InboundTime:yyyy-MM-dd HH:mm:ss},{i.SlotCode},{i.LastOperator},{i.Notes}"));
        return lines;
    }

    private static List<string> BuildOperationReport(AppState state)
    {
        var lines = new List<string> { "时间,操作人员,托盘号,工装号,项目号,型号,工单号,电解槽编号,客户名称,货位,操作说明" };
        lines.AddRange(state.Ledger.OrderByDescending(e => e.Timestamp).Select(e =>
            $"{e.Timestamp:yyyy-MM-dd HH:mm:ss},{e.OperatorName},{e.PalletNumber},{e.ToolingNumber},{e.ProjectNumber},{e.ModelType},{e.WorkOrder},{e.CellNumber},{e.CustomerName},{e.SlotCode},{e.ActionDescription}"));
        return lines;
    }
}
