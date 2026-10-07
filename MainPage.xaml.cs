using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;
using RamExpert2.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Launcher = Windows.System.Launcher;

namespace RamExpert2;

public sealed partial class MainPage : Page
{
    private readonly DispatcherQueueTimer _usageTimer;
    private readonly DispatcherQueueTimer _statusTimer;
    private bool _dialogOpen;

    public MainPage()
    {
        InitializeComponent();

        _usageTimer = DispatcherQueue.CreateTimer();
        _usageTimer.Interval = TimeSpan.FromSeconds(1);
        _usageTimer.Tick += (_, _) =>
        {
            ViewModel.RefreshUsage();
            DrawHistory();
        };

        _statusTimer = DispatcherQueue.CreateTimer();
        _statusTimer.Interval = TimeSpan.FromSeconds(3);
        _statusTimer.IsRepeating = false;
        _statusTimer.Tick += (_, _) => StatusBar.IsOpen = false;

        Loaded += (_, _) =>
        {
            SlotsView.SelectedItem = ViewModel.SelectedSlot;
            _usageTimer.Start();
        };
        Unloaded += (_, _) => _usageTimer.Stop();
    }

    public MainViewModel ViewModel { get; } = new();

    private Microsoft.UI.WindowId WindowId => XamlRoot.ContentIslandEnvironment.AppWindowId;

    // --- Archivo ---

    private void CopyReport_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard(ViewModel.BuildReport());
        ShowStatus("Informe copiado al portapapeles");
    }

    private async void SaveReport_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileSavePicker(WindowId)
        {
            SuggestedFileName = "Informe de memoria",
            DefaultFileExtension = ".txt",
        };
        picker.FileTypeChoices.Add("Texto", [".txt"]);

        var file = await picker.PickSaveFileAsync();
        if (file is null)
            return;

        try
        {
            await File.WriteAllTextAsync(file.Path, ViewModel.BuildReport());
            ShowStatus($"Informe guardado en {Path.GetFileName(file.Path)}");
        }
        catch (Exception ex)
        {
            ShowStatus($"No se pudo guardar el informe: {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Exit();

    // --- Ver y Ayuda ---

    private async void ShowUsage_Click(object sender, RoutedEventArgs e) => await ShowDialogAsync(UsageDialog);

    private async void ShowSystem_Click(object sender, RoutedEventArgs e) => await ShowDialogAsync(SystemDialog);

    private async void ShowTips_Click(object sender, RoutedEventArgs e) => await ShowDialogAsync(TipsDialog);

    private async void About_Click(object sender, RoutedEventArgs e) => await ShowDialogAsync(AboutDialog);

    private void AlwaysOnTop_Click(object sender, RoutedEventArgs e)
    {
        if (AppWindow.GetFromWindowId(WindowId).Presenter is OverlappedPresenter presenter)
            presenter.IsAlwaysOnTop = AlwaysOnTopItem.IsChecked;
    }

    private async Task ShowDialogAsync(ContentDialog dialog)
    {
        // Solo puede haber un diálogo abierto (los atajos de teclado podrían abrir otro encima).
        if (_dialogOpen)
            return;

        _dialogOpen = true;
        try
        {
            dialog.XamlRoot = XamlRoot;
            await dialog.ShowAsync();
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    // --- Ranuras y requisitos ---

    private void SlotsView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SlotsView.SelectedItem is SlotViewModel slot)
            ViewModel.SelectedSlot = slot;
        else if (ViewModel.SelectedSlot is not null)
            SlotsView.SelectedItem = ViewModel.SelectedSlot; // Siempre queda una ranura seleccionada.
    }

    private void CopyRequirements_Click(object sender, RoutedEventArgs e)
    {
        CopyToClipboard(ViewModel.BuildRequirementsText());
        ShowStatus("Requisitos copiados al portapapeles");
    }

    private async void SearchModules_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SearchQuery is { } query)
            await Launcher.LaunchUriAsync(new Uri("https://www.google.com/search?q=" + Uri.EscapeDataString(query)));
    }

    // --- Utilidades ---

    private static void CopyToClipboard(string text)
    {
        var package = new DataPackage();
        package.SetText(text);
        Clipboard.SetContent(package);
    }

    private void ShowStatus(string message, InfoBarSeverity severity = InfoBarSeverity.Success)
    {
        StatusBar.Title = message;
        StatusBar.Severity = severity;
        StatusBar.IsOpen = true;
        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private void HistoryHost_SizeChanged(object sender, SizeChangedEventArgs e) => DrawHistory();

    /// <summary>Dibuja el historial de uso como una línea con relleno, alineada a la derecha.</summary>
    private void DrawHistory()
    {
        double width = HistoryHost.ActualWidth;
        double height = HistoryHost.ActualHeight;
        if (width <= 0 || height <= 0 || ViewModel.History.Count == 0)
            return;

        double step = width / (MainViewModel.HistoryLength - 1);
        double x = width - step * (ViewModel.History.Count - 1);

        var line = new PointCollection();
        var area = new PointCollection { new Point(x, height) };
        foreach (double percent in ViewModel.History)
        {
            var point = new Point(x, height - percent / 100 * height);
            line.Add(point);
            area.Add(point);
            x += step;
        }
        area.Add(new Point(width, height));

        HistoryLine.Points = line;
        HistoryFill.Points = area;
    }
}
