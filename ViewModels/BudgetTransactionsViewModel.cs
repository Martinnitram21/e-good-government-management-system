using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;

namespace GoodGovernanceApp.ViewModels;

public class BudgetTransactionsViewModel : ViewModelBase
{
    private readonly DatabaseHelper _db;
    private readonly AppDbContext _dbContext;

    // ── Raw data loaded from DB ───────────────────────────────────────────────
    private ObservableCollection<TransactionRow> _allRows = new();

    // ── Collection bound to the DataGrid (filtered view) ──────────────────────
    private ICollectionView _transactionsView = null!;

    // ── Tab 1 filter fields ───────────────────────────────────────────────────
    private string _filterOfficeCode   = string.Empty;
    private string _filterProjectCode  = string.Empty;
    private string _filterStatus       = string.Empty;
    private string _filterFreeText     = string.Empty;

    // ── Selected row ──────────────────────────────────────────────────────────
    private TransactionRow? _selectedTransaction;

    // ── Loading indicator ─────────────────────────────────────────────────────
    private bool _isLoading;

    // =========================================================================
    // Public Properties
    // =========================================================================

    public ObservableCollection<TransactionRow> AllRows
    {
        get => _allRows;
        private set
        {
            _allRows = value;
            OnPropertyChanged();

            _transactionsView = CollectionViewSource.GetDefaultView(_allRows);
            _transactionsView.Filter = ApplyFilter;
            OnPropertyChanged(nameof(TransactionsView));
        }
    }

    public ICollectionView TransactionsView => _transactionsView;

    public TransactionRow? SelectedTransaction
    {
        get => _selectedTransaction;
        set { _selectedTransaction = value; OnPropertyChanged(); }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); }
    }

    // ── Filter properties (each setter triggers a view refresh) ───────────────
    public string FilterOfficeCode
    {
        get => _filterOfficeCode;
        set { _filterOfficeCode = value; OnPropertyChanged(); _transactionsView?.Refresh(); }
    }

    public ICommand PrintVoucherCommand { get; }

    public string FilterProjectCode
    {
        get => _filterProjectCode;
        set { _filterProjectCode = value; OnPropertyChanged(); _transactionsView?.Refresh(); }
    }

    public string FilterStatus
    {
        get => _filterStatus;
        set { _filterStatus = value; OnPropertyChanged(); _transactionsView?.Refresh(); }
    }

    /// <summary>Free-text search across all visible columns.</summary>
    public string FilterFreeText
    {
        get => _filterFreeText;
        set { _filterFreeText = value; OnPropertyChanged(); _transactionsView?.Refresh(); }
    }

    // ── Status dropdown choices (empty string = no filter) ────────────────────
    public ObservableCollection<string> StatusOptions { get; } =
        new() { string.Empty, "Active", "Inactive", "Pending", "Completed" };

    public ICommand RefreshCommand             { get; }
    public ICommand ClearFilterCommand         { get; }

    // =========================================================================
    // Constructor
    // =========================================================================

    private string _currentDatabaseMode = string.Empty;


    public BudgetTransactionsViewModel(DatabaseHelper db, AppDbContext dbContext)
    {
        if (App.AppHost == null) return;

        _db = db;
        _dbContext = dbContext;

        // Initialise an empty view so bindings don't throw before data arrives
        _allRows = new ObservableCollection<TransactionRow>();
        _transactionsView = CollectionViewSource.GetDefaultView(_allRows);
        _transactionsView.Filter = ApplyFilter;

        RefreshCommand                 = new RelayCommand(async _ => await LoadTransactionsAsync());
        ClearFilterCommand             = new RelayCommand(_ => ClearFilters());


        PrintVoucherCommand = new RelayCommand(row =>
        {
            if (row is TransactionRow t)
            {
                var window = new VoucherPrintWindow(t);
                window.ShowDialog();
            }
        });

        _ = LoadTransactionsAsync();
    }

    private void LogToFile(string message)
    {
        try
        {
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_error_log.txt");
            System.IO.File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - {message}\n");
        }
        catch { }
    }

    private async Task LoadTransactionsAsync()
    {
        IsLoading = true;

        try
        {
            const string sql = @"
        SELECT
            t.id                           AS Id,
            COALESCE(o.office_code, '')    AS OfficeCode,
            COALESCE(o.name, '')           AS OfficeName,
            COALESCE(p.program, '')        AS ProgramName,
            COALESCE(t.voucher_code, '')   AS VoucherCode,
            COALESCE(t.transaction_type,'')AS TransactionType,
            COALESCE(t.status, '')         AS Status,
            COALESCE(t.description, '')    AS Description,
            COALESCE(t.purpose, '')        AS Purpose,
            COALESCE(t.recipient_type, '') AS RecipientType,
            COALESCE(t.recipient_name, '') AS RecipientName,
            COALESCE(t.priority, '')       AS Priority,
            COALESCE(t.return_reason, '')  AS ReturnReason,
            t.amount                       AS Amount,
            t.transaction_date             AS TransactionDate,
            t.date_applied_                AS DateApplied,
            t.date_approved                AS DateApproved,
            t.expected_completion_date     AS ExpectedCompletion,
            t.returned_at                  AS ReturnedAt,
            t.created_at                   AS CreatedAt,
            t.updated_at                   AS UpdatedAt,
            t.user_id                      AS UserId,
            t.constituent_id               AS ConstituentId,
            t.request_id                   AS RequestId,
            t.registry_id                  AS RegistryId,
            t.services_id                  AS ServicesId,
            t.budget_allocation_id         AS BudgetAllocationId
        FROM tbl_transaction t
        LEFT JOIN tbl_program_provision p ON t.program_id = p.id
        LEFT JOIN tbl_offices o ON t.office_id = o.id
        ORDER BY COALESCE(t.transaction_date, t.created_at) DESC;";

            DataTable dt = await _db.ExecuteQueryAsync(sql);

            DateTime? ParseDate(object val) =>
                val == DBNull.Value || val == null ? null : Convert.ToDateTime(val);

            long? ParseLong(object val) =>
                val == DBNull.Value || val == null ? null : Convert.ToInt64(val);

            var rows = new ObservableCollection<TransactionRow>();

            foreach (DataRow row in dt.Rows)
            {
                rows.Add(new TransactionRow
                {
                    Id                 = Convert.ToInt32(row["Id"]),
                    OfficeCode         = row["OfficeCode"].ToString()      ?? string.Empty,
                    OfficeName         = row["OfficeName"].ToString()      ?? string.Empty,
                    ProgramName        = row["ProgramName"].ToString()     ?? string.Empty,
                    VoucherCode        = row["VoucherCode"].ToString()     ?? string.Empty,
                    TransactionType    = row["TransactionType"].ToString() ?? string.Empty,
                    Status             = row["Status"].ToString()          ?? string.Empty,
                    Description        = row["Description"].ToString()     ?? string.Empty,
                    Purpose            = row["Purpose"].ToString()         ?? string.Empty,
                    RecipientType      = row["RecipientType"].ToString()   ?? string.Empty,
                    RecipientName      = row["RecipientName"].ToString()   ?? string.Empty,
                    Priority           = row["Priority"].ToString()        ?? string.Empty,
                    ReturnReason       = row["ReturnReason"].ToString()    ?? string.Empty,
                    Amount             = row["Amount"] == DBNull.Value ? 0 : Convert.ToDecimal(row["Amount"]),
                    TransactionDate    = ParseDate(row["TransactionDate"]),
                    DateApplied        = ParseDate(row["DateApplied"]),
                    DateApproved       = ParseDate(row["DateApproved"]),
                    ExpectedCompletion = ParseDate(row["ExpectedCompletion"]),
                    ReturnedAt         = ParseDate(row["ReturnedAt"]),
                    CreatedAt          = ParseDate(row["CreatedAt"]),
                    UpdatedAt          = ParseDate(row["UpdatedAt"]),
                    UserId             = ParseLong(row["UserId"]),
                    ConstituentId      = ParseLong(row["ConstituentId"]),
                    RequestId          = ParseLong(row["RequestId"]),
                    RegistryId         = ParseLong(row["RegistryId"]),
                    ServicesId         = ParseLong(row["ServicesId"]),
                    BudgetAllocationId = ParseLong(row["BudgetAllocationId"]),
                });
            }

            AllRows = rows;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[BudgetTransactionsViewModel] Load error: {ex.Message}"
            );
            LogToFile($"[BudgetTransactionsViewModel] Load error: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }

    // =========================================================================
    // Filter Logic
    // =========================================================================

    private bool ApplyFilter(object obj)
    {
        if (obj is not TransactionRow row) return false;

        // Office code filter
        if (!string.IsNullOrWhiteSpace(FilterOfficeCode) &&
            !row.OfficeCode.Contains(FilterOfficeCode, StringComparison.OrdinalIgnoreCase))
            return false;

        // Project code / name filter
        if (!string.IsNullOrWhiteSpace(FilterProjectCode) &&
            !row.ProjectCode.Contains(FilterProjectCode, StringComparison.OrdinalIgnoreCase) &&
            !row.ProjectName.Contains(FilterProjectCode, StringComparison.OrdinalIgnoreCase))
            return false;

        // Status filter (exact match from dropdown)
        if (!string.IsNullOrWhiteSpace(FilterStatus) &&
            !row.Status.Equals(FilterStatus, StringComparison.OrdinalIgnoreCase))
            return false;

        // Free-text search across all visible columns
        if (!string.IsNullOrWhiteSpace(FilterFreeText))
        {
            string q = FilterFreeText;
            bool hit = row.OfficeCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.OfficeName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.ProgramName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.VoucherCode.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.RecipientName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.Description.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.Purpose.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.TransactionType.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.Status.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.Priority.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.Amount.ToString().Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.Date.ToString("yyyy-MM-dd").Contains(q, StringComparison.OrdinalIgnoreCase);
            if (!hit) return false;
        }

        return true;
    }

    private void ClearFilters()
    {
        FilterOfficeCode  = string.Empty;
        FilterProjectCode = string.Empty;
        FilterStatus      = string.Empty;
        FilterFreeText    = string.Empty;
    }
}
