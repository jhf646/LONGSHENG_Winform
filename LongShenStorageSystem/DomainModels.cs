using System.ComponentModel;
using System.Text.Json.Serialization;

namespace LongShenStorageSystem;

public enum TransactionType
{
    [Description("入库")] Inbound,
    [Description("出库")] Outbound
}

public sealed class WorkpieceRecord
{
    [DisplayName("编号")] public Guid Id { get; set; } = Guid.NewGuid();
    [DisplayName("工件编码")] public string Code { get; set; } = string.Empty;
    [DisplayName("批次号")] public string Batch { get; set; } = string.Empty;
    [DisplayName("入库时间")] public DateTime InboundTime { get; set; } = DateTime.Now;
    [DisplayName("货位号")] public string SlotCode { get; set; } = string.Empty;
    [DisplayName("操作人员")] public string LastOperator { get; set; } = string.Empty;
    [DisplayName("最后更新")] public DateTime LastUpdated { get; set; } = DateTime.Now;
    [DisplayName("备注")] public string Notes { get; set; } = string.Empty;

    [JsonIgnore]
    [DisplayName("搜索")]
    public string SearchText => string.Join(' ', Code, Batch, SlotCode, LastOperator, Notes);
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
    [DisplayName("工件编码")] public string WorkpieceCode { get; set; } = string.Empty;
    [DisplayName("批次号")] public string Batch { get; set; } = string.Empty;
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
}