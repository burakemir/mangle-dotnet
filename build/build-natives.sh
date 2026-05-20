#!/usr/bin/env bash
# Build the mangle-ffi cdylib for the *host* RID and drop it into
# src/Mangle/runtimes/<rid>/native/ for local dev + testing.
#
# CI's release workflow builds every RID across an OS matrix; this
# script just covers the machine you're on. Requires the mangle-rs
# submodule checked out (git submodule update --init).
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ffi_manifest="$repo_root/third-party/mangle-rs/crates/mangle-ffi/Cargo.toml"

if [ ! -f "$ffi_manifest" ]; then
  echo "error: $ffi_manifest not found — run: git submodule update --init" >&2
  exit 1
fi

host="$(rustc -vV | sed -n 's/^host: //p')"
case "$host" in
  aarch64-apple-darwin)        rid="osx-arm64";   lib="libmangle_ffi.dylib" ;;
  x86_64-apple-darwin)         rid="osx-x64";     lib="libmangle_ffi.dylib" ;;
  x86_64-unknown-linux-gnu)    rid="linux-x64";   lib="libmangle_ffi.so" ;;
  aarch64-unknown-linux-gnu)   rid="linux-arm64"; lib="libmangle_ffi.so" ;;
  x86_64-pc-windows-msvc)      rid="win-x64";     lib="mangle_ffi.dll" ;;
  *) echo "error: unmapped host triple: $host" >&2; exit 1 ;;
esac

echo "Building mangle-ffi (release) for host $host -> RID $rid"
cargo build --release -p mangle-ffi --manifest-path "$ffi_manifest"

target_dir="$repo_root/third-party/mangle-rs/target/release"
dest="$repo_root/src/Mangle/runtimes/$rid/native"
mkdir -p "$dest"
cp "$target_dir/$lib" "$dest/$lib"
echo "Copied $lib -> $dest/"
