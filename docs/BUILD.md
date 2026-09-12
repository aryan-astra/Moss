# Current build note

The expanded source still uses this build process. Current functionality/verification is described in ACCEPTANCE.md and TECHNICAL-ARCHITECTURE.md. Markdig 0.37.0 is an additional pinned dependency. Reminder scheduling uses Windows Task Scheduler, not the incoming-notification listener capability. The versioned executable identifier is 1.3.0.

# Build and distribution

## Toolchain

- Windows 10 build 19041+ or Windows 11 for running the product.
- .NET **8 SDK**, with the Windows targeting pack restored from NuGet. The delivery was built using SDK **8.0.425**, runtime **8.0.31**, Windows SDK .NET projection **10.0.19041.56**.
- PowerShell for the convenience scripts. Visual Studio is optional; open `Moss.sln` if preferred.
- Windows 10/11 SDK **MakeAppx and SignTool**, plus a publisher signing certificate, only for optional MSIX packaging.
- First dependency restore needs internet. Fundamental runtime behavior is offline.

## Ordinary Windows build

From the extracted source root:

```powershell
./scripts/build.ps1
```

The script checks each command's exit code. It restores, compiles, runs automated tests, publishes all required content and runtime files, copies documentation/licenses and creates `artifacts/Moss-win-x64.zip`.

Equivalent individual commands if scripts are restricted:

```powershell
dotnet restore Moss.sln
dotnet build Moss.sln -c Release --no-restore
dotnet run --project tests/Moss.Tests -c Release --no-build -- .
dotnet publish src/Moss.Windows -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=false -p:DebugType=None -o artifacts/Moss-win-x64
Copy-Item README.md,LICENSE,THIRD-PARTY-NOTICES.md artifacts/Moss-win-x64
Copy-Item docs,licenses artifacts/Moss-win-x64 -Recurse
Copy-Item scripts/uninstall-portable.ps1 artifacts/Moss-win-x64
Compress-Archive artifacts/Moss-win-x64/* artifacts/Moss-win-x64.zip -Force
```

Run `artifacts/Moss-win-x64/Moss.exe`. A build with no bundled runtime can also be run from Visual Studio or `dotnet run --project src/Moss.Windows` on Windows with the desktop runtime installed.

ARM64 publishing: `./scripts/build.ps1 -Runtime win-arm64`. This path is configured but was not built or run in the delivery environment. x86 is deliberately not offered: the Win32 ABI wrapper uses 64-bit `GetWindowLongPtr` exports.

The project uses package lock files for resolved dependencies. `dotnet restore --locked-mode` checks for dependency changes for the current target graph; a new runtime identifier can legitimately require regenerating lock data. A self-contained runtime is chosen over single-file extraction to keep startup and pack paths inspectable. Do not trim WinForms/WinRT without independently validating reflection and projected types.

## Cross-compilation from Linux

`EnableWindowsTargeting=true` is supplied in `Directory.Build.props`. With .NET 8 installed, the restore/build/test/publish commands above work on Linux. Only the core test executable runs there. The Windows app does **not** become Linux-compatible, and cross-compilation proves neither launch nor UI correctness.

## Signed MSIX (notification capability)

The source includes `packaging/AppxManifest.xml`, required logos and `scripts/package-msix.ps1`. This adds package identity, `runFullTrust`, `userNotificationListener` and a disabled-by-default desktop startup task. The app checks identity at runtime before requesting notification access.

To produce a distributable package on Windows:

```powershell
./scripts/package-msix.ps1 `
  -CertificatePath C:\Signing\publisher.pfx `
  -CertificatePassword '<your-password>' `
  -Publisher 'CN=Your Publisher'
```

Publisher must match the signing certificate's subject exactly. The script replaces manifest tokens, packages with MakeAppx and signs using SHA-256. It does not silently generate certificates or change trust stores. For an organization/Store distribution use your normal signing and trust pipeline. Protect signing keys and do not commit PFX files; passwords on a command line may be visible in shell history/process inspection, so use a controlled signing workstation or adapt to your certificate-store signing workflow.

For **local developer testing only**, a user-owned code-signing certificate can be created in Windows. This is not a substitute for a trusted public publisher:

```powershell
$cert = New-SelfSignedCertificate -Type Custom -Subject 'CN=Moss Local Development' `
  -KeyUsage DigitalSignature -FriendlyName 'Moss Local Development' `
  -CertStoreLocation Cert:\CurrentUser\My `
  -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3','2.5.29.19={text}')
$password = Read-Host 'PFX password' -AsSecureString
Export-PfxCertificate -Cert $cert -FilePath .\moss-dev.pfx -Password $password
Export-Certificate -Cert $cert -FilePath .\moss-dev.cer
```

Trust that certificate on a test machine only through the standard Windows certificate workflow after inspecting its fingerprint and understanding the trust change. Installation may require an administrator or organizational approval for certificate trust; **Moss itself never requires elevation**. Remove development trust and the private key after testing. No development certificate is included in this delivery.

Install the signed package by double-clicking it in Windows App Installer, or `Add-AppxPackage .\artifacts\Moss-x64.msix` when its signer is trusted. Enable notifications in Moss Settings; Windows must separately grant listener access. Packaging and the listener's desktop permission path have not been executed in this environment.

## CI

`.github/workflows/windows.yml` defines a Windows restore/build/test/publish job. Uploading this project to a Git repository can run that job. No CI run was performed as part of this delivery. A successful hosted CI build is still not equivalent to interactive Windows acceptance.

## Reproducibility and release hygiene

- No downloaded content executes as part of a character pack.
- `characters/moss/character.json`, all licenses and docs must accompany the executable distribution.
- Binary ZIP checksums are supplied in the delivery's `SHA256SUMS.txt`.
- Binary/runtime files are generated artifacts; source archives exclude `bin`, `obj`, local caches, private keys and generated packages.
- Do not label the build production-accepted until the native acceptance and Windows profiling rows in VERIFICATION have actually been executed. There is no hidden test result, signing credential or unpublished dependency that satisfies those rows.

## Additional verification projects

`dotnet run --project tests/Moss.WindowsChecks -c Release` exercises the actual dropdown helper, RichEdit and DIB implementation. This delivery's recorded execution used Wine, not native Windows.

`dotnet run --project tests/Moss.RenderChecks -c Release -- . <output-folder>` creates a Linux/libgdiplus renderer contact sheet. Its System.Drawing.Common 6 dependency and UnixSupport switch are test-only; production retains the .NET 8 Windows Desktop graphics implementation.
