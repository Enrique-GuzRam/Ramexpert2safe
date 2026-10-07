using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using RamExpert2.Hardware;

namespace RamExpert2.ViewModels;

/// <summary>Una cifra del resumen; <see cref="Detail"/> se muestra como información sobre herramientas.</summary>
public sealed partial record SummaryItem(string Title, string Value, string Detail);

public sealed partial record NoteItem(string Glyph, string Text);

public sealed partial class MainViewModel : INotifyPropertyChanged
{
    /// <summary>Muestras del historial de uso (una por segundo).</summary>
    public const int HistoryLength = 60;

    private readonly Queue<double> _history = new();
    private readonly StatItem _inUse = new("En uso");
    private readonly StatItem _available = new("Disponible");
    private readonly StatItem _commit = new("Confirmada", "Memoria comprometida: lo que Windows tiene reservado entre la RAM y el archivo de paginación");

    private SlotViewModel? _selectedSlot;
    private double _loadPercent;
    private string _usageText = "";

    public MainViewModel()
    {
        try
        {
            Report = MemoryReader.Read();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }

        // La memoria utilizable no cambia mientras el equipo está encendido.
        var status = MemoryStatus.Read();
        ulong installed = Report is { InstalledBytes: > 0 } ? Report.InstalledBytes : MemoryStatus.ReadInstalledBytes();
        InstalledText = installed > 0 ? Units.Size(installed) : "—";
        UsableText = Units.Size(status.TotalPhys, oneDecimal: true);
        ReservedText = installed > status.TotalPhys ? Units.Size(installed - status.TotalPhys) : "—";

        var system = Report?.System;
        SystemLine = Join(system?.Manufacturer, system?.Model) ?? Join(system?.BoardManufacturer, system?.BoardModel) ?? "Equipo desconocido";

        Stats =
        [
            _inUse,
            _available,
            _commit,
            new("Instalada") { Value = InstalledText },
            new("Utilizable") { Value = UsableText },
            new("Reservada por hardware", "Memoria que el firmware o la gráfica integrada reservan y Windows no puede usar") { Value = ReservedText },
        ];

        Slots = Report?.Slots.Select(s => new SlotViewModel(s)).ToList() ?? [];
        _selectedSlot = Slots.FirstOrDefault(s => s.IsPopulated) ?? Slots.FirstOrDefault();
        Summary = BuildSummary();
        SystemDetails = BuildSystemDetails();

        if (Report is not null)
        {
            var advisor = new UpgradeAdvisor(Report);
            UpgradeVerdict = advisor.Verdict();
            Requirements = advisor.Requirements();
            SearchQuery = advisor.SearchQuery();
            UpgradeNotes = advisor.Notes();
        }

        RefreshUsage();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public MemoryReport? Report { get; }
    public string? ErrorMessage { get; }
    public bool HasError => ErrorMessage is not null;

    public string SystemLine { get; }
    public IReadOnlyList<SummaryItem> Summary { get; }
    public IReadOnlyList<SlotViewModel> Slots { get; }
    public IReadOnlyList<DetailItem> SystemDetails { get; }

    public string UpgradeVerdict { get; } = "";
    public IReadOnlyList<DetailItem> Requirements { get; } = [];
    public IReadOnlyList<NoteItem> UpgradeNotes { get; } = [];
    public string? SearchQuery { get; }
    public bool CanSearch => SearchQuery is not null;

    public string InstalledText { get; }
    public string UsableText { get; }
    public string ReservedText { get; }

    public SlotViewModel? SelectedSlot
    {
        get => _selectedSlot;
        set => SetField(ref _selectedSlot, value);
    }

    // --- Uso en vivo ---

    /// <summary>Cifras del diálogo de uso: las tres primeras cambian en vivo.</summary>
    public IReadOnlyList<StatItem> Stats { get; }
    public IReadOnlyCollection<double> History => _history;

    public double LoadPercent { get => _loadPercent; private set => SetField(ref _loadPercent, value); }
    public string UsageText { get => _usageText; private set => SetField(ref _usageText, value); }
    public string LoadText => $"{LoadPercent:0} %";

    public void RefreshUsage()
    {
        var status = MemoryStatus.Read();

        LoadPercent = status.LoadPercent;
        UsageText = $"{Units.Size(status.UsedPhys, oneDecimal: true)} de {UsableText}  ·  {LoadText}";
        OnPropertyChanged(nameof(LoadText));
        _inUse.Value = Units.Size(status.UsedPhys, oneDecimal: true);
        _available.Value = Units.Size(status.AvailPhys, oneDecimal: true);
        _commit.Value = $"{Units.Size(status.CommitUsed, oneDecimal: true)} de {Units.Size(status.CommitLimit, oneDecimal: true)}";

        _history.Enqueue(status.LoadPercent);
        while (_history.Count > HistoryLength)
            _history.Dequeue();
    }

    private List<SummaryItem> BuildSummary()
    {
        var items = new List<SummaryItem> { new("Instalada", InstalledText, $"Utilizable por Windows: {UsableText}") };
        if (Report is null)
            return items;

        int free = Report.SlotCount - Report.UsedSlots;
        items.Add(new("Ranuras", $"{Report.UsedSlots} de {Report.SlotCount}",
            free switch { 0 => "No quedan ranuras libres", 1 => "1 ranura libre", _ => $"{free} ranuras libres" }));

        ulong max = Report.MaxCapacityBytes;
        ulong perSlot = Report.MaxPerSlotBytes;
        items.Add(new("Máximo", max > 0 ? Units.Size(max) : "—",
            max == 0 ? "La BIOS no indica la capacidad máxima" : perSlot > 0 ? $"Hasta {Units.Size(perSlot)} por ranura" : ""));

        var modules = Report.Slots.Where(s => s.IsPopulated).ToList();
        if (modules.Count > 0)
        {
            uint speed = modules.Max(m => m.ConfiguredSpeedMts);
            string type = string.Join("/", modules.Select(m => m.TypeName).Distinct());
            items.Add(new("Tipo", speed > 0 ? $"{type}-{speed}" : type, string.Join(" · ", modules.Select(m => m.FormFactorName).Distinct())));
        }

        return items;
    }

    private List<DetailItem> BuildSystemDetails()
    {
        var system = Report?.System;
        return
        [
            new("Equipo", Join(system?.Manufacturer, system?.Model) ?? "—"),
            new("Placa base", Join(system?.BoardManufacturer, system?.BoardModel) ?? "—"),
            new("BIOS", Join(system?.BiosVendor, system?.BiosVersion) ?? "—"),
            new("Fecha de la BIOS", system?.BiosDate ?? "—"),
            new("Versión SMBIOS", system?.SmbiosVersion ?? "—"),
            new("Ranuras", Report is null ? "—" : $"{Report.SlotCount} ({Report.UsedSlots} ocupadas)"),
            new("Capacidad máxima", Report is { MaxCapacityBytes: > 0 } ? Units.Size(Report.MaxCapacityBytes) : "—"),
            new("Corrección de errores", Report?.ErrorCorrection ?? "—"),
            new("Instalada", InstalledText),
            new("Utilizable", UsableText),
            new("Reservada por hardware", ReservedText),
        ];
    }

    /// <summary>Requisitos de compra en texto, para pegarlos en una tienda o un mensaje.</summary>
    public string BuildRequirementsText()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Requisitos de memoria RAM para {SystemLine}");
        foreach (var item in Requirements)
            AppendLine(sb, item.Label, item.Value);
        if (Report is not null)
        {
            AppendLine(sb, "Capacidad máxima", Report.MaxCapacityBytes > 0 ? Units.Size(Report.MaxCapacityBytes) : null);
            AppendLine(sb, "Ranuras libres", $"{Report.SlotCount - Report.UsedSlots} de {Report.SlotCount}");
        }
        sb.AppendLine();
        sb.AppendLine(UpgradeVerdict);
        return sb.ToString();
    }

    /// <summary>Informe en texto plano con todos los datos, para copiar o guardar.</summary>
    public string BuildReport()
    {
        var sb = new StringBuilder();
        sb.AppendLine("RamExpert2 — Informe de memoria");
        sb.AppendLine($"Generado: {DateTime.Now:g}");
        sb.AppendLine();

        foreach (var item in SystemDetails)
            AppendLine(sb, item.Label, item.Value);
        AppendLine(sb, "En uso", UsageText);

        foreach (var slot in Slots)
        {
            sb.AppendLine();
            sb.AppendLine($"[{slot.Header}]");
            AppendLine(sb, "  Ubicación", slot.Location);
            foreach (var item in slot.Details)
                AppendLine(sb, "  " + item.Label, item.Value);
        }

        if (Requirements.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Requisitos para ampliar");
            foreach (var item in Requirements)
                AppendLine(sb, "  " + item.Label, item.Value);
            sb.AppendLine("  " + UpgradeVerdict);
        }

        if (UpgradeNotes.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Consejos");
            foreach (var note in UpgradeNotes)
                sb.AppendLine($"- {note.Text}");
        }

        return sb.ToString();
    }

    private static void AppendLine(StringBuilder sb, string label, string? value) =>
        sb.AppendLine($"{(label + ":").PadRight(26)}{value ?? "—"}");

    private static string? Join(params string?[] parts)
    {
        var present = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return present.Count > 0 ? string.Join(" ", present) : null;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return;
        field = value;
        OnPropertyChanged(propertyName);
    }

    private void OnPropertyChanged(string? propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
