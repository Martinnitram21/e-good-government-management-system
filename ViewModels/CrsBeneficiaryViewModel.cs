using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.Utilities;
using GoodGovernanceApp.ViewModels;
using GoodGovernanceApp.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;

namespace GoodGovernanceApp.ViewModels;

public class CrsBeneficiaryViewModel : ViewModelBase
{
    // ── Backing Fields ─────────────────────────────────────────────────────────
    private string _statusMessage        = "Press Load to fetch beneficiaries.";
    private bool   _isLoading;
    private string _searchText           = string.Empty;
    private string _beneficiaryIdFilter  = string.Empty;

    // ── Collections ────────────────────────────────────────────────────────────

    /// <summary>Master list — never filtered directly.</summary>
    public ObservableCollection<Beneficiary> Beneficiaries { get; } = new();

    /// <summary>Filtered view bound to the DataGrid.</summary>
    public ICollectionView BeneficiariesView { get; }

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
            BeneficiariesView.Refresh();
            StatusMessage = string.IsNullOrWhiteSpace(value)
                ? $"✅ Showing all {Beneficiaries.Count:N0} beneficiaries."
                : $"🔍 Filtering by \"{value}\" — {BeneficiariesView.Cast<Beneficiary>().Count():N0} result(s) found.";
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
    private readonly GoodGovernanceApp.Services.IConnectivityService _connectivityService;
    private readonly GoodGovernanceApp.Data.IDatabaseConfig _databaseConfig;
    private readonly GoodGovernanceApp.Services.ICrsBeneficiaryService _crsBeneficiaryService;

    // ── Constructor ────────────────────────────────────────────────────────────
    public CrsBeneficiaryViewModel(AppDbContext dbContext, GoodGovernanceApp.Services.IConnectivityService connectivityService, GoodGovernanceApp.Data.IDatabaseConfig databaseConfig, GoodGovernanceApp.Services.ICrsBeneficiaryService crsBeneficiaryService)
    {
        _dbContext = dbContext;
        _connectivityService = connectivityService;
        _databaseConfig = databaseConfig;
        _crsBeneficiaryService = crsBeneficiaryService;
        BeneficiariesView = CollectionViewSource.GetDefaultView(Beneficiaries);
        BeneficiariesView.Filter = FilterBeneficiary;

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

        var keyword = _searchText.Trim().ToLower();

        return (b.LastName?.ToLower().Contains(keyword) ?? false)
            || (b.FirstName?.ToLower().Contains(keyword) ?? false)
            || (b.MiddleName?.ToLower().Contains(keyword) ?? false)
            || (b.FullName?.ToLower().Contains(keyword) ?? false)
            || (b.BeneficiaryId?.ToLower().Contains(keyword) ?? false)
            || (b.Address?.ToLower().Contains(keyword) ?? false)
            || (b.Sex?.ToLower().Contains(keyword) ?? false)
            || (b.MaritalStatus?.ToLower().Contains(keyword) ?? false)
            || (b.DisabilityType?.ToLower().Contains(keyword) ?? false);
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
        IsLoading = true;
        StatusMessage = "Loading beneficiaries...";
        Beneficiaries.Clear();

        try
        {
            // Refresh now instead of relying on the background monitor's cached
            // value; the page can be opened before its first CRS check completes.
            await _connectivityService.RefreshNowAsync();

            if (_connectivityService.IsCrsOnline)
            {
                await LoadFromCloudAsync();
                StatusMessage = $"✅ Loaded {Beneficiaries.Count:N0} beneficiaries from Cloud.";
            }
            else
            {
                await LoadFromCacheAsync();
                StatusMessage = $"⚠️ Offline: Loaded {Beneficiaries.Count:N0} beneficiaries from Cache.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Failed to load: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public Task LoadAsync() => LoadBeneficiariesAsync();

    private async Task LoadFromCloudAsync(string? filterId = null)
    {
        using var conn = new MySqlConnector.MySqlConnection(_databaseConfig.CrsConnectionString);
        await conn.OpenAsync();

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

        using var cmd = new MySqlConnector.MySqlCommand(sql, conn);
        if (!string.IsNullOrEmpty(filterId))
            cmd.Parameters.AddWithValue("@id", $"%{filterId}%");

        using var reader = await cmd.ExecuteReaderAsync();



        while (await reader.ReadAsync())
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
            Beneficiaries.Add(b);
        }

        await reader.DisposeAsync();
        await TryUpdateCacheAsync(Beneficiaries.ToList());
    }

    private async Task TryUpdateCacheAsync(IReadOnlyCollection<Beneficiary> beneficiaries)
    {
        try
        {
            var beneficiaryIds = beneficiaries
                .Select(b => b.BeneficiaryId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var existing = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions
                .ToListAsync(_dbContext.CrsBeneficiaryCaches
                    .Where(cache => beneficiaryIds.Contains(cache.BeneficiaryId)));
            var existingById = existing.ToDictionary(
                cache => cache.BeneficiaryId,
                StringComparer.OrdinalIgnoreCase);

            foreach (var beneficiary in beneficiaries)
            {
                if (string.IsNullOrWhiteSpace(beneficiary.BeneficiaryId))
                    continue;

                if (!existingById.TryGetValue(beneficiary.BeneficiaryId, out var cache))
                {
                    cache = new CrsBeneficiaryCache { BeneficiaryId = beneficiary.BeneficiaryId };
                    _dbContext.CrsBeneficiaryCaches.Add(cache);
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

            await _dbContext.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Cache is optional. A live CRS result must still be displayed even
            // when the main online database is temporarily unavailable.
            System.Diagnostics.Debug.WriteLine($"[CRS] Cache update skipped: {ex.Message}");
        }
    }

    private async Task LoadFromCacheAsync(string? filterId = null)
    {
        var query = _dbContext.CrsBeneficiaryCaches.AsQueryable();

        if (!string.IsNullOrEmpty(filterId))
            query = query.Where(c => c.BeneficiaryId.Contains(filterId));

        var cachedItems = query.Take(1000).ToList();

        foreach (var cache in cachedItems)
        {
            Beneficiaries.Add(new Beneficiary
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
            });
        }
        await Task.CompletedTask;
    }

    // ── Search by Beneficiary ID ────────────────────────────────────────────────
    private async Task SearchByBeneficiaryIdAsync()
    {
        string id = BeneficiaryIdFilter.Trim();
        if (string.IsNullOrWhiteSpace(id)) return;

        IsLoading = true;
        StatusMessage = $"Searching for Beneficiary ID: {id}…";
        Beneficiaries.Clear();

        try
        {
            await _connectivityService.RefreshNowAsync();

            if (_connectivityService.IsCrsOnline)
            {
                await LoadFromCloudAsync(id);
                StatusMessage = Beneficiaries.Count > 0
                    ? $"✅ Found {Beneficiaries.Count:N0} result(s) from Cloud for ID '{id}'."
                    : $"⚠️ No beneficiary found with ID '{id}'.";
            }
            else
            {
                await LoadFromCacheAsync(id);
                StatusMessage = Beneficiaries.Count > 0
                    ? $"⚠️ Offline: Found {Beneficiaries.Count:N0} result(s) in Cache for '{id}'."
                    : $"⚠️ Offline: No beneficiary found in Cache with ID '{id}'.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Search failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}
