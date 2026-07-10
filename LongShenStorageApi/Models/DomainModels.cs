using System.ComponentModel;
using System.Text.Json.Serialization;

namespace LongShenStorageApi.Models;

public enum TransactionType
{
    [Description("入库")] Inbound,
    [Description("出库")] Outbound
}

public sealed class WorkpieceRecord
{
    [DisplayName("编号")] public Guid Id { get; set; } = Guid.NewGuid();
    [DisplayName("托盘号")] public string PalletNumber { get; set; } = string.Empty;
    [DisplayName("工装号")] public string ToolingNumber { get; set; } = string.Empty;
    [DisplayName("项目号")] public string ProjectNumber { get; set; } = string.Empty;
    [DisplayName("型号")] public string ModelType { get; set; } = string.Empty;
    [DisplayName("工单号")] public string WorkOrder { get; set; } = string.Empty;
    [DisplayName("电解槽编号")] public string CellNumber { get; set; } = string.Empty;
    [DisplayName("组件节数")] public int ComponentSections { get; set; } = 1;
    [DisplayName("客户名称")] public string CustomerName { get; set; } = string.Empty;
    [DisplayName("入库时间")] public DateTime InboundTime { get; set; } = DateTime.Now;
    [DisplayName("货位号")] public string SlotCode { get; set; } = string.Empty;
    [DisplayName("操作人员")] public string LastOperator { get; set; } = string.Empty;
    [DisplayName("最后更新")] public DateTime LastUpdated { get; set; } = DateTime.Now;
    [DisplayName("备注")] public string Notes { get; set; } = string.Empty;

    [JsonIgnore]
    [DisplayName("搜索")]
    public string SearchText => string.Join(' ', PalletNumber, ToolingNumber, ProjectNumber, ModelType, WorkOrder, CellNumber, CustomerName, SlotCode, LastOperator, Notes);
}

public sealed class StorageSlot
{
    [DisplayName("货位号")] public string SlotCode { get; set; } = string.Empty;
    [DisplayName("状态")] public bool IsOccupied { get; set; }
    [DisplayName("工件编号")] public Guid? WorkpieceId { get; set; }
    [DisplayName("库区")] public string Zone { get; set; } = string.Empty;
    [DisplayName("排")] public int RowNumber { get; set; }
    [DisplayName("列")] public int ColumnNumber { get; set; }
    [DisplayName("层")] public int LevelNumber { get; set; }
}

public sealed class LedgerEntry
{
    [DisplayName("编号")] public Guid Id { get; set; } = Guid.NewGuid();
    [DisplayName("操作类型")] public TransactionType Type { get; set; }
    [DisplayName("时间")] public DateTime Timestamp { get; set; } = DateTime.Now;
    [DisplayName("操作人员")] public string OperatorName { get; set; } = string.Empty;
    [DisplayName("托盘号")] public string PalletNumber { get; set; } = string.Empty;
    [DisplayName("工装号")] public string ToolingNumber { get; set; } = string.Empty;
    [DisplayName("项目号")] public string ProjectNumber { get; set; } = string.Empty;
    [DisplayName("型号")] public string ModelType { get; set; } = string.Empty;
    [DisplayName("工单号")] public string WorkOrder { get; set; } = string.Empty;
    [DisplayName("电解槽编号")] public string CellNumber { get; set; } = string.Empty;
    [DisplayName("组件节数")] public int ComponentSections { get; set; }
    [DisplayName("客户名称")] public string CustomerName { get; set; } = string.Empty;
    [DisplayName("货位号")] public string SlotCode { get; set; } = string.Empty;
    [DisplayName("操作说明")] public string ActionDescription { get; set; } = string.Empty;
}

public sealed class InventoryAlertSettings
{
    public int MinThreshold { get; set; } = 2;
    public int MaxThreshold { get; set; } = 18;
}

public sealed class AppState
{
    public List<WorkpieceRecord> Inventory { get; set; } = new();
    public List<StorageSlot> Slots { get; set; } = new();
    public List<LedgerEntry> Ledger { get; set; } = new();
    public InventoryAlertSettings AlertSettings { get; set; } = new();
    public List<string> PalletNumbers { get; set; } = Enumerable.Range(1, 66).Select(i => $"T{i}").ToList();
    public List<string> ToolingNumbers { get; set; } = new();
    public List<string> ProjectNumbers { get; set; } = new();
    public List<string> ModelTypes { get; set; } = new();
    public List<string> CustomerNames { get; set; } = new();
}

/// <summary>
/// 前端仪表盘数据
/// </summary>
public sealed class DashboardData
{
    public int InventoryCount { get; set; }
    public int OccupiedSlots { get; set; }
    public int FreeSlots { get; set; }
    public string AlertStatus { get; set; } = "正常";
    public bool IsAlert { get; set; }
    public List<StorageSlot> Slots { get; set; } = new();
    public List<WorkpieceRecord> RecentInventory { get; set; } = new();
}

/// <summary>
/// 入库请求
/// </summary>
public sealed class InboundRequest
{
    public string PalletNumber { get; set; } = string.Empty;
    public string ToolingNumber { get; set; } = string.Empty;
    public string ProjectNumber { get; set; } = string.Empty;
    public string ModelType { get; set; } = string.Empty;
    public string WorkOrder { get; set; } = string.Empty;
    public string CellNumber { get; set; } = string.Empty;
    public int ComponentSections { get; set; } = 1;
    public string CustomerName { get; set; } = string.Empty;
    public string OperatorName { get; set; } = string.Empty;
    public string? SpecifiedSlot { get; set; }
    public string Notes { get; set; } = string.Empty;
}

/// <summary>
/// 出库请求
/// </summary>
public sealed class OutboundRequest
{
    public Guid RecordId { get; set; }
    public string OperatorName { get; set; } = string.Empty;
    public string? SpecifiedSlot { get; set; }
}

/// <summary>
/// 台账查询参数
/// </summary>
public sealed class LedgerQueryRequest
{
    public TransactionType? Type { get; set; }
    public string? PalletNumber { get; set; }
    public string? WorkOrder { get; set; }
    public string? OperatorName { get; set; }
    public DateTime? StartTime { get; set; }
    public DateTime? EndTime { get; set; }
}

/// <summary>
/// 报表参数
/// </summary>
public sealed class ReportRequest
{
    public string ReportType { get; set; } = "进出台账"; // 进出台账, 库存清单, 操作记录
    public int MinThreshold { get; set; } = 2;
    public int MaxThreshold { get; set; } = 18;
}
