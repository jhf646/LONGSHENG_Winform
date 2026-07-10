using Microsoft.Data.SqlClient;

namespace LongShenStorageSystem;

public sealed class SqlServerRepository
{
    private readonly string _connectionString;

    public SqlServerRepository()
    {
        _connectionString = "Server=DESKTOP-L654TSI;Database=LongShenStorage;User Id=sa;Password=123456;TrustServerCertificate=True;";
        EnsureDatabase();
        EnsureTables();
        ClearAllData();
    }

    public void ClearAllData()
    {
        try
        {
            using var conn = new SqlConnection(_connectionString);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM WorkpieceRecords;
                DELETE FROM LedgerEntries;
                DELETE FROM AlertSettings;
                UPDATE StorageSlots SET IsOccupied = 0, WorkpieceId = NULL;";
            cmd.ExecuteNonQuery();
        }
        catch
        {
            // 清空操作失败时静默处理
        }
    }

    private void EnsureDatabase()
    {
        var masterConn = "Server=DESKTOP-L654TSI;Database=master;User Id=sa;Password=123456;TrustServerCertificate=True;";
        using var conn = new SqlConnection(masterConn);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'LongShenStorage')
            BEGIN
                CREATE DATABASE [LongShenStorage];
            END";
        cmd.ExecuteNonQuery();
    }

    private void EnsureTables()
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AlertSettings' AND xtype='U')
            CREATE TABLE AlertSettings (
                Id INT PRIMARY KEY DEFAULT 1 CHECK (Id = 1),
                MinThreshold INT NOT NULL DEFAULT 2,
                MaxThreshold INT NOT NULL DEFAULT 18
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='StorageSlots' AND xtype='U')
            CREATE TABLE StorageSlots (
                SlotCode NVARCHAR(50) PRIMARY KEY,
                IsOccupied BIT NOT NULL DEFAULT 0,
                WorkpieceId UNIQUEIDENTIFIER NULL,
                Zone NVARCHAR(50) NOT NULL DEFAULT '',
                RowNumber INT NOT NULL,
                ColumnNumber INT NOT NULL,
                LevelNumber INT NOT NULL
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='WorkpieceRecords' AND xtype='U')
            CREATE TABLE WorkpieceRecords (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                InboundTime DATETIME2 NOT NULL DEFAULT GETDATE(),
                SlotCode NVARCHAR(50) NOT NULL,
                LastOperator NVARCHAR(100) NOT NULL DEFAULT '',
                LastUpdated DATETIME2 NOT NULL DEFAULT GETDATE(),
                Notes NVARCHAR(500) NOT NULL DEFAULT '',
                PalletNumber NVARCHAR(50) NOT NULL DEFAULT '',
                ToolingNumber NVARCHAR(200) NOT NULL DEFAULT '',
                ProjectNumber NVARCHAR(200) NOT NULL DEFAULT '',
                ModelType NVARCHAR(200) NOT NULL DEFAULT '',
                WorkOrder NVARCHAR(200) NOT NULL DEFAULT '',
                CellNumber NVARCHAR(200) NOT NULL DEFAULT '',
                ComponentSections INT NOT NULL DEFAULT 1,
                CustomerName NVARCHAR(200) NOT NULL DEFAULT ''
            );

            IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='LedgerEntries' AND xtype='U')
            CREATE TABLE LedgerEntries (
                Id UNIQUEIDENTIFIER PRIMARY KEY,
                Type INT NOT NULL,
                Timestamp DATETIME2 NOT NULL DEFAULT GETDATE(),
                OperatorName NVARCHAR(100) NOT NULL DEFAULT '',
                SlotCode NVARCHAR(50) NOT NULL DEFAULT '',
                ActionDescription NVARCHAR(500) NOT NULL DEFAULT '',
                PalletNumber NVARCHAR(50) NOT NULL DEFAULT '',
                ToolingNumber NVARCHAR(200) NOT NULL DEFAULT '',
                ProjectNumber NVARCHAR(200) NOT NULL DEFAULT '',
                ModelType NVARCHAR(200) NOT NULL DEFAULT '',
                WorkOrder NVARCHAR(200) NOT NULL DEFAULT '',
                CellNumber NVARCHAR(200) NOT NULL DEFAULT '',
                ComponentSections INT NOT NULL DEFAULT 0,
                CustomerName NVARCHAR(200) NOT NULL DEFAULT ''
            );

            -- 向下兼容：给旧表添加缺失的列（如果不存在）
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='PalletNumber')
                ALTER TABLE WorkpieceRecords ADD PalletNumber NVARCHAR(50) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='ToolingNumber')
                ALTER TABLE WorkpieceRecords ADD ToolingNumber NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='ProjectNumber')
                ALTER TABLE WorkpieceRecords ADD ProjectNumber NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='ModelType')
                ALTER TABLE WorkpieceRecords ADD ModelType NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='WorkOrder')
                ALTER TABLE WorkpieceRecords ADD WorkOrder NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='CellNumber')
                ALTER TABLE WorkpieceRecords ADD CellNumber NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='ComponentSections')
                ALTER TABLE WorkpieceRecords ADD ComponentSections INT NOT NULL DEFAULT 1;
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='CustomerName')
                ALTER TABLE WorkpieceRecords ADD CustomerName NVARCHAR(200) NOT NULL DEFAULT '';

            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='PalletNumber')
                ALTER TABLE LedgerEntries ADD PalletNumber NVARCHAR(50) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='ToolingNumber')
                ALTER TABLE LedgerEntries ADD ToolingNumber NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='ProjectNumber')
                ALTER TABLE LedgerEntries ADD ProjectNumber NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='ModelType')
                ALTER TABLE LedgerEntries ADD ModelType NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='WorkOrder')
                ALTER TABLE LedgerEntries ADD WorkOrder NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='CellNumber')
                ALTER TABLE LedgerEntries ADD CellNumber NVARCHAR(200) NOT NULL DEFAULT '';
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='ComponentSections')
                ALTER TABLE LedgerEntries ADD ComponentSections INT NOT NULL DEFAULT 0;
            IF NOT EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='CustomerName')
                ALTER TABLE LedgerEntries ADD CustomerName NVARCHAR(200) NOT NULL DEFAULT '';

            -- 删除旧字段前先删除相关的默认值约束
            DECLARE @sql NVARCHAR(MAX);
            SELECT @sql = COALESCE(@sql + ';', '')
                + 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(parent_object_id) + '].[' + OBJECT_NAME(parent_object_id) + '] DROP CONSTRAINT [' + name + ']'
            FROM sys.default_constraints
            WHERE parent_object_id IN (OBJECT_ID('WorkpieceRecords'), OBJECT_ID('LedgerEntries'))
              AND COL_NAME(parent_object_id, parent_column_id) IN ('Code', 'Batch', 'WorkpieceCode');
            IF @sql IS NOT NULL EXEC sp_executesql @sql;

            IF EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='Code')
                ALTER TABLE WorkpieceRecords DROP COLUMN Code;
            IF EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('WorkpieceRecords') AND name='Batch')
                ALTER TABLE WorkpieceRecords DROP COLUMN Batch;
            IF EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='WorkpieceCode')
                ALTER TABLE LedgerEntries DROP COLUMN WorkpieceCode;
            IF EXISTS (SELECT * FROM syscolumns WHERE id=OBJECT_ID('LedgerEntries') AND name='Batch')
                ALTER TABLE LedgerEntries DROP COLUMN Batch;";
        cmd.ExecuteNonQuery();
    }

    public AppState Load()
    {
        try
        {
            var state = new AppState();
            state.AlertSettings = LoadAlertSettings();
            state.Slots = LoadSlots();
            state.Inventory = LoadInventory();
            state.Ledger = LoadLedger();
            return Normalize(state);
        }
        catch
        {
            return CreateDefaultState();
        }
    }

    public void Save(AppState state)
    {
        state = Normalize(state);
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
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private InventoryAlertSettings LoadAlertSettings()
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT TOP 1 MinThreshold, MaxThreshold FROM AlertSettings", conn);
        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new InventoryAlertSettings
            {
                MinThreshold = reader.GetInt32(0),
                MaxThreshold = reader.GetInt32(1)
            };
        }
        return new InventoryAlertSettings();
    }

    private List<StorageSlot> LoadSlots()
    {
        var slots = new List<StorageSlot>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand("SELECT SlotCode, IsOccupied, WorkpieceId, Zone, RowNumber, ColumnNumber, LevelNumber FROM StorageSlots ORDER BY RowNumber, ColumnNumber, LevelNumber", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            slots.Add(new StorageSlot
            {
                SlotCode = reader.GetString(0),
                IsOccupied = reader.GetBoolean(1),
                WorkpieceId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                Zone = reader.GetString(3),
                RowNumber = reader.GetInt32(4),
                ColumnNumber = reader.GetInt32(5),
                LevelNumber = reader.GetInt32(6)
            });
        }
        return slots;
    }

    private List<WorkpieceRecord> LoadInventory()
    {
        var list = new List<WorkpieceRecord>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(@"SELECT Id, InboundTime, SlotCode, LastOperator, LastUpdated, Notes,
            PalletNumber, ToolingNumber, ProjectNumber, ModelType, WorkOrder, CellNumber, ComponentSections, CustomerName
            FROM WorkpieceRecords ORDER BY InboundTime DESC", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new WorkpieceRecord
            {
                Id = reader.GetGuid(0),
                InboundTime = reader.GetDateTime(1),
                SlotCode = reader.GetString(2),
                LastOperator = reader.GetString(3),
                LastUpdated = reader.GetDateTime(4),
                Notes = reader.GetString(5),
                PalletNumber = reader.GetString(6),
                ToolingNumber = reader.GetString(7),
                ProjectNumber = reader.GetString(8),
                ModelType = reader.GetString(9),
                WorkOrder = reader.GetString(10),
                CellNumber = reader.GetString(11),
                ComponentSections = reader.GetInt32(12),
                CustomerName = reader.GetString(13)
            });
        }
        return list;
    }

    private List<LedgerEntry> LoadLedger()
    {
        var list = new List<LedgerEntry>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = new SqlCommand(@"SELECT Id, Type, Timestamp, OperatorName, SlotCode, ActionDescription,
            PalletNumber, ToolingNumber, ProjectNumber, ModelType, WorkOrder, CellNumber, ComponentSections, CustomerName
            FROM LedgerEntries ORDER BY Timestamp DESC", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(new LedgerEntry
            {
                Id = reader.GetGuid(0),
                Type = (TransactionType)reader.GetInt32(1),
                Timestamp = reader.GetDateTime(2),
                OperatorName = reader.GetString(3),
                SlotCode = reader.GetString(4),
                ActionDescription = reader.GetString(5),
                PalletNumber = reader.GetString(6),
                ToolingNumber = reader.GetString(7),
                ProjectNumber = reader.GetString(8),
                ModelType = reader.GetString(9),
                WorkOrder = reader.GetString(10),
                CellNumber = reader.GetString(11),
                ComponentSections = reader.GetInt32(12),
                CustomerName = reader.GetString(13)
            });
        }
        return list;
    }

    private static void SaveAlertSettings(SqlConnection conn, SqlTransaction tx, InventoryAlertSettings settings)
    {
        using var cmd = new SqlCommand(@"
            MERGE AlertSettings AS target
            USING (SELECT 1 AS Id) AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET MinThreshold = @Min, MaxThreshold = @Max
            WHEN NOT MATCHED THEN
                INSERT (Id, MinThreshold, MaxThreshold) VALUES (1, @Min, @Max);", conn, tx);
        cmd.Parameters.AddWithValue("@Min", settings.MinThreshold);
        cmd.Parameters.AddWithValue("@Max", settings.MaxThreshold);
        cmd.ExecuteNonQuery();
    }

    private static void SaveSlots(SqlConnection conn, SqlTransaction tx, List<StorageSlot> slots)
    {
        using var cmd = new SqlCommand(@"
            MERGE StorageSlots AS target
            USING (SELECT @SlotCode AS SlotCode) AS source
            ON target.SlotCode = source.SlotCode
            WHEN MATCHED THEN
                UPDATE SET IsOccupied = @Occ, WorkpieceId = @Wid, Zone = @Zone, RowNumber = @Row, ColumnNumber = @Col, LevelNumber = @Lev
            WHEN NOT MATCHED THEN
                INSERT (SlotCode, IsOccupied, WorkpieceId, Zone, RowNumber, ColumnNumber, LevelNumber)
                VALUES (@SlotCode, @Occ, @Wid, @Zone, @Row, @Col, @Lev);", conn, tx);

        cmd.Parameters.Add("@SlotCode", System.Data.SqlDbType.NVarChar, 50);
        cmd.Parameters.Add("@Occ", System.Data.SqlDbType.Bit);
        cmd.Parameters.Add("@Wid", System.Data.SqlDbType.UniqueIdentifier);
        cmd.Parameters.Add("@Zone", System.Data.SqlDbType.NVarChar, 50);
        cmd.Parameters.Add("@Row", System.Data.SqlDbType.Int);
        cmd.Parameters.Add("@Col", System.Data.SqlDbType.Int);
        cmd.Parameters.Add("@Lev", System.Data.SqlDbType.Int);

        foreach (var slot in slots)
        {
            cmd.Parameters["@SlotCode"].Value = slot.SlotCode;
            cmd.Parameters["@Occ"].Value = slot.IsOccupied;
            cmd.Parameters["@Wid"].Value = slot.WorkpieceId.HasValue ? (object)slot.WorkpieceId.Value : DBNull.Value;
            cmd.Parameters["@Zone"].Value = slot.Zone;
            cmd.Parameters["@Row"].Value = slot.RowNumber;
            cmd.Parameters["@Col"].Value = slot.ColumnNumber;
            cmd.Parameters["@Lev"].Value = slot.LevelNumber;
            cmd.ExecuteNonQuery();
        }
    }

    private static void SaveInventory(SqlConnection conn, SqlTransaction tx, List<WorkpieceRecord> inventory)
    {
        using var cmd = new SqlCommand(@"
            MERGE WorkpieceRecords AS target
            USING (SELECT @Id AS Id) AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET InboundTime = @InTime, SlotCode = @Slot,
                           LastOperator = @Op, LastUpdated = @Upd, Notes = @Notes,
                           PalletNumber = @Pallet, ToolingNumber = @Tool, ProjectNumber = @Proj,
                           ModelType = @Model, WorkOrder = @Wo, CellNumber = @Cell,
                           ComponentSections = @Comp, CustomerName = @Cust
            WHEN NOT MATCHED THEN
                INSERT (Id, InboundTime, SlotCode, LastOperator, LastUpdated, Notes,
                        PalletNumber, ToolingNumber, ProjectNumber, ModelType, WorkOrder, CellNumber,
                        ComponentSections, CustomerName)
                VALUES (@Id, @InTime, @Slot, @Op, @Upd, @Notes,
                        @Pallet, @Tool, @Proj, @Model, @Wo, @Cell, @Comp, @Cust);", conn, tx);

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
        using var cmd = new SqlCommand(@"
            MERGE LedgerEntries AS target
            USING (SELECT @Id AS Id) AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET Type = @Type, Timestamp = @Ts, OperatorName = @Op,
                           SlotCode = @Slot, ActionDescription = @Desc,
                           PalletNumber = @Pallet, ToolingNumber = @Tool, ProjectNumber = @Proj,
                           ModelType = @Model, WorkOrder = @Wo, CellNumber = @Cell,
                           ComponentSections = @Comp, CustomerName = @Cust
            WHEN NOT MATCHED THEN
                INSERT (Id, Type, Timestamp, OperatorName, SlotCode, ActionDescription,
                        PalletNumber, ToolingNumber, ProjectNumber, ModelType, WorkOrder, CellNumber,
                        ComponentSections, CustomerName)
                VALUES (@Id, @Type, @Ts, @Op, @Slot, @Desc,
                        @Pallet, @Tool, @Proj, @Model, @Wo, @Cell, @Comp, @Cust);", conn, tx);

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

    private static AppState Normalize(AppState state)
    {
        state.Inventory ??= [];
        state.Slots ??= [];
        state.Ledger ??= [];
        state.AlertSettings ??= new InventoryAlertSettings();

        if (!HasExpectedSlotLayout(state.Slots))
        {
            state.Slots = RebuildSlots(state.Inventory);
        }
        else
        {
            SyncSlotsFromInventory(state);
        }

        return state;
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
        {
            for (var column = 1; column <= 4; column++)
            {
                for (var level = 1; level <= 8; level++)
                {
                    slots.Add(new StorageSlot
                    {
                        RowNumber = row,
                        ColumnNumber = column,
                        LevelNumber = level,
                        Zone = $"{row}排",
                        SlotCode = BuildSlotCode(row, column, level),
                        IsOccupied = false
                    });
                }
            }
        }
        return slots;
    }

    private static bool HasExpectedSlotLayout(List<StorageSlot> slots)
    {
        if (slots.Count != 64) return false;
        return slots.All(slot =>
            slot.RowNumber is >= 1 and <= 2 &&
            slot.ColumnNumber is >= 1 and <= 4 &&
            slot.LevelNumber is >= 1 and <= 8 &&
            slot.SlotCode == BuildSlotCode(slot.RowNumber, slot.ColumnNumber, slot.LevelNumber));
    }

    private static List<StorageSlot> RebuildSlots(List<WorkpieceRecord> inventory)
    {
        var slots = CreateDefaultSlots();
        var orderedInventory = inventory.OrderBy(item => item.InboundTime).ToList();
        for (var index = 0; index < orderedInventory.Count && index < slots.Count; index++)
        {
            var record = orderedInventory[index];
            var slot = slots[index];
            slot.IsOccupied = true;
            slot.WorkpieceId = record.Id;
            record.SlotCode = slot.SlotCode;
            record.LastUpdated = DateTime.Now;
        }
        return slots;
    }

    private static void SyncSlotsFromInventory(AppState state)
    {
        foreach (var slot in state.Slots)
        {
            slot.Zone = $"{slot.RowNumber}排";
            slot.SlotCode = BuildSlotCode(slot.RowNumber, slot.ColumnNumber, slot.LevelNumber);
            slot.IsOccupied = false;
            slot.WorkpieceId = null;
        }
        foreach (var record in state.Inventory)
        {
            var slot = state.Slots.FirstOrDefault(item => item.SlotCode == record.SlotCode);
            if (slot is null)
            {
                slot = state.Slots.FirstOrDefault(item => !item.IsOccupied);
                if (slot is null) continue;
                record.SlotCode = slot.SlotCode;
                record.LastUpdated = DateTime.Now;
            }
            slot.IsOccupied = true;
            slot.WorkpieceId = record.Id;
        }
    }

    private static string BuildSlotCode(int row, int column, int level)
    {
        return $"{row}排-{column}列-{level}层";
    }
}
