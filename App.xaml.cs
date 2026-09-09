using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Views;
using System.Globalization;
using System.Threading;

namespace GoodGovernanceApp;

public partial class App : Application
    {
            public static IHost? AppHost { get; private set; }
    public static IConfiguration Config { get; set; } = null!;

    public App()
    {
        this.DispatcherUnhandledException += (s, e) =>
        {
            string msg = e.Exception.Message;
            var inner = e.Exception.InnerException;
            while (inner != null)
            {
                msg += $"\n\nDetails: {inner.Message}";
                inner = inner.InnerException;
            }
            MessageBox.Show($"UI EXCEPTION:\n\n{msg}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                string msg = ex.Message;
                var inner = ex.InnerException;
                while (inner != null)
                {
                    msg += $"\n\nDetails: {inner.Message}";
                    inner = inner.InnerException;
                }
                MessageBox.Show($"FATAL EXCEPTION:\n\n{msg}", "Fatal Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        var culture = new CultureInfo("en-PH");
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage(culture.IetfLanguageTag)));

        try
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string appDataFolder = !string.IsNullOrEmpty(appData) ? Path.Combine(appData, "GoodGovernanceApp") : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(appDataFolder);
            string appDataSettingsPath = Path.Combine(appDataFolder, "appsettings.json");

            if (!File.Exists(appDataSettingsPath))
            {
                // When using PublishSingleFile, BaseDirectory is a temp folder. We need the actual .exe path.
                string exePath = Environment.ProcessPath ?? "";
                string exeDir = !string.IsNullOrEmpty(exePath) ? (Path.GetDirectoryName(exePath) ?? AppDomain.CurrentDomain.BaseDirectory) : AppDomain.CurrentDomain.BaseDirectory;
                
                string exeSettingsPath = !string.IsNullOrEmpty(exeDir) ? Path.Combine(exeDir, "appsettings.json") : "";
                string baseSettingsPath = !string.IsNullOrEmpty(AppDomain.CurrentDomain.BaseDirectory) ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json") : "";

                if (!string.IsNullOrEmpty(exeSettingsPath) && File.Exists(exeSettingsPath))
                {
                    File.Copy(exeSettingsPath, appDataSettingsPath, true);
                }
                else if (!string.IsNullOrEmpty(baseSettingsPath) && File.Exists(baseSettingsPath))
                {
                    File.Copy(baseSettingsPath, appDataSettingsPath, true);
                }
                else
                {
                    // Fallback: create default appsettings.json automatically
                    const string defaultJson = @"{
  ""ConnectionStrings"": {
    ""RemoteConnection"": ""Server=193.203.175.157;Port=3306;Database=u518908950_ggms;User=u518908950_ggms;Password=Sulop@2025;AllowZeroDateTime=True;ConvertZeroDateTime=True;"",
    ""LocalConnection"": ""Server=127.0.0.1;Port=3306;Database=govern;User=root;Password=root;SslMode=None;AllowPublicKeyRetrieval=True;""
  },
  ""CrsConnection"": {
    ""Server"": ""193.203.175.157"",
    ""Port"": ""3306"",
    ""Database"": ""u518908950_crs"",
    ""User"": ""u518908950_crs"",
    ""Password"": ""Sulop@2025""
  },
  ""SmtpConnection"": {
    ""EmailAddress"": ""bryanluy822@gmail.com"",
    ""AppPassword"": ""afyyknunzimihtrv""
  },
  ""AppSettings"": {
    ""DatabaseMode"": ""Remote"",
    ""UseRemoteDatabase"": false,
    ""MySqlDumpPath"": ""mysqldump""
  }
}";
                    File.WriteAllText(appDataSettingsPath, defaultJson);
                }
            }

            AppHost = Host.CreateDefaultBuilder()
                .ConfigureAppConfiguration((context, config) =>
                {
                    config.Sources.Clear();
                    config.SetBasePath(appDataFolder);
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<AppDbContext>(options =>
                {
                    options.UseSqlite($"Data Source={Path.Combine(appDataFolder, "ggms.db")}");
                }, ServiceLifetime.Transient, ServiceLifetime.Transient);

                services.AddDbContext<CloudDbContext>((serviceProvider, options) =>
                {
                    try
                    {
                        var dbConfig = serviceProvider.GetRequiredService<IDatabaseConfig>();
                        var connStr = dbConfig.ConnectionString;
                        options.UseMySql(connStr, new MySqlServerVersion(new Version(8, 0, 31)));
                    }
                    catch
                    {
                        // No valid MySQL connection string configured — use a dummy so DI doesn't crash.
                        // The CloudDbContext will only be used when IsOnline = true.
                        options.UseMySql("Server=localhost;Database=ggms;User=root;Password=;", new MySqlServerVersion(new Version(8, 0, 31)));
                    }
                }, ServiceLifetime.Transient, ServiceLifetime.Transient);

                services.AddSingleton<IDatabaseConfig, DatabaseConfig>();
                services.AddSingleton<GoodGovernanceApp.Services.IConnectivityService, GoodGovernanceApp.Services.ConnectivityService>();
                services.AddSingleton<GoodGovernanceApp.Services.ISyncService, GoodGovernanceApp.Services.SyncService>();
                services.AddSingleton<GoodGovernanceApp.Services.IBackupSchedulerService, GoodGovernanceApp.Services.BackupSchedulerService>();

                services.AddSingleton<DatabaseHelper>();
                services.AddSingleton<GoodGovernanceApp.Services.ValidationService>();
                services.AddSingleton<GoodGovernanceApp.Services.SessionService>();
                services.AddSingleton<GoodGovernanceApp.Services.FileService>();
                services.AddSingleton<GoodGovernanceApp.Services.BackupService>();
                services.AddSingleton<GoodGovernanceApp.Services.EmailService>();
                services.AddSingleton<GoodGovernanceApp.Services.OtpService>();
                services.AddTransient<GoodGovernanceApp.Services.ICrsBeneficiaryService, GoodGovernanceApp.Services.CrsBeneficiaryService>();

                services.AddTransient<MainWindow>();
                services.AddTransient<GoodGovernanceApp.Views.LoginWindow>();
                services.AddTransient<GoodGovernanceApp.ViewModels.LoginViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.AddProjectViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.ApplicationProfileViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.AuditLogViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.BudgetAllocationViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.BudgetTransactionsViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.BudgetYearSelectionViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.ConsolidatedTransactionsPageViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.ConsolidatedTransactionsViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.CopyrightProfileViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.CrsBeneficiaryViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.DashboardViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.DepartmentManagementViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.EvaluationViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.FileUploadViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.MainViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.ParametersViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.ProfileViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.ProjectDetailsViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.ReportsViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.Reports.BeneficiaryReportsViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.Reports.FinancialReportsViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.Reports.ProjectReportsViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.Reports.SystemReportsViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.Reports.TransactionReportsViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.SettingsViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.SimpleDashboardViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.SystemsApplicationProfileViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.TrademarkViewModel>();
                services.AddTransient<GoodGovernanceApp.ViewModels.UserManagementViewModel>();
            })
            .Build();

            Config = AppHost.Services.GetRequiredService<IConfiguration>();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"CRITICAL STARTUP ERROR:\n\n{ex.Message}\n\nThe application will now close.", "Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Environment.Exit(1);
        }
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await AppHost!.StartAsync();

        var connectivity = AppHost.Services.GetRequiredService<GoodGovernanceApp.Services.IConnectivityService>();
        connectivity.StartMonitoring();

        var syncService = AppHost.Services.GetRequiredService<GoodGovernanceApp.Services.ISyncService>();
        syncService.StartAutoSync();

        string appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GoodGovernanceApp");

        Directory.CreateDirectory(Path.Combine(appData, "Logos"));
        Directory.CreateDirectory(Path.Combine(appData, "ProfilePhotos"));

        base.OnStartup(e);

        // ── Initialise SQLite database BEFORE showing the login window ──────────
        await Task.Run(async () =>
        {
            using var scope = AppHost.Services.CreateScope();
            var services = scope.ServiceProvider;
            try
            {
                var dbContext = services.GetRequiredService<AppDbContext>();

                // Step 1: Create all SQLite tables from EF model
                bool created = await dbContext.Database.EnsureCreatedAsync();
                System.Diagnostics.Debug.WriteLine($"[DB Init] EnsureCreated result: {created}");

                // ── Step 2: Verify the users table actually exists ────────────────
                bool usersTableExists = false;
                try
                {
                    var conn = dbContext.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open)
                        await conn.OpenAsync();

                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='users';";
                    var result = await cmd.ExecuteScalarAsync();
                    usersTableExists = result != null;
                    System.Diagnostics.Debug.WriteLine($"[DB Init] 'users' table exists: {usersTableExists}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[DB Init] Table check failed: {ex.Message}");
                }

                // ── Step 3: Connectivity check (warning only, not a blocker) ─────
                Exception? connError = null;
                bool canConn = await Task.Run(() =>
                {
                    try { return dbContext.Database.CanConnect(); }
                    catch (Exception ex) { connError = ex; return false; }
                });

                if (!canConn)
                {
                    string dbMode = Config["AppSettings:DatabaseMode"] ?? "Local";
                    if (dbMode != "Local")
                    {
                        string errorMsg = "Cloud/LAN Database Connection Failed!\n\nThe application will continue using the local SQLite database.";
                        if (connError != null)
                        {
                            string realErr = connError.InnerException?.Message ?? connError.Message;
                            errorMsg += $"\n\nError Details:\n{realErr}";
                        }
                        Application.Current.Dispatcher.Invoke(() =>
                            MessageBox.Show(errorMsg, "Cloud Sync Warning",
                                MessageBoxButton.OK, MessageBoxImage.Warning));
                    }
                }

                // ── Step 4: SQLite schema patches (safe to run on every startup) ──
                //   Two-step pattern:
                //   1) ADD COLUMN as TEXT NULL — works on ALL SQLite versions.
                //      (SQLite rejects NOT NULL with non-constant DEFAULT expressions.)
                //   2) UPDATE to backfill any rows that still have a NULL SyncId.
                //   Each statement is wrapped in try/catch so it is silently skipped
                //   if the column already exists.
                await Task.Run(() =>
                {
                    var conn = dbContext.Database.GetDbConnection();
                    if (conn.State != System.Data.ConnectionState.Open)
                        conn.Open();

                    // UUID expression that works in SQLite (generates a v4-like UUID)
                    const string uuidExpr =
                        "lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' ||" +
                        " substr(lower(hex(randomblob(2))),2) || '-' ||" +
                        " substr('89ab', abs(random()) % 4 + 1, 1) ||" +
                        " substr(lower(hex(randomblob(2))),2) || '-' || lower(hex(randomblob(6)))";

                    void AddSyncCols(string table)
                    {
                        using (var cmd = conn.CreateCommand())
                        {
                            // Step 1 – add as nullable (never fails on existing column)
                            cmd.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"SyncId\" TEXT NULL;";
                            try { cmd.ExecuteNonQuery(); } catch { }

                            cmd.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"updated_at\" TEXT NULL;";
                            try { cmd.ExecuteNonQuery(); } catch { }

                            // Step 2 – backfill existing rows that have no SyncId
                            cmd.CommandText = $"UPDATE \"{table}\" SET \"SyncId\" = ({uuidExpr}) WHERE \"SyncId\" IS NULL OR \"SyncId\" = '';";
                            try { cmd.ExecuteNonQuery(); } catch { }
                        }
                    }

                    void AddCol(string table, string column, string typeDef)
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = $"ALTER TABLE \"{table}\" ADD COLUMN \"{column}\" {typeDef};";
                        try { cmd.ExecuteNonQuery(); } catch { }
                    }

                    // All tables that participate in bidirectional sync
                    AddSyncCols("project_details");
                    AddSyncCols("users");
                    AddSyncCols("tbl_offices");
                    AddSyncCols("master_budget");
                    AddSyncCols("officeallocations");
                    AddSyncCols("tbl_program_provision");
                    AddSyncCols("tbl_services");
                    AddSyncCols("tbl_transaction");

                    AddSyncCols("consolidated_transactions");
                    AddSyncCols("master_budget");


                    // Extra columns added later by schema evolution
                    AddCol("project_details",        "voucher_code", "TEXT NULL");
                    AddCol("tbl_transaction",         "voucher_code", "TEXT NULL");
                    AddCol("tbl_offices",             "office_code",  "TEXT NULL");

                    AddCol("consolidated_transactions", "barangay",   "TEXT NULL");
                    AddCol("consolidated_transactions", "household_no", "TEXT NULL");
                    AddCol("project_details",         "status",       "TEXT NOT NULL DEFAULT 'active'");
                    AddCol("project_details",         "system_name",  "TEXT NULL");
                    AddCol("consolidated_transactions", "project_details_id", "TEXT NULL");

                    // Seed sqlite sequences with seconds since 2026 to guarantee monotonically increasing 
                    // IDs across database wipes, completely avoiding Cloud vs Local ID collisions!
                    int epochOffset = (int)(DateTime.UtcNow - new DateTime(2026, 1, 1)).TotalSeconds;
                    // Ensure a minimum offset just in case the system clock is wrong
                    if (epochOffset < 1000000) epochOffset = 1000000;

                    void SeedSequence(string table)
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = $"INSERT OR IGNORE INTO sqlite_sequence (name, seq) VALUES ('{table}', {epochOffset});";
                        try { cmd.ExecuteNonQuery(); } catch { }
                    }

                    SeedSequence("project_details");
                    SeedSequence("users");
                    SeedSequence("tbl_offices");
                    SeedSequence("master_budget");
                    SeedSequence("officeallocations");
                    SeedSequence("tbl_program_provision");
                    SeedSequence("tbl_services");
                    SeedSequence("tbl_transaction");
                    SeedSequence("consolidated_transactions");

                    // Ensure master_budget table and columns exist in local SQLite
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            CREATE TABLE IF NOT EXISTS ""master_budget"" (
                                ""id"" INTEGER PRIMARY KEY AUTOINCREMENT,
                                ""budget_year"" TEXT,
                                ""total_budget"" TEXT,
                                ""description"" TEXT,
                                ""SyncId"" TEXT,
                                ""updated_at"" TEXT
                            );";
                        try { cmd.ExecuteNonQuery(); } catch { }
                    }
                    AddCol("master_budget", "budget_year", "TEXT NULL");
                    AddCol("master_budget", "total_budget", "TEXT NULL");
                    AddCol("master_budget", "allocated_budget", "TEXT NULL");
                    AddCol("master_budget", "remaining_budget", "TEXT NULL");
                    AddCol("master_budget", "status", "TEXT NULL");
                    AddCol("master_budget", "description", "TEXT NULL");
                    AddCol("master_budget", "created_by", "INTEGER NULL");
                    AddCol("master_budget", "created_at", "TEXT NULL");

                    // Migrate data from the old yearlybudgets table to the new master_budget table locally
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = @"
                            INSERT INTO ""master_budget"" (""budget_year"", ""total_budget"", ""description"", ""SyncId"")
                            SELECT CAST(""Year"" AS TEXT), ""TotalAmount"", ""Description"", ""SyncId""
                            FROM ""yearlybudgets""
                            WHERE CAST(""Year"" AS TEXT) NOT IN (SELECT ""budget_year"" FROM ""master_budget"");
                        ";
                        try { cmd.ExecuteNonQuery(); } catch { }

                        // Fix DBNull errors for EF Core by setting defaults on non-nullable fields
                        cmd.CommandText = @"
                            UPDATE ""master_budget"" SET ""allocated_budget"" = '0.00' WHERE ""allocated_budget"" IS NULL;
                            UPDATE ""master_budget"" SET ""remaining_budget"" = ""total_budget"" WHERE ""remaining_budget"" IS NULL;
                            UPDATE ""master_budget"" SET ""status"" = 'active' WHERE ""status"" IS NULL;
                            UPDATE ""master_budget"" SET ""created_by"" = 1 WHERE ""created_by"" IS NULL;
                        ";
                        try { cmd.ExecuteNonQuery(); } catch { }
                    }
                });

                // ── Step 4b: MySQL-only schema patches ───────────────────────────
                string mode = Config["AppSettings:DatabaseMode"] ?? "Local";
                if (mode != "Local")
                {
                    await Task.Run(() =>
                    {
                        try { dbContext.Database.ExecuteSqlRaw("ALTER TABLE tbl_offices ADD COLUMN office_code VARCHAR(30) NULL AFTER name;"); } catch { }
                        try { dbContext.Database.ExecuteSqlRaw("ALTER TABLE tbl_offices ALTER COLUMN active SET DEFAULT 1;"); } catch { }


                    });
                }

                // ── Step 5: Seed only after tables are confirmed to exist ─────────
                if (usersTableExists)
                {
                    await Task.Run(() => DatabaseSeeder.SeedData(dbContext));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[DB Init] Skipping seed — 'users' table missing after EnsureCreated. Delete ggms.db and restart.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[App Startup] DB init error: {ex.Message}\n{ex.StackTrace}");
            }
        });

        // Show login window only AFTER the database is fully initialized
        var loginWindow = AppHost.Services.GetRequiredService<GoodGovernanceApp.Views.LoginWindow>();
        loginWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await AppHost!.StopAsync();
        AppHost.Dispose();
        base.OnExit(e);
    }
}



