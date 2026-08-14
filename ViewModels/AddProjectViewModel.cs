using System;
using System.Collections.ObjectModel;
using System.Data;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;

namespace GoodGovernanceApp.ViewModels;

public class AddProjectViewModel : ViewModelBase
{
    private DatabaseHelper _db = null!;

    // ── Backing fields ──────────────────────────────────────────────────────────
    private string _projectId       = string.Empty;
    private string _projectName     = string.Empty;
    private string _description     = string.Empty;
    private string _totalBudget     = string.Empty;
    private int?   _selectedYear;
    private string _selectedOfficeCode = string.Empty;
    private int?   _resolvedYearlyBudgetId;
    private string _voucherCode = string.Empty;

    // ── Public properties ────────────────────────────────────────────────────────
    public string ProjectId
    {
        get => _projectId;
        private set { _projectId = value; OnPropertyChanged(); }
    }

    public string VoucherCode
    {
        get => _voucherCode;
        private set { _voucherCode = value; OnPropertyChanged(); }
    }

    public string ProjectName
    {
        get => _projectName;
        set { _projectName = value; OnPropertyChanged(); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; OnPropertyChanged(); }
    }

    public string TotalBudget
    {
        get => _totalBudget;
        set { _totalBudget = value; OnPropertyChanged(); }
    }


    public int? SelectedYear
    {
        get => _selectedYear;
        set
        {
            _selectedYear = value;
            OnPropertyChanged();
            _ = LoadOfficeCodes();
        }
    }

    public string SelectedOfficeCode
    {
        get => _selectedOfficeCode;
        set
        {
            _selectedOfficeCode = value;
            OnPropertyChanged();
            ResolveYearlyBudgetId();
        }
    }

    // ── Collections ──────────────────────────────────────────────────────────────
    public ObservableCollection<int>    AvailableYears        { get; } = new();
    public ObservableCollection<string> FilteredOfficeCodes   { get; } = new();

    // ── Commands ─────────────────────────────────────────────────────────────────
    public ICommand SaveCommand   { get; }
    public ICommand CancelCommand { get; }

    // Raised when save succeeds – the Window subscribes to close itself
    public event EventHandler? SaveSucceeded;

    private readonly GoodGovernanceApp.Services.SessionService _sessionService;

    // ── Constructor ──────────────────────────────────────────────────────────────
    public AddProjectViewModel(DatabaseHelper db, GoodGovernanceApp.Services.SessionService sessionService)
    {
        // Design-time safety check for Visual Studio XAML Designer
        if (App.AppHost == null)
        {
            SaveCommand   = new RelayCommand(_ => { }, _ => false);
            CancelCommand = new RelayCommand(_ => { });
            return;
        }

        _db = db;
        _sessionService = sessionService;

        SaveCommand   = new RelayCommand(async _ => await SaveAsync(), _ => CanSave());
        CancelCommand = new RelayCommand(_ => { });

        _ = InitializeAsync();
    }

    public void SetContext(int? year, string officeCode)
    {
        _selectedYear = year;
        OnPropertyChanged(nameof(SelectedYear));
        _ = LoadOfficeCodes().ContinueWith(_ =>
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                SelectedOfficeCode = officeCode;
            });
        });
    }

    private async Task InitializeAsync()
    {
        await Task.WhenAll(GenerateProjectIdAsync(), LoadYearsAsync(), GenerateUniqueVoucherCodeAsync());
    }

    private async Task GenerateUniqueVoucherCodeAsync()
    {
        bool isUnique = false;
        string code = string.Empty;
        
        while (!isUnique)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var random = new Random();
            code = new string(System.Linq.Enumerable.Repeat(chars, 10)
                .Select(s => s[random.Next(s.Length)])
                .ToArray());

            try
            {
                const string sql = "SELECT COUNT(1) FROM project_details WHERE voucher_code = @code";
                var result = await _db.ExecuteScalarAsync(sql, new SqliteParameter("@code", code));
                if (result != null && int.TryParse(result.ToString(), out int count))
                {
                    if (count == 0) isUnique = true;
                }
                else
                {
                    isUnique = true; // Fallback to allow continuing if check fails
                }
            }
            catch 
            {
                isUnique = true; // Safety fallback
            }
        }
        
        VoucherCode = code;
        System.Windows.Application.Current?.Dispatcher.Invoke(
            System.Windows.Input.CommandManager.InvalidateRequerySuggested);
    }

    // ── Project-ID generation ────────────────────────────────────────────────────
    private async Task GenerateProjectIdAsync()
    {
        const string sql = @"
            SELECT project_details_id
            FROM project_details
            WHERE project_details_id LIKE 'OPP-%'
            ORDER BY project_details_id DESC
            LIMIT 1;";

        try
        {
            var dt = await _db.ExecuteQueryAsync(sql);
            int nextSeq = 1;

            if (dt.Rows.Count > 0)
            {
                string last = dt.Rows[0][0]?.ToString() ?? string.Empty;
                // format: OPP-YYYY-NNNN
                var parts = last.Split('-');
                if (parts.Length == 3 && int.TryParse(parts[2], out int n))
                    nextSeq = n + 1;
            }

            int year = DateTime.Now.Year;
            ProjectId = $"OPP-{year}-{nextSeq:D4}";
            System.Windows.Application.Current?.Dispatcher.Invoke(
                System.Windows.Input.CommandManager.InvalidateRequerySuggested);
        }
        catch
        {
            ProjectId = $"OPP-{DateTime.Now.Year}-0001";
            System.Windows.Application.Current?.Dispatcher.Invoke(
                System.Windows.Input.CommandManager.InvalidateRequerySuggested);
        }
    }

    // ── Load distinct years from MasterBudgets ───────────────────────────────────
    private async Task LoadYearsAsync()
    {
        const string sql = "SELECT DISTINCT budget_year FROM master_budget ORDER BY budget_year DESC;"; 
        try
        {
            var dt = await _db.ExecuteQueryAsync(sql);
            AvailableYears.Clear();
            foreach (DataRow row in dt.Rows)
                if (int.TryParse(row[0]?.ToString(), out int y))
                    AvailableYears.Add(y);
        }
        catch { /* leave empty */ }
    }

    // ── Filter office codes by year ──────────────────────────────────────────────
    private async Task LoadOfficeCodes()
    {
        FilteredOfficeCodes.Clear();
        SelectedOfficeCode = string.Empty;
        _resolvedYearlyBudgetId = null;

        if (_selectedYear == null) return;

        const string sql = @"
            SELECT oa.office_code
            FROM officeallocations oa
            INNER JOIN master_budget yb ON oa.YearlyBudgetId = yb.id
            WHERE yb.budget_year = @year
            ORDER BY oa.office_code;";

        try
        {
            var dt = await _db.ExecuteQueryAsync(sql,
                new SqliteParameter("@year", _selectedYear.Value.ToString()));

            foreach (DataRow row in dt.Rows)
            {
                string? code = row[0]?.ToString();
                if (!string.IsNullOrEmpty(code))
                    FilteredOfficeCodes.Add(code);
            }
        }
        catch { /* leave empty */ }
    }

    // ── Resolve YearlyBudgetId silently in the background ───────────────────────
    private void ResolveYearlyBudgetId()
    {
        _resolvedYearlyBudgetId = null;
        if (_selectedYear == null || string.IsNullOrEmpty(_selectedOfficeCode)) return;

        _ = Task.Run(async () =>
        {
            const string sql = @"
                SELECT oa.YearlyBudgetId
                FROM officeallocations oa
                INNER JOIN master_budget yb ON oa.YearlyBudgetId = yb.id
                WHERE yb.budget_year = @year AND oa.office_code = @code
                LIMIT 1;";

            try
            {
                var result = await _db.ExecuteScalarAsync(sql,
                    new SqliteParameter("@year", _selectedYear!.Value.ToString()),
                    new SqliteParameter("@code", _selectedOfficeCode));

                if (result != null && int.TryParse(result.ToString(), out int id))
                    _resolvedYearlyBudgetId = id;
            }
            catch { /* silently ignore */ }
        });
    }



    // ── Validation ───────────────────────────────────────────────────────────────
    private bool CanSave()
    {
        return !string.IsNullOrWhiteSpace(ProjectId)
            && !string.IsNullOrWhiteSpace(VoucherCode)
            && !string.IsNullOrWhiteSpace(ProjectName)
            && !string.IsNullOrWhiteSpace(SelectedOfficeCode)
            && (string.IsNullOrWhiteSpace(TotalBudget) || decimal.TryParse(TotalBudget, out _));
    }

    // ── Save ─────────────────────────────────────────────────────────────────────
    private async Task SaveAsync()
    {
        if (!CanSave()) return;

        // Safety net: these should never be empty if CanSave() passed, but be explicit
        if (string.IsNullOrWhiteSpace(ProjectId) || string.IsNullOrWhiteSpace(VoucherCode))
        {
            MessageBox.Show("Project ID or Voucher Code is not yet generated. Please wait a moment and try again.",
                "Not Ready", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        decimal? budget = null;
        if (!string.IsNullOrWhiteSpace(TotalBudget) && decimal.TryParse(TotalBudget, out decimal parsed))
            budget = parsed;

        if (_resolvedYearlyBudgetId == null && _selectedYear.HasValue && !string.IsNullOrEmpty(_selectedOfficeCode))
        {
            const string resolveSql = @"
            SELECT oa.YearlyBudgetId
            FROM officeallocations oa
            INNER JOIN master_budget yb ON oa.YearlyBudgetId = yb.id
            WHERE yb.budget_year = @year AND oa.office_code = @code
            LIMIT 1;";
            var res = await _db.ExecuteScalarAsync(resolveSql,
                new SqliteParameter("@year", _selectedYear!.Value.ToString()),
                new SqliteParameter("@code", _selectedOfficeCode));
            if (res != null && int.TryParse(res.ToString(), out int rid))
                _resolvedYearlyBudgetId = rid;
        }

        const string insertSql = @"
        INSERT INTO project_details
            (project_details_id, project, description, office_code, total_budget, contact_person, yearly_budget_id, create_at, updated_at, voucher_code, SyncId, status)
        VALUES
            (@pid, @project, @desc, @code, @budget, @contact, @ybid, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, @voucher, @syncId1, 'active');";

        // --- INSERT 1: project_details ---
        try
        {
            await _db.ExecuteNonQueryAsync(insertSql,
                new SqliteParameter("@pid",     ProjectId),
                new SqliteParameter("@project", ProjectName),
                new SqliteParameter("@desc",    (object?)Description ?? DBNull.Value),
                new SqliteParameter("@code",    SelectedOfficeCode),
                new SqliteParameter("@budget",  (object?)budget ?? DBNull.Value),
                new SqliteParameter("@contact", DBNull.Value),
                new SqliteParameter("@ybid",    (object?)_resolvedYearlyBudgetId ?? DBNull.Value),
                new SqliteParameter("@voucher", VoucherCode),
                new SqliteParameter("@syncId1", Guid.NewGuid().ToString()));
        }
        catch (Exception ex)
        {
            MessageBox.Show($"project_details INSERT failed:\n{ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            return; 
        }

        // --- INSERT 2: audit_trails ---
        try
        {
            long userId = _sessionService.CurrentUser?.Id ?? 0;
            
            const string auditSql = @"
            INSERT INTO audit_trails 
                (user_id, action, model_type, model_id, description, created_at, updated_at, SyncId)
            VALUES
                (@uid, 'create', 'project_details', 0, @auddesc, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP, @syncId3);";
                
            await _db.ExecuteNonQueryAsync(auditSql,
                new Microsoft.Data.Sqlite.SqliteParameter("@uid", userId),
                new Microsoft.Data.Sqlite.SqliteParameter("@auddesc", $"Created new project '{ProjectName}' (ID: {ProjectId}) with voucher '{VoucherCode}'."),
                new Microsoft.Data.Sqlite.SqliteParameter("@syncId3", Guid.NewGuid().ToString()));
        }
        catch (Exception)
        {
            // Fail silently for audit logs so it doesn't stop the user if logging fails
        }

        MessageBox.Show($"Project \"{ProjectName}\" saved successfully!", "Success",
            MessageBoxButton.OK, MessageBoxImage.Information);

        SaveSucceeded?.Invoke(this, EventArgs.Empty);
    }
}
