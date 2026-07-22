using Microsoft.AspNetCore.Mvc;
using LongShenStorageApi.Data;
using LongShenStorageApi.Models;

namespace LongShenStorageApi.Controllers;

/// <summary>
/// 系统状态与仪表盘
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AppStateController : ControllerBase
{
    private readonly SqlServerRepository _repo;

    public AppStateController(SqlServerRepository repo) => _repo = repo;

    /// <summary>获取完整系统状态</summary>
    [HttpGet]
    public ActionResult<AppState> GetState()
    {
        var state = _repo.Load();
        return Ok(state);
    }

    /// <summary>获取仪表盘数据</summary>
    [HttpGet("dashboard")]
    public ActionResult<DashboardData> GetDashboard()
    {
        var state = _repo.Load();
        var inventoryCount = state.Inventory.Count;
        var occupied = state.Slots.Count(s => s.IsOccupied);
        var free = state.Slots.Count(s => !s.IsOccupied);
        var alert = inventoryCount < state.AlertSettings.MinThreshold || inventoryCount > state.AlertSettings.MaxThreshold;
        var alertText = inventoryCount < state.AlertSettings.MinThreshold ? "低于下限" : inventoryCount > state.AlertSettings.MaxThreshold ? "高于上限" : "正常";

        var today = DateTime.Today;
        var todayInbound = state.Ledger.Count(e => e.Type == TransactionType.Inbound && e.Timestamp >= today);
        var todayOutbound = state.Ledger.Count(e => e.Type == TransactionType.Outbound && e.Timestamp >= today);

        return Ok(new DashboardData
        {
            InventoryCount = inventoryCount,
            OccupiedSlots = occupied,
            FreeSlots = free,
            TodayInbound = todayInbound,
            TodayOutbound = todayOutbound,
            AlertStatus = alertText,
            IsAlert = alert,
            Slots = state.Slots,
            RecentInventory = state.Inventory.OrderByDescending(i => i.InboundTime).Take(20).ToList()
        });
    }

    /// <summary>获取下拉选项列表</summary>
    [HttpGet("dropdowns")]
    public ActionResult<object> GetDropdowns()
    {
        var state = _repo.Load();
        var palletNumbers = state.PalletNumbers?.Count > 0 ? state.PalletNumbers : Enumerable.Range(1, 66).Select(i => $"{i:D3}").ToList();
        return Ok(new
        {
            palletNumbers,
            state.ToolingNumbers,
            state.ProjectNumbers,
            state.ModelTypes,
            state.CustomerNames
        });
    }

    /// <summary>保存预警阈值</summary>
    [HttpPost("alerts")]
    public IActionResult SaveAlerts([FromBody] InventoryAlertSettings settings)
    {
        var state = _repo.Load();
        state.AlertSettings = settings;
        _repo.Save(state);
        return Ok(new { message = "预警阈值已保存" });
    }

    /// <summary>保存下拉选项（用户新增的项）</summary>
    [HttpPost("dropdowns")]
    public IActionResult SaveDropdowns([FromBody] AppState updatedState)
    {
        var state = _repo.Load();
        SaveDropdownCategory(state, updatedState.PalletNumbers, "PalletNumber");
        SaveDropdownCategory(state, updatedState.ToolingNumbers, "ToolingNumber");
        SaveDropdownCategory(state, updatedState.ProjectNumbers, "ProjectNumber");
        SaveDropdownCategory(state, updatedState.ModelTypes, "ModelType");
        SaveDropdownCategory(state, updatedState.CustomerNames, "CustomerName");
        _repo.Save(state);
        return Ok(new { message = "下拉选项已保存" });
    }

    private void SaveDropdownCategory(AppState state, List<string>? items, string category)
    {
        if (items is null || items.Count == 0) return;
        foreach (var value in items)
        {
            if (!string.IsNullOrWhiteSpace(value))
                _repo.SaveDropdownOption(category, value);
        }
        // 重新加载
        LoadDropdownCategory(state, category);
    }

    private void LoadDropdownCategory(AppState state, string category)
    {
        var loaded = _repo.LoadDropdownOptions(category);
        switch (category)
        {
            case "PalletNumber": state.PalletNumbers = loaded; break;
            case "ToolingNumber": state.ToolingNumbers = loaded; break;
            case "ProjectNumber": state.ProjectNumbers = loaded; break;
            case "ModelType": state.ModelTypes = loaded; break;
            case "CustomerName": state.CustomerNames = loaded; break;
        }
    }
}
