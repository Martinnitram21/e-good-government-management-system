using System;
using System.Net.NetworkInformation;
using System.Net.Sockets;
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
    private readonly IDatabaseConfig _dbConfig;
    private readonly CancellationTokenSource _cts = new();

    public event Action<bool>? OnConnectionStatusChanged;

    public bool IsOnline => _isOnline;
    public bool IsNetworkOnline => _isNetworkOnline;
    public bool IsCrsOnline => _isCrsOnline;
    public bool IsInternetAvailable => _isInternetAvailable;

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
        _ = Task.Run(async () =>
        {
            // Initial check
            await RefreshNowAsync();

            // Periodic polling every 15 seconds
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(15), _cts.Token);
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
        _isInternetAvailable = NetworkInterface.GetIsNetworkAvailable();

        var hostingerTask = CheckHostingerAsync();
        var networkTask = CheckNetworkAsync();
        var crsTask = CheckCrsAsync();
        await Task.WhenAll(hostingerTask, networkTask, crsTask);

        _isOnline = hostingerTask.Result;
        _isNetworkOnline = networkTask.Result;
        _isCrsOnline = crsTask.Result;

        OnConnectionStatusChanged?.Invoke(_isOnline);
        return _isOnline;
    }

    // ── Hostinger GGMS ────────────────────────────────────────────────────────
    private async Task<bool> CheckHostingerAsync()
    {
        try
        {
            string connStr = _dbConfig.ConnectionString;
            if (string.IsNullOrWhiteSpace(connStr))
                return false;

            string host = "193.203.175.157";
            int port = 3306;

            try
            {
                var builder = new MySqlConnectionStringBuilder(connStr);
                if (!string.IsNullOrWhiteSpace(builder.Server))
                    host = builder.Server;
                if (builder.Port > 0)
                    port = (int)builder.Port;
            }
            catch { }

            // Quick TCP check (3 second timeout)
            bool tcpOk = await TcpPingAsync(host, port, 3000);
            if (!tcpOk)
                return false;

            // Direct connection verification with 5s timeout
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

            string host = "192.168.0.42";
            int port = 3306;

            try
            {
                var builder = new MySqlConnectionStringBuilder(connStr);
                if (!string.IsNullOrWhiteSpace(builder.Server))
                    host = builder.Server;
                if (builder.Port > 0)
                    port = (int)builder.Port;
            }
            catch { }

            bool tcpOk = await TcpPingAsync(host, port, 3000);
            if (!tcpOk)
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

            string host = "193.203.175.157";
            int port = 3306;

            try
            {
                var builder = new MySqlConnectionStringBuilder(connStr);
                if (!string.IsNullOrWhiteSpace(builder.Server))
                    host = builder.Server;
                if (builder.Port > 0)
                    port = (int)builder.Port;
            }
            catch { }

            bool tcpOk = await TcpPingAsync(host, port, 3000);
            if (!tcpOk)
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

    // ── TCP Ping helper ───────────────────────────────────────────────────────
    private static async Task<bool> TcpPingAsync(string host, int port, int timeoutMs = 3000)
    {
        try
        {
            using var client = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            var connectTask = client.ConnectAsync(host, port);
            var delayTask = Task.Delay(timeoutMs, cts.Token);

            var completed = await Task.WhenAny(connectTask, delayTask);
            if (completed == connectTask && client.Connected)
            {
                cts.Cancel();
                return true;
            }
            return false;
        }
        catch
        {
            return false;
        }
    }
}
