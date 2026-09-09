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
    void SaveToAppsettings(string mode, string ggmsConnStr, string crsServer, string crsPort, string crsDb, string crsUser, string crsPass, string networkConnStr = "");
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
            return _config.GetConnectionString("RemoteConnection")
                ?? _config.GetConnectionString("LocalConnection")
                ?? "Server=193.203.175.157;Port=3306;Database=u518908950_ggms;User=u518908950_ggms;Password=Sulop@2025;AllowZeroDateTime=True;ConvertZeroDateTime=True;";
        }
    }

    public string NetworkConnectionString
    {
        get
        {
            // Prefilled office-network (LAN) database; editable in Settings.
            return _config.GetConnectionString("NetworkConnection")
                ?? _config.GetConnectionString("LanConnection")
                ?? "Server=192.168.0.42;Port=3306;Database=agms_db;User=root;Password=network@2026;AllowZeroDateTime=True;ConvertZeroDateTime=True;";
        }
    }

    public string CrsConnectionString
    {
        get
        {
            var builder = new MySqlConnectionStringBuilder
            {
                Server = _config["CrsConnection:Server"] ?? "193.203.175.157",
                Port = uint.TryParse(_config["CrsConnection:Port"], out var p) ? p : 3306,
                Database = _config["CrsConnection:Database"] ?? "u518908950_crs",
                UserID = _config["CrsConnection:User"] ?? "u518908950_crs",
                Password = _config["CrsConnection:Password"] ?? "Sulop@2025",
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
        string crsServer, string crsPort, string crsDb, string crsUser, string crsPass,
        string networkConnStr = "")
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
        if (!root.ContainsKey("CrsConnection") || root["CrsConnection"] is not JsonObject)
            root["CrsConnection"] = new JsonObject();

        root["AppSettings"]!["DatabaseMode"] = "Remote";
        root["ConnectionStrings"]!["RemoteConnection"] = ggmsConnStr;
        if (!string.IsNullOrWhiteSpace(networkConnStr))
            root["ConnectionStrings"]!["NetworkConnection"] = networkConnStr;

        root["CrsConnection"]!["Server"]   = crsServer;
        root["CrsConnection"]!["Port"]     = crsPort;
        root["CrsConnection"]!["Database"] = crsDb;
        root["CrsConnection"]!["User"]     = crsUser;
        root["CrsConnection"]!["Password"] = crsPass;

        var options = new JsonSerializerOptions { WriteIndented = true };
        ConfigFileHelper.AtomicWriteJson(path, root.ToJsonString(options));
    }
}
