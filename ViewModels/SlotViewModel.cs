using RamExpert2.Hardware;

namespace RamExpert2.ViewModels;

/// <summary>Una fila "etiqueta: valor" de los paneles de detalles.</summary>
public sealed partial record DetailItem(string Label, string Value);

/// <summary>Una ranura para la fila de módulos y su panel de detalles.</summary>
public sealed partial class SlotViewModel
{
    private const string None = "—";

    public SlotViewModel(MemorySlot slot)
    {
        Slot = slot;
        Details = slot.IsPopulated ? BuildDetails() : [new("Estado", "Libre")];
    }

    public MemorySlot Slot { get; }

    public string Name => $"Ranura {Slot.Index + 1}";

    public string Location
    {
        get
        {
            var parts = new[] { Slot.DeviceLocator, Slot.BankLocator }.Where(p => !string.IsNullOrEmpty(p));
            return parts.Any() ? string.Join(" · ", parts) : None;
        }
    }

    public bool IsPopulated => Slot.IsPopulated;
    public bool IsEmpty => !Slot.IsPopulated;
    public double TileOpacity => IsPopulated ? 1 : 0.6;

    /// <summary>Texto corto bajo el dibujo del módulo: "8 GB DDR4".</summary>
    public string ShortText => IsPopulated ? $"{Units.Size(Slot.SizeBytes)} {Slot.TypeName}" : "Vacía";

    public string Header => IsPopulated ? $"{Name}: {Units.Size(Slot.SizeBytes)} {Slot.TypeName}" : $"{Name}: vacía";

    public IReadOnlyList<DetailItem> Details { get; }

    private List<DetailItem> BuildDetails()
    {
        var tags = new List<string> { Slot.TypeName, Slot.FormFactorName };
        if (Slot.IsSoldered) tags.Add("(soldada)");
        if (Slot.HasEcc) tags.Add("ECC");

        string? rating = SmbiosNames.ModuleRating(Slot.MemoryTypeCode, Slot.SpeedMts);
        string speed = Slot.SpeedMts == 0 ? None : $"{Slot.TypeName}-{Slot.SpeedMts}{(rating is null ? "" : $" ({rating})")}";

        return
        [
            new("Tipo", string.Join(" ", tags)),
            new("Fabricante", Slot.Manufacturer ?? None),
            new("Nº de pieza", Slot.PartNumber ?? None),
            new("Nº de serie", Slot.SerialNumber ?? None),
            new("Velocidad", speed),
            new("Vel. configurada", Units.Speed(Slot.ConfiguredSpeedMts)),
            new("Voltaje", Units.Voltage(Slot.ConfiguredVoltageMv)),
            new("Rangos", Slot.Rank switch { 0 => None, 1 => "1 (simple)", 2 => "2 (doble)", 4 => "4 (cuádruple)", var n => n.ToString() }),
        ];
    }
}
