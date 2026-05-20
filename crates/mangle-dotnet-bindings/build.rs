//! Regenerate the C# P/Invoke layer from the mangle-ffi C ABI.
//!
//! csbindgen parses the `#[unsafe(no_mangle)] pub extern "C"` functions
//! and `#[repr(C)]` structs in the mangle-ffi Rust sources and emits a
//! matching C# `NativeMethods` partial class plus the interop structs
//! (e.g. `MangleBuffer`). The output lands in the C# project so it's
//! checked in and reviewable.
//!
//! Run `cargo build -p mangle-dotnet-bindings` to regenerate. CI does
//! this and fails if the committed `NativeMethods.g.cs` drifts.
//!
//! Prereq: the mangle-rs submodule must be checked out at
//! `third-party/mangle-rs` (see the repo README). csbindgen reads the
//! source files directly — it does not compile mangle-ffi.

use std::path::PathBuf;

fn main() {
    // Repo root is two levels up from this crate
    // (crates/mangle-dotnet-bindings/).
    let repo_root = PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .parent()
        .and_then(|p| p.parent())
        .expect("repo root")
        .to_path_buf();

    let ffi_src = repo_root
        .join("third-party")
        .join("mangle-rs")
        .join("crates")
        .join("mangle-ffi")
        .join("src");

    let out = repo_root
        .join("src")
        .join("Mangle")
        .join("NativeMethods.g.cs");

    // Every mangle-ffi source file that declares extern "C" entry
    // points or #[repr(C)] interop structs. Keep this list in sync
    // when the FFI surface grows (the header-diff CI check on the
    // mangle-rs side flags surface changes; this list is the
    // downstream mirror).
    let files = [
        "lib.rs",        // mangle_version + error-code consts
        "buffer.rs",     // MangleBuffer + mangle_buffer_free
        "error.rs",      // mangle_last_error
        "engine.rs",     // engine lifecycle, load_rules, insert/retract
        "cursor.rs",     // mangle_query + cursor ops
        "io.rs",         // load_facts_mgr + save/dump + compression consts
        "schema.rs",     // schema_snapshot, relation_names, facts_snapshot
        "derivation.rs", // derivation_tree
        "value.rs",      // val accessors
        "builder.rs",    // val builder + build_* constructors
    ];

    if !ffi_src.exists() {
        // Soft-fail with a clear message rather than a confusing
        // csbindgen panic — the submodule probably isn't checked out.
        println!(
            "cargo:warning=mangle-ffi sources not found at {}. \
             Did you run `git submodule update --init`? Skipping codegen.",
            ffi_src.display()
        );
        return;
    }

    let mut builder = csbindgen::Builder::default();
    for f in files {
        builder = builder.input_extern_file(ffi_src.join(f));
        println!("cargo:rerun-if-changed={}", ffi_src.join(f).display());
    }

    builder
        // The cdylib base name. .NET resolves the platform prefix /
        // extension (lib*.so, lib*.dylib, *.dll) and looks under
        // runtimes/<rid>/native/ in the NuGet package.
        .csharp_dll_name("mangle_ffi")
        .csharp_namespace("Mangle.Native")
        .csharp_class_name("NativeMethods")
        .csharp_use_nint_types(true)
        .generate_csharp_file(&out)
        .expect("csbindgen failed to generate NativeMethods.g.cs");

    println!("cargo:warning=regenerated {}", out.display());
}
