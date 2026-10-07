namespace RamExpert2.Hardware;

public sealed record SystemInfo(
    string? Manufacturer,
    string? Model,
    string? BoardManufacturer,
    string? BoardModel,
    string? BiosVendor,
    string? BiosVersion,
    string? BiosDate,
    string SmbiosVersion);

/// <summary>Una ranura (o dispositivo de memoria soldado), ocupada o vacía.</summary>
public sealed record MemorySlot
{
    public required int Index { get; init; }
    public string? DeviceLocator { get; init; }
    public string? BankLocator { get; init; }
    public ulong SizeBytes { get; init; }
    public byte MemoryTypeCode { get; init; }
    public required string TypeName { get; init; }
    public byte FormFactorCode { get; init; }
    public required string FormFactorName { get; init; }
    public ushort TypeDetail { get; init; }
    public uint SpeedMts { get; init; }
    public uint ConfiguredSpeedMts { get; init; }
    public string? Manufacturer { get; init; }
    public string? PartNumber { get; init; }
    public string? SerialNumber { get; init; }
    public int Rank { get; init; }
    public uint ConfiguredVoltageMv { get; init; }
    public ushort DataWidth { get; init; }
    public ushort TotalWidth { get; init; }
    public bool IsSoldered { get; init; }

    public bool IsPopulated => SizeBytes > 0;

    // Bits del campo "Type Detail": 13 = registrada, 15 = LRDIMM.
    public bool IsRegistered => (TypeDetail & 0xA000) != 0;
    public bool IsLoadReduced => (TypeDetail & 0x8000) != 0;

    /// <summary>Hay bits extra respecto al ancho de datos: el módulo lleva ECC.</summary>
    public bool HasEcc => DataWidth is > 0 and < 0xFFFF && TotalWidth is > 0 and < 0xFFFF && TotalWidth > DataWidth;
}

public sealed record MemoryReport(SystemInfo System, IReadOnlyList<MemorySlot> Slots, ulong MaxCapacityBytes, string? ErrorCorrection)
{
    public int SlotCount => Slots.Count;
    public int UsedSlots => Slots.Count(s => s.IsPopulated);
    public ulong InstalledBytes => Slots.Aggregate(0UL, (sum, s) => sum + s.SizeBytes);
    public ulong SolderedBytes => Slots.Where(s => s.IsSoldered).Aggregate(0UL, (sum, s) => sum + s.SizeBytes);

    /// <summary>
    /// Capacidad máxima estimada por ranura: lo que queda del máximo tras la memoria soldada,
    /// repartido entre las ranuras donde se puede poner un módulo.
    /// </summary>
    public ulong MaxPerSlotBytes
    {
        get
        {
            int socketed = Slots.Count(s => !s.IsSoldered);
            return socketed > 0 && MaxCapacityBytes > SolderedBytes ? (MaxCapacityBytes - SolderedBytes) / (ulong)socketed : 0;
        }
    }
}

/// <summary>Construye el informe de memoria a partir de la tabla SMBIOS.</summary>
internal static class MemoryReader
{
    public static MemoryReport Read() => Read(SmbiosTable.ReadFromFirmware());

    public static MemoryReport Read(SmbiosTable table)
    {
        // Tipo 16: matrices de memoria física. Solo interesan las de memoria del sistema (uso = 3).
        var arrays = table.OfType(16).Where(a => a.GetByte(0x05) == 0x03).ToList();
        var arrayHandles = arrays.Select(a => a.Handle).ToHashSet();

        ulong maxCapacity = 0;
        foreach (var array in arrays)
            maxCapacity += MaxCapacity(array);

        // Tipo 17: un dispositivo por ranura, esté ocupada o no (WMI solo devuelve las ocupadas).
        var slots = table.OfType(17)
            .Where(d => arrayHandles.Count == 0 || arrayHandles.Contains(d.GetWord(0x04)))
            .Select(ReadSlot)
            .ToList();

        string? errorCorrection = arrays.Count > 0 ? SmbiosNames.ErrorCorrection(arrays[0].GetByte(0x06)) : null;
        return new MemoryReport(ReadSystem(table), slots, maxCapacity, errorCorrection);
    }

    private static SystemInfo ReadSystem(SmbiosTable table)
    {
        var bios = table.FirstOfType(0);
        var system = table.FirstOfType(1);
        var board = table.FirstOfType(2);

        return new SystemInfo(
            Manufacturer: SmbiosNames.Clean(system?.GetString(0x04)),
            Model: SmbiosNames.Clean(system?.GetString(0x05)),
            BoardManufacturer: SmbiosNames.Clean(board?.GetString(0x04)),
            BoardModel: SmbiosNames.Clean(board?.GetString(0x05)),
            BiosVendor: SmbiosNames.Clean(bios?.GetString(0x04)),
            BiosVersion: SmbiosNames.Clean(bios?.GetString(0x05)),
            BiosDate: SmbiosNames.Clean(bios?.GetString(0x08)),
            SmbiosVersion: $"{table.Version.Major}.{table.Version.Minor}");
    }

    private static ulong MaxCapacity(SmbiosStructure array)
    {
        uint kilobytes = array.GetDWord(0x07);
        if (kilobytes == 0x80000000) // El valor real está en el campo extendido, en bytes.
            return array.GetQWord(0x0F);
        return kilobytes * 1024UL;
    }

    private static MemorySlot ReadSlot(SmbiosStructure device, int index)
    {
        byte type = device.GetByte(0x12);
        byte formFactor = device.GetByte(0x0E);

        return new MemorySlot
        {
            Index = index,
            DeviceLocator = SmbiosNames.Clean(device.GetString(0x10)),
            BankLocator = SmbiosNames.Clean(device.GetString(0x11)),
            SizeBytes = Size(device),
            MemoryTypeCode = type,
            TypeName = SmbiosNames.MemoryType(type),
            FormFactorCode = formFactor,
            FormFactorName = SmbiosNames.FormFactor(formFactor),
            TypeDetail = device.GetWord(0x13),
            SpeedMts = Speed(device, 0x15, 0x54),
            ConfiguredSpeedMts = Speed(device, 0x20, 0x58),
            Manufacturer = SmbiosNames.Manufacturer(SmbiosNames.Clean(device.GetString(0x17)), device.GetWord(0x2C)),
            SerialNumber = SmbiosNames.Clean(device.GetString(0x18)),
            PartNumber = SmbiosNames.Clean(device.GetString(0x1A)),
            Rank = device.GetByte(0x1B) & 0x0F,
            ConfiguredVoltageMv = device.GetWord(0x26),
            TotalWidth = device.GetWord(0x08),
            DataWidth = device.GetWord(0x0A),
            // LPDDR va soldada salvo en formato CAMM (0x11).
            IsSoldered = SmbiosNames.IsSolderedFormFactor(formFactor) || (SmbiosNames.IsLowPower(type) && formFactor != 0x11),
        };
    }

    private static ulong Size(SmbiosStructure device)
    {
        const ulong MB = 1024 * 1024;
        ushort size = device.GetWord(0x0C);

        if (size is 0 or 0xFFFF) // Vacía o desconocida
            return 0;
        if (size == 0x7FFF) // 32 GB o más: el tamaño está en el campo extendido, en MB.
            return (device.GetDWord(0x1C) & 0x7FFFFFFF) * MB;
        if ((size & 0x8000) != 0) // Bit 15 activo: el valor está en KB.
            return (size & 0x7FFFUL) * 1024;
        return size * MB;
    }

    private static uint Speed(SmbiosStructure device, int offset, int extendedOffset)
    {
        ushort speed = device.GetWord(offset);
        return speed == 0xFFFF ? device.GetDWord(extendedOffset) & 0x7FFFFFFF : speed;
    }
}
