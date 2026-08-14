using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using GoodGovernanceApp.Data;
using MySqlConnector;

namespace GoodGovernanceApp.Services;

public interface IConnectivityService
{
    bool IsOnline { get; }
    bool IsCrsOnline { get; }
    event Action<bool>? OnConnectionStatusChanged;
    void StartMonitoring();
    void SyncCurrentStatus();
}

public class ConnectivityService : IConnectivityService
{
    private bool _isOnline = false;
    private bool _isCrsOnline = false;
    private readonly IDatabaseConfig _dbConfig;

    public event Action<bool>? OnConnectionStatusChanged;

    public bool IsOnline => _isOnline;
    public bool IsCrsOnline => _isCrsOnline;

    public ConnectivityService(IDatabaseConfig dbConfig)
    {
        _dbConfig = dbConfig;
    }

    public void StartMonitoring()
    {
        _ = Task.Run(async () =>
        {
            // ── First check immediately at startup ───────────────────────────
            _isOnline    = await CheckHostingerAsync();
            _isCrsOnline = await CheckCrsAsync();
            OnConnectionStatusChanged?.Invoke(_isOnline);

            // ── Then poll every 30 seconds ────────────────────────────────────
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(30));

                _isOnline    = await CheckHostingerAsync();
                _isCrsOnline = await CheckCrsAsync();

                // Always fire — subscribers decide if they care about unchanged values
                OnConnectionStatusChanged?.Invoke(_isOnline);
            }
        });
    }

    /// <summary>Immediately push current connectivity state to all subscribers.</summary>
    public void SyncCurrentStatus()
    {
        OnConnectionStatusChanged?.Invoke(_isOnline);
    }

    // ── Hostinger GGMS ────────────────────────────────────────────────────────
    private async Task<bool> CheckHostingerAsync()
    {
        try
        {
            string connStr = _dbConfig.ConnectionString;

            // Quick TCP check first (fast fail if host is unreachable)
            if (!await TcpPingAsync("194.59.164.58", 3306))
                return false;

            // Then verify with a real MySQL connection
            using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();
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

            if (!await TcpPingAsync("194.59.164.58", 3306))
                return false;

            using var conn = new MySqlConnection(connStr);
            await conn.OpenAsync();
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ── TCP Ping helper ───────────────────────────────────────────────────────
    private async Task<bool> TcpPingAsync(string host, int port, int timeoutMs = 3000)
    {
        try
        {
            using var client = new TcpClient();
            var task = client.ConnectAsync(host, port);
            return await Task.WhenAny(task, Task.Delay(timeoutMs)) == task && client.Connected;
        }
        catch
        {
            return false;
        }
    }
}
