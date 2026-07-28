namespace Tdms.Core;

/// <summary>
/// TDMS on-disk data type codes (the <c>tdsDataType</c> enumeration used by both raw
/// indices and property values).
/// </summary>
public enum TdmsDataType
{
    /// <summary>No type — used for channels that never carried raw data.</summary>
    Void = 0,

    /// <summary>Signed 8-bit integer.</summary>
    I8 = 1,

    /// <summary>Signed 16-bit integer.</summary>
    I16 = 2,

    /// <summary>Signed 32-bit integer.</summary>
    I32 = 3,

    /// <summary>Signed 64-bit integer.</summary>
    I64 = 4,

    /// <summary>Unsigned 8-bit integer.</summary>
    U8 = 5,

    /// <summary>Unsigned 16-bit integer.</summary>
    U16 = 6,

    /// <summary>Unsigned 32-bit integer.</summary>
    U32 = 7,

    /// <summary>Unsigned 64-bit integer.</summary>
    U64 = 8,

    /// <summary>IEEE-754 single precision float.</summary>
    F32 = 9,

    /// <summary>IEEE-754 double precision float.</summary>
    F64 = 10,

    /// <summary>Length-prefixed UTF-8 string.</summary>
    String = 0x20,

    /// <summary>Single byte boolean (0 = false).</summary>
    Boolean = 0x21,

    /// <summary>TDMS timestamp: u64 fractions of a second + i64 seconds since 1904-01-01 UTC.</summary>
    Timestamp = 0x44,
}

/// <summary>Helpers for <see cref="TdmsDataType"/>.</summary>
public static class TdmsDataTypes
{
    /// <summary>Short lowercase name used in the UI and in error messages.</summary>
    /// <param name="type">Data type code.</param>
    /// <returns>A short name such as <c>f64</c>, or <c>dtype(n)</c> for unknown codes.</returns>
    public static string Name(TdmsDataType type) => type switch
    {
        TdmsDataType.Void => "void",
        TdmsDataType.I8 => "i8",
        TdmsDataType.I16 => "i16",
        TdmsDataType.I32 => "i32",
        TdmsDataType.I64 => "i64",
        TdmsDataType.U8 => "u8",
        TdmsDataType.U16 => "u16",
        TdmsDataType.U32 => "u32",
        TdmsDataType.U64 => "u64",
        TdmsDataType.F32 => "f32",
        TdmsDataType.F64 => "f64",
        TdmsDataType.String => "string",
        TdmsDataType.Boolean => "bool",
        TdmsDataType.Timestamp => "timestamp",
        _ => $"dtype({(int)type})",
    };

    /// <summary>Size in bytes of one value, for the fixed-width types.</summary>
    /// <param name="type">Data type code.</param>
    /// <param name="size">Receives the value size in bytes.</param>
    /// <returns><see langword="true"/> for fixed-width types; <see langword="false"/> for
    /// <see cref="TdmsDataType.String"/> and unknown codes.</returns>
    public static bool TryGetFixedSize(TdmsDataType type, out int size)
    {
        size = type switch
        {
            TdmsDataType.I8 or TdmsDataType.U8 or TdmsDataType.Boolean => 1,
            TdmsDataType.I16 or TdmsDataType.U16 => 2,
            TdmsDataType.I32 or TdmsDataType.U32 or TdmsDataType.F32 => 4,
            TdmsDataType.I64 or TdmsDataType.U64 or TdmsDataType.F64 => 8,
            TdmsDataType.Timestamp => 16,
            _ => 0,
        };
        return size > 0;
    }

    /// <summary>Whether the type carries a floating point value.</summary>
    /// <param name="type">Data type code.</param>
    /// <returns><see langword="true"/> for <c>f32</c>/<c>f64</c>.</returns>
    public static bool IsFloating(TdmsDataType type) =>
        type is TdmsDataType.F32 or TdmsDataType.F64;

    /// <summary>Whether the type is one of the integer or boolean codes.</summary>
    /// <param name="type">Data type code.</param>
    /// <returns><see langword="true"/> for integers and booleans.</returns>
    public static bool IsIntegral(TdmsDataType type) => type
        is TdmsDataType.I8 or TdmsDataType.I16 or TdmsDataType.I32 or TdmsDataType.I64
        or TdmsDataType.U8 or TdmsDataType.U16 or TdmsDataType.U32 or TdmsDataType.U64
        or TdmsDataType.Boolean;

    /// <summary>Whether the type is one of the unsigned integer codes.</summary>
    /// <param name="type">Data type code.</param>
    /// <returns><see langword="true"/> for <c>u8</c>…<c>u64</c>.</returns>
    public static bool IsUnsigned(TdmsDataType type) => type
        is TdmsDataType.U8 or TdmsDataType.U16 or TdmsDataType.U32 or TdmsDataType.U64;
}
