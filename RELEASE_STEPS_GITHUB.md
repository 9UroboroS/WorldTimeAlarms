# GitHub Release Steps (WorldTimeAlarms)

## Repository
- Owner: `9UroboroS`
- Repo: `WorldTimeAlarms`
- Branch: `main`

## 1) Commit and push current changes
Run in Git Bash from repository root:

```bash
git add .gitignore update.json WorldTimeAlarms/AppUpdateService.cs Installer/BuildInstaller.ps1 Setup.iss RELEASE_STEPS_GITHUB.md
git commit -m "Configure updater URLs for 9UroboroS and prepare release workflow"
git push
```

If you removed tracked runtime binaries:

```bash
git add .gitignore
git commit -m "Stop tracking bundled runtime binaries"
git push
```

## 2) Build installer (auto-bumps version)

```powershell
powershell -ExecutionPolicy Bypass -File Installer\BuildInstaller.ps1
```

Expected outputs:
- `InstallerOutput\WorldTimeAlarms-Setup-<version>.exe`
- `InstallerOutput\WorldTimeAlarms-Setup-<version>.sha256.txt`
- `update.json` updated with same `<version>` and GitHub release URL.

## 3) Commit post-build metadata updates

```bash
git add Setup.iss WorldTimeAlarms/WorldTimeAlarms.csproj update.json
git commit -m "Release metadata sync for v<version>"
git push
```

Replace `<version>` with the generated version (example: `1.0.17`).

## 4) Create GitHub release
1. Open: `https://github.com/9UroboroS/WorldTimeAlarms/releases/new`
2. Tag: `v<version>`
3. Target: `main`
4. Title: `WorldTimeAlarms v<version>`
5. Attach files:
   - `InstallerOutput/WorldTimeAlarms-Setup-<version>.exe`
   - `InstallerOutput/WorldTimeAlarms-Setup-<version>.sha256.txt`
6. Publish release.

## 5) Verify updater endpoint
Check in browser:
- `https://raw.githubusercontent.com/9UroboroS/WorldTimeAlarms/main/update.json`

Must return JSON with:
- `version: "<version>"`
- `installerUrl: "https://github.com/9UroboroS/WorldTimeAlarms/releases/download/v<version>/WorldTimeAlarms-Setup-<version>.exe"`

## 6) Verify in-app update
From an older installed version:
1. Open app.
2. Settings → **Buscar ahora / Check now**.
3. Confirm it detects `v<version>` and opens installer.
