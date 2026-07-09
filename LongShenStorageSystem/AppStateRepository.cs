using System.Text.Json;

namespace LongShenStorageSystem;

public sealed class AppStateRepository
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true
    };

    public AppStateRepository()
    {
        var dataDirectory = Path.Combine(AppContext.BaseDirectory, "Data");
        Directory.CreateDirectory(dataDirectory);
        _filePath = Path.Combine(dataDirectory, "app-state.json");
    }

    public AppState Load()
    {
        if (!File.Exists(_filePath))
        {
            return CreateDefaultState();
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var state = JsonSerializer.Deserialize<AppState>(json, _jsonOptions);
            return state is null ? CreateDefaultState() : Normalize(state);
        }
        catch
        {
            return CreateDefaultState();
        }
    }

    public void Save(AppState state)
    {
        var normalized = Normalize(state);
        var json = JsonSerializer.Serialize(normalized, _jsonOptions);
        File.WriteAllText(_filePath, json);
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
        if (slots.Count != 64)
        {
            return false;
        }

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
                if (slot is null)
                {
                    continue;
                }

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