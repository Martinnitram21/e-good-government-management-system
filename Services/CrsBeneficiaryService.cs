using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;

namespace GoodGovernanceApp.Services;

/// <summary>
/// READ-ONLY access to the CRS beneficiary data for a single beneficiary_id.
/// Mirrors the cloud-first / SQLite-cache-fallback pattern used in
/// CrsBeneficiaryViewModel. Never writes to the CRS database.
/// </summary>
public interface ICrsBeneficiaryService
{
    Task<Beneficiary?> GetByIdAsync(string beneficiaryId);
}

/// <summary>
/// READ-ONLY access to the CRS beneficiary data for a single beneficiary_id.
/// Mirrors the cloud-first / SQLite-cache-fallback pattern used in
/// CrsBeneficiaryViewModel. Never writes to the CRS database.
/// </summary>
public class CrsBeneficiaryService : ICrsBeneficiaryService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConnectivityService _connectivityService;
    private readonly IDatabaseConfig _dbConfig;

    public CrsBeneficiaryService(IServiceScopeFactory scopeFactory, IConnectivityService connectivityService, IDatabaseConfig dbConfig)
    {
        _scopeFactory = scopeFactory;
        _connectivityService = connectivityService;
        _dbConfig = dbConfig;
    }
    /// <summary>
    /// Returns the CRS <see cref="Beneficiary"/> record for the given
    /// <paramref name="beneficiaryId"/>, or <c>null</c> if no record exists.
    /// </summary>
    /// <remarks>
    /// Strategy (same as <c>CrsBeneficiaryViewModel</c>):
    ///   1. If CRS cloud is reachable → query <c>val_beneficiaries</c> for an exact
    ///      <c>beneficiary_id</c> match and update the local SQLite cache on the way.
    ///   2. Otherwise → fall back to <c>crs_beneficiary_cache</c> in SQLite.
    /// </remarks>
    public async Task<Beneficiary?> GetByIdAsync(string beneficiaryId)
    {
        if (string.IsNullOrWhiteSpace(beneficiaryId))
            return null;

        try
        {
            await _connectivityService.RefreshNowAsync();

            if (_connectivityService.IsCrsOnline)
                return await FetchFromCloudAsync(beneficiaryId);
            else
                return await FetchFromCacheAsync(beneficiaryId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CrsBeneficiaryService] GetByIdAsync failed: {ex.Message}");

            // Last resort: try the cache even if cloud lookup was attempted
            try { return await FetchFromCacheAsync(beneficiaryId); }
            catch { return null; }
        }
    }

    // ── Cloud fetch ─────────────────────────────────────────────────────────────
    private async Task<Beneficiary?> FetchFromCloudAsync(string beneficiaryId)
    {
        using var conn = new MySqlConnector.MySqlConnection(_dbConfig.CrsConnectionString);
        await conn.OpenAsync();

        const string sql = @"
            SELECT
                id, residents_id, beneficiary_id, user_id, civilregistry_id,
                last_name, first_name, middle_name, full_name,
                sex, date_of_birth, age, marital_status, address,
                is_pwd, pwd_id_no, is_senior, senior_id_no,
                disability_type, cause_of_disability,
                created_at, updated_at
            FROM val_beneficiaries
            WHERE beneficiary_id = @id
            LIMIT 1;";

        using var cmd = new MySqlConnector.MySqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", beneficiaryId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        var b = new Beneficiary
        {
            Id                 = reader.GetInt64("id"),
            ResidentsId        = reader.IsDBNull(reader.GetOrdinal("residents_id"))
                                     ? 0 : reader.GetInt64("residents_id"),
            BeneficiaryId      = reader["beneficiary_id"]?.ToString() ?? "",
            UserId             = reader.IsDBNull(reader.GetOrdinal("user_id"))
                                     ? null : reader.GetInt32("user_id"),
            CivilRegistryId    = reader["civilregistry_id"]?.ToString(),
            LastName           = reader["last_name"]?.ToString(),
            FirstName          = reader["first_name"]?.ToString(),
            MiddleName         = reader["middle_name"]?.ToString(),
            FullName           = reader["full_name"]?.ToString(),
            Sex                = reader["sex"]?.ToString(),
            DateOfBirthRaw     = reader["date_of_birth"]?.ToString(),
            AgeRaw             = reader["age"]?.ToString(),
            MaritalStatus      = reader["marital_status"]?.ToString(),
            Address            = reader["address"]?.ToString(),
            IsPwd              = !reader.IsDBNull(reader.GetOrdinal("is_pwd"))
                                     && reader.GetInt32("is_pwd") == 1,
            PwdIdNo            = reader["pwd_id_no"]?.ToString(),
            IsSenior           = !reader.IsDBNull(reader.GetOrdinal("is_senior"))
                                     && reader.GetInt32("is_senior") == 1,
            SeniorIdNo         = reader["senior_id_no"]?.ToString(),
            DisabilityType     = reader["disability_type"]?.ToString(),
            CauseOfDisability  = reader["cause_of_disability"]?.ToString(),
            CreatedAt          = reader.IsDBNull(reader.GetOrdinal("created_at"))
                                     ? null : reader.GetDateTime("created_at"),
            UpdatedAt          = reader.IsDBNull(reader.GetOrdinal("updated_at"))
                                     ? null : reader.GetDateTime("updated_at"),
        };

        // ── Opportunistic cache update ─────────────────────────────────────────
        await UpdateCacheAsync(b);

        return b;
    }

    private async Task<Beneficiary?> FetchFromCacheAsync(string beneficiaryId)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cache = await System.Threading.Tasks.Task.Run(() =>
            dbContext.CrsBeneficiaryCaches
                     .FirstOrDefault(c => c.BeneficiaryId == beneficiaryId));

        if (cache == null) return null;

        return new Beneficiary
        {
            BeneficiaryId = cache.BeneficiaryId,
            FullName      = cache.FullName,
            FirstName     = cache.FirstName,
            LastName      = cache.LastName,
            MiddleName    = cache.MiddleName,
            Sex           = cache.Sex,
            // Cache stores DateOnly; surface as DateOfBirthRaw so the
            // computed DisplayDateOfBirth property on Beneficiary still works.
            DateOfBirthRaw = cache.DateOfBirth?.ToString("yyyy-MM-dd"),
            AgeRaw         = cache.Age?.ToString(),
            Address        = cache.Address,
            MaritalStatus  = cache.MaritalStatus,
            IsPwd          = cache.IsPwd,
            IsSenior       = cache.IsSenior,
            // Fields not stored in the lean cache are left null/default.
        };
    }

    // ── Cache writer (fire-and-forget, errors are non-fatal) ───────────────────
    private async Task UpdateCacheAsync(Beneficiary b)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var cache = await System.Threading.Tasks.Task.Run(() =>
                dbContext.CrsBeneficiaryCaches
                         .FirstOrDefault(c => c.BeneficiaryId == b.BeneficiaryId));

            if (cache == null)
            {
                cache = new CrsBeneficiaryCache { BeneficiaryId = b.BeneficiaryId };
                dbContext.CrsBeneficiaryCaches.Add(cache);
            }

            cache.FullName      = b.FullName;
            cache.FirstName     = b.FirstName;
            cache.LastName      = b.LastName;
            cache.MiddleName    = b.MiddleName;
            cache.Sex           = b.Sex;
            cache.Address       = b.Address;
            cache.MaritalStatus = b.MaritalStatus;
            cache.IsPwd         = b.IsPwd;
            cache.IsSenior      = b.IsSenior;
            cache.CachedAt      = DateTime.Now;

            if (int.TryParse(b.AgeRaw, out int age))
                cache.Age = age;

            if (b.DateOfBirth.HasValue)
                cache.DateOfBirth = DateOnly.FromDateTime(b.DateOfBirth.Value);

            await dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[CrsBeneficiaryService] Cache update failed: {ex.Message}");
        }
    }
}
