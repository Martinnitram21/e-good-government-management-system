using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace GoodGovernanceApp.Services;

/// <summary>
/// Provides automated and manual backups for SQLite and MySQL databases.
/// </summary>
public class BackupService
{
    public string MySqlDumpPath { get; set; } = "mysqldump";

    public string SqliteDbPath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "GoodGovernanceApp", "ggms.db");

    // ── Public entry points ───────────────────────────────────────────────────

    public async Task<bool> CreateFullBackupAsync(string connectionString, string backupDirectory)
    {
        if (File.Exists(SqliteDbPath) || string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase))
        {
            return await CreateSqliteBackupAsync(backupDirectory, "FullBackup");
        }
        return await ExecuteBackupAsync(connectionString, backupDirectory, "FullBackup", extraArgs: "--single-transaction --routines --triggers --events");
    }

    public async Task<bool> CreateDifferentialBackupAsync(string connectionString, string backupDirectory)
    {
        if (File.Exists(SqliteDbPath) || string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase))
        {
            return await CreateSqliteBackupAsync(backupDirectory, "DiffBackup");
        }
        return await ExecuteBackupAsync(connectionString, backupDirectory, "DiffBackup", extraArgs: "--single-transaction --no-create-info");
    }

    public async Task<bool> CreateIncrementalBackupAsync(string connectionString, string backupDirectory)
    {
        if (File.Exists(SqliteDbPath) || string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains(".db", StringComparison.OrdinalIgnoreCase))
        {
            return await CreateSqliteBackupAsync(backupDirectory, "IncBackup");
        }
        return await ExecuteBackupAsync(connectionString, backupDirectory, "IncBackup", extraArgs: "--single-transaction --no-create-info");
    }

    // ── SQLite Backup (Safe Online Snapshot) ───────────────────────────────────
    public async Task<bool> CreateSqliteBackupAsync(string backupDirectory, string prefix = "FullBackup")
    {
        try
        {
            if (!Directory.Exists(backupDirectory))
                Directory.CreateDirectory(backupDirectory);

            if (!File.Exists(SqliteDbPath))
            {
                LogError(backupDirectory, $"SQLite database file not found at: {SqliteDbPath}");
                return false;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName = $"{prefix}_ggms_{timestamp}.db";
            string destFile = Path.Combine(backupDirectory, fileName);

            using (var sourceConn = new SqliteConnection($"Data Source={SqliteDbPath}"))
            using (var destConn = new SqliteConnection($"Data Source={destFile}"))
            {
                await sourceConn.OpenAsync();
                await destConn.OpenAsync();
                sourceConn.BackupDatabase(destConn);
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError(backupDirectory, $"SQLite backup error: {ex.Message}");
            return false;
        }
    }

    // ── Core execution ────────────────────────────────────────────────────────

    private async Task<bool> ExecuteBackupAsync(
        string connectionString,
        string backupDirectory,
        string prefix,
        string extraArgs = "")
    {
        try
        {
            if (!Directory.Exists(backupDirectory))
                Directory.CreateDirectory(backupDirectory);

            string? dbServer = ExtractValue(connectionString, "Server");
            string? dbPort   = ExtractValue(connectionString, "Port") ?? "3306";
            string? dbUser   = ExtractValue(connectionString, "User")
                            ?? ExtractValue(connectionString, "Uid");
            string? dbPass   = ExtractValue(connectionString, "Password")
                            ?? ExtractValue(connectionString, "Pwd");
            string? dbName   = ExtractValue(connectionString, "Database");

            if (string.IsNullOrWhiteSpace(dbName))
            {
                LogError(backupDirectory, "Cannot determine database name from connection string.");
                return false;
            }

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fileName  = $"{prefix}_{dbName}_{timestamp}.sql";
            string filePath  = Path.Combine(backupDirectory, fileName);

            string passwordArg = string.IsNullOrWhiteSpace(dbPass) ? "" : $"-p\"{dbPass}\"";
            string arguments   =
                $"-h \"{dbServer}\" -P {dbPort} -u \"{dbUser}\" {passwordArg} " +
                $"{extraArgs} \"{dbName}\" --result-file=\"{filePath}\"";

            var psi = new ProcessStartInfo
            {
                FileName               = MySqlDumpPath,
                Arguments              = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            using var process = Process.Start(psi);
            if (process is null)
            {
                LogError(backupDirectory, "Failed to start mysqldump process. Check MySqlDumpPath.");
                return false;
            }

            // Capture stderr for error logging.
            string stderr = await process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                LogError(backupDirectory,
                    $"mysqldump exited with code {process.ExitCode}.\nStderr: {stderr}");
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogError(backupDirectory, $"Exception in {prefix}: {ex.Message}");
            return false;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void LogError(string backupDirectory, string message)
    {
        try
        {
            if (!Directory.Exists(backupDirectory))
                Directory.CreateDirectory(backupDirectory);

            string logPath = Path.Combine(backupDirectory, "backup_errors.log");
            string entry   = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}";
            File.AppendAllText(logPath, entry);
        }
        catch { /* Must not throw from an error handler. */ }
    }

    private static string? ExtractValue(string connectionString, string key)
    {
        foreach (var part in connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim().Equals(key, StringComparison.OrdinalIgnoreCase))
                return kv[1].Trim();
        }
        return null;
    }
}
