using System.Runtime.InteropServices;

namespace Mangle.Native;

/// <summary>
/// Opaque value handle. The Rust FFI exposes <c>MangleVal</c> — a type
/// alias for <c>mangle_common::Value</c>. csbindgen resolves the alias
/// to <c>Value</c> in the generated signatures but can't emit a
/// definition (the underlying type lives outside the crate csbindgen
/// parses), so we supply the opaque struct here. This is the C#
/// counterpart of the <c>typedef struct MangleVal MangleVal;</c>
/// forward declaration in <c>mangle.h</c>. Pointer-only — never
/// dereferenced from managed code.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe partial struct Value
{
}
