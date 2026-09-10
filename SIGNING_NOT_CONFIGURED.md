# Code Signing Notice — Signing Not Configured

**Product**: AirGesture AI v4.1 RTM  
**Status**: `UNSIGNED` (Code Signing Not Configured)

---

## Technical Details

The build output binaries (`AirGestureAI.exe`, associated DLLs, and installer package) generated in this environment are **unsigned**. No code-signing certificate (EV or standard Authenticode) was supplied to the build process.

- **Authenticode Signature**: Missing
- **Signature State**: `Unsigned`
- **Certificate Status**: No PFX / EV hardware token configured

---

## Production Guidance

To prepare production-ready signed releases:

1. Obtain a valid Windows Authenticode Code Signing Certificate (EV recommended to bypass SmartScreen warnings).
2. Store the PFX file or HSM key container securely in CI/CD secret management.
3. Pass certificate credentials to the build script:
   ```powershell
   .\Build\BuildRelease.ps1 -CertPath "path\to\cert.pfx" -CertPassword "secret"
   ```
   Or use `signtool.exe`:
   ```cmd
   signtool sign /f cert.pfx /p secret /tr http://timestamp.digicert.com /td sha256 /fd sha256 dist\*.exe dist\*.dll
   ```
4. Re-run `generate-checksums.ps1` and `ReleaseValidator` after signing binaries.

---

*Do NOT distribute unsigned binaries to end users without clear instructions on bypass or explicit user trust configuration.*
