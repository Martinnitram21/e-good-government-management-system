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
    string NetworkConnectionString { get; }
    string CrsConnectionString { get; }
    string ActiveMode { get; }
    void SaveToAppsettings(string mode, string ggmsConnStr, string networkConnStr = "", string crsConnStr = "");
}

public class DatabaseConfig : IDatabaseConfig
{
    private readonly IConfiguration _config;
    
    public DatabaseConfig(IConfiguration config)
    {
        _config = config;
    }

    public string ActiveMode
    {
        get
        {
            string mode = _config["AppSettings:DatabaseMode"] ?? "Remote";
            return mode;
        }
    }

    public string ConnectionString
    {
        get
        {
            // The footer status shows ONLY this connection — the database the
            // system is actually using. Remote (Online) is the default;
            // Network (LAN) is used when DatabaseMode is set to Network/LAN.
            string mode = (_config["AppSettings:DatabaseMode"] ?? "Remote").Trim();
            if (mode.Equals("Network", StringComparison.OrdinalIgnoreCase)
                || mode.Equals("LAN", StringComparison.OrdinalIgnoreCase)
                || mode.Equals("LanConnection", StringComparison.OrdinalIgnoreCase))
            {
                string lan = _config.GetConnectionString("NetworkConnection")
                    ?? _config.GetConnectionString("LanConnection");
                if (!string.IsNullOrWhiteSpace(lan))
                    return lan;
            }

            return _config.GetConnectionString("RemoteConnection")
                ?? _config.GetConnectionString("LocalConnection")
                ?? "Server=194.59.164.58;Port=3306;Database=u621755393_ggms;User=u621755393_ggms_user;Password=Ggms@2026;AllowZeroDateTime=True;ConvertZeroDateTime=True;";
        }
    }

    public string NetworkConnectionString
    {
        get
        {
            // Prefilled office-network (LAN) database; editable in Settings.
            return _config.GetConnectionString("NetworkConnection")
                ?? _config.GetConnectionString("LanConnection")
                ?? "Server=192.168.0.47;Port=3306;Database=ggms_db;User=root;Password=network@2026;AllowZeroDateTime=True;ConvertZeroDateTime=True;";
        }
    }

    public string CrsConnectionString
    {
        get
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = _config["CrsConnection:Server"] ?? "192.168.0.47",
                Port = uint.TryParse(_config["CrsConnection:Port"], out var p) ? p : 3306,
                Database = _config["CrsConnection:Database"] ?? "crs_db",
                UserID = _config["CrsConnection:User"] ?? "root",
                Password = _config["CrsConnection:Password"] ?? "network@2026",
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
        string networkConnStr = "", string crsConnStr = "")
    {
        string appDataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GoodGovernanceApp");
        Directory.CreateDirectory(appDataFolder);
        string path = Path.Combine(appDataFolder, "appsettings.json");

        JsonObject root;
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                root = JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
            }
            catch
            {
                root = new JsonObject();
            }
        }
        else
        {
            root = new JsonObject();
        }

        if (!root.ContainsKey("AppSettings") || root["AppSettings"] is not JsonObject)
            root["AppSettings"] = new JsonObject();
        if (!root.ContainsKey("ConnectionStrings") || root["ConnectionStrings"] is not JsonObject)
            root["ConnectionStrings"] = new JsonObject();

        root["AppSettings"]!["DatabaseMode"] = string.IsNullOrWhiteSpace(mode) ? "Remote" : mode;
        root["AppSettings"]!["UseRemoteDatabase"] = true;
        root["ConnectionStrings"]!["RemoteConnection"] = ggmsConnStr;
        if (!string.IsNullOrWhiteSpace(networkConnStr))
            root["ConnectionStrings"]!["NetworkConnection"] = networkConnStr;
        if (!string.IsNullOrWhiteSpace(crsConnStr))
        {
            // CRS is stored as a section (Server/Port/Database/User/Password),
            // not a connection string — split it so connectivity + services keep working.
            try
            {
                var cb = new MySqlConnectionStringBuilder(crsConnStr);
                if (!root.ContainsKey("CrsConnection") || root["CrsConnection"] is not JsonObject)
                    root["CrsConnection"] = new JsonObject();
                root["CrsConnection"]!["Server"] = string.IsNullOrWhiteSpace(cb.Server) ? "192.168.0.47" : cb.Server;
                root["CrsConnection"]!["Port"] = cb.Port > 0 ? cb.Port.ToString() : "3306";
                root["CrsConnection"]!["Database"] = string.IsNullOrWhiteSpace(cb.Database) ? "crs_db" : cb.Database;
                root["CrsConnection"]!["User"] = string.IsNullOrWhiteSpace(cb.UserID) ? "root" : cb.UserID;
                root["CrsConnection"]!["Password"] = cb.Password ?? "network@2026";
            }
            catch { }
        }

        var options = new JsonSerializerOptions { WriteIndented = true };
        ConfigFileHelper.AtomicWriteJson(path, root.ToJsonString(options));
    }
}
