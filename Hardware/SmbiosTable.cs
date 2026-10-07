using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace RamExpert2.Hardware;

/// <summary>
/// Una estructura SMBIOS: el área formateada (cabecera incluida) y sus cadenas.
/// Los campos que no existen en la versión de SMBIOS del equipo se leen como 0.
/// </summary>
internal sealed class SmbiosStructure(byte[] data, string[] strings)
{
    public byte Type => data[0];
    public ushort Handle => BitConverter.ToUInt16(data, 2);

    public bool Has(int offset, int size) => offset + size <= data.Length;

    public byte GetByte(int offset) => Has(offset, 1) ? data[offset] : (byte)0;
    public ushort GetWord(int offset) => Has(offset, 2) ? BitConverter.ToUInt16(data, offset) : (ushort)0;
    public uint GetDWord(int offset) => Has(offset, 4) ? BitConverter.ToUInt32(data, offset) : 0;
    public ulong GetQWord(int offset) => Has(offset, 8) ? BitConverter.ToUInt64(data, offset) : 0;

    /// <summary>Lee un campo de tipo cadena: el byte es un índice (base 1) a la lista de cadenas.</summary>
    public string? GetString(int offset)
    {
        int index = GetByte(offset);
        return index > 0 && index <= strings.Length ? strings[index - 1] : null;
    }
}

/// <summary>Tabla SMBIOS del firmware, la misma fuente que usan RamExpert o CPU-Z.</summary>
internal sealed class SmbiosTable
{
    private SmbiosTable(Version version, List<SmbiosStructure> structures)
    {
        Version = version;
        Structures = structures;
    }

    public Version Version { get; }
    public IReadOnlyList<SmbiosStructure> Structures { get; }

    public IEnumerable<SmbiosStructure> OfType(byte type) => Structures.Where(s => s.Type == type);

    public SmbiosStructure? FirstOfType(byte type) => Structures.FirstOrDefault(s => s.Type == type);

    public static SmbiosTable ReadFromFirmware()
    {
        uint size = NativeMethods.GetSystemFirmwareTable(NativeMethods.RSMB, 0, null, 0);
        if (size == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "El firmware no expone la tabla SMBIOS.");

        var buffer = new byte[size];
        if (NativeMethods.GetSystemFirmwareTable(NativeMethods.RSMB, 0, buffer, size) == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());

        return Parse(buffer);
    }

    /// <summary>
    /// Interpreta un bloque RawSMBIOSData: cabecera de 8 bytes (versión y longitud)
    /// seguida de las estructuras, una detrás de otra.
    /// </summary>
    public static SmbiosTable Parse(byte[] raw)
    {
        if (raw.Length < 8)
            throw new InvalidDataException("La tabla SMBIOS está incompleta.");

        var version = new Version(raw[1], raw[2]);
        int end = (int)Math.Min(8L + BitConverter.ToUInt32(raw, 4), raw.Length);
        var structures = new List<SmbiosStructure>();

        int offset = 8;
        while (offset + 4 <= end)
        {
            byte type = raw[offset];
            byte length = raw[offset + 1];
            if (length < 4 || offset + length > end)
                break;

            var (strings, next) = ReadStrings(raw, offset + length, end);
            structures.Add(new SmbiosStructure(raw.AsSpan(offset, length).ToArray(), strings));

            if (type == 127) // Fin de tabla
                break;
            offset = next;
        }

        return new SmbiosTable(version, structures);
    }

    // Tras el área formateada vienen las cadenas, cada una terminada en \0, y un \0 extra
    // que cierra el conjunto. Si la estructura no tiene cadenas, son simplemente \0\0.
    private static (string[] Strings, int Next) ReadStrings(byte[] raw, int offset, int end)
    {
        var strings = new List<string>();
        int pos = offset;
        while (pos < end)
        {
            int start = pos;
            while (pos < end && raw[pos] != 0)
                pos++;
            if (pos == start)
            {
                pos++;
                break;
            }
            strings.Add(Encoding.UTF8.GetString(raw, start, pos - start));
            pos++;
        }

        if (strings.Count == 0 && pos < end && raw[pos] == 0)
            pos++;

        return (strings.ToArray(), pos);
    }
}
