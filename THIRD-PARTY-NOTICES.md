# Third-party notices

Moss's source, original procedural character, parameter animations, icon art and synthesized sound are covered by the root MIT LICENSE. No downloaded artwork, fonts or recordings are bundled. Segoe UI is requested from Windows, not redistributed.

Runtime dependencies:

- **.NET 8 / Windows Desktop runtime 8.0.31** — Microsoft and .NET contributors. MIT with component notices. The self-contained Windows archive includes the runtime. See `licenses/dotnet-runtime-LICENSE.txt`, `licenses/dotnet-desktop-LICENSE.txt` and the third-party notice files in `licenses/`. Source: https://github.com/dotnet/runtime and https://github.com/dotnet/winforms . The framework also ships Microsoft native graphics components covered by the runtime distribution's notices.
- **NAudio.Core and NAudio.Wasapi 2.2.1** — Mark Heath and contributors, MIT. Used for opt-in Windows speaker-output analysis. See `licenses/NAudio-MIT.txt`. Source: https://github.com/naudio/NAudio . ASIO, MIDI and NAudio WinForms are not runtime dependencies of this application.
- **Windows SDK .NET projection 10.0.19041.56 / WinRT.Runtime (CsWinRT)** — Microsoft, MIT; see `licenses/CsWinRT-LICENSE.txt`. Source: https://github.com/microsoft/CsWinRT . Windows SDK build tooling itself is subject to Microsoft's SDK license; it is not redistributed in the project.

Build-only tools: .NET SDK; Windows SDK MakeAppx and SignTool for optional signed MSIX packaging. Automated CI definitions reference GitHub's checkout, setup-dotnet and upload-artifact actions; these actions are not included in the desktop executable. Building/restoring uses NuGet over the network; running Moss does not require a network connection.

Exact resolved package versions and hashes are recorded in each project's `packages.lock.json`. This file is not an assertion of Windows runtime acceptance or a security audit.

Additional dependency in this expanded build:

- **Markdig 0.37.0** — Alexandre Mutel and contributors, BSD 2-Clause. Used for Markdown parsing in the core; it does not download images or execute embedded HTML. See `licenses/Markdig-LICENSE.txt`. Source: https://github.com/xoofx/markdig . This remains a legacy/core utility; the 1.2 editor uses native RichEdit, not a Markdown read view.

Test-only: Moss.RenderChecks uses System.Drawing.Common 6.0.0 (Microsoft, MIT) under libgdiplus. Neither that older assembly, Wine, libgdiplus nor test fonts are bundled in the Windows product.
