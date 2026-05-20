using System.Runtime.InteropServices;
using System.Text;
using Mangle.Native;
// The public class `Mangle.MangleEngine` (this file) and the generated
// opaque struct `Mangle.Native.MangleEngine` share a name. Alias the
// native struct so `NativeEngine*` is unambiguous in pointer contexts.
using NativeEngine = Mangle.Native.MangleEngine;

namespace Mangle;

/// <summary>
/// A compiled + evaluated Mangle program.
///
/// Construct with rule source(s), then query the resulting relations.
/// Mirrors the engine lifecycle of the C ABI: an engine is
/// single-threaded — do not share an instance across threads without
/// external synchronization (the underlying Rust state is
/// <c>!Sync</c>, same as mangle-py's <c>unsendable</c> Program).
///
/// This is a representative slice of the surface (construct, load,
/// query, snapshots). Insert/retract, fact I/O (.mgr load/save),
/// query-dump, and derivation trees are additional FFI entry points
/// already present in <see cref="Native.NativeMethods"/> — wrap them
/// here following the same pattern.
/// </summary>
public sealed unsafe class MangleEngine : IDisposable
{
    private readonly MangleEngineHandle _handle;
    private bool _hasRules;

    /// <param name="enableProvenance">
    /// Record derivation provenance during evaluation (needed for
    /// derivation-tree queries). Costs memory; off by default.
    /// </param>
    public MangleEngine(bool enableProvenance = false)
    {
        _handle = MangleEngineHandle.Create(enableProvenance);
    }

    /// <summary>Compile + execute one or more Mangle source units,
    /// replacing any previously loaded program.</summary>
    public void LoadRules(params string[] sources)
    {
        if (sources is null || sources.Length == 0)
        {
            throw new ArgumentException("at least one source unit is required", nameof(sources));
        }

        var utf8 = new byte[sources.Length][];
        var ptrs = new nint[sources.Length];
        var lens = new nuint[sources.Length];
        var pins = new GCHandle[sources.Length];
        try
        {
            for (int i = 0; i < sources.Length; i++)
            {
                utf8[i] = Encoding.UTF8.GetBytes(sources[i]);
                pins[i] = GCHandle.Alloc(utf8[i], GCHandleType.Pinned);
                ptrs[i] = pins[i].AddrOfPinnedObject();
                lens[i] = (nuint)utf8[i].Length;
            }
            fixed (nint* pPtrs = ptrs)
            fixed (nuint* pLens = lens)
            {
                int rc = NativeMethods.mangle_load_rules(
                    _handle.Ptr, (byte**)pPtrs, pLens, (nuint)sources.Length);
                MangleException.ThrowIfError(rc);
            }
            _hasRules = true;
        }
        finally
        {
            for (int i = 0; i < pins.Length; i++)
            {
                if (pins[i].IsAllocated)
                {
                    pins[i].Free();
                }
            }
        }
    }

    /// <summary>Run a query atom (e.g. <c>reachable("a", X)</c>) and
    /// materialize all matching rows.</summary>
    public IReadOnlyList<MangleValue[]> Query(string query)
    {
        EnsureRules();
        var bytes = Encoding.UTF8.GetBytes(query);
        MangleCursor* cursor;
        fixed (byte* p = bytes)
        {
            int rc = NativeMethods.mangle_query(_handle.Ptr, p, (nuint)bytes.Length, &cursor);
            MangleException.ThrowIfError(rc);
        }

        var rows = new List<MangleValue[]>();
        try
        {
            while (true)
            {
                int rc = NativeMethods.mangle_cursor_next(cursor);
                if (rc == 1)
                {
                    break; // end of stream
                }
                MangleException.ThrowIfError(rc);

                int arity = NativeMethods.mangle_cursor_arity(cursor);
                var row = new MangleValue[arity];
                for (uint c = 0; c < arity; c++)
                {
                    Value* col = NativeMethods.mangle_cursor_col(cursor, c);
                    // Read immediately: the pointer is only valid until
                    // the next cursor_next.
                    row[c] = MangleValue.Read(col);
                }
                rows.Add(row);
            }
        }
        finally
        {
            NativeMethods.mangle_cursor_free(cursor);
        }
        return rows;
    }

    /// <summary>Schema overview as JSON (predicates + rule edges).</summary>
    public string SchemaSnapshotJson()
    {
        EnsureRules();
        MangleBuffer buf = default;
        MangleException.ThrowIfError(NativeMethods.mangle_schema_snapshot(_handle.Ptr, &buf));
        return MangleBufferOps.ReadAndFree(&buf);
    }

    /// <summary>Facts overview as JSON (per-relation count + sample).</summary>
    public string FactsSnapshotJson(int perRelationLimit = 16)
    {
        if (perRelationLimit < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(perRelationLimit), "must be non-negative");
        }
        EnsureRules();
        MangleBuffer buf = default;
        MangleException.ThrowIfError(
            NativeMethods.mangle_facts_snapshot(_handle.Ptr, (uint)perRelationLimit, &buf));
        return MangleBufferOps.ReadAndFree(&buf);
    }

    /// <summary>The native library's semantic version.</summary>
    public static string NativeVersion()
    {
        MangleBuffer buf = default;
        MangleException.ThrowIfError(NativeMethods.mangle_version(&buf));
        return MangleBufferOps.ReadAndFree(&buf);
    }

    private void EnsureRules()
    {
        if (!_hasRules)
        {
            throw new InvalidOperationException("no rules loaded; call LoadRules first");
        }
    }

    public void Dispose() => _handle.Dispose();
}

/// <summary>
/// SafeHandle over the opaque <c>MangleEngine*</c>. Guarantees the
/// engine is freed exactly once even on exceptions / finalization.
/// </summary>
internal sealed unsafe class MangleEngineHandle : SafeHandle
{
    private MangleEngineHandle() : base(nint.Zero, ownsHandle: true) { }

    public override bool IsInvalid => handle == nint.Zero;

    internal static MangleEngineHandle Create(bool enableProvenance)
    {
        NativeEngine* raw;
        int rc = NativeMethods.mangle_engine_new(enableProvenance ? 1 : 0, &raw);
        MangleException.ThrowIfError(rc);
        var h = new MangleEngineHandle();
        h.SetHandle((nint)raw);
        return h;
    }

    /// <summary>The typed pointer for FFI calls. The owning
    /// <see cref="MangleEngine"/> keeps this handle alive for the
    /// duration of each call.</summary>
    internal NativeEngine* Ptr => (NativeEngine*)handle;

    protected override bool ReleaseHandle()
    {
        NativeMethods.mangle_engine_free((NativeEngine*)handle);
        return true;
    }
}
