using GoodGovernanceApp.Models;
using GoodGovernanceApp.Services;
using GoodGovernanceApp.Utilities;
using GoodGovernanceApp.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Win32;
using Microsoft.Data.Sqlite;
using MySqlConnector;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace GoodGovernanceApp.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
#pragma warning disable CS8618
        public SettingsViewModel() { }
#pragma warning restore CS8618
        // ── Fields ───────────────────────────────────────────────────────────
        private readonly SessionService _sessionService;
        private readonly BackupService _backupService;
        private readonly GoodGovernanceApp.Services.IBackupSchedulerService _scheduler;
        private readonly Microsoft.Extensions.Configuration.IConfiguration _config;
        private readonly GoodGovernanceApp.Data.IDatabaseConfig _databaseConfig;


        // ── Constructor ──────────────────────────────────────────────────────
        public SettingsViewModel(
            SessionService sessionService, 
            Microsoft.Extensions.Configuration.IConfiguration config, 
            GoodGovernanceApp.Data.IDatabaseConfig dbConfig,
            BackupService backupService,
            GoodGovernanceApp.Services.IBackupSchedulerService scheduler)
        {
            _sessionService = sessionService;
            _config = config;
            _databaseConfig = dbConfig;
            _backupService = backupService;
            _scheduler = scheduler;

            LoadSettings();
            LoadBackupSettings();
            RefreshSqliteInfo();

            // ── Connection commands ──────────────────────────────────────────
            TestBothCommand = new RelayCommand(async _ => await ExecuteTestBoth(), _ => !IsTesting);
            SaveSettingsCommand = new RelayCommand(async _ => await ExecuteSaveSettings(null));
            OpenSqliteFolderCommand = new RelayCommand(_ => OpenSqliteFolder());

            // ── Backup commands — all gated behind OTP ───────────────────────
            SaveBackupSettingsCommand = new RelayCommand(async _ => await ExecuteWithOtpAsync(ExecuteSaveBackupSettingsAsync));
            FullBackupCommand = new RelayCommand(async _ => await ExecuteWithOtpAsync(() => ExecuteManualBackupAsync("Full")));
            DifferentialBackupCommand = new RelayCommand(async _ => await ExecuteWithOtpAsync(() => ExecuteManualBackupAsync("Differential")));
            IncrementalBackupCommand = new RelayCommand(async _ => await ExecuteWithOtpAsync(() => ExecuteManualBackupAsync("Incremental")));

            // ── Other commands ───────────────────────────────────────────────
            BrowseBackupFolderCommand = new RelayCommand(_ => BrowseBackupFolder());
            BrowseMySqlDumpCommand = new RelayCommand(_ => BrowseMySqlDump());
            OpenSystemsProfileCommand = new RelayCommand(_ => new SystemsApplicationProfile().ShowDialog());
            OpenCopyrightProfileCommand = new RelayCommand(_ => new CopyrightProfileWindow { DataContext = App.AppHost?.Services.GetRequiredService<CopyrightProfileViewModel>() }.ShowDialog());
            OpenDepartmentsCommand = new RelayCommand(_ =>
            {
                var window = new Window
                {
                    Title = "Department & Project Management",
                    Content = new Views.DepartmentManagementView(),
                    Width = 950,
                    Height = 650,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen
                };
                window.ShowDialog();
            });
        }

        // ── SQLite Local Database Properties ─────────────────────────────────
        public string SqliteDbPath { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GoodGovernanceApp", "ggms.db");

        private string _sqliteStatus = string.Empty;
        public string SqliteStatus
        {
            get => _sqliteStatus;
            set { _sqliteStatus = value; OnPropertyChanged(); }
        }

        private string _sqliteTestResult = string.Empty;
        public string SqliteTestResult
        {
            get => _sqliteTestResult;
            set { _sqliteTestResult = value; OnPropertyChanged(); }
        }

        public ICommand OpenSqliteFolderCommand { get; }

        private void OpenSqliteFolder()
        {
            try
            {
                string folder = Path.GetDirectoryName(SqliteDbPath) ?? string.Empty;
                if (Directory.Exists(folder))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{folder}\"",
                        UseShellExecute = true
                    });
                }
            }
            catch { }
        }

        private void RefreshSqliteInfo()
        {
            try
            {
                if (File.Exists(SqliteDbPath))
                {
                    var fi = new FileInfo(SqliteDbPath);
                    double kb = fi.Length / 1024.0;
                    SqliteStatus = $"Database file ready ({kb:F1} KB) — Last Modified: {fi.LastWriteTime:yyyy-MM-dd HH:mm}";
                }
                else
                {
                    SqliteStatus = "Database file will be created automatically on startup.";
                }
            }
            catch (Exception ex)
            {
                SqliteStatus = $"Status error: {ex.Message}";
            }
        }

        // ── OTP Gate ─────────────────────────────────────────────────────────
        /// <summary>
        /// Opens the OTP verification window using the logged-in user's email.
        /// Only proceeds with <paramref name="action"/> if the OTP is verified.
        /// </summary>
        private async Task ExecuteWithOtpAsync(Func<Task> action)
        {
            string? email = _sessionService.CurrentUser?.Email;

            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show(
                    "No email address is linked to your account.\nCannot send OTP — please contact your administrator.",
                    "OTP Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var otpWindow = new OtpVerificationWindow(email);
            otpWindow.ShowDialog();

            if (otpWindow.IsVerified)
            {
                await action();
            }
            else
            {
                BackupStatusMessage = "⚠ Action cancelled — OTP was not verified.";
            }
        }

        // ── Database Mode ────────────────────────────────────────────────────
        public string DatabaseMode { get; set; } = "Remote";

        // ── Cloud Sync (Hostinger) Fields ────────────────────────────────────
        private string _remoteServer = "194.59.164.58";
        public string RemoteServer
        {
            get => _remoteServer;
            set { _remoteServer = value; OnPropertyChanged(); }
        }

        private string _remotePort = "3306";
        public string RemotePort
        {
            get => _remotePort;
            set { _remotePort = value; OnPropertyChanged(); }
        }

        private string _remoteDatabase = "u621755393_ggms";
        public string RemoteDatabase
        {
            get => _remoteDatabase;
            set { _remoteDatabase = value; OnPropertyChanged(); }
        }

        private string _remoteUser = "u621755393_ggms_user";
        public string RemoteUser
        {
            get => _remoteUser;
            set { _remoteUser = value; OnPropertyChanged(); }
        }

        private string _remotePassword = string.Empty;
        public string RemotePassword
        {
            get => _remotePassword;
            set { _remotePassword = value; OnPropertyChanged(); }
        }

        public string RemoteConnectionString => BuildRemoteConnStr();

        // ── CRS Connection Fields ────────────────────────────────────────────
        private string _crsServer = "localhost";
        public string CrsServer
        {
            get => _crsServer;
            set { _crsServer = value; OnPropertyChanged(); }
        }

        private string _crsPort = "3306";
        public string CrsPort
        {
            get => _crsPort;
            set { _crsPort = value; OnPropertyChanged(); }
        }

        private string _crsDatabase = "crs_db";
        public string CrsDatabase
        {
            get => _crsDatabase;
            set { _crsDatabase = value; OnPropertyChanged(); }
        }

        private string _crsUser = "root";
        public string CrsUser
        {
            get => _crsUser;
            set { _crsUser = value; OnPropertyChanged(); }
        }

        private string _crsPassword = string.Empty;
        public string CrsPassword
        {
            get => _crsPassword;
            set { _crsPassword = value; OnPropertyChanged(); }
        }



        // ── Status ───────────────────────────────────────────────────────────
        private string _statusMessage = string.Empty;
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private string _ggmsTestResult = string.Empty;
        public string GgmsTestResult
        {
            get => _ggmsTestResult;
            set { _ggmsTestResult = value; OnPropertyChanged(); }
        }

        private string _crsTestResult = string.Empty;
        public string CrsTestResult
        {
            get => _crsTestResult;
            set { _crsTestResult = value; OnPropertyChanged(); }
        }

        private bool _isTesting;
        public bool IsTesting
        {
            get => _isTesting;
            set { _isTesting = value; OnPropertyChanged(); }
        }

        // ── Backup Settings ──────────────────────────────────────────────────
        private string _backupType = "Full";
        public string BackupType
        {
            get => _backupType;
            set { _backupType = value; OnPropertyChanged(); }
        }

        private string _scheduleType = "Daily";
        public string ScheduleType
        {
            get => _scheduleType;
            set
            {
                _scheduleType = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsMonthlyOrOnce));
                OnPropertyChanged(nameof(IsOnce));
            }
        }

        private int _scheduleDay = 1;
        public int ScheduleDay
        {
            get => _scheduleDay;
            set { _scheduleDay = value; OnPropertyChanged(); }
        }

        private DateTime _nextRunTime = DateTime.Now.AddDays(1);
        public DateTime NextRunTime
        {
            get => _nextRunTime;
            set { _nextRunTime = value; OnPropertyChanged(); }
        }

        private string _backupFolder = string.Empty;
        public string BackupFolder
        {
            get => _backupFolder;
            set { _backupFolder = value; OnPropertyChanged(); }
        }

        private string _mySqlDumpPath = "mysqldump";
        public string MySqlDumpPath
        {
            get => _mySqlDumpPath;
            set { _mySqlDumpPath = value; OnPropertyChanged(); }
        }

        private bool _isBackupEnabled;
        public bool IsBackupEnabled
        {
            get => _isBackupEnabled;
            set { _isBackupEnabled = value; OnPropertyChanged(); }
        }

        private string _backupStatusMessage = string.Empty;
        public string BackupStatusMessage
        {
            get => _backupStatusMessage;
            set { _backupStatusMessage = value; OnPropertyChanged(); }
        }

        // Visibility helpers for XAML
        public bool IsMonthlyOrOnce => ScheduleType == "Monthly" || ScheduleType == "Once";
        public bool IsOnce => ScheduleType == "Once";

        // ── Commands ─────────────────────────────────────────────────────────
        public ICommand TestBothCommand { get; }
        public ICommand SaveSettingsCommand { get; }
        public ICommand SaveBackupSettingsCommand { get; }
        public ICommand FullBackupCommand { get; }
        public ICommand DifferentialBackupCommand { get; }
        public ICommand IncrementalBackupCommand { get; }
        public ICommand BrowseBackupFolderCommand { get; }
        public ICommand BrowseMySqlDumpCommand { get; }
        public ICommand OpenSystemsProfileCommand { get; }
        public ICommand OpenCopyrightProfileCommand { get; }
        public ICommand OpenDepartmentsCommand { get; }

        public string BuildRemoteConnStr()
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = RemoteServer,
                Port = uint.TryParse(RemotePort, out var p) ? p : 3306,
                Database = RemoteDatabase,
                UserID = RemoteUser,
                Password = RemotePassword,
                AllowZeroDateTime = true,
                ConvertZeroDateTime = true,
                ConnectionTimeout = 15,
                SslMode = MySqlSslMode.None
            };
            return builder.ConnectionString;
        }

        public string BuildCrsConnStr()
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = CrsServer,
                Port = uint.TryParse(CrsPort, out var p) ? p : 3306,
                Database = CrsDatabase,
                UserID = CrsUser,
                Password = CrsPassword,
                AllowZeroDateTime = true,
                ConvertZeroDateTime = true,
                ConnectionTimeout = 15,
                SslMode = MySqlSslMode.None
            };
            return builder.ConnectionString;
        }

        private string ActiveGgmsConnStr => BuildRemoteConnStr();

        // ── Connection Test ───────────────────────────────────────────────────
        private async Task ExecuteTestBoth()
        {
            IsTesting = true;
            SqliteTestResult = "Testing Local SQLite...";
            GgmsTestResult = "Testing Cloud / Sync...";
            CrsTestResult = "Testing CRS...";

            // 1. Test Local SQLite DB
            var sqliteTask = Task.Run(async () =>
            {
                try
                {
                    if (!File.Exists(SqliteDbPath))
                        return (false, "SQLite database file not found yet.");
                    using var conn = new SqliteConnection($"Data Source={SqliteDbPath}");
                    await conn.OpenAsync();
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT COUNT(*) FROM users;";
                    var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
                    return (true, $"{count} users found");
                }
                catch (Exception ex)
                {
                    return (false, ex.Message);
                }
            });

            // 2. Test Cloud Sync (MySQL)
            var ggmsTask = TestConnectionAsync(ActiveGgmsConnStr);

            // 3. Test CRS (MySQL)
            var crsTask = TestConnectionAsync(BuildCrsConnStr());

            await Task.WhenAll(sqliteTask, ggmsTask, crsTask);

            var (sqliteOk, sqliteMsg) = sqliteTask.Result;
            var (ggmsOk, ggmsMsg) = ggmsTask.Result;
            var (crsOk, crsMsg) = crsTask.Result;

            SqliteTestResult = sqliteOk ? $"✅ Local SQLite: Connected ({sqliteMsg})" : $"❌ Local SQLite: {sqliteMsg}";
            GgmsTestResult = ggmsOk ? "✅ Cloud Sync: Connected" : $"⚠ Cloud Sync: Offline / Unreachable ({ggmsMsg})";
            CrsTestResult = crsOk ? "✅ CRS: Connected" : $"⚠ CRS: Offline / Unreachable ({crsMsg})";
            IsTesting = false;
        }

        private static async Task<(bool ok, string msg)> TestConnectionAsync(string connStr)
        {
            if (string.IsNullOrWhiteSpace(connStr))
                return (false, "Connection string is empty");

            try
            {
                // Use a 10-second timeout to avoid long freezes on unreachable hosts
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

                // Push the connection work to a thread pool thread so DNS
                // resolution and TCP handshake don't block the UI thread
                return await Task.Run(async () =>
                {
                    using var conn = new MySqlConnection(connStr);
                    await conn.OpenAsync(cts.Token);
                    return (true, "OK");
                }, cts.Token);
            }
            catch (OperationCanceledException) { return (false, "Connection timed out (10s)"); }
            catch (MySqlException ex) { return (false, $"MySQL Error [{ex.Number}]: {ex.Message}"); }
            catch (Exception ex) { return (false, $"General Error: {ex.Message}"); }
        }

        // ── Load / Save Settings ─────────────────────────────────────────────
        private void LoadSettings()
        {
            try
            {
                // Read Remote Connection string from appsettings.json and split into fields
                string rawRemote = _config.GetConnectionString("RemoteConnection") 
                    ?? "Server=194.59.164.58;Port=3306;Database=u621755393_ggms;User=u621755393_ggms_user;Password=Ggms@2026;AllowZeroDateTime=True;ConvertZeroDateTime=True;";

                if (!string.IsNullOrWhiteSpace(rawRemote))
                {
                    try
                    {
                        var b = new MySqlConnectionStringBuilder(rawRemote);
                        RemoteServer = !string.IsNullOrWhiteSpace(b.Server) ? b.Server : "194.59.164.58";
                        RemotePort = b.Port > 0 ? b.Port.ToString() : "3306";
                        RemoteDatabase = !string.IsNullOrWhiteSpace(b.Database) ? b.Database : "u621755393_ggms";
                        RemoteUser = !string.IsNullOrWhiteSpace(b.UserID) ? b.UserID : "u621755393_ggms_user";
                        RemotePassword = b.Password ?? "";
                    }
                    catch
                    {
                        RemoteServer = "194.59.164.58";
                        RemotePort = "3306";
                        RemoteDatabase = "u621755393_ggms";
                        RemoteUser = "u621755393_ggms_user";
                        RemotePassword = "Ggms@2026";
                    }
                }

                // Load CRS fields from appsettings.json CrsConnection section
                CrsServer   = _config["CrsConnection:Server"]   ?? "svr12367.hstgr.io";
                CrsPort     = _config["CrsConnection:Port"]     ?? "3306";
                CrsDatabase = _config["CrsConnection:Database"] ?? "u621755393_crs";
                CrsUser     = _config["CrsConnection:User"]     ?? "u621755393_crs_user";
                CrsPassword = _config["CrsConnection:Password"] ?? "Crs@2026";
            }
            catch { StatusMessage = "Error loading settings."; }
        }

        private void LoadBackupSettings()
        {
            var s = BackupConfigHelper.Load();
            BackupType = s.BackupType;
            ScheduleType = s.ScheduleType;
            ScheduleDay = s.ScheduleDay;
            NextRunTime = s.NextRunTime;
            BackupFolder = s.BackupFolder;
            MySqlDumpPath = s.MySqlDumpPath;
            IsBackupEnabled = s.IsEnabled;

            _scheduler.Start(s, ActiveGgmsConnStr);
        }

        private async Task ExecuteSaveSettings(object? parameter)
        {
            try
            {
                IsTesting = true;
                StatusMessage = "Verifying and saving settings...";
                await Task.Delay(100);
                IsTesting = false;

                // Write updated settings to appsettings.json (single source of truth)
                _databaseConfig.SaveToAppsettings(
                    DatabaseMode, ActiveGgmsConnStr,
                    CrsServer, CrsPort, CrsDatabase, CrsUser, CrsPassword);

                RefreshSqliteInfo();
                StatusMessage = "Settings saved successfully. Please restart the application.";
                MessageBox.Show(
                    "Settings saved successfully!\n\nPlease restart the application to apply the changes.",
                    "Restart Required", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) 
            { 
                StatusMessage = $"Error saving settings: {ex.Message}";
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Backup: Browse ────────────────────────────────────────────────────
        private void BrowseBackupFolder()
        {
            var dlg = new SaveFileDialog
            {
                Title = "Select Backup Destination Folder",
                FileName = "[Select this folder]",
                Filter = "Folder|*.none",
                CheckFileExists = false,
                CheckPathExists = true,
                ValidateNames = false
            };
            if (!string.IsNullOrWhiteSpace(BackupFolder) && Directory.Exists(BackupFolder))
                dlg.InitialDirectory = BackupFolder;

            if (dlg.ShowDialog() == true)
                BackupFolder = Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
        }

        private void BrowseMySqlDump()
        {
            var dlg = new OpenFileDialog
            {
                Title = "Select mysqldump.exe",
                Filter = "mysqldump.exe|mysqldump.exe|All Executables|*.exe"
            };
            if (dlg.ShowDialog() == true)
                MySqlDumpPath = dlg.FileName;
        }

        // ── Backup: Save Settings (OTP-gated) ────────────────────────────────
        private Task ExecuteSaveBackupSettingsAsync()
        {
            var settings = new BackupSettings
            {
                BackupType = BackupType,
                ScheduleType = ScheduleType,
                ScheduleDay = ScheduleDay,
                NextRunTime = NextRunTime,
                BackupFolder = BackupFolder,
                MySqlDumpPath = MySqlDumpPath,
                IsEnabled = IsBackupEnabled
            };

            BackupConfigHelper.Save(settings);
            _scheduler.Start(settings, ActiveGgmsConnStr);

            BackupStatusMessage = IsBackupEnabled
                ? $"✅ Auto backup enabled — next run: {NextRunTime:yyyy-MM-dd HH:mm}"
                : "⏸ Auto backup disabled and settings saved.";

            return Task.CompletedTask;
        }

        // ── Backup: Manual Run (OTP-gated) ────────────────────────────────────
        private async Task ExecuteManualBackupAsync(string type)
        {
            BackupStatusMessage = $"⏳ Running {type} backup...";

            string folder = string.IsNullOrWhiteSpace(BackupFolder)
                ? Path.Combine(AppContext.BaseDirectory, "Backups")
                : BackupFolder;

            _backupService.MySqlDumpPath = MySqlDumpPath;

            bool success = type switch
            {
                "Differential" => await _backupService.CreateDifferentialBackupAsync(ActiveGgmsConnStr, folder),
                "Incremental" => await _backupService.CreateIncrementalBackupAsync(ActiveGgmsConnStr, folder),
                _ => await _backupService.CreateFullBackupAsync(ActiveGgmsConnStr, folder)
            };

            BackupStatusMessage = success
                ? $"✅ {type} backup saved to: {folder}"
                : $"❌ {type} backup failed. Check backup_errors.log in {folder}";
        }
    }
}
