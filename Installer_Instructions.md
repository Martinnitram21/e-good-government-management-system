# Good Governance Management System Installer

The Windows installer is built with Inno Setup and contains a self-contained
`win-x64` .NET application. Target computers do not need a separate .NET
installation.

## Prerequisite

Install Inno Setup 6 on the build computer.

## Build

Open PowerShell in the project directory and run:

```powershell
powershell -ExecutionPolicy Bypass -File .\build_installer.ps1
```

The script creates a clean multi-file publish, which is required by the WPF
resources in this application, and then compiles the installer.

## Prefilled database configuration

The installer displays prefilled wizard pages for:

- Online (remote) GGMS database
- Office Network (LAN) GGMS database
- Office Network (LAN) CRS database

After installation, the confirmed settings are written to both the application
folder and `%AppData%\GoodGovernanceApp\appsettings.json`. Writing the per-user
copy ensures that reinstalling over an older version refreshes the configuration
that the application actually loads.

The credentials are intentionally bundled in the installer to meet the prefilled
deployment requirement. Distribute the installer only to authorized users.

## Output

The generated installer is:

`InstallerOutput\GoodGovernanceSetup-1.0.8.exe`

The build script also copies it to the current user's Downloads folder.
