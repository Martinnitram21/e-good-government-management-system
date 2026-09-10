# Good Governance Management System Installer Instructions

Since we are targeting a Windows environment, the most straightforward approach to generate a clean "Package Installer" for both the Super Admin and standard User Clients is to use **Inno Setup** to package the published `.NET 8/10 WPF` application.

## Prerequisites
1. Download and install **Inno Setup** (https://jrsoftware.org/isinfo.php).
2. Install the **.NET 8.0/10.0 Desktop Runtime** on the target machines (or choose self-contained deployment below).

## Step 1: Publish the Application
We need to generate a standalone build that is separated from development files.

Open a terminal in the `C:\Users\Asus\Documents\GGS\GoodGovernanceApp` directory and run:

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
```

This produces a **self-contained single-file executable** that bundles the .NET runtime — recipients do **NOT** need to install .NET separately. The output will be in the `.\publish` folder.

## Step 2: Create the Inno Setup Script (`installer.iss`)
Create a new file named `installer.iss` in the project root with the following content:

```pascal
[Setup]
AppName=Good Governance System
AppVersion=1.0.0
DefaultDirName={pf}\GoodGovernanceSystem
DefaultGroupName=Good Governance System
OutputDir=.\Installer Output
OutputBaseFilename=GoodGovernanceSetup
Compression=lzma2
SolidCompression=yes

[Files]
; Copy all published files to the installation directory
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
; Create Desktop and Start Menu shortcuts pointing to the main executeable
Name: "{group}\Good Governance System"; Filename: "{app}\GoodGovernanceApp.exe"
Name: "{commondesktop}\Good Governance System"; Filename: "{app}\GoodGovernanceApp.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop icon"; GroupDescription: "Additional icons:"
```

## Step 3: Compile the Installer
1. Open the `installer.iss` file you just created using the **Inno Setup Compiler**.
2. Click the **Compile** button (or press `Ctrl+F9`).
3. Once compiled, an `Installer Output/GoodGovernanceSetup.exe` file will be generated. 

## Step 4: Client Distribution
- You can distribute the generated `GoodGovernanceSetup.exe` to your users.
- Because we exposed the dual Database Connection settings within the UI (under the "Settings" menu), Super Admins and Users can install the *exact same underlying package*!
- Upon first launch, the App will automatically use the bundled **Local SQLite database** (`ggms.db`), requiring zero setup or installation of MySQL on the client machine!
- To enable automatic sync, the Super Admin simply logs in, opens Settings, and verifies the connections: **Online Database (main)** on `194.59.164.58` and the **Network databases** (GGMS + CRS) on `192.168.0.47`, then saves. The system will then seamlessly operate in an offline-first manner, syncing with the online server in the background whenever it is reachable.
