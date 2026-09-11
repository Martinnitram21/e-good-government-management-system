using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;

namespace GoodGovernanceApp.Services;

public interface ISyncService
{
    event Action<bool>? OnSyncStatusChanged;
    event Action<string>? OnSyncProgress;
    event Action<List<string>>? OnSyncErrorsCollected;
    void StartAutoSync();
    Task SyncNowAsync();
}

public class SyncService : ISyncService
{
    // e-KARD pattern adapted for GGMS (both sides are MySQL):
    //   AppDbContext   = Online/Remote (194.59.164.58 u621755393_ggms) — the fully-used primary.
    //   CloudDbContext = Network/LAN  (192.168.0.47 ggms_db, prefilled) — the office server.
    // The footer Sync button syncs Online <-> Network with SyncId + UpdatedAt last-write-wins.
    // Online credentials and SQLite are never touched by sync.
    private bool _isSyncing = false;
    private readonly SemaphoreSlim _syncGate = new(1, 1);
    // Schema migrate/repair runs once per process. Re-running 30x ALTERs + 12x
    // orphan-repair scans on every 5-min sync is pure WAN overhead.
    private static int _schemaEnsured;
    private static int _repairDone;
    public event Action<bool>?   OnSyncStatusChanged;
    public event Action<string>? OnSyncProgress;
    public event Action<List<string>>? OnSyncErrorsCollected;

    private readonly IConnectivityService _connectivityService;
    private readonly IServiceScopeFactory _scopeFactory;

    private sealed record ForeignKeyRule(
        string PropertyName,
        string PrincipalTable,
        string PrincipalKeyColumn);

    private static readonly IReadOnlyDictionary<Type, ForeignKeyRule[]> ForeignKeyRules =
        new Dictionary<Type, ForeignKeyRule[]>
        {
            [typeof(User)] =
            [
                new(nameof(User.OfficeId), "tbl_offices", "id")
            ],
            [typeof(MasterBudget)] =
            [
                new(nameof(MasterBudget.CreatedById), "users", "id")
            ],
            [typeof(ProgramProvision)] =
            [
                new(nameof(ProgramProvision.OfficeId), "tbl_offices", "id")
            ],
            [typeof(BudgetAllocation)] =
            [
                new(nameof(BudgetAllocation.MasterBudgetId), "master_budget", "id"),
                new(nameof(BudgetAllocation.OfficeId), "tbl_offices", "id")
            ],
            [typeof(ProjectDetail)] =
            [
                new(nameof(ProjectDetail.MasterBudgetId), "master_budget", "id")
            ],
            [typeof(TblTransaction)] =
            [
                new(nameof(TblTransaction.ProgramId), "tbl_program_provision", "id"),
                new(nameof(TblTransaction.BudgetAllocationId), "officeallocations", "Id"),
                new(nameof(TblTransaction.DistributedById), "users", "id"),
                new(nameof(TblTransaction.UserId), "users", "id"),
                new(nameof(TblTransaction.OfficeId), "tbl_offices", "id"),
                new(nameof(TblTransaction.ServicesId), "tbl_services", "services_id")
            ]
        };

    private sealed class ForeignKeyTranslator
    {
        private readonly DbConnection _localConnection;
        private readonly DbConnection _cloudConnection;
        private readonly Dictionary<string, object> _cache = new(StringComparer.OrdinalIgnoreCase);

        public ForeignKeyTranslator(DbConnection localConnection, DbConnection cloudConnection)
        {
            _localConnection = localConnection;
            _cloudConnection = cloudConnection;
        }

        public async Task<IReadOnlyDictionary<string, object?>> GetOverridesAsync<T>(
            T record,
            bool sourceIsMySql,
            List<string> errors) where T : class
        {
            var overrides = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            if (!ForeignKeyRules.TryGetValue(typeof(T), out var rules))
                return overrides;

            foreach (var rule in rules)
            {
                var property = typeof(T).GetProperty(rule.PropertyName);
                if (property == null)
                    continue;

                var sourceValue = property.GetValue(record);
                if (sourceValue == null || sourceValue == DBNull.Value)
                {
                    overrides[rule.PropertyName] = DBNull.Value;
                    continue;
                }

                var translated = await TranslateAsync(
                    sourceValue,
                    rule.PrincipalTable,
                    rule.PrincipalKeyColumn,
                    sourceIsMySql);

                if (translated == DBNull.Value)
                {
                    AddSyncError(errors,
                        $"{typeof(T).Name}.{rule.PropertyName}: referenced {rule.PrincipalTable} record is missing; relationship stored as NULL.");
                }

                overrides[rule.PropertyName] = translated;
            }

            return overrides;
        }

        private async Task<object> TranslateAsync(
            object sourceKey,
            string principalTable,
            string principalKeyColumn,
            bool sourceIsMySql)
        {
            string cacheKey = $"{sourceIsMySql}|{principalTable}|{sourceKey}";
            if (_cache.TryGetValue(cacheKey, out var cached))
                return cached;

            // Both sides are MySQL (Online + Network), so quoting/cast are identical.
            // sourceIsMySql only selects direction: true = Network→Online, false = Online→Network.
            var sourceConnection = sourceIsMySql ? _cloudConnection : _localConnection;
            var targetConnection = sourceIsMySql ? _localConnection : _cloudConnection;

            const string Q = "`";

            using var syncIdCommand = sourceConnection.CreateCommand();
            syncIdCommand.CommandText =
                $"SELECT {Q}SyncId{Q} FROM {Q}{principalTable}{Q} " +
                $"WHERE {Q}{principalKeyColumn}{Q} = @sourceKey LIMIT 1";
            AddParameter(syncIdCommand, "@sourceKey", sourceKey);
            var syncIdValue = await syncIdCommand.ExecuteScalarAsync();
            if (syncIdValue == null || syncIdValue == DBNull.Value || string.IsNullOrWhiteSpace(syncIdValue.ToString()))
            {
                _cache[cacheKey] = DBNull.Value;
                return DBNull.Value;
            }

            using var targetKeyCommand = targetConnection.CreateCommand();
            targetKeyCommand.CommandText =
                $"SELECT {Q}{principalKeyColumn}{Q} FROM {Q}{principalTable}{Q} " +
                $"WHERE LOWER(CAST({Q}SyncId{Q} AS CHAR)) = @syncId LIMIT 1";
            AddParameter(targetKeyCommand, "@syncId", syncIdValue.ToString()!.ToLowerInvariant());
            var targetKey = await targetKeyCommand.ExecuteScalarAsync();
            var result = targetKey == null || targetKey == DBNull.Value ? DBNull.Value : targetKey;
            _cache[cacheKey] = result;
            return result;
        }
    }

    public SyncService(IConnectivityService connectivityService, IServiceScopeFactory scopeFactory)
    {
        _connectivityService = connectivityService;
        _scopeFactory = scopeFactory;
    }


    public void StartAutoSync()
    {
        _ = Task.Run(async () =>
        {
            // Initial sync on startup as soon as both endpoints are ready.
            // App runs fully on Online/Remote; auto-sync keeps Network/LAN matching.
            // Delayed 30s so login + shell init don't compete with a 9-table sync
            // for connections to the hosted DB (startup connection storm fix).
            await Task.Delay(TimeSpan.FromSeconds(30));
            if (_connectivityService.IsOnline && _connectivityService.IsNetworkOnline && !_isSyncing)
            {
                await SyncNowAsync();
            }

            while (true)
            {
                await Task.Delay(TimeSpan.FromMinutes(5));

                if (_connectivityService.IsOnline && _connectivityService.IsNetworkOnline && !_isSyncing)
                    await SyncNowAsync();
            }
        });

        _connectivityService.OnConnectionStatusChanged += (isOnline) =>
        {
            if (isOnline && _connectivityService.IsNetworkOnline && !_isSyncing)
            {
                _ = Task.Run(SyncNowAsync);
            }
        };
    }

    public async Task SyncNowAsync()
    {
        if (!await _syncGate.WaitAsync(0)) return;

        _isSyncing = true;
        OnSyncStatusChanged?.Invoke(true);
        OnSyncProgress?.Invoke("Syncing Online with Network...");

        var syncErrors = new List<string>();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            // Online = Remote primary (fully used). Network = office LAN server.
            var onlineDb = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var networkDb = scope.ServiceProvider.GetRequiredService<CloudDbContext>();

            bool canReachOnline = await onlineDb.Database.CanConnectAsync();
            bool canReachNetwork = await networkDb.Database.CanConnectAsync();
            if (!canReachOnline && !canReachNetwork)
            {
                OnSyncProgress?.Invoke("Online and Network unreachable. Sync skipped.");
                return;
            }
            if (!canReachOnline)
            {
                OnSyncProgress?.Invoke("Online database unreachable. Sync skipped.");
                return;
            }
            if (!canReachNetwork)
            {
                OnSyncProgress?.Invoke("Network server unreachable. Sync skipped.");
                return;
            }

            var localDb = onlineDb;
            var cloudDb = networkDb;

            if (System.Threading.Interlocked.Exchange(ref _schemaEnsured, 1) == 0)
            {
                OnSyncProgress?.Invoke("Preparing Online and Network schema...");
                await MigrateCloudTablesAsync(onlineDb);
                await MigrateCloudTablesAsync(cloudDb);
            }

            var localConn = localDb.Database.GetDbConnection();
            if (localConn.State != System.Data.ConnectionState.Open) await localConn.OpenAsync();

            var cloudConn = cloudDb.Database.GetDbConnection();
            if (cloudConn.State != System.Data.ConnectionState.Open) await cloudConn.OpenAsync();

            if (System.Threading.Interlocked.CompareExchange(ref _schemaEnsured, 1, 1) == 1)
            {
                // Repair legacy orphaned references before enforcing relationships.
                // Both sides are MySQL (Online/Remote + Network/LAN). Runs with the
                // first sync only per process; later syncs skip the 12 full scans.
                if (System.Threading.Interlocked.Exchange(ref _repairDone, 1) == 0)
                {
                    await RepairOrphanedForeignKeysAsync(localConn, isMySql: true, syncErrors);
                    await RepairOrphanedForeignKeysAsync(cloudConn, isMySql: true, syncErrors);
                }
            }

            var foreignKeyTranslator = new ForeignKeyTranslator(localConn, cloudConn);

            // ── Align Superadmin SyncId to prevent duplicate PK errors ──
            var localAdmin = await localDb.Users.FirstOrDefaultAsync(u => u.Name == "superadmin");
            var cloudAdmin = await cloudDb.Users.FirstOrDefaultAsync(u => u.Name == "superadmin");
            if (localAdmin != null && cloudAdmin != null && localAdmin.SyncId != cloudAdmin.SyncId)
            {
                localAdmin.SyncId = cloudAdmin.SyncId;
                await localDb.SaveChangesAsync();
            }

            OnSyncProgress?.Invoke("Syncing offices...");
            await SyncTableAsync(localDb, cloudDb, localDb.Offices, cloudDb.Offices, foreignKeyTranslator, syncErrors);

            OnSyncProgress?.Invoke("Syncing users...");
            await SyncTableAsync(localDb, cloudDb, localDb.Users, cloudDb.Users, foreignKeyTranslator, syncErrors);

            OnSyncProgress?.Invoke("Syncing master budgets...");
            await SyncTableAsync(localDb, cloudDb, localDb.MasterBudgets, cloudDb.MasterBudgets, foreignKeyTranslator, syncErrors);

            OnSyncProgress?.Invoke("Syncing program provisions...");
            await SyncTableAsync(localDb, cloudDb, localDb.ProgramProvisions, cloudDb.ProgramProvisions, foreignKeyTranslator, syncErrors);

            OnSyncProgress?.Invoke("Syncing budget allocations...");
            await SyncTableAsync(localDb, cloudDb, localDb.BudgetAllocations, cloudDb.BudgetAllocations, foreignKeyTranslator, syncErrors);

            OnSyncProgress?.Invoke("Syncing services...");
            await SyncTableAsync(localDb, cloudDb, localDb.TblServices, cloudDb.TblServices, foreignKeyTranslator, syncErrors);


            OnSyncProgress?.Invoke("Syncing project details...");
            await SyncTableAsync(localDb, cloudDb, localDb.ProjectDetails, cloudDb.ProjectDetails, foreignKeyTranslator, syncErrors);


            OnSyncProgress?.Invoke("Syncing office transactions...");
            await SyncTableAsync(localDb, cloudDb, localDb.TblTransactions, cloudDb.TblTransactions, foreignKeyTranslator, syncErrors);

            OnSyncProgress?.Invoke("Syncing consolidated transactions...");
            await SyncTableAsync(localDb, cloudDb, localDb.ConsolidatedTransactions, cloudDb.ConsolidatedTransactions, foreignKeyTranslator, syncErrors);

            OnSyncProgress?.Invoke(syncErrors.Count == 0 ? "✔ Synced successfully" : $"⚠ Synced with {syncErrors.Count} error(s)");
        }
        catch (Exception ex)
        {
            var innerMsg = ex.InnerException != null ? ex.InnerException.Message : "";
            syncErrors.Add($"Fatal: {ex.Message} | {innerMsg}");
            OnSyncProgress?.Invoke($"⚠ Sync Error: {ex.Message}\nDetails: {innerMsg}");
            System.Diagnostics.Debug.WriteLine($"[SyncService] Error: {ex}");
        }
        finally
        {
            _isSyncing = false;
            OnSyncStatusChanged?.Invoke(false);
            OnSyncErrorsCollected?.Invoke(syncErrors);
            WriteSyncLog(syncErrors);
            _ = Task.Delay(15000).ContinueWith(_ => OnSyncProgress?.Invoke("Idle"));
            _syncGate.Release();
        }
    }

    private async Task MigrateCloudTablesAsync(DbContext db)
    {
        var migrations = new[]
        {
            ("users",                     "SyncId",    "CHAR(36) NOT NULL DEFAULT (UUID())"),
            ("users",                     "updated_at","DATETIME NULL"),
            ("tbl_offices",               "SyncId",    "CHAR(36) NOT NULL DEFAULT (UUID())"),
            ("tbl_offices",               "updated_at","DATETIME NULL"),
            ("master_budget",             "SyncId",    "CHAR(36) NOT NULL DEFAULT (UUID())"),
            ("master_budget",             "updated_at","DATETIME NULL"),
            ("officeallocations",         "SyncId",    "CHAR(36) NOT NULL DEFAULT (UUID())"),
            ("officeallocations",         "updated_at","DATETIME NULL"),
            ("tbl_program_provision",     "SyncId",    "CHAR(36) NOT NULL DEFAULT (UUID())"),
            ("tbl_program_provision",     "updated_at","DATETIME NULL"),
            ("tbl_services",              "SyncId",    "CHAR(36) NOT NULL DEFAULT (UUID())"),
            ("tbl_services",              "updated_at","DATETIME NULL"),
            ("transactions",              "SyncId",    "CHAR(36) NOT NULL DEFAULT (UUID())"),
            ("transactions",              "updated_at","DATETIME NULL"),
            ("project_details",           "SyncId",    "CHAR(36) NOT NULL DEFAULT (UUID())"),
            ("project_details",           "updated_at","DATETIME NULL"),
            ("tbl_transaction",           "SyncId",    "CHAR(36) NOT NULL DEFAULT (UUID())"),
            ("tbl_transaction",           "updated_at","DATETIME NULL"),
            ("tbl_transaction",           "distributed_by_id", "BIGINT NULL"),
            ("tbl_transaction",           "transaction_date",  "DATETIME NULL"),
            ("consolidated_transactions", "SyncId",    "CHAR(36) NOT NULL DEFAULT (UUID())"),
            ("consolidated_transactions", "updated_at","DATETIME NULL"),
            ("master_budget",             "SyncId",    "CHAR(36) NOT NULL DEFAULT (UUID())"),
            ("master_budget",             "updated_at","DATETIME NULL"),
            ("project_details",           "voucher_code", "VARCHAR(45) NULL"),
            ("transactions",              "voucher_code", "VARCHAR(45) NULL"),
            ("tbl_transaction",           "voucher_code", "VARCHAR(45) NULL"),
            ("tbl_offices",               "office_code",  "VARCHAR(45) NULL"),
            ("consolidated_transactions", "barangay",     "VARCHAR(45) NULL"),
            ("consolidated_transactions", "household_no", "VARCHAR(45) NULL"),
        };

        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync();

        foreach (var (table, column, definition) in migrations)
        {
            try
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = $"ALTER TABLE `{table}` ADD COLUMN `{column}` {definition};";
                await cmd.ExecuteNonQueryAsync();
            }
            catch
            {
                // Column already exists — MySQL 1060. Safe to ignore.
            }

            if (column == "SyncId")
            {
                try
                {
                    using var updateCmd = conn.CreateCommand();
                    updateCmd.CommandText = $"UPDATE `{table}` SET `SyncId` = UUID() WHERE `SyncId` IS NULL OR `SyncId` = '';";
                    await updateCmd.ExecuteNonQueryAsync();
                }
                catch { }
            }
        }
    }

    /// <summary>
    /// Bidirectional sync using SyncId as the shared key, UpdatedAt for last-write-wins.
    ///
    /// UPDATE path uses raw SQL to completely bypass EF Core's change tracker,
    /// which prevents the "cannot change principal of an entity with an identifying
    /// foreign key" error caused by EF misinterpreting FK changes on Attach/SetValues.
    ///
    /// Duplicate SyncIds are collapsed via GroupBy before building lookup dictionaries,
    /// preventing the "An item with the same key has already been added" crash.
    /// </summary>
    private async Task SyncTableAsync<T>(
        AppDbContext  localDb,
        CloudDbContext cloudDb,
        DbSet<T>      localSet,
        DbSet<T>      cloudSet,
        ForeignKeyTranslator foreignKeyTranslator,
        List<string> syncErrors) where T : class
    {
        var syncIdProp  = typeof(T).GetProperty("SyncId");
        var updatedProp = typeof(T).GetProperty("UpdatedAt");

        if (syncIdProp == null || updatedProp == null) return;

        // ── Ensure Online SyncIds are not NULL (MySQL backfill) ─────────────
        // Both sides are MySQL now (Online/Remote + Network/LAN).
        try
        {
            var meta  = localDb.Model.FindEntityType(typeof(T))!;
            var tblName = meta.GetTableName()!;
            var conn = localDb.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = $"UPDATE `{tblName}` SET `SyncId` = UUID() WHERE `SyncId` IS NULL OR `SyncId` = '';";
            await cmd.ExecuteNonQueryAsync();
        }
        catch { }

        // ── Load all records without tracking ────────────────────────────────
        var localRecords = await localSet.AsNoTracking().ToListAsync();
        var cloudRecords = await cloudSet.AsNoTracking().ToListAsync();

        // GroupBy collapses duplicate SyncIds so ToDictionary never throws
        var localById = localRecords
            .Where(r => (Guid)syncIdProp.GetValue(r)! != Guid.Empty)
            .GroupBy(r => (Guid)syncIdProp.GetValue(r)!)
            .ToDictionary(g => g.Key, g => g.First());

        var cloudById = cloudRecords
            .Where(r => (Guid)syncIdProp.GetValue(r)! != Guid.Empty)
            .GroupBy(r => (Guid)syncIdProp.GetValue(r)!)
            .ToDictionary(g => g.Key, g => g.First());

        // ── Gather EF metadata (scalar props & PK info) ──────────────────────
        var localMeta  = localDb.Model.FindEntityType(typeof(T))!;
        var cloudMeta  = cloudDb.Model.FindEntityType(typeof(T))!;

        var localScalars   = localMeta.GetProperties().Where(p => !p.IsShadowProperty()).ToList();
        var cloudScalars   = cloudMeta.GetProperties().Where(p => !p.IsShadowProperty()).ToList();
        var localPkNames   = localMeta.FindPrimaryKey()!.Properties.Select(p => p.Name).ToHashSet();
        var cloudPkNames   = cloudMeta.FindPrimaryKey()!.Properties.Select(p => p.Name).ToHashSet();
        var localTableName = localMeta.GetTableName()!;
        var cloudTableName = cloudMeta.GetTableName()!;

        // ── Open raw connections ──────────────────────────────────────────────
        var cloudConn = cloudDb.Database.GetDbConnection();
        if (cloudConn.State != System.Data.ConnectionState.Open)
            await cloudConn.OpenAsync();

        var localConn = localDb.Database.GetDbConnection();
        if (localConn.State != System.Data.ConnectionState.Open)
            await localConn.OpenAsync();

        // ── Push Online → Network ───────────────────────────────────────────
        foreach (var local in localRecords)
        {
            var syncId  = (Guid)syncIdProp.GetValue(local)!;
            if (syncId == Guid.Empty) continue;

            var localAt = (DateTime?)updatedProp.GetValue(local);

            if (!cloudById.TryGetValue(syncId, out var cloudMatch))
            {
                try
                {
                    var overrides = await foreignKeyTranslator.GetOverridesAsync(local, sourceIsMySql: true, syncErrors);
                    await RawSqlInsertAsync(cloudConn, cloudTableName, cloudScalars, local,
                        isMySql: true, skipCols: cloudPkNames, overrides);
                }
                catch (Exception ex)
                {
                    AddSyncError(syncErrors, $"Network insert failed ({typeof(T).Name}): {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[SyncService] Network insert failed ({typeof(T).Name}): {ex.Message}");
                }
            }
            else
            {
                // UPDATE via raw SQL — never touches EF change tracker
                var cloudAt = (DateTime?)updatedProp.GetValue(cloudMatch) ?? DateTime.MinValue;
                var localTime = localAt ?? DateTime.MinValue;
                if (localTime > cloudAt)
                {
                    try
                    {
                        var overrides = await foreignKeyTranslator.GetOverridesAsync(local, sourceIsMySql: true, syncErrors);
                        await RawSqlUpdateAsync(cloudConn, cloudTableName, cloudScalars, cloudPkNames,
                            incoming: local, existing: cloudMatch, isMySql: true, overrides);
                    }
                    catch (Exception ex)
                    {
                        AddSyncError(syncErrors, $"Network update failed ({typeof(T).Name}): {ex.Message}");
                    }
                }
            }
        }

        // ── Pull Network → Online ───────────────────────────────────────────
        // Reload cloud in case push inserted new rows
        cloudRecords = await cloudSet.AsNoTracking().ToListAsync();
        // Assign a fresh SyncId to any cloud rows that still have Guid.Empty
        // (i.e. their SyncId column is NULL in MySQL). This ensures they are
        // not silently dropped when building the lookup dictionary.
        foreach (var rec in cloudRecords)
        {
            var sid = (Guid)syncIdProp.GetValue(rec)!;
            if (sid == Guid.Empty)
                syncIdProp.SetValue(rec, Guid.NewGuid());
        }
        cloudById = cloudRecords
            .GroupBy(r => (Guid)syncIdProp.GetValue(r)!)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var cloud in cloudRecords)
        {
            var syncId  = (Guid)syncIdProp.GetValue(cloud)!;
            if (syncId == Guid.Empty) continue;

            var cloudAt = (DateTime?)updatedProp.GetValue(cloud);

            if (!localById.TryGetValue(syncId, out var localMatch))
            {
                try
                {
                    var overrides = await foreignKeyTranslator.GetOverridesAsync(cloud, sourceIsMySql: true, syncErrors);
                    await RawSqlInsertAsync(localConn, localTableName, localScalars, cloud,
                        isMySql: true, skipCols: localPkNames, overrides);
                }
                catch (Exception ex)
                {
                    AddSyncError(syncErrors, $"Online insert failed ({typeof(T).Name}): {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[SyncService] Online insert failed ({typeof(T).Name}): {ex.Message}");
                }
            }
            else
            {
                // UPDATE via raw SQL — never touches EF change tracker
                var localAt = (DateTime?)updatedProp.GetValue(localMatch) ?? DateTime.MinValue;
                var cloudTime = cloudAt ?? DateTime.MinValue;
                var overrides = await foreignKeyTranslator.GetOverridesAsync(cloud, sourceIsMySql: true, syncErrors);
                if (cloudTime > localAt)
                {
                    try
                    {
                        await RawSqlUpdateAsync(localConn, localTableName, localScalars, localPkNames,
                            incoming: cloud, existing: localMatch, isMySql: true, overrides);
                    }
                    catch (Exception ex)
                    {
                        AddSyncError(syncErrors, $"Online update failed ({typeof(T).Name}): {ex.Message}");
                    }
                }
                else if (overrides.Count > 0)
                {
                    try
                    {
                        await RawSqlUpdateOverridesAsync(localConn, localTableName, localScalars,
                            localPkNames, localMatch, isMySql: true, overrides);
                    }
                    catch (Exception ex)
                    {
                        AddSyncError(syncErrors, $"Online relationship repair failed ({typeof(T).Name}): {ex.Message}");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Fires a raw SQL UPDATE for a single record without involving EF Core's change tracker.
    ///
    /// - <paramref name="incoming"/> supplies the NEW column values to write.
    /// - <paramref name="existing"/> supplies the PK of the ROW to update in the target DB.
    /// - The PK column itself is never written (only used in the WHERE clause).
    /// - isMySql=true uses backtick quoting; false uses double-quote quoting (SQLite).
    /// </summary>
    private async Task RawSqlUpdateAsync<T>(
        System.Data.Common.DbConnection conn,
        string tableName,
        IEnumerable<IProperty> scalarProps,
        HashSet<string> pkPropNames,
        T incoming,
        T existing,
        bool isMySql,
        IReadOnlyDictionary<string, object?>? overrides = null) where T : class
    {
        string Q(string n) => isMySql ? $"`{n}`" : $"\"{n}\"";

        using var cmd = conn.CreateCommand();
        var setClauses = new List<string>();
        int idx = 0;

        foreach (var prop in scalarProps)
        {
            if (pkPropNames.Contains(prop.Name)) continue; // never overwrite PK

            var clrProp = typeof(T).GetProperty(prop.Name);
            if (clrProp == null) continue;

            var value = overrides != null && overrides.TryGetValue(prop.Name, out var overrideValue)
                ? overrideValue ?? DBNull.Value
                : clrProp.GetValue(incoming) ?? DBNull.Value;
            var p = cmd.CreateParameter();
            p.ParameterName = $"@p{idx}";
            p.Value = value;
            cmd.Parameters.Add(p);
            setClauses.Add($"{Q(prop.GetColumnName())} = @p{idx}");
            idx++;
        }

        if (!setClauses.Any()) return;

        // WHERE uses the PK of the EXISTING row in the target DB
        var pkName   = pkPropNames.First();
        var pkClrProp = typeof(T).GetProperty(pkName)!;
        var pkColName = scalarProps.First(p => p.Name == pkName).GetColumnName();
        var pkValue  = pkClrProp.GetValue(existing) ?? DBNull.Value;

        var pkParam = cmd.CreateParameter();
        pkParam.ParameterName = "@pkVal";
        pkParam.Value = pkValue;
        cmd.Parameters.Add(pkParam);

        cmd.CommandText = $"UPDATE {Q(tableName)} SET {string.Join(", ", setClauses)} WHERE {Q(pkColName)} = @pkVal";

        await cmd.ExecuteNonQueryAsync();
    }

    private async Task RawSqlInsertAsync<T>(
        System.Data.Common.DbConnection conn,
        string tableName,
        IEnumerable<IProperty> scalarProps,
        T incoming,
        bool isMySql,
        HashSet<string>? skipCols = null,
        IReadOnlyDictionary<string, object?>? overrides = null) where T : class
    {
        string Q(string n) => isMySql ? $"`{n}`" : $"\"{n}\"";

        using var cmd = conn.CreateCommand();
        var cols = new List<string>();
        var vals = new List<string>();
        int idx = 0;

        foreach (var prop in scalarProps)
        {
            if (skipCols != null && skipCols.Contains(prop.Name)) continue;

            var clrProp = typeof(T).GetProperty(prop.Name);
            if (clrProp == null) continue;

            var value = overrides != null && overrides.TryGetValue(prop.Name, out var overrideValue)
                ? overrideValue ?? DBNull.Value
                : clrProp.GetValue(incoming) ?? DBNull.Value;
            var p = cmd.CreateParameter();
            p.ParameterName = $"@p{idx}";
            p.Value = value;
            cmd.Parameters.Add(p);
            
            cols.Add(Q(prop.GetColumnName()));
            vals.Add($"@p{idx}");
            idx++;
        }

        cmd.CommandText = $"INSERT INTO {Q(tableName)} ({string.Join(", ", cols)}) VALUES ({string.Join(", ", vals)})";
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task RawSqlUpdateOverridesAsync<T>(
        DbConnection conn,
        string tableName,
        IEnumerable<IProperty> scalarProps,
        HashSet<string> pkPropNames,
        T existing,
        bool isMySql,
        IReadOnlyDictionary<string, object?> overrides) where T : class
    {
        string Q(string name) => isMySql ? $"`{name}`" : $"\"{name}\"";

        using var cmd = conn.CreateCommand();
        var setClauses = new List<string>();
        int index = 0;

        foreach (var prop in scalarProps)
        {
            if (pkPropNames.Contains(prop.Name) || !overrides.TryGetValue(prop.Name, out var value))
                continue;

            string parameterName = $"@p{index}";
            AddParameter(cmd, parameterName, value ?? DBNull.Value);
            setClauses.Add($"{Q(prop.GetColumnName())} = {parameterName}");
            index++;
        }

        if (setClauses.Count == 0)
            return;

        string pkName = pkPropNames.First();
        var pkProperty = typeof(T).GetProperty(pkName)!;
        string pkColumnName = scalarProps.First(p => p.Name == pkName).GetColumnName();
        AddParameter(cmd, "@pkValue", pkProperty.GetValue(existing) ?? DBNull.Value);
        cmd.CommandText =
            $"UPDATE {Q(tableName)} SET {string.Join(", ", setClauses)} WHERE {Q(pkColumnName)} = @pkValue";
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task RepairOrphanedForeignKeysAsync(
        DbConnection connection,
        bool isMySql,
        List<string> errors)
    {
        var relationships = new[]
        {
            (Table: "users", ForeignKey: "office_id", Principal: "tbl_offices", PrincipalKey: "id", RequiredFallback: false),
            (Table: "master_budget", ForeignKey: "created_by", Principal: "users", PrincipalKey: "id", RequiredFallback: true),
            (Table: "tbl_program_provision", ForeignKey: "office_id", Principal: "tbl_offices", PrincipalKey: "id", RequiredFallback: false),
            (Table: "officeallocations", ForeignKey: "YearlyBudgetId", Principal: "master_budget", PrincipalKey: "id", RequiredFallback: false),
            (Table: "officeallocations", ForeignKey: "office_id", Principal: "tbl_offices", PrincipalKey: "id", RequiredFallback: false),
            (Table: "project_details", ForeignKey: "yearly_budget_id", Principal: "master_budget", PrincipalKey: "id", RequiredFallback: false),
            (Table: "tbl_transaction", ForeignKey: "program_id", Principal: "tbl_program_provision", PrincipalKey: "id", RequiredFallback: false),
            (Table: "tbl_transaction", ForeignKey: "budget_allocation_id", Principal: "officeallocations", PrincipalKey: "Id", RequiredFallback: false),
            (Table: "tbl_transaction", ForeignKey: "distributed_by_id", Principal: "users", PrincipalKey: "id", RequiredFallback: false),
            (Table: "tbl_transaction", ForeignKey: "user_id", Principal: "users", PrincipalKey: "id", RequiredFallback: false),
            (Table: "tbl_transaction", ForeignKey: "office_id", Principal: "tbl_offices", PrincipalKey: "id", RequiredFallback: false),
            (Table: "tbl_transaction", ForeignKey: "services_id", Principal: "tbl_services", PrincipalKey: "services_id", RequiredFallback: false)
        };

        foreach (var relationship in relationships)
        {
            try
            {
                using var cmd = connection.CreateCommand();
                if (isMySql)
                {
                    string replacement = relationship.RequiredFallback
                        ? "(SELECT fallback.`id` FROM `users` fallback WHERE LOWER(fallback.`name`) = 'superadmin' ORDER BY fallback.`id` LIMIT 1)"
                        : "NULL";
                    cmd.CommandText =
                        $"UPDATE `{relationship.Table}` dependent " +
                        $"LEFT JOIN `{relationship.Principal}` principal " +
                        $"ON dependent.`{relationship.ForeignKey}` = principal.`{relationship.PrincipalKey}` " +
                        $"SET dependent.`{relationship.ForeignKey}` = {replacement} " +
                        $"WHERE dependent.`{relationship.ForeignKey}` IS NOT NULL " +
                        $"AND principal.`{relationship.PrincipalKey}` IS NULL";
                }
                else
                {
                    string replacement = relationship.RequiredFallback
                        ? "(SELECT \"id\" FROM \"users\" WHERE LOWER(\"name\") = 'superadmin' ORDER BY \"id\" LIMIT 1)"
                        : "NULL";
                    cmd.CommandText =
                        $"UPDATE \"{relationship.Table}\" SET \"{relationship.ForeignKey}\" = {replacement} " +
                        $"WHERE \"{relationship.ForeignKey}\" IS NOT NULL AND NOT EXISTS (" +
                        $"SELECT 1 FROM \"{relationship.Principal}\" principal " +
                        $"WHERE principal.\"{relationship.PrincipalKey}\" = " +
                        $"\"{relationship.Table}\".\"{relationship.ForeignKey}\")";
                }

                int repaired = await cmd.ExecuteNonQueryAsync();
                if (repaired > 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SyncService] Repaired {repaired} orphaned {relationship.Table}.{relationship.ForeignKey} value(s).");
                }
            }
            catch (Exception ex)
            {
                AddSyncError(errors,
                    $"Relationship validation failed ({relationship.Table}.{relationship.ForeignKey}): {ex.Message}");
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static void AddSyncError(List<string> errors, string message)
    {
        if (!errors.Contains(message, StringComparer.Ordinal))
            errors.Add(message);
    }

    /// <summary>
    /// Persists sync results to a log file with timestamps.
    /// </summary>
    private void WriteSyncLog(List<string> errors)
    {
        try
        {
            string logDir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "GoodGovernanceApp",
                "Logs");
            if (!System.IO.Directory.Exists(logDir))
                System.IO.Directory.CreateDirectory(logDir);

            string logPath = System.IO.Path.Combine(logDir, "sync_log.txt");
            var lines = new List<string>
            {
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] Sync completed — {errors.Count} error(s)"
            };

            foreach (var err in errors)
                lines.Add($"  ERROR: {err}");

            lines.Add(""); // blank line separator
            System.IO.File.AppendAllLines(logPath, lines);
        }
        catch { /* Logging must never crash the sync flow */ }
    }
}
