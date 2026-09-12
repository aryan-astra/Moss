# Reconstructed source note

The `src/` tree was recovered with ILSpy 9.1 from the shipped Moss 1.3.0
portable binaries (`Moss.Core.dll`, `Moss.dll`), whose original C# source
was not in version control. It compiles cleanly and reproduces the shipped
binaries to matching size, and the rebuilt application was smoke-tested
(starts, renders, logs normally).

What was changed versus raw decompiler output (build hygiene only,
no product behavior changes):

- Projects rewritten: `net8.0` / `net8.0-windows10.0.19041.0`, NuGet
  references (Markdig 0.37.0, NAudio.Core/Wasapi 2.2.1) instead of absolute
  `C:\...` DLL paths; Windows SDK projections resolve from the framework.
- `AssemblyName` restored to `Moss` so outputs are `Moss.dll`/`Moss.exe`.
- Decompiler `AssemblyInfo.cs` files removed; SDK-generated assembly info
  applies and the version comes solely from `Directory.Build.props`.
- Restored one dropped `using` alias (`Activity`) and two dropped fire-and-
  forget discards; enabled nullable annotations (remaining nullable warnings
  are accepted decompiler noise, not errors).
- Binary version restamped from the shipped 1.3.0 to the 1.0.0 release line.

Not recovered (never shipped in the portable build): the original
`Moss.Tests` / `Moss.RenderChecks` / `Moss.WindowsChecks` harnesses,
`scripts/build.ps1`, packaging sources, and commit history. `tests/`
contains a new small honest harness over `Moss.Core`, not the originals.

If the original source tree resurfaces, it supersedes this reconstruction.
