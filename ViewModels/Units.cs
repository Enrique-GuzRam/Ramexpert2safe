namespace RamExpert2.ViewModels;

internal static class Units
{
    private const double KB = 1024, MB = KB * 1024, GB = MB * 1024, TB = GB * 1024;

    /// <summary>
    /// Tamaño legible en potencias de 1024, como lo muestra Windows ("16 GB", "23,7 GB").
    /// Con <paramref name="oneDecimal"/> siempre lleva un decimal, para que los valores en vivo no bailen.
    /// </summary>
    public static string Size(ulong bytes, bool oneDecimal = false)
    {
        var (value, unit) = bytes switch
        {
            >= (ulong)TB => (bytes / TB, "TB"),
            >= (ulong)GB => (bytes / GB, "GB"),
            >= (ulong)MB => (bytes / MB, "MB"),
            _ => (bytes / KB, "KB"),
        };

        string format = oneDecimal ? "0.0" : value >= 100 || value % 1 == 0 ? "0" : "0.#";
        return $"{value.ToString(format)} {unit}";
    }

    public static string Speed(uint mts) => mts > 0 ? $"{mts} MT/s" : "—";

    public static string Voltage(uint millivolts) => millivolts > 0 ? $"{millivolts / 1000.0:0.00} V" : "—";
}
