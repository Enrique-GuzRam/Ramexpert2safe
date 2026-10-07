using RamExpert2.Hardware;

namespace RamExpert2.ViewModels;

/// <summary>
/// Deduce qué memoria admite el equipo a partir de los módulos instalados y de la BIOS:
/// requisitos de compra, si se puede ampliar y consejos.
/// </summary>
internal sealed class UpgradeAdvisor
{
    private const string InfoGlyph = "\uE946";
    private const string WarningGlyph = "\uE7BA";
    private const string CheckGlyph = "\uE73E";

    private readonly MemoryReport _report;
    private readonly List<MemorySlot> _modules;
    private readonly MemorySlot? _reference;
    private readonly int _free;
    private readonly bool _replaceable;

    public UpgradeAdvisor(MemoryReport report)
    {
        _report = report;
        _modules = report.Slots.Where(s => s.IsPopulated).ToList();
        _free = report.SlotCount - _modules.Count;
        _replaceable = _modules.Any(m => !m.IsSoldered);
        // Módulo de referencia: uno que se pueda cambiar; si todo va soldado, al menos indica el tipo.
        _reference = _modules.FirstOrDefault(m => !m.IsSoldered) ?? _modules.FirstOrDefault();
    }

    private ulong Max => _report.MaxCapacityBytes;
    private ulong Installed => _report.InstalledBytes;
    private ulong PerSlot => _report.MaxPerSlotBytes;
    private bool AtMax => Max > 0 && Installed >= Max;
    private bool CanUpgrade => !AtMax && (_free > 0 || _replaceable);
    private uint Speed => _modules.Count > 0 ? _modules.Max(m => m.SpeedMts) : 0;
    private string FreeSlots => _free == 1 ? "1 ranura libre" : $"{_free} ranuras libres";

    /// <summary>Resumen de una línea: si se puede ampliar y cómo.</summary>
    public string Verdict()
    {
        if (_modules.Count == 0)
            return "No se encontraron módulos de memoria.";
        if (AtMax)
            return "Ya tienes la capacidad máxima que admite el equipo.";
        if (_free == 0 && !_replaceable)
            return "La memoria va soldada y no hay ranuras libres: no se puede ampliar.";
        if (Max == 0)
            return _free > 0 ? $"Tienes {FreeSlots}, pero la BIOS no indica la capacidad máxima." : "La BIOS no indica la capacidad máxima.";
        if (_free > 0)
        {
            ulong addable = Math.Min(Max - Installed, PerSlot * (ulong)_free);
            return $"Puedes añadir hasta {Units.Size(addable)} en {(_free == 1 ? "la ranura libre" : $"las {_free} ranuras libres")}.";
        }
        return $"Ranuras llenas: para ampliar, sustituye módulos por otros de hasta {Units.Size(PerSlot)}.";
    }

    /// <summary>Características que debe cumplir la memoria que se compre.</summary>
    public List<DetailItem> Requirements()
    {
        // Sin módulos, o todo soldado y sin ranuras libres: no hay nada que comprar.
        if (_reference is not { } module || (_free == 0 && !_replaceable))
            return [];

        var items = new List<DetailItem>();
        if (BestConfiguration() is { } best)
            items.Add(new("Mejor opción", best));
        items.Add(new("Tipo", TypeName(module)));
        items.Add(new("Formato", FormFactor(module)));
        items.Add(new("Velocidad mín.", SpeedRequirement(module)));
        items.Add(new("Voltaje", Units.Voltage(module.ConfiguredVoltageMv)));
        items.Add(new("Módulo", ModuleKind()));
        items.Add(new("Por módulo", PerSlot > 0 ? $"Hasta {Units.Size(PerSlot)}" : "Desconocido"));
        return items;
    }

    /// <summary>Texto para buscar módulos compatibles en internet ("DDR4 SO-DIMM 3200 32GB"), o null si no se puede ampliar.</summary>
    public string? SearchQuery()
    {
        if (_reference is not { } module || !CanUpgrade)
            return null;

        var parts = new List<string> { TypeName(module) };
        if (!module.IsSoldered && module.FormFactorCode is 0x09 or 0x0D)
            parts.Add(module.FormFactorName);
        if (Speed > 0)
            parts.Add(Speed.ToString());
        if (PerSlot > 0)
            parts.Add(Units.Size(PerSlot).Replace(" ", ""));
        return string.Join(" ", parts);
    }

    public List<NoteItem> Notes()
    {
        var notes = new List<NoteItem>();
        if (_modules.Count == 0)
            return notes;

        if (AtMax)
        {
            notes.Add(new(CheckGlyph, $"Ya tienes instalada la capacidad máxima que admite el equipo ({Units.Size(Max)})."));
        }
        else if (_free == 0 && !_replaceable)
        {
            notes.Add(new(WarningGlyph, "No hay ranuras libres y la memoria va soldada a la placa, así que no se puede ampliar."));
        }
        else if (Max == 0)
        {
            string prefix = _free > 0 ? $"Tienes {FreeSlots}, pero la" : "La";
            notes.Add(new(InfoGlyph, $"{prefix} BIOS no indica la capacidad máxima de memoria. Consúltala en el manual de tu placa base o portátil."));
        }
        else if (_free > 0)
        {
            ulong addable = Math.Min(Max - Installed, PerSlot * (ulong)_free);
            notes.Add(new(InfoGlyph, $"Tienes {FreeSlots}: puedes añadir hasta {Units.Size(addable)} más (máximo {Units.Size(PerSlot)} por ranura y {Units.Size(Max)} en total)."));
        }
        else
        {
            notes.Add(new(InfoGlyph, $"Todas las ranuras están ocupadas. Para ampliar tendrías que sustituir algún módulo por otro de más capacidad (máximo {Units.Size(PerSlot)} por ranura y {Units.Size(Max)} en total)."));
        }

        if (_report.SolderedBytes > 0 && (_free > 0 || _replaceable))
        {
            notes.Add(new(WarningGlyph, $"Hay {Units.Size(_report.SolderedBytes)} soldados a la placa: esa memoria no se puede cambiar."));
        }

        if (CanUpgrade)
        {
            notes.Add(new(InfoGlyph, "Un módulo más rápido que la velocidad mínima también sirve, pero funcionará a la velocidad que admita el equipo."));
        }

        if (_reference?.FormFactorCode is 0x09 or 0x0D && CanUpgrade)
        {
            notes.Add(new(InfoGlyph, "SO-DIMM es el formato corto de los portátiles y mini PC; DIMM es el largo de los PC de sobremesa. No son intercambiables."));
        }

        if (_modules.Count == 1 && _free > 0)
        {
            notes.Add(new(InfoGlyph, "Solo hay un módulo, así que la memoria funciona en un solo canal. Añadir otro de la misma capacidad activaría el doble canal y mejoraría el rendimiento, sobre todo con gráficos integrados."));
        }
        else if (_modules.Select(m => m.SizeBytes).Distinct().Count() > 1)
        {
            notes.Add(new(InfoGlyph, "Los módulos tienen capacidades distintas: el doble canal solo cubre una parte de la memoria y el resto funciona en un solo canal. Con módulos iguales se aprovecha al máximo."));
        }

        if (_modules.Select(m => m.SpeedMts).Where(s => s > 0).Distinct().Count() > 1)
        {
            notes.Add(new(InfoGlyph, "Los módulos tienen velocidades distintas: todos funcionan a la velocidad del más lento."));
        }

        if (Max > 0 && (_free > 0 || _replaceable))
        {
            notes.Add(new(InfoGlyph, "La capacidad máxima la indica la BIOS y a veces es un valor teórico. Antes de comprar, confírmala en la web o el manual del fabricante."));
        }

        return notes;
    }

    /// <summary>La ampliación más grande posible, p. ej. "2 × 32 GB = 64 GB".</summary>
    private string? BestConfiguration()
    {
        int socketed = _report.Slots.Count(s => !s.IsSoldered);
        if (PerSlot == 0 || socketed == 0)
            return null;

        ulong total = _report.SolderedBytes + PerSlot * (ulong)socketed;
        string modules = $"{socketed} × {Units.Size(PerSlot)} = {Units.Size(total)}";
        return _report.SolderedBytes > 0 ? $"{Units.Size(_report.SolderedBytes)} soldados + {modules}" : modules;
    }

    // DDR3 a 1,35 V es DDR3L: en portátiles no se puede mezclar con DDR3 de 1,5 V.
    private static string TypeName(MemorySlot module) =>
        module.MemoryTypeCode == 0x18 && module.ConfiguredVoltageMv is > 0 and <= 1350 ? "DDR3L" : module.TypeName;

    private static string FormFactor(MemorySlot module)
    {
        if (module.IsSoldered)
            return "Consulta el manual (la instalada va soldada)";

        return SmbiosNames.PinCount(module.MemoryTypeCode, module.FormFactorCode) is { } pins
            ? $"{module.FormFactorName} de {pins} pines"
            : module.FormFactorName;
    }

    private string SpeedRequirement(MemorySlot module)
    {
        if (Speed == 0)
            return "Desconocida";
        string type = TypeName(module);
        string? rating = SmbiosNames.ModuleRating(module.MemoryTypeCode, Speed);
        if (type == "DDR3L")
            rating = rating?.Replace("PC3-", "PC3L-");
        return $"{type}-{Speed}{(rating is null ? "" : $" ({rating})")}";
    }

    private string ModuleKind()
    {
        string buffer = _modules.Any(m => m.IsLoadReduced) ? "LRDIMM"
            : _modules.Any(m => m.IsRegistered) ? "Registrada (RDIMM)"
            : "Sin búfer";
        string ecc = _modules.Any(m => m.HasEcc) ? "con ECC" : "sin ECC";
        return $"{buffer}, {ecc}";
    }
}
