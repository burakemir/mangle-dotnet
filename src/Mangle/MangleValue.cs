using Mangle.Native;

namespace Mangle;

/// <summary>Value kind tags, mirroring <c>MANGLE_VAL_*</c> in mangle.h.</summary>
public enum MangleKind
{
    Null = 0,
    Number = 1,
    Float = 2,
    String = 3,
    Name = 4,
    Time = 5,
    Duration = 6,
    Compound = 7,
}

/// <summary>
/// A materialized Mangle value. Read out of a borrowed <c>Value</c>
/// handle immediately (cursor column pointers are only valid until the
/// next row), so this struct owns its data.
///
/// Scalar kinds only in this skeleton; compound (list/map/struct)
/// walking via the <c>mangle_val_compound_*</c> accessors (already
/// present in <see cref="Native.NativeMethods"/>) is a TODO — add a
/// recursive read here.
/// </summary>
public readonly struct MangleValue
{
    public MangleKind Kind { get; }
    private readonly long _int;     // Number / Time / Duration (ns)
    private readonly double _float; // Float
    private readonly string? _text; // String / Name

    private MangleValue(MangleKind kind, long i, double f, string? s)
    {
        Kind = kind;
        _int = i;
        _float = f;
        _text = s;
    }

    public long AsInt64() => Kind is MangleKind.Number or MangleKind.Time or MangleKind.Duration
        ? _int
        : throw new InvalidOperationException($"value is {Kind}, not an integer kind");

    public double AsDouble() => Kind == MangleKind.Float
        ? _float
        : throw new InvalidOperationException($"value is {Kind}, not Float");

    public string AsString() => Kind is MangleKind.String or MangleKind.Name
        ? _text!
        : throw new InvalidOperationException($"value is {Kind}, not String/Name");

    public override string ToString() => Kind switch
    {
        MangleKind.Null => "null",
        MangleKind.Number => _int.ToString(),
        MangleKind.Float => _float.ToString(System.Globalization.CultureInfo.InvariantCulture),
        MangleKind.String => $"\"{_text}\"",
        MangleKind.Name => _text!,
        MangleKind.Time => $"time({_int}ns)",
        MangleKind.Duration => $"dur({_int}ns)",
        MangleKind.Compound => "<compound>",
        _ => "<?>",
    };

    /// <summary>
    /// Read a borrowed <c>Value</c> handle into an owned value. The
    /// handle must be valid at the time of the call.
    /// </summary>
    internal static unsafe MangleValue Read(Value* val)
    {
        var kind = (MangleKind)NativeMethods.mangle_val_kind(val);
        switch (kind)
        {
            case MangleKind.Number:
            case MangleKind.Time:
            case MangleKind.Duration:
                long i;
                MangleException.ThrowIfError(NativeMethods.mangle_val_as_i64(val, &i));
                return new MangleValue(kind, i, 0, null);
            case MangleKind.Float:
                double f;
                MangleException.ThrowIfError(NativeMethods.mangle_val_as_f64(val, &f));
                return new MangleValue(kind, 0, f, null);
            case MangleKind.String:
            case MangleKind.Name:
                MangleBuffer buf = default;
                MangleException.ThrowIfError(NativeMethods.mangle_val_as_str(val, &buf));
                var s = MangleBufferOps.ReadAndFree(&buf);
                return new MangleValue(kind, 0, 0, s);
            default:
                // Null and Compound carry no scalar payload here.
                return new MangleValue(kind, 0, 0, null);
        }
    }
}
