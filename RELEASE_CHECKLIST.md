# Release Checklist

## 1) Pre-checks
- [ ] `Installer\Redist\windowsdesktop-runtime-10.0.11-win-x64.exe` exists
- [ ] Solution builds cleanly in Release (`0 errors`, ideally `0 warnings`)
- [ ] Latest alarm popup/UI/audio changes verified locally

## 2) Build (single command)
- [ ] Run:
  - `powershell -ExecutionPolicy Bypass -File Installer\BuildInstaller.ps1`
- [ ] Confirm script completed successfully

## 3) Version verification
- [ ] `Setup.iss` -> `#define MyAppVersion` incremented
- [ ] `WorldTimeAlarms.csproj` synchronized:
  - `<Version>`
  - `<AssemblyVersion>`
  - `<FileVersion>`
  - `<InformationalVersion>`
- [ ] Installer filename matches version:
  - `InstallerOutput\WorldTimeAlarms-Setup-<version>.exe`

## 4) Installer validation
- [ ] Test install on clean machine/VM
- [ ] Confirm .NET runtime prerequisite installs correctly
- [ ] Confirm app launches after install
- [ ] Confirm About window shows the same version

## 5) Functional smoke test
- [ ] Create alarm and trigger popup
- [ ] Verify popup height/content (no extra empty space)
- [ ] Verify updated alarm/notification sounds
- [ ] Verify language switching (ES/EN)

## 6) Distribution readiness
- [ ] Generate SHA256 hash of final installer
- [ ] Store build date/time + version + hash in release notes
- [ ] Digitally sign installer executable (recommended)
