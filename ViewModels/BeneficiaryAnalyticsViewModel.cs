using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace GoodGovernanceApp.ViewModels;

public class BeneficiaryAnalyticsViewModel : ViewModelBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICrsBeneficiaryService _crsBeneficiaryService;
    public string BeneficiaryId { get; }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    private string _fullName = string.Empty;
    public string FullName
    {
        get => _fullName;
        set { _fullName = value; OnPropertyChanged(); }
    }

    // ── CRS Demographic Profile ────────────────────────────────────────────────

    /// <summary>
    /// True when <see cref="BeneficiaryId"/> exists in transaction data but
    /// has no matching record in the CRS database (online) or cache (offline).
    /// Bound to a "No CRS record" placeholder in the view.
    /// </summary>
    private bool _crsProfileNotFound;
    public bool CrsProfileNotFound
    {
        get => _crsProfileNotFound;
        set { _crsProfileNotFound = value; OnPropertyChanged(); }
    }

    private string _sex = string.Empty;
    public string Sex
    {
        get => _sex;
        set { _sex = value; OnPropertyChanged(); }
    }

    private string _displayDateOfBirth = "N/A";
    public string DisplayDateOfBirth
    {
        get => _displayDateOfBirth;
        set { _displayDateOfBirth = value; OnPropertyChanged(); }
    }

    private string _displayAge = "N/A";
    public string DisplayAge
    {
        get => _displayAge;
        set { _displayAge = value; OnPropertyChanged(); }
    }

    private string _maritalStatus = string.Empty;
    public string MaritalStatus
    {
        get => _maritalStatus;
        set { _maritalStatus = value; OnPropertyChanged(); }
    }

    private string _classifications = "None";
    public string Classifications
    {
        get => _classifications;
        set { _classifications = value; OnPropertyChanged(); }
    }

    private bool _isPwd;
    public bool IsPwd
    {
        get => _isPwd;
        set { _isPwd = value; OnPropertyChanged(); }
    }

    private string _pwdIdNo = string.Empty;
    public string PwdIdNo
    {
        get => _pwdIdNo;
        set { _pwdIdNo = value; OnPropertyChanged(); }
    }

    private bool _isSenior;
    public bool IsSenior
    {
        get => _isSenior;
        set { _isSenior = value; OnPropertyChanged(); }
    }

    private string _seniorIdNo = string.Empty;
    public string SeniorIdNo
    {
        get => _seniorIdNo;
        set { _seniorIdNo = value; OnPropertyChanged(); }
    }

    private string _address = string.Empty;
    public string Address
    {
        get => _address;
        set { _address = value; OnPropertyChanged(); }
    }

    private int _totalTransactions;
    public int TotalTransactions
    {
        get => _totalTransactions;
        set { _totalTransactions = value; OnPropertyChanged(); }
    }

    private decimal _totalAmount;
    public decimal TotalAmount
    {
        get => _totalAmount;
        set { _totalAmount = value; OnPropertyChanged(); }
    }

    private decimal _consolidatedAmount;
    public decimal ConsolidatedAmount
    {
        get => _consolidatedAmount;
        set { _consolidatedAmount = value; OnPropertyChanged(); }
    }

    private decimal _departmentBudgetAmount;
    public decimal DepartmentBudgetAmount
    {
        get => _departmentBudgetAmount;
        set { _departmentBudgetAmount = value; OnPropertyChanged(); }
    }

    private decimal _averageAmount;
    public decimal AverageAmount
    {
        get => _averageAmount;
        set { _averageAmount = value; OnPropertyChanged(); }
    }

    public ObservableCollection<ConsolidatedTransactionsViewModel> RecentTransactions { get; } = new();

    // LiveCharts collections
    private ObservableCollection<ISeries> _typeBreakdownSeries = new();
    public ObservableCollection<ISeries> TypeBreakdownSeries
    {
        get => _typeBreakdownSeries;
        set { _typeBreakdownSeries = value; OnPropertyChanged(); }
    }

    private ObservableCollection<ISeries> _statusBreakdownSeries = new();
    public ObservableCollection<ISeries> StatusBreakdownSeries
    {
        get => _statusBreakdownSeries;
        set { _statusBreakdownSeries = value; OnPropertyChanged(); }
    }

    private ObservableCollection<ISeries> _officeBreakdownSeries = new();
    public ObservableCollection<ISeries> OfficeBreakdownSeries
    {
        get => _officeBreakdownSeries;
        set { _officeBreakdownSeries = value; OnPropertyChanged(); }
    }

    private ObservableCollection<ISeries> _monthlyTrendSeries = new();
    public ObservableCollection<ISeries> MonthlyTrendSeries
    {
        get => _monthlyTrendSeries;
        set { _monthlyTrendSeries = value; OnPropertyChanged(); }
    }

    private ObservableCollection<string> _monthlyLabels = new();
    public ObservableCollection<string> MonthlyLabels
    {
        get => _monthlyLabels;
        set { _monthlyLabels = value; OnPropertyChanged(); }
    }

    public Func<double, string> Formatter { get; set; }

    public BeneficiaryAnalyticsViewModel(AppDbContext dbContext, ICrsBeneficiaryService crsBeneficiaryService, string beneficiaryId, string fullName)
    {
        _dbContext    = dbContext;
        _crsBeneficiaryService = crsBeneficiaryService;
        BeneficiaryId = beneficiaryId;
        FullName      = fullName;
        Formatter     = value => value.ToString("C");

        _ = InitializeAsync();
    }

    /// <summary>
    /// Kicks off both data-fetches in parallel so neither waits on the other.
    /// <see cref="IsLoading"/> stays true until both are done.
    /// </summary>
    private async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            await LoadCrsProfileAsync();
            await LoadAnalyticsAsync();
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── CRS profile fetch ──────────────────────────────────────────────────────
    private async Task LoadCrsProfileAsync()
    {
        try
        {
            var b = await _crsBeneficiaryService.GetByIdAsync(BeneficiaryId);

            if (b == null)
            {
                CrsProfileNotFound = true;
                return;
            }

            // Prefer the CRS full name if it is richer than what was passed in
            if (!string.IsNullOrWhiteSpace(b.DisplayName))
                FullName = b.DisplayName;

            Sex                = b.Sex ?? string.Empty;
            DisplayDateOfBirth = b.DisplayDateOfBirth;
            DisplayAge         = b.DisplayAge;
            MaritalStatus      = b.MaritalStatus ?? string.Empty;
            Classifications    = b.Classifications;
            IsPwd              = b.IsPwd;
            PwdIdNo            = b.PwdIdNo ?? string.Empty;
            IsSenior           = b.IsSenior;
            SeniorIdNo         = b.SeniorIdNo ?? string.Empty;
            Address            = b.Address ?? string.Empty;
            CrsProfileNotFound = false;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[BeneficiaryAnalyticsViewModel] LoadCrsProfileAsync failed: {ex.Message}");
            CrsProfileNotFound = true;
        }
    }

    // ── Transaction analytics ──────────────────────────────────────────────────
    private async Task LoadAnalyticsAsync()
    {
        // IsLoading is already set by InitializeAsync — do NOT touch it here
        try
        {
            // ── Step 1: Find all dept projects linked to this Beneficiary ID ─────
            var linkedProjects = await _dbContext.ProjectDetails
                .Where(pd => pd.ContactPerson == BeneficiaryId && pd.ProjectDetailsID != null && pd.Status == "active")
                .ToListAsync();

            var projectCodes = linkedProjects
                .Select(pd => pd.ProjectDetailsID!)
                .ToList();

            // ── Step 2: Load dept budget transactions (tbl_transaction table) ────────
            long.TryParse(BeneficiaryId, out long beneficiaryIdLong);
            var departmentTransactions = await _dbContext.TblTransactions
                .Where(t => t.ConstituentId == beneficiaryIdLong || t.RecipientName == FullName)
                .ToListAsync();

            // ── Step 3: Load consolidated transactions by beneficiary_id ──────────
            var consolidatedTransactions = await _dbContext.ConsolidatedTransactions
                .Where(ct => ct.BeneficiaryId == BeneficiaryId)
                .ToListAsync();

            // ── Step 4: Map dept transactions into unified view model ──────────────
            var deptList = departmentTransactions.Select(t => new ConsolidatedTransactionsViewModel
            {
                Id              = (int)t.Id,
                BeneficiaryId   = BeneficiaryId,
                FullName        = FullName,
                TransactionType = t.TransactionType ?? "Department Project",
                Amount          = t.Amount,
                TransactionDate = t.TransactionDate,
                Status          = t.Status ?? "Completed",
                Source          = "Department Budget",
                CreatedAt       = t.CreatedAt ?? DateTime.MinValue,
                OfficeId        = t.Office?.OfficeCode ?? "Unknown"
            }).ToList();

            // ── Step 5: Map consolidated transactions into unified view model ──────
            var consolidatedList = consolidatedTransactions.Select(ct => new ConsolidatedTransactionsViewModel
            {
                Id              = ct.Id,
                BeneficiaryId   = ct.BeneficiaryId ?? BeneficiaryId,
                FullName        = ct.FullName ?? FullName,
                TransactionType = ct.TransactionType ?? "Consolidated",
                Amount          = ct.Amount ?? 0,
                TransactionDate = ct.TransactionDate,
                Status          = ct.Status ?? "Unknown",
                Source          = "Consolidated",
                CreatedAt       = ct.CreatedAt ?? DateTime.MinValue,
                OfficeId        = ct.OfficeId ?? "Unknown"
            }).ToList();

            // ── Step 6: Merge both lists ──────────────────────────────────────────
            var combinedList = deptList.Concat(consolidatedList).ToList();

            // ── Step 7: Per-source summary stats ─────────────────────────────────
            //   ConsolidatedAmount     = sum of consolidated_transactions rows
            //   DepartmentBudgetAmount = sum of dept transactions rows
            //   TotalAmount            = grand total of both
            TotalTransactions      = combinedList.Count;
            ConsolidatedAmount     = consolidatedList.Sum(t => t.Amount);
            DepartmentBudgetAmount = deptList.Sum(t => t.Amount);
            TotalAmount            = ConsolidatedAmount + DepartmentBudgetAmount;
            AverageAmount          = TotalTransactions > 0 ? TotalAmount / TotalTransactions : 0;

            // ── Step 8: Recent transactions list (top 10, newest first) ──────────
            foreach (var r in combinedList.OrderByDescending(t => t.CreatedAt).Take(10))
                RecentTransactions.Add(r);

            if (!combinedList.Any()) return;

            // ── Step 9: Pie Chart – Amount by Transaction Type (both sources) ─────
            var typeSeries = new ObservableCollection<ISeries>();
            foreach (var grp in combinedList
                .GroupBy(t => t.TransactionType ?? "Unknown")
                .Select(g => new { Type = g.Key, Amount = g.Sum(x => x.Amount) }))
            {
                typeSeries.Add(new PieSeries<double>
                {
                    Name       = grp.Type,
                    Values     = new double[] { (double)grp.Amount },
                    DataLabelsFormatter = point => point.Model.ToString()
                });
            }
            TypeBreakdownSeries = typeSeries;

            // ── Step 10: Pie Chart – Source Breakdown (Consolidated vs Dept) ──────
            var sourceSeries = new ObservableCollection<ISeries>();
            if (consolidatedList.Any())
                sourceSeries.Add(new PieSeries<int>
                {
                    Name       = "Consolidated",
                    Values     = new int[] { consolidatedList.Count },
                    DataLabelsFormatter = point => point.Model.ToString()
                });
            if (deptList.Any())
                sourceSeries.Add(new PieSeries<int>
                {
                    Name       = "Department Budget",
                    Values     = new int[] { deptList.Count },
                    DataLabelsFormatter = point => point.Model.ToString()
                });
            StatusBreakdownSeries = sourceSeries;

            // ── Step 10.5: Pie Chart — Office Breakdown ─────────────────────────────
            var officeSeries = new ObservableCollection<ISeries>();
            foreach (var grp in combinedList
                .GroupBy(t => string.IsNullOrWhiteSpace(t.OfficeId) ? "Unknown" : t.OfficeId)
                .Select(g => new { Office = g.Key, Amount = g.Sum(x => x.Amount) }))
            {
                officeSeries.Add(new PieSeries<double>
                {
                    Name       = grp.Office,
                    Values     = new double[] { (double)grp.Amount },
                    DataLabelsFormatter = point => point.Model.ToString()
                });
            }
            OfficeBreakdownSeries = officeSeries;

            // ── Step 11: Monthly Trend – two columns per month (one per source) ───
            var allMonths = combinedList
                .Where(t => t.TransactionDate.HasValue)
                .Select(t => new { t.TransactionDate!.Value.Year, t.TransactionDate.Value.Month })
                .Distinct()
                .OrderBy(m => m.Year).ThenBy(m => m.Month)
                .ToList();

            var trendDept         = new ObservableCollection<double>();
            var trendConsolidated = new ObservableCollection<double>();
            var labels            = new ObservableCollection<string>();

            foreach (var m in allMonths)
            {
                labels.Add($"{new DateTime(m.Year, m.Month, 1):MMM yyyy}");

                trendDept.Add((double)deptList
                    .Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Year == m.Year && t.TransactionDate.Value.Month == m.Month)
                    .Sum(t => t.Amount));

                trendConsolidated.Add((double)consolidatedList
                    .Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Year == m.Year && t.TransactionDate.Value.Month == m.Month)
                    .Sum(t => t.Amount));
            }

            MonthlyLabels = labels;
            MonthlyTrendSeries = new ObservableCollection<ISeries>
            {
                new ColumnSeries<double> { Name = "Department Budget", Values = trendDept         },
                new ColumnSeries<double> { Name = "Consolidated",      Values = trendConsolidated }
            };
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Error loading beneficiary analytics:\n{ex}",
                "Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
        // NOTE: IsLoading is cleared by InitializeAsync, not here.
    }
}


