using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.Utilities;
using GoodGovernanceApp.Data;

namespace GoodGovernanceApp.Services;

/// <summary>
/// Singleton background service that checks every minute whether a scheduled
/// backup is due and triggers it automatically.
/// 
/// Scheduling logic:
///   Once      – runs one time at NextRunTime, then disables itself.
///   Daily     – advances NextRunTime by 1 day after each run.
///   Weekly    – advances NextRunTime by 7 days after each run.
///   Monthly   – advances NextRunTime by 1 calendar month after each run,
///               keeping the configured ScheduleDay as the day of the month.
/// </summary>
public interface IBackupSchedulerService : IDisposable
{
    void Start(BackupSettings settings, string connectionString = "");
    void Stop();
}

public class BackupSchedulerService : IBackupSchedulerService
{
    private readonly IDatabaseConfig _dbConfig;
    private Timer? _timer;
    private BackupSettings _settings = new();
    private readonly object _lock = new();
    private readonly BackupService _backupService = new();

    // ── Public API ────────────────────────────────────────────────────────────

    public BackupSchedulerService(IDatabaseConfig dbConfig)
    {
        _dbConfig = dbConfig;
    }

    /// <summary>Start (or restart) the scheduler with the given settings.</summary>
    public void Start(BackupSettings settings, string connectionString = "")
    {
        lock (_lock)
        {
            _settings = settings;
            _backupService.MySqlDumpPath = settings.MySqlDumpPath;
        }

        // Stop any running timer before restarting.
        _timer?.Dispose();

        if (!settings.IsEnabled) return;

        // Tick every 60 seconds; first tick after 5 s so startup delay is minimal.
        _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1));
    }

    /// <summary>Stop the scheduler without changing saved settings.</summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    // ── Timer Callback ────────────────────────────────────────────────────────

    private void OnTimerTick(object? state)
    {
        BackupSettings settings;
        lock (_lock)
        {
            settings = _settings;
        }
        if (!settings.IsEnabled) return;
        if (DateTime.Now < settings.NextRunTime) return;

        // Run backup asynchronously; fire-and-forget with proper logging inside.
        _ = Task.Run(async () =>
        {
            string folder = string.IsNullOrWhiteSpace(settings.BackupFolder)
                ? Path.Combine(AppContext.BaseDirectory, "Backups")
                : settings.BackupFolder;

            string connStr = _dbConfig.ConnectionString;
            bool success = settings.BackupType switch
            {
                "Differential" => await _backupService.CreateDifferentialBackupAsync(connStr, folder),
                "Incremental"  => await _backupService.CreateIncrementalBackupAsync(connStr, folder),
                _              => await _backupService.CreateFullBackupAsync(connStr, folder)
            };

            string logLine = success
                ? $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ✅ Scheduled {settings.BackupType} backup completed."
                : $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ❌ Scheduled {settings.BackupType} backup FAILED.";

            AppendToLog(folder, logLine);

            // Advance NextRunTime and persist.
            lock (_lock)
            {
                AdvanceSchedule();
                BackupConfigHelper.Save(_settings);
            }
        });
    }

    // ── Schedule Advancement ─────────────────────────────────────────────────

    private void AdvanceSchedule()
    {
        switch (_settings.ScheduleType)
        {
            case "Once":
                // Only runs once — disable after execution.
                _settings.IsEnabled = false;
                Stop();
                break;

            case "Daily":
                _settings.NextRunTime = _settings.NextRunTime.AddDays(1);
                break;

            case "Weekly":
                _settings.NextRunTime = _settings.NextRunTime.AddDays(7);
                break;

            case "Monthly":
                // Keep the same time-of-day, advance to the next calendar month.
                var next = _settings.NextRunTime.AddMonths(1);
                int maxDay = DateTime.DaysInMonth(next.Year, next.Month);
                int targetDay = Math.Min(_settings.ScheduleDay, maxDay);
                _settings.NextRunTime = new DateTime(next.Year, next.Month, targetDay,
                    _settings.NextRunTime.Hour, _settings.NextRunTime.Minute, 0);
                break;
        }
    }

    // ── Log Helper ────────────────────────────────────────────────────────────

    private static void AppendToLog(string folder, string message)
    {
        try
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            string logPath = Path.Combine(folder, "backup_schedule.log");
            File.AppendAllText(logPath, message + Environment.NewLine);
        }
        catch { /* If logging itself fails we must not crash the timer thread. */ }
    }

    public void Dispose()
    {
        _timer?.Dispose();
    }
}
