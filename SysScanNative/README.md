# SysScan Native (C#)

This is a native Windows rewrite of the original Python `SysScan` cleanup tool.

Build requirements
- .NET SDK 7.0 or later (for Windows)

Quick build and run

1. Open a Developer PowerShell and navigate to this folder.
2. Run the app locally:

```powershell
dotnet run --project "SysScanNative.csproj"
```

Produce a single-file self-contained EXE (windows x64):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false
```

The produced exe will be under `bin\Release\net7.0-windows\win-x64\publish`.

Notes
- Running some cleanup operations (SFC, DISM, clearing system recycle bin) may require administrative privileges.
- The current implementation uses safe file enumeration and best-effort deletion. Some files in use or protected by Windows will fail to delete.

CI / GitHub Actions
- This repository includes a GitHub Actions workflow to build and publish a single-file exe for Windows and upload it as an artifact. The workflow file is at `.github/workflows/publish-windows.yml`.

To trigger a build on GitHub, push to the `main` branch or run the workflow manually from the Actions tab. After the run completes, download the artifact named `SysScanNative-win-x64` from the workflow run.

Installer & Signing
- An NSIS installer script is included at `installer/SysScanInstaller.nsi` and a workflow at `.github/workflows/build-installer.yml` builds an installer and uploads it as the artifact `SysScanNative-installer`.
- Signing: the workflow supports optional code signing using a PFX certificate. To enable signing, add two repository secrets: `SIGN_CERT_BASE64` (the base64-encoded PFX file) and `SIGN_CERT_PASSWORD` (the PFX password). When present, the workflow will sign the installer using `signtool`.

Building locally
- To create the installer locally you can:

```powershell
cd SysScanNative
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:PublishTrimmed=false -o publish
choco install nsis -y
copy .\publish ..\installer\publish /Y /R
makensis installer\SysScanInstaller.nsi
```

The resulting `SysScanNative-Setup.exe` will be created in the working directory.
