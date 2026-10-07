using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using RamExpert2.Hardware;
using Windows.Graphics;

namespace RamExpert2;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico"));
        PlaceWindow();

        RootFrame.Navigate(typeof(MainPage));
    }

    /// <summary>Ventana compacta de 740×680 (escalada según el DPI), centrada en la pantalla.</summary>
    private void PlaceWindow()
    {
        double scale = NativeMethods.GetDpiForWindow(Win32Interop.GetWindowFromWindowId(AppWindow.Id)) / 96.0;
        var workArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary).WorkArea;

        int width = Math.Min((int)(740 * scale), workArea.Width);
        int height = Math.Min((int)(680 * scale), workArea.Height);
        AppWindow.MoveAndResize(new RectInt32(
            workArea.X + (workArea.Width - width) / 2,
            workArea.Y + (workArea.Height - height) / 2,
            width,
            height));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(460 * scale);
            presenter.PreferredMinimumHeight = (int)(420 * scale);
        }
    }
}
