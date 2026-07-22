using Microsoft.Data.SqlClient;
using LongShenStorageApi.Models;

namespace LongShenStorageApi.Data;

public sealed class SqlServerRepository
{
    private readonly string _connectionString;

    public SqlServerRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Server=DESKTOP-L654TSI;Database=LongShenStorage;User Id=sa;Password=123456;TrustServerCertificate=True;";
        EnsureDatabase();
        EnsureTables();
        SeedDefaultAdmin();
    }

    private void EnsureDatabase()
    {
        var masterConn = _connectionString.Replace("Database=LongShenStorage", "Database=master");
        using var conn = new SqlConnection(masterConn);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'LongShenStorage')
            BEGIN CREATE DATABASE [LongShenStorage]; END";
        cmd.ExecuteNonQuery();
    }

    private void EnsureTables()
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AlertSettings' AND xtype='U')
            CREATE TABLE AlertSettings (Id INT PRIMARY KEY DEFAULT 1 CHECK (Id = 1), MinThreshold INT NOT NULL DEFAULT 2, MaxThreshold INT NOT NULL DEFAULT 18);
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='StorageSlots' AND xtype='U')
            CREATE TABLE StorageSlots (SlotCode NVARCHAR(50) PRIMARY KEY, IsOccupied BIT NOT NULL DEFAULT 0, WorkpieceId UNIQUEIDENTIFIER NULL, Zone NVARCHAR(50) NOT NULL DEFAULT '', RowNumber INT NOT NULL, ColumnNumber INT NOT NULL, LevelNumber INT NOT NULL, InternalNumber INT NOT NULL DEFAULT 0, IsEnabled BIT NOT NULL DEFAULT 1);
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='WorkpieceRecords' AND xtype='U')
            CREATE TABLE WorkpieceRecords (Id UNIQUEIDENTIFIER PRIMARY KEY, InboundTime DATETIME2 NOT NULL DEFAULT GETDATE(), SlotCode NVARCHAR(50) NOT NULL, LastOperator NVARCHAR(100) NOT NULL DEFAULT '', LastUpdated DATETIME2 NOT NULL DEFAULT GETDATE(), Notes NVARCHAR(500) NOT NULL DEFAULT '', PalletNumber NVARCHAR(50) NOT NULL DEFAULT '', ToolingNumber NVARCHAR(200) NOT NULL DEFAULT '', ProjectNumber NVARCHAR(200) NOT NULL DEFAULT '', ModelType NVARCHAR(200) NOT NULL DEFAULT '', WorkOrder NVARCHAR(200) NOT NULL DEFAULT '', CellNumber NVARCHAR(200) NOT NULL DEFAULT '', ComponentSections INT NOT NULL DEFAULT 1, CustomerName NVARCHAR(200) NOT NULL DEFAULT '');
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LedgerEntries' AND xtype='U')
            CREATE TABLE LedgerEntries (Id UNIQUEIDENTIFIER PRIMARY KEY, Type INT NOT NULL, Timestamp DATETIME2 NOT NULL DEFAULT GETDATE(), OperatorName NVARCHAR(100) NOT NULL DEFAULT '', SlotCode NVARCHAR(50) NOT NULL DEFAULT '', ActionDescription NVARCHAR(500) NOT NULL DEFAULT '', PalletNumber NVARCHAR(50) NOT NULL DEFAULT '', ToolingNumber NVARCHAR(200) NOT NULL DEFAULT '', ProjectNumber NVARCHAR(200) NOT NULL DEFAULT '', ModelType NVARCHAR(200) NOT NULL DEFAULT '', WorkOrder NVARCHAR(200) NOT NULL DEFAULT '', CellNumber NVARCHAR(200) NOT NULL DEFAULT '', ComponentSections INT NOT NULL DEFAULT 0, CustomerName NVARCHAR(200) NOT NULL DEFAULT '');
            -- 向下兼容
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='PalletNumber') ALTER TABLE WorkpieceRecords ADD PalletNumber NVARCHAR(50) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='ToolingNumber') ALTER TABLE WorkpieceRecords ADD ToolingNumber NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='ProjectNumber') ALTER TABLE WorkpieceRecords ADD ProjectNumber NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='ModelType') ALTER TABLE WorkpieceRecords ADD ModelType NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='WorkOrder') ALTER TABLE WorkpieceRecords ADD WorkOrder NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='CellNumber') ALTER TABLE WorkpieceRecords ADD CellNumber NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='ComponentSections') ALTER TABLE WorkpieceRecords ADD ComponentSections INT NOT NULL DEFAULT 1;
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='CustomerName') ALTER TABLE WorkpieceRecords ADD CustomerName NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='PalletNumber') ALTER TABLE LedgerEntries ADD PalletNumber NVARCHAR(50) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='ToolingNumber') ALTER TABLE LedgerEntries ADD ToolingNumber NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='ProjectNumber') ALTER TABLE LedgerEntries ADD ProjectNumber NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='ModelType') ALTER TABLE LedgerEntries ADD ModelType NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='WorkOrder') ALTER TABLE LedgerEntries ADD WorkOrder NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='CellNumber') ALTER TABLE LedgerEntries ADD CellNumber NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='ComponentSections') ALTER TABLE LedgerEntries ADD ComponentSections INT NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='CustomerName') ALTER TABLE LedgerEntries ADD CustomerName NVARCHAR(200) NOT NULL DEFAULT '';
            -- 删除旧字段
            DECLARE @sql NVARCHAR(MAX);
            SELECT @sql = COALESCE(@sql + ';', '') + 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(parent_object_id) + '].[' + OBJECT_NAME(parent_object_id) + '] DROP CONSTRAINT [' + name + ']' FROM sys.default_constraints WHERE parent_object_id IN (OBJECT_ID('WorkpieceRecords'), OBJECT_ID('LedgerEntries')) AND COL_NAME(parent_object_id, parent_column_id) IN ('Code','Batch','WorkpieceCode');
            IF @sql IS NOT NULL EXEC sp_executesql @sql;
            IF EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='Code') ALTER TABLE WorkpieceRecords DROP COLUMN Code;
            IF EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='Batch') ALTER TABLE WorkpieceRecords DROP COLUMN Batch;
            IF EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='WorkpieceCode') ALTER TABLE LedgerEntries DROP COLUMN WorkpieceCode;
            IF EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='Batch') ALTER TABLE LedgerEntries DROP COLUMN Batch;
            -- 用户表
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Users' AND xtype='U')
            CREATE TABLE Users (Id UNIQUEIDENTIFIER PRIMARY KEY, Username NVARCHAR(100) NOT NULL UNIQUE, PasswordHash NVARCHAR(500) NOT NULL, Role INT NOT NULL DEFAULT 1, DisplayName NVARCHAR(100) NOT NULL DEFAULT '', IsActive BIT NOT NULL DEFAULT 1, CreatedAt DATETIME2 NOT NULL DEFAULT GETDATE());
            -- 向下兼容：内部编号和启用状态
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('StorageSlots') AND name='InternalNumber')
                ALTER TABLE StorageSlots ADD InternalNumber INT NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('StorageSlots') AND name='IsEnabled')
                ALTER TABLE StorageSlots ADD IsEnabled BIT NOT NULL DEFAULT 1;
            -- 逐层优先不分排：1层→8层，每层内先1排后2排
            EXEC('UPDATE StorageSlots SET InternalNumber =
                CASE
                    WHEN RowNumber = 1 AND LevelNumber = 1 AND ColumnNumber = 1 THEN 0
                    WHEN LevelNumber = 1 AND RowNumber = 1 THEN ColumnNumber - 1
                    WHEN LevelNumber = 1 AND RowNumber = 2 THEN ColumnNumber + 3
                    WHEN RowNumber = 1 THEN 8 * LevelNumber - 9 + ColumnNumber
                    ELSE 8 * LevelNumber - 5 + ColumnNumber
                END');
            -- 下拉选项持久化表
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='DropdownOptions' AND xtype='U')
            CREATE TABLE DropdownOptions (Category NVARCHAR(50) NOT NULL, Value NVARCHAR(200) NOT NULL, PRIMARY KEY (Category, Value));
            -- 角色权限表
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='RolePermissions' AND xtype='U')
            CREATE TABLE RolePermissions (RoleName NVARCHAR(50) NOT NULL, PageId NVARCHAR(50) NOT NULL, PRIMARY KEY (RoleName, PageId));";
        cmd.ExecuteNonQuery();
    }

    // ===== 加载方法 =====

    public AppState Load()
    {
        try
        {
            var state = new AppState
            {
                AlertSettings = LoadAlertSettings(),
                Slots = LoadSlots(),
                Inventory = LoadInventory(),
                Ledger = LoadLedger()
            };
            LoadDropdownsIntoState(state);
            return state;
        }
        catch { return CreateDefaultState(); }
    }

    private InventoryAlertSettings LoadAlertSettings()
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT TOP 1 MinThreshold, MaxThreshold FROM AlertSettings", conn);
        using var reader = cmd.ExecuteReader();
        if (reader.Read()) return new InventoryAlertSettings { MinThreshold = reader.GetInt32(0), MaxThreshold = reader.GetInt32(1) };
        return new InventoryAlertSettings();
    }

    private List<StorageSlot> LoadSlots()
    {
        var slots = new List<StorageSlot>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT SlotCode, IsOccupied, WorkpieceId, Zone, RowNumber, ColumnNumber, LevelNumber, InternalNumber, IsEnabled FROM StorageSlots ORDER BY RowNumber, ColumnNumber, LevelNumber", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            slots.Add(new StorageSlot { SlotCode = reader.GetString(0), IsOccupied = reader.GetBoolean(1), WorkpieceId = reader.IsDBNull(2) ? null : reader.GetGuid(2), Zone = reader.GetString(3), RowNumber = reader.GetInt32(4), ColumnNumber = reader.GetInt32(5), LevelNumber = reader.GetInt32(6), InternalNumber = reader.GetInt32(7), IsEnabled = reader.GetBoolean(8) });
        return slots;
    }

    public List<WorkpieceRecord> LoadInventory()
    {
        var list = new List<WorkpieceRecord>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(@"SELECT Id, InboundTime, SlotCode, LastOperator, LastUpdated, Notes, PalletNumber, ToolingNumber, ProjectNumber, ModelType, WorkOrder, CellNumber, ComponentSections, CustomerName FROM WorkpieceRecords ORDER BY InboundTime DESC", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new WorkpieceRecord { Id = reader.GetGuid(0), InboundTime = reader.GetDateTime(1), SlotCode = reader.GetString(2), LastOperator = reader.GetString(3), LastUpdated = reader.GetDateTime(4), Notes = reader.GetString(5), PalletNumber = reader.GetString(6), ToolingNumber = reader.GetString(7), ProjectNumber = reader.GetString(8), ModelType = reader.GetString(9), WorkOrder = reader.GetString(10), CellNumber = reader.GetString(11), ComponentSections = reader.GetInt32(12), CustomerName = reader.GetString(13) });
        return list;
    }

    public List<LedgerEntry> LoadLedger()
    {
        var list = new List<LedgerEntry>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(@"SELECT Id, Type, Timestamp, OperatorName, SlotCode, ActionDescription, PalletNumber, ToolingNumber, ProjectNumber, ModelType, WorkOrder, CellNumber, ComponentSections, CustomerName FROM LedgerEntries ORDER BY Timestamp DESC", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new LedgerEntry { Id = reader.GetGuid(0), Type = (TransactionType)reader.GetInt32(1), Timestamp = reader.GetDateTime(2), OperatorName = reader.GetString(3), SlotCode = reader.GetString(4), ActionDescription = reader.GetString(5), PalletNumber = reader.GetString(6), ToolingNumber = reader.GetString(7), ProjectNumber = reader.GetString(8), ModelType = reader.GetString(9), WorkOrder = reader.GetString(10), CellNumber = reader.GetString(11), ComponentSections = reader.GetInt32(12), CustomerName = reader.GetString(13) });
        return list;
    }

    // ===== 下拉选项持久化 =====

    public List<string> LoadDropdownOptions(string category)
    {
        var list = new List<string>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT Value FROM DropdownOptions WHERE Category = @c ORDER BY Value", conn);
        cmd.Parameters.AddWithValue("@c", category);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(reader.GetString(0));
        return list;
    }

    public void SaveDropdownOption(string category, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("IF NOT EXISTS (SELECT 1 FROM DropdownOptions WHERE Category = @c AND Value = @v) INSERT INTO DropdownOptions (Category, Value) VALUES (@c, @v)", conn);
        cmd.Parameters.AddWithValue("@c", category);
        cmd.Parameters.AddWithValue("@v", value);
        cmd.ExecuteNonQuery();
    }

    public void LoadDropdownsIntoState(AppState state)
    {
        state.PalletNumbers = LoadDropdownOptions("PalletNumber");
        state.ToolingNumbers = LoadDropdownOptions("ToolingNumber");
        state.ProjectNumbers = LoadDropdownOptions("ProjectNumber");
        state.ModelTypes = LoadDropdownOptions("ModelType");
        state.CustomerNames = LoadDropdownOptions("CustomerName");

        // 从已有工件记录中提取并补充
        if (state.Inventory.Count > 0)
        {
            AddUnique(state.PalletNumbers, state.Inventory.Where(r => !string.IsNullOrWhiteSpace(r.PalletNumber)).Select(r => r.PalletNumber));
            AddUnique(state.ToolingNumbers, state.Inventory.Where(r => !string.IsNullOrWhiteSpace(r.ToolingNumber)).Select(r => r.ToolingNumber));
            AddUnique(state.ProjectNumbers, state.Inventory.Where(r => !string.IsNullOrWhiteSpace(r.ProjectNumber)).Select(r => r.ProjectNumber));
            AddUnique(state.ModelTypes, state.Inventory.Where(r => !string.IsNullOrWhiteSpace(r.ModelType)).Select(r => r.ModelType));
            AddUnique(state.CustomerNames, state.Inventory.Where(r => !string.IsNullOrWhiteSpace(r.CustomerName)).Select(r => r.CustomerName));
        }

        // 默认托盘号
        if (state.PalletNumbers.Count == 0)
            state.PalletNumbers = Enumerable.Range(1, 66).Select(i => $"{i:D3}").ToList();
    }

    private static void AddUnique(List<string> list, IEnumerable<string> items)
    {
        foreach (var item in items)
            if (!string.IsNullOrWhiteSpace(item) && !list.Contains(item, StringComparer.OrdinalIgnoreCase))
                list.Add(item);
    }

    // ===== 保存方法 =====

    public void Save(AppState state)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            SaveAlertSettings(conn, tx, state.AlertSettings);
            SaveSlots(conn, tx, state.Slots);
            SaveInventory(conn, tx, state.Inventory);
            SaveLedger(conn, tx, state.Ledger);
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }

    private static void SaveAlertSettings(SqlConnection conn, SqlTransaction tx, InventoryAlertSettings settings)
    {
        using var cmd = new SqlCommand(@"MERGE AlertSettings AS target USING (SELECT 1 AS Id) AS source ON target.Id = source.Id WHEN MATCHED THEN UPDATE SET MinThreshold = @Min, MaxThreshold = @Max WHEN NOT MATCHED THEN INSERT (Id, MinThreshold, MaxThreshold) VALUES (1, @Min, @Max);", conn, tx);
        cmd.Parameters.AddWithValue("@Min", settings.MinThreshold);
        cmd.Parameters.AddWithValue("@Max", settings.MaxThreshold);
        cmd.ExecuteNonQuery();
    }

    private static void SaveSlots(SqlConnection conn, SqlTransaction tx, List<StorageSlot> slots)
    {
        using var cmd = new SqlCommand(@"MERGE StorageSlots AS target USING (SELECT @SlotCode AS SlotCode) AS source ON target.SlotCode = source.SlotCode WHEN MATCHED THEN UPDATE SET IsOccupied = @Occ, WorkpieceId = @Wid, Zone = @Zone, RowNumber = @Row, ColumnNumber = @Col, LevelNumber = @Lev, InternalNumber = @IntNum, IsEnabled = @Enabled WHEN NOT MATCHED THEN INSERT (SlotCode, IsOccupied, WorkpieceId, Zone, RowNumber, ColumnNumber, LevelNumber, InternalNumber, IsEnabled) VALUES (@SlotCode, @Occ, @Wid, @Zone, @Row, @Col, @Lev, @IntNum, @Enabled);", conn, tx);
        cmd.Parameters.Add("@SlotCode", System.Data.SqlDbType.NVarChar, 50);
        cmd.Parameters.Add("@Occ", System.Data.SqlDbType.Bit);
        cmd.Parameters.Add("@Wid", System.Data.SqlDbType.UniqueIdentifier);
        cmd.Parameters.Add("@Zone", System.Data.SqlDbType.NVarChar, 50);
        cmd.Parameters.Add("@Row", System.Data.SqlDbType.Int);
        cmd.Parameters.Add("@Col", System.Data.SqlDbType.Int);
        cmd.Parameters.Add("@Lev", System.Data.SqlDbType.Int);
        cmd.Parameters.Add("@IntNum", System.Data.SqlDbType.Int);
        cmd.Parameters.Add("@Enabled", System.Data.SqlDbType.Bit);
        foreach (var slot in slots)
        {
            cmd.Parameters["@SlotCode"].Value = slot.SlotCode;
            cmd.Parameters["@Occ"].Value = slot.IsOccupied;
            cmd.Parameters["@Wid"].Value = slot.WorkpieceId.HasValue ? (object)slot.WorkpieceId.Value : DBNull.Value;
            cmd.Parameters["@Zone"].Value = slot.Zone;
            cmd.Parameters["@Row"].Value = slot.RowNumber;
            cmd.Parameters["@Col"].Value = slot.ColumnNumber;
            cmd.Parameters["@Lev"].Value = slot.LevelNumber;
            cmd.Parameters["@IntNum"].Value = slot.InternalNumber;
            cmd.Parameters["@Enabled"].Value = slot.IsEnabled;
            cmd.ExecuteNonQuery();
        }
    }

    private static void SaveInventory(SqlConnection conn, SqlTransaction tx, List<WorkpieceRecord> inventory)
    {
        using var cmd = new SqlCommand(@"MERGE WorkpieceRecords AS target USING (SELECT @Id AS Id) AS source ON target.Id = source.Id WHEN MATCHED THEN UPDATE SET InboundTime = @InTime, SlotCode = @Slot, LastOperator = @Op, LastUpdated = @Upd, Notes = @Notes, PalletNumber = @Pallet, ToolingNumber = @Tool, ProjectNumber = @Proj, ModelType = @Model, WorkOrder = @Wo, CellNumber = @Cell, ComponentSections = @Comp, CustomerName = @Cust WHEN NOT MATCHED THEN INSERT (Id, InboundTime, SlotCode, LastOperator, LastUpdated, Notes, PalletNumber, ToolingNumber, ProjectNumber, ModelType, WorkOrder, CellNumber, ComponentSections, CustomerName) VALUES (@Id, @InTime, @Slot, @Op, @Upd, @Notes, @Pallet, @Tool, @Proj, @Model, @Wo, @Cell, @Comp, @Cust);", conn, tx);
        cmd.Parameters.Add("@Id", System.Data.SqlDbType.UniqueIdentifier);
        cmd.Parameters.Add("@InTime", System.Data.SqlDbType.DateTime2);
        cmd.Parameters.Add("@Slot", System.Data.SqlDbType.NVarChar, 50);
        cmd.Parameters.Add("@Op", System.Data.SqlDbType.NVarChar, 100);
        cmd.Parameters.Add("@Upd", System.Data.SqlDbType.DateTime2);
        cmd.Parameters.Add("@Notes", System.Data.SqlDbType.NVarChar, 500);
        cmd.Parameters.Add("@Pallet", System.Data.SqlDbType.NVarChar, 50);
        cmd.Parameters.Add("@Tool", System.Data.SqlDbType.NVarChar, 200);
        cmd.Parameters.Add("@Proj", System.Data.SqlDbType.NVarChar, 200);
        cmd.Parameters.Add("@Model", System.Data.SqlDbType.NVarChar, 200);
        cmd.Parameters.Add("@Wo", System.Data.SqlDbType.NVarChar, 200);
        cmd.Parameters.Add("@Cell", System.Data.SqlDbType.NVarChar, 200);
        cmd.Parameters.Add("@Comp", System.Data.SqlDbType.Int);
        cmd.Parameters.Add("@Cust", System.Data.SqlDbType.NVarChar, 200);
        foreach (var item in inventory)
        {
            cmd.Parameters["@Id"].Value = item.Id;
            cmd.Parameters["@InTime"].Value = item.InboundTime;
            cmd.Parameters["@Slot"].Value = item.SlotCode;
            cmd.Parameters["@Op"].Value = item.LastOperator;
            cmd.Parameters["@Upd"].Value = item.LastUpdated;
            cmd.Parameters["@Notes"].Value = item.Notes;
            cmd.Parameters["@Pallet"].Value = item.PalletNumber;
            cmd.Parameters["@Tool"].Value = item.ToolingNumber;
            cmd.Parameters["@Proj"].Value = item.ProjectNumber;
            cmd.Parameters["@Model"].Value = item.ModelType;
            cmd.Parameters["@Wo"].Value = item.WorkOrder;
            cmd.Parameters["@Cell"].Value = item.CellNumber;
            cmd.Parameters["@Comp"].Value = item.ComponentSections;
            cmd.Parameters["@Cust"].Value = item.CustomerName;
            cmd.ExecuteNonQuery();
        }
    }

    private static void SaveLedger(SqlConnection conn, SqlTransaction tx, List<LedgerEntry> ledger)
    {
        using var cmd = new SqlCommand(@"MERGE LedgerEntries AS target USING (SELECT @Id AS Id) AS source ON target.Id = source.Id WHEN MATCHED THEN UPDATE SET Type = @Type, Timestamp = @Ts, OperatorName = @Op, SlotCode = @Slot, ActionDescription = @Desc, PalletNumber = @Pallet, ToolingNumber = @Tool, ProjectNumber = @Proj, ModelType = @Model, WorkOrder = @Wo, CellNumber = @Cell, ComponentSections = @Comp, CustomerName = @Cust WHEN NOT MATCHED THEN INSERT (Id, Type, Timestamp, OperatorName, SlotCode, ActionDescription, PalletNumber, ToolingNumber, ProjectNumber, ModelType, WorkOrder, CellNumber, ComponentSections, CustomerName) VALUES (@Id, @Type, @Ts, @Op, @Slot, @Desc, @Pallet, @Tool, @Proj, @Model, @Wo, @Cell, @Comp, @Cust);", conn, tx);
        cmd.Parameters.Add("@Id", System.Data.SqlDbType.UniqueIdentifier);
        cmd.Parameters.Add("@Type", System.Data.SqlDbType.Int);
        cmd.Parameters.Add("@Ts", System.Data.SqlDbType.DateTime2);
        cmd.Parameters.Add("@Op", System.Data.SqlDbType.NVarChar, 100);
        cmd.Parameters.Add("@Slot", System.Data.SqlDbType.NVarChar, 50);
        cmd.Parameters.Add("@Desc", System.Data.SqlDbType.NVarChar, 500);
        cmd.Parameters.Add("@Pallet", System.Data.SqlDbType.NVarChar, 50);
        cmd.Parameters.Add("@Tool", System.Data.SqlDbType.NVarChar, 200);
        cmd.Parameters.Add("@Proj", System.Data.SqlDbType.NVarChar, 200);
        cmd.Parameters.Add("@Model", System.Data.SqlDbType.NVarChar, 200);
        cmd.Parameters.Add("@Wo", System.Data.SqlDbType.NVarChar, 200);
        cmd.Parameters.Add("@Cell", System.Data.SqlDbType.NVarChar, 200);
        cmd.Parameters.Add("@Comp", System.Data.SqlDbType.Int);
        cmd.Parameters.Add("@Cust", System.Data.SqlDbType.NVarChar, 200);
        foreach (var entry in ledger)
        {
            cmd.Parameters["@Id"].Value = entry.Id;
            cmd.Parameters["@Type"].Value = (int)entry.Type;
            cmd.Parameters["@Ts"].Value = entry.Timestamp;
            cmd.Parameters["@Op"].Value = entry.OperatorName;
            cmd.Parameters["@Slot"].Value = entry.SlotCode;
            cmd.Parameters["@Desc"].Value = entry.ActionDescription;
            cmd.Parameters["@Pallet"].Value = entry.PalletNumber;
            cmd.Parameters["@Tool"].Value = entry.ToolingNumber;
            cmd.Parameters["@Proj"].Value = entry.ProjectNumber;
            cmd.Parameters["@Model"].Value = entry.ModelType;
            cmd.Parameters["@Wo"].Value = entry.WorkOrder;
            cmd.Parameters["@Cell"].Value = entry.CellNumber;
            cmd.Parameters["@Comp"].Value = entry.ComponentSections;
            cmd.Parameters["@Cust"].Value = entry.CustomerName;
            cmd.ExecuteNonQuery();
        }
    }

    // ===== 业务方法 =====

    public WorkpieceRecord? Inbound(InboundRequest request)
    {
        var state = Load();
        var targetSlot = ResolveTargetSlot(state, request.SpecifiedSlot, request.PalletNumber);
        if (targetSlot is null) return null;

        var record = new WorkpieceRecord
        {
            PalletNumber = request.PalletNumber,
            ToolingNumber = request.ToolingNumber,
            ProjectNumber = request.ProjectNumber,
            ModelType = request.ModelType,
            WorkOrder = request.WorkOrder,
            CellNumber = request.CellNumber,
            ComponentSections = request.ComponentSections,
            CustomerName = request.CustomerName,
            SlotCode = targetSlot.SlotCode,
            InboundTime = DateTime.Now,
            LastOperator = request.OperatorName,
            LastUpdated = DateTime.Now,
            Notes = request.Notes
        };

        targetSlot.IsOccupied = true;
        targetSlot.WorkpieceId = record.Id;
        state.Inventory.Add(record);
        state.Ledger.Add(new LedgerEntry
        {
            Type = TransactionType.Inbound,
            Timestamp = DateTime.Now,
            OperatorName = request.OperatorName,
            PalletNumber = record.PalletNumber,
            ToolingNumber = record.ToolingNumber,
            ProjectNumber = record.ProjectNumber,
            ModelType = record.ModelType,
            WorkOrder = record.WorkOrder,
            CellNumber = record.CellNumber,
            ComponentSections = record.ComponentSections,
            CustomerName = record.CustomerName,
            SlotCode = record.SlotCode,
            ActionDescription = $"托盘{record.PalletNumber}入库至 {targetSlot.SlotCode}"
        });
        Save(state);
        return record;
    }

    public bool Outbound(OutboundRequest request)
    {
        var state = Load();
        var record = state.Inventory.FirstOrDefault(r => r.Id == request.RecordId);
        if (record is null) return false;

        if (!string.IsNullOrWhiteSpace(request.SpecifiedSlot) && !record.SlotCode.Equals(request.SpecifiedSlot, StringComparison.OrdinalIgnoreCase))
            return false;

        var slot = state.Slots.FirstOrDefault(s => s.SlotCode == record.SlotCode);
        if (slot is null) return false;

        slot.IsOccupied = false;
        slot.WorkpieceId = null;
        state.Inventory.Remove(record);
        state.Ledger.Add(new LedgerEntry
        {
            Type = TransactionType.Outbound,
            Timestamp = DateTime.Now,
            OperatorName = request.OperatorName,
            PalletNumber = record.PalletNumber,
            ToolingNumber = record.ToolingNumber,
            ProjectNumber = record.ProjectNumber,
            ModelType = record.ModelType,
            WorkOrder = record.WorkOrder,
            CellNumber = record.CellNumber,
            ComponentSections = record.ComponentSections,
            CustomerName = record.CustomerName,
            SlotCode = record.SlotCode,
            ActionDescription = $"托盘{record.PalletNumber}出库"
        });
        Save(state);
        return true;
    }

    private static StorageSlot? ResolveTargetSlot(AppState state, string? specifiedSlot, string? palletNumber)
    {
        // 1. 如果指定了货位，使用指定货位
        if (!string.IsNullOrWhiteSpace(specifiedSlot))
            return state.Slots.FirstOrDefault(s => s.SlotCode.Equals(specifiedSlot, StringComparison.OrdinalIgnoreCase) && !s.IsOccupied);

        // 2. 根据托盘号提取数字，匹配内部编号库位（"010" → 内部编号10）
        if (!string.IsNullOrWhiteSpace(palletNumber))
        {
            var numStr = new string(palletNumber.Where(char.IsDigit).ToArray());
            if (int.TryParse(numStr, out var palletNum) && palletNum > 0)
            {
                var matchedSlot = state.Slots.FirstOrDefault(s =>
                    s.InternalNumber == palletNum && !s.IsOccupied && s.IsEnabled);
                if (matchedSlot != null)
                    return matchedSlot;
            }
        }

        // 3. 回退：找第一个空闲且启用的库位
        return state.Slots.FirstOrDefault(s => !s.IsOccupied && s.IsEnabled);
    }

    private static AppState CreateDefaultState()
    {
        return new AppState
        {
            Slots = CreateDefaultSlots(),
            AlertSettings = new InventoryAlertSettings { MinThreshold = 2, MaxThreshold = 18 }
        };
    }

    private static List<StorageSlot> CreateDefaultSlots()
    {
        var slots = new List<StorageSlot>();
        for (var row = 1; row <= 2; row++)
            for (var column = 1; column <= 4; column++)
                for (var level = 1; level <= 8; level++)
                {
                    int internalNumber;
                    // 逐层优先不分排：1层→8层，每层内先1排后2排
                    if (row == 1 && level == 1 && column == 1)
                        internalNumber = 0; // 总出入口
                    else if (level == 1 && row == 1)
                        internalNumber = column - 1; // 1层1排：col2=1, col3=2, col4=3
                    else if (level == 1 && row == 2)
                        internalNumber = column + 3; // 1层2排：col1=4, col2=5, col3=6, col4=7
                    else if (row == 1) // 2~8层1排
                        internalNumber = 8 * level - 9 + column;
                    else // 2~8层2排
                        internalNumber = 8 * level - 5 + column;

                    slots.Add(new StorageSlot
                    {
                        RowNumber = row,
                        ColumnNumber = column,
                        LevelNumber = level,
                        Zone = $"{row}排",
                        SlotCode = $"{row}排-{column}列-{level}层",
                        IsOccupied = false,
                        InternalNumber = internalNumber,
                        IsEnabled = true
                    });
                }
        return slots;
    }

    // ===== 用户管理 =====

    public User? GetUserByUsername(string username)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT Id, Username, PasswordHash, Role, DisplayName, IsActive, CreatedAt FROM Users WHERE Username = @u", conn);
        cmd.Parameters.AddWithValue("@u", username);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return new User { Id = reader.GetGuid(0), Username = reader.GetString(1), PasswordHash = reader.GetString(2), Role = (UserRole)reader.GetInt32(3), DisplayName = reader.GetString(4), IsActive = reader.GetBoolean(5), CreatedAt = reader.GetDateTime(6) };
        return null;
    }

    public User? GetUserById(Guid id)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT Id, Username, PasswordHash, Role, DisplayName, IsActive, CreatedAt FROM Users WHERE Id = @id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
            return new User { Id = reader.GetGuid(0), Username = reader.GetString(1), PasswordHash = reader.GetString(2), Role = (UserRole)reader.GetInt32(3), DisplayName = reader.GetString(4), IsActive = reader.GetBoolean(5), CreatedAt = reader.GetDateTime(6) };
        return null;
    }

    public List<User> GetAllUsers()
    {
        var list = new List<User>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT Id, Username, PasswordHash, Role, DisplayName, IsActive, CreatedAt FROM Users ORDER BY CreatedAt", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            list.Add(new User { Id = reader.GetGuid(0), Username = reader.GetString(1), PasswordHash = reader.GetString(2), Role = (UserRole)reader.GetInt32(3), DisplayName = reader.GetString(4), IsActive = reader.GetBoolean(5), CreatedAt = reader.GetDateTime(6) });
        return list;
    }

    public void CreateUser(User user)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("INSERT INTO Users (Id, Username, PasswordHash, Role, DisplayName, IsActive, CreatedAt) VALUES (@Id, @U, @P, @R, @D, @A, @C)", conn);
        cmd.Parameters.AddWithValue("@Id", user.Id);
        cmd.Parameters.AddWithValue("@U", user.Username);
        cmd.Parameters.AddWithValue("@P", user.PasswordHash);
        cmd.Parameters.AddWithValue("@R", (int)user.Role);
        cmd.Parameters.AddWithValue("@D", user.DisplayName);
        cmd.Parameters.AddWithValue("@A", user.IsActive);
        cmd.Parameters.AddWithValue("@C", user.CreatedAt);
        cmd.ExecuteNonQuery();
    }

    public void UpdateUser(User user)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("UPDATE Users SET Role = @R, DisplayName = @D, IsActive = @A, PasswordHash = @P WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", user.Id);
        cmd.Parameters.AddWithValue("@R", (int)user.Role);
        cmd.Parameters.AddWithValue("@D", user.DisplayName);
        cmd.Parameters.AddWithValue("@A", user.IsActive);
        cmd.Parameters.AddWithValue("@P", user.PasswordHash);
        cmd.ExecuteNonQuery();
    }

    public void DeleteUser(Guid id)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("DELETE FROM Users WHERE Id = @Id", conn);
        cmd.Parameters.AddWithValue("@Id", id);
        cmd.ExecuteNonQuery();
    }

    public bool SeedDefaultAdmin()
    {
        if (GetUserByUsername("admin") is not null) return false;
        CreateUser(new User
        {
            Username = "admin",
            PasswordHash = BCryptHash("admin123"),
            Role = UserRole.Admin,
            DisplayName = "管理员",
            IsActive = true
        });
        CreateUser(new User
        {
            Username = "operator",
            PasswordHash = BCryptHash("123456"),
            Role = UserRole.Operator,
            DisplayName = "操作员",
            IsActive = true
        });
        CreateUser(new User
        {
            Username = "viewer",
            PasswordHash = BCryptHash("123456"),
            Role = UserRole.Viewer,
            DisplayName = "查看员",
            IsActive = true
        });
        return true;
    }

    private static string BCryptHash(string password)
    {
        // 简化版哈希（生产环境应使用 BCrypt）
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password + "LongShenSalt2026"));
        return Convert.ToBase64String(bytes);
    }

    public bool VerifyPassword(string password, string hash)
    {
        return BCryptHash(password) == hash;
    }

    // ===== 角色权限 =====

    public List<string> GetRolePermissions(string roleName)
    {
        var list = new List<string>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT PageId FROM RolePermissions WHERE RoleName = @r", conn);
        cmd.Parameters.AddWithValue("@r", roleName);
        using var reader = cmd.ExecuteReader();
        while (reader.Read()) list.Add(reader.GetString(0));
        return list;
    }

    public void SaveRolePermissions(string roleName, List<string> pageIds)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        using var del = new SqlCommand("DELETE FROM RolePermissions WHERE RoleName = @r", conn, tx);
        del.Parameters.AddWithValue("@r", roleName);
        del.ExecuteNonQuery();

        using var ins = new SqlCommand("INSERT INTO RolePermissions (RoleName, PageId) VALUES (@r, @p)", conn, tx);
        ins.Parameters.Add("@r", System.Data.SqlDbType.NVarChar, 50);
        ins.Parameters.Add("@p", System.Data.SqlDbType.NVarChar, 50);
        foreach (var pageId in pageIds)
        {
            ins.Parameters["@r"].Value = roleName;
            ins.Parameters["@p"].Value = pageId;
            ins.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
