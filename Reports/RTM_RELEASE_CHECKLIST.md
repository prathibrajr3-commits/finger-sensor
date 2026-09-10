# AirGesture AI v4.1.0 RTM — Release Gate Checklist

**Target Release**: AirGesture AI v4.1.0 RTM  
**Handoff Date**: 2026-08-13  

---

## Final Release Verification Gates

Use this checklist during execution in the real Windows release environment:

- [ ] .NET 8 SDK verified (`dotnet --version`)
- [ ] `dotnet restore` succeeds with 0 errors
- [ ] Release build succeeds (`dotnet build --configuration Release`)
- [ ] Zero compiler warnings (`TreatWarningsAsErrors=true`)
- [ ] All xUnit test suites pass (`dotnet test`)
- [ ] `AirGestureAI-4.1.0-Portable.zip` physically created
- [ ] Portable ZIP content audit passes (executables present, source/secrets excluded)
- [ ] `Documentation.zip` physically created
- [ ] `SDK.zip` physically created
- [ ] SHA-256 digests in `checksums.sha256` independently verified
- [ ] SPDX 2.3 `SBOM.spdx.json` verified against package references
- [ ] `ReleaseValidator` service returns 100% pass rate
- [ ] v4.0 → v4.1 settings migration verified (`Installer/migrate-settings.ps1`)
- [ ] `AirGestureAI-4.1.0-Setup.msi` compiled via WiX
- [ ] MSI fresh installation verified
- [ ] MSI major upgrade behavior verified
- [ ] MSI uninstall verified (user data preserved in `%LocalAppData%\AirGestureAI`)
- [ ] Authenticode signing completed via SignTool
- [ ] Authenticode signature state verified (`Signed`, non-expired)
- [ ] Security audit complete (zero secrets, DPAPI keys, or logs leaked)
- [ ] Final release notes (`RELEASE_NOTES.md`) updated
- [ ] Git tag `v4.1.0` prepared
