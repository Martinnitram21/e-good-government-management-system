using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.Utilities;
using GoodGovernanceApp.ViewModels;
using GoodGovernanceApp.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;

namespace GoodGovernanceApp.ViewModels;

public class CrsBeneficiaryViewModel : ViewModelBase
{
    // ── Backing Fields ─────────────────────────────────────────────────────────
    private string _statusMessage        = "Press Load to fetch beneficiaries.";
    private bool   _isLoading;
    private string _searchText           = string.Empty;
    private string _beneficiaryIdFilter  = string.Empty;
    private IReadOnlyList<Beneficiary> _beneficiaries = Array.Empty<Beneficiary>();
    private ICollectionView _beneficiariesView = null!;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    // ── Collections ────────────────────────────────────────────────────────────

    /// <summary>Master list — never filtered directly.</summary>
    public IReadOnlyList<Beneficiary> Beneficiaries => _beneficiaries;

    /// <summary>Filtered view bound to the DataGrid.</summary>
    public ICollectionView BeneficiariesView => _beneficiariesView;

    // ── Properties ─────────────────────────────────────────────────────────────

    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Filters by last name, first name, beneficiary ID, or address.
    /// Updates the view automatically as the user types.
    /// </summary>
    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            _beneficiariesView.Refresh();
            StatusMessage = string.IsNullOrWhiteSpace(value)
                ? $"✅ Showing all {Beneficiaries.Count:N0} beneficiaries."
                : $"🔍 Filtering by \"{value}\" — {_beneficiariesView.Cast<Beneficiary>().Count():N0} result(s) found.";
        }
    }

    public string BeneficiaryIdFilter
    {
        get => _beneficiaryIdFilter;
        set { _beneficiaryIdFilter = value; OnPropertyChanged(); }
    }

    // ── Commands ───────────────────────────────────────────────────────────────
    public ICommand LoadCommand       { get; }
    public ICommand ClearCommand      { get; }
    public ICommand SearchByIdCommand { get; }
    public ICommand OpenAnalyticsCommand { get; }

    private readonly AppDbContext _dbContext;
    private readonly GoodGovernanceApp.Data.IDatabaseConfig _databaseConfig;
    private readonly GoodGovernanceApp.Services.ICrsBeneficiaryService _crsBeneficiaryService;
    private readonly IServiceScopeFactory _scopeFactory;

    // ── Constructor ────────────────────────────────────────────────────────────
    public CrsBeneficiaryViewModel(
        AppDbContext dbContext,
        GoodGovernanceApp.Data.IDatabaseConfig databaseConfig,
        GoodGovernanceApp.Services.ICrsBeneficiaryService crsBeneficiaryService,
        IServiceScopeFactory scopeFactory)
    {
        _dbContext = dbContext;
        _databaseConfig = databaseConfig;
        _crsBeneficiaryService = crsBeneficiaryService;
        _scopeFactory = scopeFactory;
        SetBeneficiaries(Array.Empty<Beneficiary>());

        LoadCommand       = new RelayCommand(async _ => await LoadBeneficiariesAsync());
        ClearCommand      = new RelayCommand(_ => ClearSearch());
        SearchByIdCommand = new RelayCommand(
            async _ => await SearchByBeneficiaryIdAsync(),
            _        => !string.IsNullOrWhiteSpace(BeneficiaryIdFilter));
        OpenAnalyticsCommand = new RelayCommand(ExecuteOpenAnalytics);
    }

    // ── Filter Logic ───────────────────────────────────────────────────────────
    private bool FilterBeneficiary(object obj)
    {
        if (obj is not Beneficiary b) return false;
        if (string.IsNullOrWhiteSpace(_searchText)) return true;

        var keyword = _searchText.Trim();

        return ContainsIgnoreCase(b.LastName, keyword)
            || ContainsIgnoreCase(b.FirstName, keyword)
            || ContainsIgnoreCase(b.MiddleName, keyword)
            || ContainsIgnoreCase(b.FullName, keyword)
            || ContainsIgnoreCase(b.BeneficiaryId, keyword)
            || ContainsIgnoreCase(b.Address, keyword)
            || ContainsIgnoreCase(b.Sex, keyword)
            || ContainsIgnoreCase(b.MaritalStatus, keyword)
            || ContainsIgnoreCase(b.DisabilityType, keyword);
    }

    private static bool ContainsIgnoreCase(string? value, string keyword) =>
        value?.Contains(keyword, StringComparison.OrdinalIgnoreCase) == true;

    private void SetBeneficiaries(IReadOnlyList<Beneficiary> beneficiaries)
    {
        _beneficiaries = beneficiaries;
        _beneficiariesView = CollectionViewSource.GetDefaultView(_beneficiaries);
        _beneficiariesView.Filter = FilterBeneficiary;
        OnPropertyChanged(nameof(Beneficiaries));
        OnPropertyChanged(nameof(BeneficiariesView));
    }

    private void ClearSearch()
    {
        SearchText          = string.Empty;
        BeneficiaryIdFilter = string.Empty;
    }

    // ── Data Loading ───────────────────────────────────────────────────────────
    private void ExecuteOpenAnalytics(object? parameter)
    {
        if (parameter is Beneficiary b && !string.IsNullOrWhiteSpace(b.BeneficiaryId))
        {
            var fullName = b.DisplayName;
            var vm = new GoodGovernanceApp.ViewModels.BeneficiaryAnalyticsViewModel(_dbContext, _crsBeneficiaryService, b.BeneficiaryId, fullName);
            var window = new GoodGovernanceApp.Views.BeneficiaryAnalyticsWindow(vm);
            window.Show();
        }
    }
    private async Task LoadBeneficiariesAsync()
    {
        if (!await _loadGate.WaitAsync(0))
            return;

        IsLoading = true;
        StatusMessage = "Loading beneficiaries...";
        SetBeneficiaries(Array.Empty<Beneficiary>());

        try
        {
            try
            {
                // The query itself is the authoritative connectivity check. This
                // avoids a redundant test connection immediately before loading.
                var beneficiaries = await LoadFromCloudAsync();
                SetBeneficiaries(beneficiaries);
                StatusMessage = $"✅ Loaded {Beneficiaries.Count:N0} beneficiaries from Cloud.";
                _ = TryUpdateCacheAsync(beneficiaries);
            }
            catch (Exception cloudException)
            {
                var cached = await LoadFromCacheAsync();
                SetBeneficiaries(cached);
                StatusMessage = cached.Count > 0
                    ? $"⚠️ CRS server unavailable: loaded {cached.Count:N0} cached beneficiaries."
                    : $"❌ CRS server unavailable and no cache was found: {cloudException.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Failed to load: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _loadGate.Release();
        }
    }

    public Task LoadAsync() => LoadBeneficiariesAsync();

    private async Task<List<Beneficiary>> LoadFromCloudAsync(string? filterId = null)
    {
        var connectionBuilder = new MySqlConnectionStringBuilder(_databaseConfig.CrsConnectionString)
        {
            ConnectionTimeout = 6,
            DefaultCommandTimeout = 30,
            Pooling = true
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(35));
        await using var conn = new MySqlConnection(connectionBuilder.ConnectionString);
        await conn.OpenAsync(timeout.Token).ConfigureAwait(false);

        string sql = @"
            SELECT
                id, residents_id, beneficiary_id, user_id, civilregistry_id,
                last_name, first_name, middle_name, full_name,
                sex, date_of_birth, age, marital_status, address,
                is_pwd, pwd_id_no, is_senior, senior_id_no,
                disability_type, cause_of_disability,
                created_at, updated_at
            FROM `crs_db`.`val_beneficiaries` ";

        if (!string.IsNullOrEmpty(filterId))
            sql += " WHERE beneficiary_id LIKE @id ";
            
        sql += " ORDER BY last_name, first_name LIMIT 1000;";

        await using var cmd = new MySqlCommand(sql, conn) { CommandTimeout = 30 };
        if (!string.IsNullOrEmpty(filterId))
            cmd.Parameters.AddWithValue("@id", $"%{filterId}%");

        var beneficiaries = new List<Beneficiary>(1000);
        await using var reader = await cmd.ExecuteReaderAsync(timeout.Token).ConfigureAwait(false);
        while (await reader.ReadAsync(timeout.Token).ConfigureAwait(false))
        {
            var b = new Beneficiary
            {
                Id = reader.GetInt64("id"),
                ResidentsId = reader.IsDBNull(reader.GetOrdinal("residents_id"))
                    ? 0 : reader.GetInt64("residents_id"),
                BeneficiaryId = reader["beneficiary_id"]?.ToString() ?? "",
                UserId = reader.IsDBNull(reader.GetOrdinal("user_id")) ? null : reader.GetInt32("user_id"),
                CivilRegistryId = reader["civilregistry_id"]?.ToString(),
                LastName = reader["last_name"]?.ToString(),
                FirstName = reader["first_name"]?.ToString(),
                MiddleName = reader["middle_name"]?.ToString(),
                FullName = reader["full_name"]?.ToString(),
                Sex = reader["sex"]?.ToString(),
                DateOfBirthRaw = reader["date_of_birth"]?.ToString(),
                AgeRaw = reader["age"]?.ToString(),
                MaritalStatus = reader["marital_status"]?.ToString(),
                Address = reader["address"]?.ToString(),
                IsPwd = !reader.IsDBNull(reader.GetOrdinal("is_pwd")) && reader.GetInt32("is_pwd") == 1,
                PwdIdNo = reader["pwd_id_no"]?.ToString(),
                IsSenior = !reader.IsDBNull(reader.GetOrdinal("is_senior")) && reader.GetInt32("is_senior") == 1,
                SeniorIdNo = reader["senior_id_no"]?.ToString(),
                DisabilityType = reader["disability_type"]?.ToString(),
                CauseOfDisability = reader["cause_of_disability"]?.ToString(),
                CreatedAt = reader.IsDBNull(reader.GetOrdinal("created_at")) ? null : reader.GetDateTime("created_at"),
                UpdatedAt = reader.IsDBNull(reader.GetOrdinal("updated_at")) ? null : reader.GetDateTime("updated_at"),
            };
            beneficiaries.Add(b);
        }

        return beneficiaries;
    }

    private async Task TryUpdateCacheAsync(IReadOnlyCollection<Beneficiary> beneficiaries)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var beneficiaryIds = beneficiaries
                .Select(b => b.BeneficiaryId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existing = await dbContext.CrsBeneficiaryCaches
                .Where(cache => beneficiaryIds.Contains(cache.BeneficiaryId))
                .ToListAsync()
                .ConfigureAwait(false);
            var existingById = existing.ToDictionary(
                cache => cache.BeneficiaryId,
                StringComparer.OrdinalIgnoreCase);

            dbContext.ChangeTracker.AutoDetectChangesEnabled = false;

            foreach (var beneficiary in beneficiaries)
            {
                if (string.IsNullOrWhiteSpace(beneficiary.BeneficiaryId))
                    continue;

                if (!existingById.TryGetValue(beneficiary.BeneficiaryId, out var cache))
                {
                    cache = new CrsBeneficiaryCache { BeneficiaryId = beneficiary.BeneficiaryId };
                    dbContext.CrsBeneficiaryCaches.Add(cache);
                    existingById[beneficiary.BeneficiaryId] = cache;
                }

                cache.FullName = beneficiary.FullName;
                cache.FirstName = beneficiary.FirstName;
                cache.LastName = beneficiary.LastName;
                cache.MiddleName = beneficiary.MiddleName;
                cache.Sex = beneficiary.Sex;
                cache.Age = int.TryParse(beneficiary.AgeRaw, out int age) ? age : null;
                cache.Address = beneficiary.Address;
                cache.MaritalStatus = beneficiary.MaritalStatus;
                cache.IsPwd = beneficiary.IsPwd;
                cache.IsSenior = beneficiary.IsSenior;
                cache.CachedAt = DateTime.Now;
            }

            dbContext.ChangeTracker.DetectChanges();
            await dbContext.SaveChangesAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Cache is optional. A live CRS result must still be displayed even
            // when the main online database is temporarily unavailable.
            System.Diagnostics.Debug.WriteLine($"[CRS] Cache update skipped: {ex.Message}");
        }
    }

    private async Task<List<Beneficiary>> LoadFromCacheAsync(string? filterId = null)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var query = dbContext.CrsBeneficiaryCaches.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(filterId))
            query = query.Where(c => c.BeneficiaryId.Contains(filterId));

        var cachedItems = await query.Take(1000).ToListAsync().ConfigureAwait(false);

        return cachedItems.Select(cache => new Beneficiary
            {
                BeneficiaryId = cache.BeneficiaryId,
                FullName = cache.FullName,
                FirstName = cache.FirstName,
                LastName = cache.LastName,
                MiddleName = cache.MiddleName,
                Sex = cache.Sex,
                AgeRaw = cache.Age?.ToString(),
                Address = cache.Address,
                MaritalStatus = cache.MaritalStatus,
                IsPwd = cache.IsPwd,
                IsSenior = cache.IsSenior
            }).ToList();
    }

    // ── Search by Beneficiary ID ────────────────────────────────────────────────
    private async Task SearchByBeneficiaryIdAsync()
    {
        string id = BeneficiaryIdFilter.Trim();
        if (string.IsNullOrWhiteSpace(id)) return;

        if (!await _loadGate.WaitAsync(0))
            return;

        IsLoading = true;
        StatusMessage = $"Searching for Beneficiary ID: {id}…";
        SetBeneficiaries(Array.Empty<Beneficiary>());

        try
        {
            try
            {
                var beneficiaries = await LoadFromCloudAsync(id);
                SetBeneficiaries(beneficiaries);
                StatusMessage = Beneficiaries.Count > 0
                    ? $"✅ Found {Beneficiaries.Count:N0} result(s) from Cloud for ID '{id}'."
                    : $"⚠️ No beneficiary found with ID '{id}'.";
                _ = TryUpdateCacheAsync(beneficiaries);
            }
            catch (Exception cloudException)
            {
                var cached = await LoadFromCacheAsync(id);
                SetBeneficiaries(cached);
                StatusMessage = Beneficiaries.Count > 0
                    ? $"⚠️ Offline: Found {Beneficiaries.Count:N0} result(s) in Cache for '{id}'."
                    : $"❌ Search failed and no cached result was found: {cloudException.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Search failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            _loadGate.Release();
        }
    }
}
