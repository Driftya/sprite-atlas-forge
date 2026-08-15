# Release process

## Distribution

The desktop application is distributed as an unpackaged, self-contained Windows x64 ZIP. Keep the executable and all adjacent files together after extraction; the `.exe` is not a single-file application. Windows 10 version 1809 or newer is required.

To verify a downloaded release archive in PowerShell:

```powershell
(Get-FileHash .\SpriteAtlasForge-X.Y.Z-windows-x64.zip -Algorithm SHA256).Hash.ToLowerInvariant()
Get-Content .\SpriteAtlasForge-X.Y.Z-windows-x64.zip.sha256
```

Extract the whole ZIP to a writable folder, then run `SpriteAtlasForge.exe`.

## Maintainer procedure

The release workflow creates a draft GitHub Release from a version tag.

1. Confirm the working tree is clean. Update `Version` and `ApplicationDisplayVersion` to the release version, and increment the integer `ApplicationVersion`.
2. Run the required release verification:

   ```powershell
   .\eng\verify.ps1 -Configuration Release
   ```

3. Smoke-test the self-contained publish used by CI:

   ```powershell
   dotnet publish .\src\Driftya.SpriteAtlasForge.ClientApplication\Driftya.SpriteAtlasForge.ClientApplication.csproj `
     --framework net10.0-windows10.0.19041.0 `
     --configuration Release `
     --output .\.artifacts\release-smoke-test `
     -p:RuntimeIdentifierOverride=win-x64 `
     -p:WindowsPackageType=None `
     -p:WindowsAppSDKSelfContained=true

   .\.artifacts\release-smoke-test\SpriteAtlasForge.exe
   ```

4. Commit and push the release changes. Add the release notes as `docs/releases/vX.Y.Z.md`, create an annotated `vX.Y.Z` tag on that commit, and push the tag.
5. Wait for the GitHub **Release** workflow. It verifies the solution, publishes the Windows app, records resolved dependencies, creates a ZIP and SHA-256 file, and attaches them to a draft release.
6. Download the draft assets, verify the checksum, extract the ZIP on a clean x64 Windows machine, and launch the application.
7. Edit the draft release as needed and publish it in GitHub.

## Current limitation

The release uses a portable ZIP rather than a signed MSIX installer. Windows may show a reputation warning for a newly downloaded unsigned executable. Code signing and an installer can be added later without changing the native project format.
