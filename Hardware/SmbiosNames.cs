using System.Text.RegularExpressions;

namespace RamExpert2.Hardware;

/// <summary>Traducción de los códigos SMBIOS a texto legible.</summary>
internal static partial class SmbiosNames
{
    public static string MemoryType(byte value) => value switch
    {
        0x01 => "Otro",
        0x03 => "DRAM",
        0x04 => "EDRAM",
        0x05 => "VRAM",
        0x06 => "SRAM",
        0x07 => "RAM",
        0x08 => "ROM",
        0x09 => "Flash",
        0x0A => "EEPROM",
        0x0B => "FEPROM",
        0x0C => "EPROM",
        0x0D => "CDRAM",
        0x0E => "3DRAM",
        0x0F => "SDRAM",
        0x10 => "SGRAM",
        0x11 => "RDRAM",
        0x12 => "DDR",
        0x13 => "DDR2",
        0x14 => "DDR2 FB-DIMM",
        0x18 => "DDR3",
        0x19 => "FBD2",
        0x1A => "DDR4",
        0x1B => "LPDDR",
        0x1C => "LPDDR2",
        0x1D => "LPDDR3",
        0x1E => "LPDDR4",
        0x1F => "No volátil",
        0x20 => "HBM",
        0x21 => "HBM2",
        0x22 => "DDR5",
        0x23 => "LPDDR5",
        0x24 => "HBM3",
        _ => "Desconocido",
    };

    public static bool IsLowPower(byte memoryType) => memoryType is 0x1B or 0x1C or 0x1D or 0x1E or 0x23;

    public static string FormFactor(byte value) => value switch
    {
        0x01 => "Otro",
        0x03 => "SIMM",
        0x04 => "SIP",
        0x05 => "Chip",
        0x06 => "DIP",
        0x07 => "ZIP",
        0x08 => "Tarjeta propietaria",
        0x09 => "DIMM",
        0x0A => "TSOP",
        0x0B => "Fila de chips",
        0x0C => "RIMM",
        0x0D => "SO-DIMM",
        0x0E => "SRIMM",
        0x0F => "FB-DIMM",
        0x10 => "Die",
        0x11 => "CAMM",
        _ => "Desconocido",
    };

    /// <summary>Formatos que van soldados a la placa y no se pueden cambiar.</summary>
    public static bool IsSolderedFormFactor(byte value) => value is 0x05 or 0x0B or 0x10;

    /// <summary>Número de contactos del módulo según su tipo y formato, o null si no aplica.</summary>
    public static int? PinCount(byte memoryType, byte formFactor) => (formFactor, memoryType) switch
    {
        (0x09, 0x0F) => 168,
        (0x09, 0x12) => 184,
        (0x09, 0x13 or 0x18) => 240,
        (0x09, 0x1A or 0x22) => 288,
        (0x0D, 0x0F) => 144,
        (0x0D, 0x12 or 0x13) => 200,
        (0x0D, 0x18) => 204,
        (0x0D, 0x1A) => 260,
        (0x0D, 0x22) => 262,
        _ => null,
    };

    /// <summary>
    /// Nombre comercial del módulo, por ejemplo "PC4-25600" para DDR4-3200:
    /// MT/s × 8 bytes, redondeado a la centena inferior (DDR4-2666 → PC4-21300).
    /// </summary>
    public static string? ModuleRating(byte memoryType, uint mts)
    {
        string? prefix = memoryType switch
        {
            0x13 => "PC2",
            0x18 => "PC3",
            0x1A => "PC4",
            0x22 => "PC5",
            _ => null,
        };
        return prefix is null || mts == 0 ? null : $"{prefix}-{mts * 8 / 100 * 100}";
    }

    public static string ErrorCorrection(byte value) => value switch
    {
        0x01 => "Otra",
        0x03 => "Ninguna",
        0x04 => "Paridad",
        0x05 => "ECC de un bit",
        0x06 => "ECC multibit",
        0x07 => "CRC",
        _ => "Desconocida",
    };

    // Fabricantes por código JEDEC (JEP106). Clave: (nº de códigos de continuación << 8) | código.
    private static readonly Dictionary<int, string> JedecManufacturers = new()
    {
        [0x002C] = "Micron",
        [0x00AD] = "SK hynix",
        [0x00CE] = "Samsung",
        [0x0198] = "Kingston",
        [0x029E] = "Corsair",
        [0x04CB] = "ADATA",
        [0x04CD] = "G.Skill",
        [0x04EF] = "Team Group",
        [0x059B] = "Crucial",
    };

    /// <summary>
    /// Nombre del fabricante. Muchas BIOS guardan el código JEDEC en hexadecimal ("80CE000080CE",
    /// "CE00000000000000"...) en lugar del nombre; en ese caso, o si no hay cadena, se traduce el código.
    /// </summary>
    public static string? Manufacturer(string? text, ushort moduleManufacturerId)
    {
        if (text is not null && HexCode().IsMatch(text))
        {
            var hex = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
            byte first = Convert.ToByte(hex[..2], 16);
            byte second = Convert.ToByte(hex[2..4], 16);
            // Según la BIOS, el byte de banco va delante o detrás del código.
            return LookupJedec(first, second) ?? LookupJedec(second, first) ?? text;
        }

        if (text is null && moduleManufacturerId != 0)
            return LookupJedec((byte)(moduleManufacturerId & 0xFF), (byte)(moduleManufacturerId >> 8));

        return text;
    }

    private static string? LookupJedec(byte bank, byte code)
    {
        // El bit 7 del byte de banco es de paridad.
        int key = ((bank & 0x7F) << 8) | code;
        return JedecManufacturers.GetValueOrDefault(key);
    }

    /// <summary>Limpia una cadena SMBIOS y descarta los valores de relleno que dejan muchas BIOS.</summary>
    public static string? Clean(string? value)
    {
        value = value?.Trim();
        if (string.IsNullOrEmpty(value) || Placeholder().IsMatch(value) || value.All(c => c is '0' or 'F' or 'f' or ' '))
            return null;
        return value;
    }

    [GeneratedRegex(@"^(0x)?[0-9A-F]{4}([0-9A-F]{4}){0,3}$", RegexOptions.IgnoreCase)]
    private static partial Regex HexCode();

    [GeneratedRegex(@"^(unknown|undefined|not ?specified|not ?available|n/?a|none|empty|no ?dimm|to be filled by o\.?e\.?m\.?|default string|system (manufacturer|product name)|(manufacturer|sernum|serial ?num|partnum|module ?part ?number|asset ?tag ?num|array\d*_\w*?)\d*)$", RegexOptions.IgnoreCase)]
    private static partial Regex Placeholder();
}
