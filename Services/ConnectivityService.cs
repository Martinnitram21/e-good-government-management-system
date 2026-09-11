using System;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using GoodGovernanceApp.Data;
using MySqlConnector;

namespace GoodGovernanceApp.Services;

public interface IConnectivityService
{
    bool IsOnline { get; }
    bool IsCrsOnline { get; }
    bool IsNetworkOnline { get; }
    bool IsInternetAvailable { get; }
    bool HasChecked { get; }
    event Action<bool>? OnConnectionStatusChanged;
    void StartMonitoring();
    void SyncCurrentStatus();
    Task<bool> RefreshNowAsync();
}

public class ConnectivityService : IConnectivityService
{
    private volatile bool _isOnline = false;
    private volatile bool _isNetworkOnline = false;
    private volatile bool _isCrsOnline = false;
    private volatile bool _isInternetAvailable = false;
    private volatile bool _hasChecked = false;
    private readonly IDatabaseConfig _dbConfig;
    private readonly CancellationTokenSource _cts = new();
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private DateTime _lastRefreshUtc = DateTime.MinValue;
    private int _monitorStarted;

    public event Action<bool>? OnConnectionStatusChanged;

    public bool IsOnline => _isOnline;
    public bool IsNetworkOnline => _isNetworkOnline;
    public bool IsCrsOnline => _isCrsOnline;
    public bool IsInternetAvailable => _isInternetAvailable;
    public bool HasChecked => _hasChecked;

    public ConnectivityService(IDatabaseConfig dbConfig)
    {
        _dbConfig = dbConfig;

        try
        {
            NetworkChange.NetworkAvailabilityChanged += (_, _) => _ = Task.Run(RefreshNowAsync);
            NetworkChange.NetworkAddressChanged += (_, _) => _ = Task.Run(RefreshNowAsync);
        }
        catch { }
    }

    public void StartMonitoring()
    {
        if (Interlocked.Exchange(ref _monitorStarted, 1) == 1)
            return;

        _ = Task.Run(async () =>
        {
            // Initial check
            await RefreshNowAsync();

            // Network-change events refresh immediately; periodic polling is a
            // lighter safety net that avoids continuous pressure on DB servers.
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1), _cts.Token);
                    await RefreshNowAsync();
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch { }
            }
        });
    }

    public void SyncCurrentStatus()
    {
        OnConnectionStatusChanged?.Invoke(_isOnline);
    }

    public async Task<bool> RefreshNowAsync()
    {
        await _refreshGate.WaitAsync(_cts.Token);
        try
        {
            // Network-change events can arrive in bursts. Reuse a very recent
            // result instead of opening several identical sets of DB connections.
            if (DateTime.UtcNow - _lastRefreshUtc < TimeSpan.FromSeconds(2))
                return _isOnline;

            bool wasOnline = _isOnline;
            bool wasNetworkOnline = _isNetworkOnline;
            bool wasCrsOnline = _isCrsOnline;
            bool hadChecked = _hasChecked;
            _isInternetAvailable = NetworkInterface.GetIsNetworkAvailable();

            var remoteTask = CheckRemoteAsync();
            var networkTask = CheckNetworkAsync();
            var crsTask = CheckCrsAsync();
            await Task.WhenAll(remoteTask, networkTask, crsTask);

            _isOnline = await remoteTask;
            _isNetworkOnline = await networkTask;
            _isCrsOnline = await crsTask;
            _hasChecked = true;
            _lastRefreshUtc = DateTime.UtcNow;

            if (!hadChecked || wasOnline != _isOnline ||
                wasNetworkOnline != _isNetworkOnline || wasCrsOnline != _isCrsOnline)
                OnConnectionStatusChanged?.Invoke(_isOnline);
            return _isOnline;
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    // ── Remote GGMS (194.59.164.58, main online DB) ──────────────────────────
    private async Task<bool> CheckRemoteAsync()
    {
        try
        {
            string connStr = _dbConfig.ConnectionString;
            if (string.IsNullOrWhiteSpace(connStr))
                return false;

            // A database open is already a complete reachability check.
            var testBuilder = new MySqlConnectionStringBuilder(connStr)
            {
                ConnectionTimeout = 5,
                SslMode = MySqlSslMode.None
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var conn = new MySqlConnection(testBuilder.ConnectionString);
            await conn.OpenAsync(cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── Office Network (LAN) ──────────────────────────────────────────────────
    private async Task<bool> CheckNetworkAsync()
    {
        try
        {
            string connStr = _dbConfig.NetworkConnectionString;
            if (string.IsNullOrWhiteSpace(connStr))
                return false;

            var testBuilder = new MySqlConnectionStringBuilder(connStr)
            {
                ConnectionTimeout = 5,
                SslMode = MySqlSslMode.None
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var conn = new MySqlConnection(testBuilder.ConnectionString);
            await conn.OpenAsync(cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── CRS Database ──────────────────────────────────────────────────────────
    private async Task<bool> CheckCrsAsync()
    {
        try
        {
            string connStr = _dbConfig.CrsConnectionString;
            if (string.IsNullOrWhiteSpace(connStr))
                return false;

            var testBuilder = new MySqlConnectionStringBuilder(connStr)
            {
                ConnectionTimeout = 5,
                SslMode = MySqlSslMode.None
            };

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var conn = new MySqlConnection(testBuilder.ConnectionString);
            await conn.OpenAsync(cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

}
