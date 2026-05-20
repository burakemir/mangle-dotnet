using System.Runtime.InteropServices;
using System.Text;
using Mangle.Native;

namespace Mangle;

/// <summary>
/// Thrown when a Mangle FFI call returns a nonzero status. The
/// <see cref="Code"/> mirrors the stable <c>MANGLE_ERR_*</c> codes in
/// <c>mangle.h</c>; the message comes from the engine's thread-local
/// last-error slot.
/// </summary>
public sealed class MangleException : Exception
{
    public int Code { get; }

    public MangleException(int code, string message) : base(message)
    {
        Code = code;
    }

    /// <summary>Stable status codes (see mangle.h).</summary>
    public const int Ok = 0;
    public const int Generic = -1;
    public const int InvalidArg = -2;
    public const int NoRules = -3;
    public const int CursorInvalidated = -4;
    public const int NoProvenance = -5;
    public const int FactNotFound = -6;
    public const int Parse = -7;
    public const int Panic = -8;
    public const int UnknownRelation = -9;

    /// <summary>
    /// Drain the thread-local last-error string. Always safe to call;
    /// returns empty when nothing is set.
    /// </summary>
    internal static unsafe string DrainLastError()
    {
        MangleBuffer buf = default;
        _ = NativeMethods.mangle_last_error(&buf);
        return MangleBufferOps.ReadAndFree(&buf);
    }

    /// <summary>Throw if <paramref name="code"/> is nonzero.</summary>
    internal static void ThrowIfError(int code)
    {
        if (code == Ok)
        {
            return;
        }
        var msg = DrainLastError();
        throw new MangleException(code, msg.Length == 0 ? $"mangle error {code}" : msg);
    }
}

/// <summary>Helpers for reading + releasing a native MangleBuffer.</summary>
internal static class MangleBufferOps
{
    /// <summary>
    /// Copy the buffer's bytes into a managed UTF-8 string and free
    /// the native allocation. Safe on an empty/zeroed buffer.
    /// </summary>
    internal static unsafe string ReadAndFree(MangleBuffer* buf)
    {
        string result = string.Empty;
        if (buf->data != null && buf->len != UIntPtr.Zero)
        {
            int len = checked((int)buf->len);
            var bytes = new byte[len];
            Marshal.Copy((nint)buf->data, bytes, 0, len);
            result = Encoding.UTF8.GetString(bytes);
        }
        NativeMethods.mangle_buffer_free(buf);
        return result;
    }
}
