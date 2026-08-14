using Microsoft.Extensions.Configuration;
using MySqlConnector;
using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using GoodGovernanceApp.Utilities;

namespace GoodGovernanceApp.Data;

public interface IDatabaseConfig
{
    string ConnectionString { get; }
    string CrsConnectionString { get; }
    void SaveToAppsettings(string mode, string ggmsConnStr, string crsServer, string crsPort, string crsDb, string crsUser, string crsPass);
}

public class DatabaseConfig : IDatabaseConfig
{
    private readonly IConfiguration _config;
    
    public DatabaseConfig(IConfiguration config)
    {
        _config = config;
    }

    public string ConnectionString
    {
        get
        {
            string dbMode = _config["AppSettings:DatabaseMode"] ?? "Local";
            string key = dbMode switch
            {
                "Remote" => "RemoteConnection",
                "LAN"    => "LanConnection",
                _        => "LocalConnection"
            };

            return _config.GetConnectionString(key)
                ?? throw new InvalidOperationException($"Connection string '{key}' not found in appsettings.json.");
        }
    }

    public string CrsConnectionString
    {
        get
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = _config["CrsConnection:Server"] ?? "localhost",
                Port = uint.TryParse(_config["CrsConnection:Port"], out var p) ? p : 3306,
                Database = _config["CrsConnection:Database"] ?? "crs_db",
                UserID = _config["CrsConnection:User"] ?? "root",
                Password = _config["CrsConnection:Password"] ?? "",
                AllowZeroDateTime = true,
                ConvertZeroDateTime = true,
                ConnectionTimeout = 15,
                SslMode = MySqlSslMode.None
            };
            return builder.ConnectionString;
        }
    }

    public void SaveToAppsettings(
        string mode, string ggmsConnStr,
        string crsServer, string crsPort, string crsDb, string crsUser, string crsPass)
    {
        string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GoodGovernanceApp");
        string path = Path.Combine(appDataFolder, "appsettings.json");
        string json = File.ReadAllText(path);
        var root = JsonNode.Parse(json)!.AsObject();

        root["AppSettings"]!["DatabaseMode"] = mode;

        string key = mode switch
        {
            "Remote" => "RemoteConnection",
            "LAN"    => "LanConnection",
            _        => "LocalConnection"
        };
        root["ConnectionStrings"]![key] = ggmsConnStr;

        root["CrsConnection"]!["Server"]   = crsServer;
        root["CrsConnection"]!["Port"]     = crsPort;
        root["CrsConnection"]!["Database"] = crsDb;
        root["CrsConnection"]!["User"]     = crsUser;
        root["CrsConnection"]!["Password"] = crsPass;

        var options = new JsonSerializerOptions { WriteIndented = true };
        ConfigFileHelper.AtomicWriteJson(path, root.ToJsonString(options));
    }
}
