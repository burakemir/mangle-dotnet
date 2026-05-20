# Mangle for .NET

.NET bindings for [Mangle](https://codeberg.org/TauCeti/mangle-rs), a
Datalog-family logic-programming engine. The Rust core is exposed
through a stable C ABI (`mangle-ffi`); this repo generates the C#
P/Invoke layer and ships a NuGet package with the native library
bundled for every supported platform.

## Status

Working skeleton with a verified end-to-end round-trip. The wiring
(csbindgen codegen, the C# wrappers, the cross-build + pack workflow)
is in place, and `samples/MangleSample` runs a real query through the
full stack (C# → P/Invoke → Rust engine). Finishing steps for
publishing are noted below.

## Layout

```
mangle-dotnet/
├── third-party/mangle-rs/             # git submodule, pinned commit (the engine)
├── crates/mangle-dotnet-bindings/     # Rust: runs csbindgen → NativeMethods.g.cs
├── src/Mangle/                        # the C# library + NuGet package
│   ├── NativeMethods.g.cs             # generated P/Invoke (committed, do not hand-edit)
│   ├── MangleEngine.cs                # SafeHandle-backed wrapper
│   ├── MangleValue.cs                 # value reader
│   ├── MangleException.cs             # status-code → exception
│   └── runtimes/<rid>/native/         # cdylibs land here (gitignored; CI/script populated)
├── build/build-natives.sh             # build the host RID's cdylib locally
└── .github/workflows/release.yml      # cross-build matrix → pack → push to nuget.org
```

## Packaging model

A single **fat package** (`Mangle`): managed wrappers + every RID's
native cdylib under `runtimes/`. Consumers `dotnet add package Mangle`
and .NET copies the right platform's lib automatically. Supported
RIDs: `linux-x64`, `osx-arm64`, `osx-x64`, `win-x64` (extend the
matrix in `release.yml` to add more).

## Setup (one-time)

1. Create the repo and add the engine as a submodule:
   ```sh
   git init
   git submodule add https://codeberg.org/TauCeti/mangle-rs third-party/mangle-rs
   git -C third-party/mangle-rs checkout <pinned-commit>
   ```
2. Add a workspace `Cargo.toml` at the repo root if you want
   `cargo build -p mangle-dotnet-bindings` / `-p mangle-ffi` to resolve
   from the top level, or rely on the explicit `--manifest-path` the
   scripts/CI already use.
3. Publishing uses nuget.org **trusted publishing** (OIDC) — no
   long-lived API key. Configure a trusted-publisher policy on
   nuget.org for this repo + the `build + pack` workflow, and set a
   repository **variable** `NUGET_USER` to your nuget.org account name
   (the account that owns the policy; not a secret). The `pack` job
   mints a short-lived key per run via OIDC; a `v*` tag triggers the
   push.

## Regenerate the C# bindings

```sh
git submodule update --init
cargo build -p mangle-dotnet-bindings   # rewrites src/Mangle/NativeMethods.g.cs
```

Never edit `NativeMethods.g.cs` by hand — change the Rust FFI and
regenerate. CI's `bindings-fresh` job fails if the committed copy
drifts. The file list to scan lives in `build.rs`; extend it when the
FFI grows new source files with `extern "C"` entry points.

## Build + run locally

```sh
git submodule update --init             # the mangle-rs engine source
./build/build-natives.sh                # cdylib for your host RID -> runtimes/<rid>/native/
dotnet build src/Mangle/Mangle.csproj   # compile the managed library
dotnet run  --project samples/MangleSample   # end-to-end smoke test
dotnet pack src/Mangle/Mangle.csproj -c Release -o nupkg
```

## Native library resolution (important)

The native cdylib reaches a consumer in exactly one supported way:
**through the NuGet package.** When you `dotnet add package Mangle`,
.NET sees the `runtimes/<rid>/native/*` entries in the package and
copies the right platform's library into the consuming app's output
automatically — no manual steps.

That automatic flow does **not** happen across a bare
`ProjectReference`. Native assets are a NuGet-packaging feature, not a
project-to-project one, so a project that references `Mangle.csproj`
directly will build but fail to load the library at runtime unless the
cdylib is placed next to its executable. That's why
`samples/MangleSample` has a small dev-only `<None …>` glob that copies
`runtimes/**/native/*` flat into its own output — it is *not* something
real consumers need, only a convenience for in-repo development.

If you want to test the real consumer experience (the package's native
resolution), pack and consume locally instead of project-referencing:

```sh
dotnet pack src/Mangle/Mangle.csproj -c Release -o nupkg
# add ./nupkg as a local NuGet source, then in a throwaway project:
dotnet add package Mangle --source ./nupkg
```

That exercises the `runtimes/` auto-copy path with no glob.

## Example

```csharp
using Mangle;

using var engine = new MangleEngine();
engine.LoadRules("""
    edge(1, 2). edge(2, 3).
    reachable(X, Y) :- edge(X, Y).
    reachable(X, Z) :- edge(X, Y), reachable(Y, Z).
""");

foreach (var row in engine.Query("reachable(1, Y)"))
    Console.WriteLine(row[1].AsInt64());   // 2, 3

Console.WriteLine(engine.SchemaSnapshotJson());
```

## What's wrapped

`MangleEngine` covers a representative slice: construct (with optional
provenance), `LoadRules`, `Query` (+ cursor iteration), and the schema
/ facts snapshots. The remaining FFI entry points — `insert`/`retract`,
`.mgr` load/save, query-dump, derivation trees, compound-value
walking — follow the same pattern (raw decl in `NativeMethods`, safe
wrapper here). See `mangle.h` in the engine repo for the full surface.

## License

Apache-2.0, matching the Mangle engine.
