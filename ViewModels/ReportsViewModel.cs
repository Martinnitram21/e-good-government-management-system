using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.Sqlite;

namespace GoodGovernanceApp.ViewModels;

public class ReportsViewModel : ViewModelBase
{
    private readonly AppDbContext _context;
    private readonly GoodGovernanceApp.Services.IConnectivityService _connectivityService;
    private readonly GoodGovernanceApp.Data.IDatabaseConfig _databaseConfig;

    // ── Report Type List ──────────────────────────────────────────────────────
    public ObservableCollection<string> ReportTypes { get; } = new()
    {
        "Financial Overview",
        "Consolidated Transactions Analytics",
        "CRS Beneficiary Analytics",
        "User Activity Log",
        "Budget Summary by Category",
        "Transaction History",
        "Parameters List",
        "Office Budget Allocation",
        "System Overview",
        "Beneficiaries per Project",
        "Individual Beneficiaries Services Received",
        "Budget Utilization Report",
        "Project Implementation Status Report",
        "Public Service Delivery Report",
        "Citizen Feedback Summary Report",
        "Beneficiary Master List"
    };

    private string _selectedReportType = string.Empty;
    public string SelectedReportType
    {
        get => _selectedReportType;
        set
        {
            _selectedReportType = value;
            OnPropertyChanged();
            _ = GenerateReportAsync();
        }
    }

    // ── Global Filters ────────────────────────────────────────────────────────
    public ObservableCollection<string> AvailableYears { get; } = new ObservableCollection<string>(
        new[] { "All" }.Concat(Enumerable.Range(2020, DateTime.Now.Year - 2019).Select(y => y.ToString()))
    );

    public ObservableCollection<string> AvailableMonths { get; } = new() 
    { 
        "All", "January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December" 
    };

    private string _selectedYear = "All";
    public string SelectedYear
    {
        get => _selectedYear;
        set
        {
            _selectedYear = value;
            OnPropertyChanged();
            _ = GenerateReportAsync();
        }
    }

    private string _selectedMonth = "All";
    public string SelectedMonth
    {
        get => _selectedMonth;
        set
        {
            _selectedMonth = value;
            OnPropertyChanged();
            _ = GenerateReportAsync();
        }
    }

    private int? GetSelectedYear() => SelectedYear == "All" ? null : int.Parse(SelectedYear);
    private int? GetSelectedMonth() => SelectedMonth == "All" ? null : AvailableMonths.IndexOf(SelectedMonth);

    // ── Visibility flags ──────────────────────────────────────────────────────
    public bool IsFinancialOverviewVisible              => SelectedReportType == "Financial Overview";
    public bool IsConsolidatedAnalyticsVisible          => SelectedReportType == "Consolidated Transactions Analytics";
    public bool IsCrsAnalyticsVisible                   => SelectedReportType == "CRS Beneficiary Analytics";
    public bool IsUserActivityLogVisible                => SelectedReportType == "User Activity Log";
    public bool IsBudgetSummaryVisible                  => SelectedReportType == "Budget Summary by Category";
    public bool IsTransactionHistoryVisible             => SelectedReportType == "Transaction History";
    public bool IsParametersListVisible                 => SelectedReportType == "Parameters List";
    public bool IsDepartmentalBudgetVisible             => SelectedReportType == "Office Budget Allocation";
    public bool IsSystemOverviewVisible                 => SelectedReportType == "System Overview";
    public bool IsBeneficiariesPerProjectVisible        => SelectedReportType == "Beneficiaries per Project";
    public bool IsIndividualBeneficiariesVisible        => SelectedReportType == "Individual Beneficiaries Services Received";
    public bool IsBudgetUtilizationVisible              => SelectedReportType == "Budget Utilization Report";
    public bool IsProjectStatusVisible                  => SelectedReportType == "Project Implementation Status Report";
    public bool IsPublicServiceDeliveryVisible          => SelectedReportType == "Public Service Delivery Report";
    public bool IsCitizenFeedbackVisible                => SelectedReportType == "Citizen Feedback Summary Report";
    public bool IsBeneficiaryMasterListVisible          => SelectedReportType == "Beneficiary Master List";

    // ── Shared ────────────────────────────────────────────────────────────────
    public Func<double, string> CurrencyFormatter { get; } = v => v.ToString("C0");

    public Reports.FinancialReportsViewModel Financial { get; } = new();
    public Reports.TransactionReportsViewModel Transaction { get; } = new();
    public Reports.BeneficiaryReportsViewModel Beneficiary { get; } = new();
    public Reports.ProjectReportsViewModel Project { get; } = new();
    public Reports.SystemReportsViewModel SystemReports { get; } = new();

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand GenerateReportCommand { get; }
    public ICommand PrintExportCommand    { get; }

    // ── Constructor ───────────────────────────────────────────────────────────
    public ReportsViewModel(AppDbContext context, GoodGovernanceApp.Services.IConnectivityService connectivityService, GoodGovernanceApp.Data.IDatabaseConfig dbConfig)
    {
        _context = context;
        _connectivityService = connectivityService;
        _databaseConfig = dbConfig;

        GenerateReportCommand = new RelayCommand(async _ => await GenerateReportAsync());
        PrintExportCommand    = new RelayCommand(_ =>
        {
            if (!string.IsNullOrEmpty(SelectedReportType))
                HtmlReportExporter.ExportAndOpen(SelectedReportType, this);
        });

        SelectedReportType = ReportTypes[0];
    }

    // ── Main dispatch ─────────────────────────────────────────────────────────
    private async Task GenerateReportAsync()
    {
        // Safety: DI container may already be disposed during app shutdown
        if (_context == null || string.IsNullOrEmpty(SelectedReportType)) return;
        if (System.Windows.Application.Current == null)                   return;

        // Refresh all visibility flags at once
        OnPropertyChanged(nameof(IsFinancialOverviewVisible));
        OnPropertyChanged(nameof(IsConsolidatedAnalyticsVisible));
        OnPropertyChanged(nameof(IsCrsAnalyticsVisible));
        OnPropertyChanged(nameof(IsUserActivityLogVisible));
        OnPropertyChanged(nameof(IsBudgetSummaryVisible));
        OnPropertyChanged(nameof(IsTransactionHistoryVisible));
        OnPropertyChanged(nameof(IsParametersListVisible));
        OnPropertyChanged(nameof(IsDepartmentalBudgetVisible));
        OnPropertyChanged(nameof(IsSystemOverviewVisible));
        OnPropertyChanged(nameof(IsBeneficiariesPerProjectVisible));
        OnPropertyChanged(nameof(IsIndividualBeneficiariesVisible));
        OnPropertyChanged(nameof(IsBudgetUtilizationVisible));
        OnPropertyChanged(nameof(IsProjectStatusVisible));
        OnPropertyChanged(nameof(IsPublicServiceDeliveryVisible));
        OnPropertyChanged(nameof(IsCitizenFeedbackVisible));
        OnPropertyChanged(nameof(IsBeneficiaryMasterListVisible));

        try
        {
            switch (SelectedReportType)
            {
                case "Financial Overview":
                    await LoadFinancialOverviewAsync();
                    break;

                case "Consolidated Transactions Analytics":
                    await LoadConsolidatedAnalyticsAsync();
                    break;

                case "CRS Beneficiary Analytics":
                    await LoadCrsAnalyticsAsync();
                    break;

                case "User Activity Log":
                    int? ualYear = GetSelectedYear();
                    int? ualMonth = GetSelectedMonth();
                    var logsQuery = _context.SystemLogs.Include(l => l.User).AsQueryable();
                    if (ualYear.HasValue) logsQuery = logsQuery.Where(l => l.Timestamp.Year == ualYear.Value);
                    if (ualMonth.HasValue) logsQuery = logsQuery.Where(l => l.Timestamp.Month == ualMonth.Value);
                    var logs = await logsQuery.OrderByDescending(l => l.Timestamp).ToListAsync();
                    SystemReports.UserActivityLogs = new ObservableCollection<SystemLog>(logs);
                    break;

                case "Budget Summary by Category":
                    var categories = await _context.Categories
                        .Include(c => c.Budgets)
                        .ToListAsync();
                    var totalExpenses = await _context.TblTransactions
                        .Where(t => t.TransactionType == "Expense" || t.TransactionType == "disbursement")
                        .SumAsync(t => t.Amount);
                    var summaries = categories.Select(c => new
                    {
                        CategoryName     = c.Name,
                        TotalBudget      = c.Budgets.Sum(b => b.Amount),
                        TotalExpenses    = totalExpenses,
                        RemainingBalance = c.Budgets.Sum(b => b.Amount) - totalExpenses
                    }).ToList();
                    Financial.BudgetSummaries = new ObservableCollection<object>(summaries);
                    break;

                case "Transaction History":
                    int? txYear = GetSelectedYear();
                    int? txMonth = GetSelectedMonth();
                    var txQuery = _context.TblTransactions.AsQueryable();
                    if (txYear.HasValue) txQuery = txQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Year == txYear.Value);
                    if (txMonth.HasValue) txQuery = txQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Month == txMonth.Value);
                    var txns = await txQuery.OrderByDescending(t => t.TransactionDate).ToListAsync();
                    Transaction.TransactionHistory = new ObservableCollection<TblTransaction>(txns);
                    break;

                case "Parameters List":
                    var @params = await _context.Parameters.OrderBy(p => p.Name).ToListAsync();
                    SystemReports.ParametersList = new ObservableCollection<Parameter>(@params);
                    break;

                case "Office Budget Allocation":
                    var allocations = await _context.BudgetAllocations
                        .Include(a => a.Office)
                        .Include(a => a.MasterBudget)
                        .ToListAsync();
                    var officesList = await _context.Offices.ToListAsync();
                    var officeSummaries = allocations.Select(a => new
                    {
                        DepartmentName = officesList.FirstOrDefault(o => o.OfficeCode == a.OfficeCode)?.Name ?? "Unassigned",
                        Year           = a.MasterBudget?.FiscalYear ?? "N/A",
                        Allocated      = a.AllocatedAmount,
                    }).OrderBy(a => a.DepartmentName).ToList();
                    Financial.DepartmentalBudgets = new ObservableCollection<object>(officeSummaries);
                    break;

                case "System Overview":
                    int? sysYear = GetSelectedYear();
                    int? sysMonth = GetSelectedMonth();

                    var expQuery = _context.TblTransactions.Where(t => t.TransactionType == "Expense" || t.TransactionType == "disbursement");
                    if (sysYear.HasValue) expQuery = expQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Year == sysYear.Value);
                    if (sysMonth.HasValue) expQuery = expQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Month == sysMonth.Value);
                    var totalExp = await expQuery.SumAsync(t => t.Amount);

                    var txCountQuery = _context.TblTransactions.AsQueryable();
                    if (sysYear.HasValue) txCountQuery = txCountQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Year == sysYear.Value);
                    if (sysMonth.HasValue) txCountQuery = txCountQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Month == sysMonth.Value);

                    var cTxQuery = _context.ConsolidatedTransactions.AsQueryable();
                    if (sysYear.HasValue) cTxQuery = cTxQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Year == sysYear.Value);
                    if (sysMonth.HasValue) cTxQuery = cTxQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Month == sysMonth.Value);

                    var totalUsers     = await _context.Users.CountAsync();
                    var totalBudget    = await _context.Budgets.SumAsync(b => b.Amount);
                    var totalConsolidated = await cTxQuery.CountAsync();

                    SystemReports.SystemOverview = new ObservableCollection<object>
                    {
                        new { Metric = "Total Registered Users",          Value = totalUsers.ToString() },
                        new { Metric = "Total Budget Allocated",          Value = totalBudget.ToString("C") },
                        new { Metric = "Total Expenses (Dept)",           Value = totalExp.ToString("C") },
                        new { Metric = "Total Dept Transactions",         Value = (await txCountQuery.CountAsync()).ToString() },
                        new { Metric = "Total Consolidated Transactions", Value = totalConsolidated.ToString() },
                    };
                    break;

                case "Beneficiaries per Project":
                    await LoadBeneficiariesPerProjectAsync();
                    break;

                case "Individual Beneficiaries Services Received":
                    await LoadIndividualBeneficiariesAsync();
                    break;

                case "Budget Utilization Report":
                    await LoadBudgetUtilizationAsync();
                    break;

                case "Project Implementation Status Report":
                    await LoadProjectStatusAsync();
                    break;

                case "Public Service Delivery Report":
                    await LoadPublicServiceDeliveryAsync();
                    break;

                case "Citizen Feedback Summary Report":
                    await LoadCitizenFeedbackAsync();
                    break;

                case "Beneficiary Master List":
                    await LoadBeneficiaryMasterListAsync();
                    break;
            }
        }
        catch (Exception ex)
        {
            // Only show dialog if the UI is still alive (not during shutdown)
            if (System.Windows.Application.Current != null)
            {
                System.Windows.MessageBox.Show(
                    $"Error generating report:\n{ex.Message}", "Report Error",
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ReportsViewModel] Silent error during shutdown: {ex.Message}");
            }
        }
    }

    // ── Financial Overview ────────────────────────────────────────────────────
    private async Task LoadFinancialOverviewAsync()
    {
        int? fYear = GetSelectedYear();
        int? fMonth = GetSelectedMonth();

        // Total Budget (Filtered by Year)
        var mbQuery = _context.MasterBudgets.AsQueryable();
        if (fYear.HasValue) mbQuery = mbQuery.Where(b => b.FiscalYear == fYear.Value.ToString());
        Financial.TotalBudget = await mbQuery.SumAsync(b => b.TotalAmount);

        // Total Expenses (Filtered by Year & Month)
        var txQuery = _context.TblTransactions.AsQueryable();
        if (fYear.HasValue) txQuery = txQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Year == fYear.Value);
        if (fMonth.HasValue) txQuery = txQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Month == fMonth.Value);
        Financial.TotalExpenses = await txQuery.SumAsync(t => t.Amount);

        Financial.ActiveUsers    = await _context.Users.CountAsync(u => u.Status == "active");
        Financial.TotalProjects  = await _context.ProjectDetails.CountAsync(p => p.Status == "active");

        // Dept budget distribution pie (Filtered by Year)
        var allocQuery = _context.BudgetAllocations.Include(a => a.Office).Include(a => a.MasterBudget).AsQueryable();
        if (fYear.HasValue) allocQuery = allocQuery.Where(a => a.MasterBudget != null && a.MasterBudget.FiscalYear == fYear.Value.ToString());
        
        var allocs = await allocQuery.ToListAsync();
        var offices = await _context.Offices.ToListAsync();

        var officeData = allocs
            .GroupBy(a => offices.FirstOrDefault(o => o.OfficeCode == a.OfficeCode)?.Name ?? "Unassigned")
            .Select(g => new { Name = g.Key, Amount = (double)g.Sum(a => a.AllocatedAmount) })
            .ToList();

        var deptSeries = new ObservableCollection<ISeries>();
        foreach (var d in officeData)
            deptSeries.Add(new PieSeries<double> { Name = d.Name, Values = new double[] { d.Amount }, DataLabelsFormatter = point => point.Model.ToString() });
        Financial.DeptBudgetSeries = deptSeries;

    }

    // ── Consolidated Transactions Analytics ───────────────────────────────────
    private async Task LoadConsolidatedAnalyticsAsync()
    {
        int? cYear = GetSelectedYear();
        int? cMonth = GetSelectedMonth();
        var allQuery = _context.ConsolidatedTransactions.AsQueryable();
        if (cYear.HasValue) allQuery = allQuery.Where(ct => ct.TransactionDate.HasValue && ct.TransactionDate.Value.Year == cYear.Value);
        if (cMonth.HasValue) allQuery = allQuery.Where(ct => ct.TransactionDate.HasValue && ct.TransactionDate.Value.Month == cMonth.Value);
        var all = await allQuery.ToListAsync();

        Transaction.ConsolidatedTotalCount  = all.Count;
        Transaction.ConsolidatedTotalAmount = all.Sum(ct => ct.Amount ?? 0);
        Transaction.ConsolidatedAvgAmount   = Transaction.ConsolidatedTotalCount > 0
            ? Transaction.ConsolidatedTotalAmount / Transaction.ConsolidatedTotalCount : 0;

        // Pie — by transaction type
        var typeSeries = new ObservableCollection<ISeries>();
        foreach (var grp in all.GroupBy(ct => ct.TransactionType ?? "Unassigned")
                               .Select(g => new { Type = g.Key, Amount = g.Sum(x => x.Amount ?? 0) }))
        {
            typeSeries.Add(new PieSeries<double>
            {
                Name       = grp.Type,
                Values     = new double[] { (double)grp.Amount },
                DataLabelsFormatter = point => point.Model.ToString()
            });
        }
        Transaction.ConsolidatedTypeSeries = typeSeries;

        // Bar — monthly trend
        var monthly = all
            .Where(ct => ct.TransactionDate.HasValue)
            .GroupBy(ct => new { ct.TransactionDate!.Value.Year, ct.TransactionDate.Value.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new
            {
                Label  = $"{new DateTime(g.Key.Year, g.Key.Month, 1):MMM yyyy}",
                Amount = g.Sum(x => x.Amount ?? 0)
            })
            .ToList();

        var monthValues = new ObservableCollection<double>();
        var monthLabels = new ObservableCollection<string>();
        foreach (var m in monthly) { monthValues.Add((double)m.Amount); monthLabels.Add(m.Label); }

        Transaction.ConsolidatedMonthlyLabels = monthLabels;
        Transaction.ConsolidatedMonthlySeries = new ObservableCollection<ISeries>
        {
            new ColumnSeries<double> { Name = "Monthly Amount", Values = monthValues }
        };
    }

    // ── CRS Beneficiary Analytics (raw SQL via separate connection) ───────────
    private async Task LoadCrsAnalyticsAsync()
    {
        try
        {
            if (_connectivityService.IsCrsOnline)
            {
                using var conn = new MySqlConnector.MySqlConnection(_databaseConfig.CrsConnectionString);
                await conn.OpenAsync();

                // Aggregate query — count, PWD, senior, gender, age groups
                const string sql = @"
                    SELECT
                        COUNT(*)                              AS total,
                        SUM(is_pwd)                           AS pwd_count,
                        SUM(is_senior)                        AS senior_count,
                        SUM(CASE WHEN LOWER(sex)='male'   THEN 1 ELSE 0 END) AS male_count,
                        SUM(CASE WHEN LOWER(sex)='female' THEN 1 ELSE 0 END) AS female_count,
                        SUM(CASE WHEN CAST(age AS UNSIGNED) BETWEEN  0 AND 17  THEN 1 ELSE 0 END) AS age_0_17,
                        SUM(CASE WHEN CAST(age AS UNSIGNED) BETWEEN 18 AND 35  THEN 1 ELSE 0 END) AS age_18_35,
                        SUM(CASE WHEN CAST(age AS UNSIGNED) BETWEEN 36 AND 60  THEN 1 ELSE 0 END) AS age_36_60,
                        SUM(CASE WHEN CAST(age AS UNSIGNED) > 60               THEN 1 ELSE 0 END) AS age_60_plus
                    FROM `crs_db`.`val_beneficiaries`;";

                using var cmd    = new MySqlConnector.MySqlCommand(sql, conn);
                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    Beneficiary.CrsTotalCount  = reader.GetInt32("total");
                    Beneficiary.CrsPwdCount    = reader.GetInt32("pwd_count");
                    Beneficiary.CrsSeniorCount = reader.GetInt32("senior_count");

                    int male   = reader.GetInt32("male_count");
                    int female = reader.GetInt32("female_count");
                    int other  = Beneficiary.CrsTotalCount - male - female;

                    // Gender pie
                    var genderSeries = new ObservableCollection<ISeries>();
                    if (male   > 0) genderSeries.Add(new PieSeries<int> { Name = "Male",   Values = new int[] { male },   DataLabelsFormatter = point => point.Model.ToString() });
                    if (female > 0) genderSeries.Add(new PieSeries<int> { Name = "Female", Values = new int[] { female }, DataLabelsFormatter = point => point.Model.ToString() });
                    if (other  > 0) genderSeries.Add(new PieSeries<int> { Name = "Other",  Values = new int[] { other },  DataLabelsFormatter = point => point.Model.ToString() });
                    Beneficiary.CrsGenderSeries = genderSeries;

                    // Age histogram
                    var ageCounts = new int[]
                    {
                        reader.GetInt32("age_0_17"),
                        reader.GetInt32("age_18_35"),
                        reader.GetInt32("age_36_60"),
                        reader.GetInt32("age_60_plus")
                    };
                    var ageValues = new ObservableCollection<int>(ageCounts);
                    Beneficiary.CrsAgeGroupSeries = new ObservableCollection<ISeries>
                    {
                        new ColumnSeries<int>
                        {
                            Name   = "Beneficiaries",
                            Values = ageValues
                        }
                    };
                }
            }
            else
            {
                // OFFLINE MODE
                var cache = await _context.CrsBeneficiaryCaches.ToListAsync();

                Beneficiary.CrsTotalCount = cache.Count;
                Beneficiary.CrsPwdCount = cache.Count(c => c.IsPwd);
                Beneficiary.CrsSeniorCount = cache.Count(c => c.IsSenior);

                int male = cache.Count(c => string.Equals(c.Sex, "male", StringComparison.OrdinalIgnoreCase));
                int female = cache.Count(c => string.Equals(c.Sex, "female", StringComparison.OrdinalIgnoreCase));
                int other = Beneficiary.CrsTotalCount - male - female;

                var genderSeries = new ObservableCollection<ISeries>();
                if (male > 0) genderSeries.Add(new PieSeries<int> { Name = "Male", Values = new int[] { male }, DataLabelsFormatter = point => point.Model.ToString() });
                if (female > 0) genderSeries.Add(new PieSeries<int> { Name = "Female", Values = new int[] { female }, DataLabelsFormatter = point => point.Model.ToString() });
                if (other > 0) genderSeries.Add(new PieSeries<int> { Name = "Other", Values = new int[] { other }, DataLabelsFormatter = point => point.Model.ToString() });
                Beneficiary.CrsGenderSeries = genderSeries;

                var ageCounts = new int[]
                {
                    cache.Count(c => c.Age >= 0 && c.Age <= 17),
                    cache.Count(c => c.Age >= 18 && c.Age <= 35),
                    cache.Count(c => c.Age >= 36 && c.Age <= 60),
                    cache.Count(c => c.Age > 60)
                };
                Beneficiary.CrsAgeGroupSeries = new ObservableCollection<ISeries>
                {
                    new ColumnSeries<int>
                    {
                        Name = "Beneficiaries (Cached)",
                        Values = ageCounts
                    }
                };
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"CRS connection error:\n{ex.Message}\n\nCheck your CRS database settings.",
                "CRS Analytics Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Warning);
        }
    }

    // ── Beneficiaries per Project ─────────────────────────────────────────────
    private async Task LoadBeneficiariesPerProjectAsync()
    {
        int? bpYear = GetSelectedYear();
        int? bpMonth = GetSelectedMonth();
        var bpQuery = _context.ConsolidatedTransactions.Where(ct => ct.ProjectName != null);
        if (bpYear.HasValue) bpQuery = bpQuery.Where(ct => ct.TransactionDate.HasValue && ct.TransactionDate.Value.Year == bpYear.Value);
        if (bpMonth.HasValue) bpQuery = bpQuery.Where(ct => ct.TransactionDate.HasValue && ct.TransactionDate.Value.Month == bpMonth.Value);

        var rows = await bpQuery
            .GroupBy(ct => new { ct.ProjectName, ct.ProjectCode })
            .Select(g => new
            {
                ProjectName      = g.Key.ProjectName ?? "(no name)",
                ProjectCode      = g.Key.ProjectCode ?? "",
                BeneficiaryCount = g.Select(x => x.BeneficiaryId).Distinct().Count(),
                TotalAmount      = g.Sum(x => x.Amount ?? 0),
                TransactionCount = g.Count()
            })
            .OrderByDescending(x => x.BeneficiaryCount)
            .ToListAsync();

        Beneficiary.BeneficiariesPerProject = new ObservableCollection<object>(rows.Cast<object>());

        // Bar chart
        var barValues = new ObservableCollection<int>(rows.Select(r => r.BeneficiaryCount));
        var barLabels = new ObservableCollection<string>(rows.Select(r =>
            r.ProjectName.Length > 20 ? r.ProjectName[..20] + "…" : r.ProjectName));

        Beneficiary.BppBarLabels = barLabels;
        Beneficiary.BppBarSeries = new ObservableCollection<ISeries>
        {
            new ColumnSeries<int> { Name = "Beneficiaries", Values = barValues }
        };
    }

    // ── Individual Beneficiaries Services Received ────────────────────────────
    private async Task LoadIndividualBeneficiariesAsync()
    {
        int? ibYear = GetSelectedYear();
        int? ibMonth = GetSelectedMonth();
        var ibQuery = _context.ConsolidatedTransactions.Where(ct => ct.BeneficiaryId != null);
        if (ibYear.HasValue) ibQuery = ibQuery.Where(ct => ct.TransactionDate.HasValue && ct.TransactionDate.Value.Year == ibYear.Value);
        if (ibMonth.HasValue) ibQuery = ibQuery.Where(ct => ct.TransactionDate.HasValue && ct.TransactionDate.Value.Month == ibMonth.Value);

        var rows = await ibQuery
            .GroupBy(ct => new { ct.BeneficiaryId, ct.FullName })
            .Select(g => new
            {
                BeneficiaryId    = g.Key.BeneficiaryId ?? "",
                FullName         = g.Key.FullName ?? "(unassigned)",
                ServicesReceived = g.Select(x => x.TransactionType).Distinct().Count(),
                TotalTransactions= g.Count(),
                TotalAmount      = g.Sum(x => x.Amount ?? 0),
                LastServiceDate  = g.Max(x => x.TransactionDate)
            })
            .ToListAsync();

        var sortedRows = rows.OrderByDescending(x => x.TotalAmount).ToList();

        Beneficiary.IndividualBeneficiaries = new ObservableCollection<object>(sortedRows.Cast<object>());
    }

    // ── Budget Utilization Report ─────────────────────────────────────────────
    private async Task LoadBudgetUtilizationAsync()
    {
        var projects = await _context.ProjectDetails
            .Where(p => p.Status == "active" && p.ProjectDetailsID != null)
            .ToListAsync();

        var projectCodes = projects
            .Where(p => !string.IsNullOrEmpty(p.ProjectDetailsID))
            .Select(p => p.ProjectDetailsID!)
            .ToList();

        int? buYear = GetSelectedYear();
        int? buMonth = GetSelectedMonth();
        var buTxQuery = _context.ConsolidatedTransactions
            .Where(t => t.ProjectCode != null && projectCodes.Contains(t.ProjectCode) && (t.TransactionType == "Expense" || t.TransactionType == "disbursement"));
        if (buYear.HasValue) buTxQuery = buTxQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Year == buYear.Value);
        if (buMonth.HasValue) buTxQuery = buTxQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Month == buMonth.Value);

        var spentByCode = await buTxQuery
            .GroupBy(t => t.ProjectCode)
            .Select(g => new { Code = g.Key!, Spent = g.Sum(x => x.Amount ?? 0) })
            .ToListAsync();

        var spentDict = spentByCode.ToDictionary(x => x.Code, x => x.Spent);

        var rows = projects.Select(p =>
        {
            var budget  = p.Budget ?? 0;
            var spent   = p.ProjectDetailsID != null && spentDict.TryGetValue(p.ProjectDetailsID, out var s) ? s : 0;
            var util    = budget > 0 ? Math.Round((double)(spent / budget) * 100, 1) : 0.0;
            return new
            {
                ProjectName    = p.Name,
                ProjectCode    = p.ProjectDetailsID ?? "",
                Budget         = budget,
                Spent          = spent,
                Remaining      = budget - spent,
                UtilizationPct = util
            };
        }).OrderByDescending(x => x.UtilizationPct).ToList();

        Financial.BudgetUtilization = new ObservableCollection<object>(rows.Cast<object>());

        // Stacked bar: Budget vs Spent per project
        var budgetVals = new ObservableCollection<double>(rows.Select(r => (double)r.Budget));
        var spentVals  = new ObservableCollection<double>(rows.Select(r => (double)r.Spent));
        var labels     = new ObservableCollection<string>(rows.Select(r =>
            r.ProjectName.Length > 18 ? r.ProjectName[..18] + "…" : r.ProjectName));

        Financial.BudgetUtilLabels = labels;
        Financial.BudgetUtilSeries = new ObservableCollection<ISeries>
        {
            new StackedColumnSeries<double> { Name = "Budget",  Values = budgetVals, Fill = new SolidColorPaint(SKColors.SteelBlue)  },
            new StackedColumnSeries<double> { Name = "Spent",   Values = spentVals,  Fill = new SolidColorPaint(SKColors.Tomato)     }
        };
    }

    // ── Project Implementation Status Report ──────────────────────────────────
    private async Task LoadProjectStatusAsync()
    {
        var projects = await _context.ProjectDetails.ToListAsync();

        var projectCodes = projects
            .Where(p => !string.IsNullOrEmpty(p.ProjectDetailsID))
            .Select(p => p.ProjectDetailsID!)
            .ToList();

        int? psYear = GetSelectedYear();
        int? psMonth = GetSelectedMonth();
        var psTxQuery = _context.ConsolidatedTransactions
            .Where(t => t.ProjectCode != null && projectCodes.Contains(t.ProjectCode) && (t.TransactionType == "Expense" || t.TransactionType == "disbursement"));
        if (psYear.HasValue) psTxQuery = psTxQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Year == psYear.Value);
        if (psMonth.HasValue) psTxQuery = psTxQuery.Where(t => t.TransactionDate.HasValue && t.TransactionDate.Value.Month == psMonth.Value);

        var spentByCode = await psTxQuery
            .GroupBy(t => t.ProjectCode)
            .Select(g => new { Code = g.Key!, Spent = g.Sum(x => x.Amount ?? 0) })
            .ToListAsync();

        var spentDict = spentByCode.ToDictionary(x => x.Code, x => x.Spent);

        var rows = projects.Select(p =>
        {
            var budget  = p.Budget ?? 0;
            var spent   = p.ProjectDetailsID != null && spentDict.TryGetValue(p.ProjectDetailsID, out var s) ? s : 0;
            var util    = budget > 0 ? Math.Round((double)(spent / budget) * 100, 1) : 0.0;
            return new
            {
                ProjectName    = p.Name,
                Office         = p.OfficeCode ?? "—",
                Status         = p.Status,
                Budget         = budget,
                Spent          = spent,
                Remaining      = budget - spent,
                UtilizationPct = util
            };
        }).OrderBy(x => x.Status).ThenByDescending(x => x.Budget).ToList();

        Project.ProjectStatusRows = new ObservableCollection<object>(rows.Cast<object>());

        Project.ActiveProjectCount = rows.Count(r => r.Status == "active");
        Project.ClosedProjectCount = rows.Count(r => r.Status != "active");

        Project.ProjectStatusSeries = new ObservableCollection<ISeries>
        {
            new PieSeries<int> { Name = "Active", Values = new int[] { Project.ActiveProjectCount }, DataLabelsFormatter = point => point.Model.ToString() },
            new PieSeries<int> { Name = "Closed", Values = new int[] { Project.ClosedProjectCount }, DataLabelsFormatter = point => point.Model.ToString() }
        };
    }

    // ── Public Service Delivery Report ────────────────────────────────────────
    private async Task LoadPublicServiceDeliveryAsync()
    {
        int? psdYear = GetSelectedYear();
        int? psdMonth = GetSelectedMonth();
        var psdQuery = _context.ConsolidatedTransactions.AsQueryable();
        if (psdYear.HasValue) psdQuery = psdQuery.Where(ct => ct.TransactionDate.HasValue && ct.TransactionDate.Value.Year == psdYear.Value);
        if (psdMonth.HasValue) psdQuery = psdQuery.Where(ct => ct.TransactionDate.HasValue && ct.TransactionDate.Value.Month == psdMonth.Value);

        var rows = await psdQuery
            .GroupBy(ct => new { ct.OfficeName, ct.OfficeId })
            .Select(g => new
            {
                OfficeName       = g.Key.OfficeName ?? g.Key.OfficeId ?? "Unassigned Office",
                BeneficiaryCount = g.Select(x => x.BeneficiaryId).Distinct().Count(),
                TotalAmount      = g.Sum(x => x.Amount ?? 0),
                TransactionCount = g.Count()
            })
            .OrderByDescending(x => x.BeneficiaryCount)
            .ToListAsync();

        Transaction.PublicServiceRows = new ObservableCollection<object>(rows.Cast<object>());

        // Bar chart — beneficiaries served per office
        var vals   = new ObservableCollection<int>(rows.Select(r => r.BeneficiaryCount));
        var labels = new ObservableCollection<string>(rows.Select(r =>
            r.OfficeName.Length > 18 ? r.OfficeName[..18] + "…" : r.OfficeName));

        Transaction.PublicServiceLabels = labels;
        Transaction.PublicServiceSeries = new ObservableCollection<ISeries>
        {
            new ColumnSeries<int> { Name = "Beneficiaries Served", Values = vals }
        };
    }

    // ── Citizen Feedback Summary Report ──────────────────────────────────────
    private async Task LoadCitizenFeedbackAsync()
    {
        int? fbYear = GetSelectedYear();
        int? fbMonth = GetSelectedMonth();
        var evalQuery = _context.Evaluations.Include(e => e.Evaluator).Include(e => e.UploadedFile).AsQueryable();
        if (fbYear.HasValue) evalQuery = evalQuery.Where(e => e.EvaluationDate.Year == fbYear.Value);
        if (fbMonth.HasValue) evalQuery = evalQuery.Where(e => e.EvaluationDate.Month == fbMonth.Value);

        var evals = await evalQuery
            .OrderByDescending(e => e.EvaluationDate)
            .ToListAsync();

        SystemReports.TotalFeedbackCount = evals.Count;
        SystemReports.AvgFeedbackScore   = evals.Count > 0 ? Math.Round(evals.Average(e => (double)e.Score), 1) : 0;

        var rows = evals.Select(e => new
        {
            Date      = e.EvaluationDate.ToString("MMM dd, yyyy"),
            Evaluator = e.Evaluator?.Name ?? "—",
            File      = e.UploadedFile?.FileName ?? "—",
            Score     = e.Score,
            Rating    = e.Score >= 90 ? "Excellent" : e.Score >= 75 ? "Good" : e.Score >= 60 ? "Fair" : "Poor",
            Comments  = e.Comments ?? ""
        }).ToList();

        SystemReports.CitizenFeedbackRows = new ObservableCollection<object>(rows.Cast<object>());

        // Bar chart: score distribution buckets
        int excellent = evals.Count(e => e.Score >= 90);
        int good      = evals.Count(e => e.Score >= 75 && e.Score < 90);
        int fair      = evals.Count(e => e.Score >= 60 && e.Score < 75);
        int poor      = evals.Count(e => e.Score < 60);

        SystemReports.FeedbackScoreLabels = new ObservableCollection<string> { "Excellent (90+)", "Good (75–89)", "Fair (60–74)", "Poor (<60)" };
        SystemReports.FeedbackScoreSeries = new ObservableCollection<ISeries>
        {
            new ColumnSeries<int>
            {
                Name  = "Evaluations",
                Values = new int[] { excellent, good, fair, poor }
            }
        };
    }

    // ── Beneficiary Master List ───────────────────────────────────────────────
    private async Task LoadBeneficiaryMasterListAsync()
    {
        // Step 1: Aggregate beneficiary transaction summaries from consolidated_transactions
        int? bmYear = GetSelectedYear();
        int? bmMonth = GetSelectedMonth();
        var bmQuery = _context.ConsolidatedTransactions.Where(ct => ct.BeneficiaryId != null);
        if (bmYear.HasValue) bmQuery = bmQuery.Where(ct => ct.TransactionDate.HasValue && ct.TransactionDate.Value.Year == bmYear.Value);
        if (bmMonth.HasValue) bmQuery = bmQuery.Where(ct => ct.TransactionDate.HasValue && ct.TransactionDate.Value.Month == bmMonth.Value);

        var txSummaries = await bmQuery
            .GroupBy(ct => new
            {
                ct.BeneficiaryId,
                ct.FullName,
                ct.FirstName,
                ct.LastName,
                ct.MiddleName,
                ct.Barangay,
                ct.HouseholdNo
            })
            .Select(g => new
            {
                BeneficiaryId     = g.Key.BeneficiaryId!,
                FullName          = g.Key.FullName ?? $"{g.Key.LastName}, {g.Key.FirstName}",
                FirstName         = g.Key.FirstName ?? "",
                LastName          = g.Key.LastName  ?? "",
                Barangay          = g.Key.Barangay  ?? "",
                HouseholdNo       = g.Key.HouseholdNo ?? "",
                ServicesReceived  = g.Select(x => x.TransactionType).Distinct().Count(),
                TotalTransactions = g.Count(),
                TotalAmount       = g.Sum(x => x.Amount ?? 0),
                LastServiceDate   = g.Max(x => x.TransactionDate)
            })
            .ToListAsync();

        // Step 2: Enrich with CRS personal data
        var beneficiaryIds = txSummaries.Select(x => x.BeneficiaryId).ToList();

        // Dictionary: beneficiaryId -> (Sex, Age, Address, MaritalStatus, IsPwd, IsSenior)
        var profileDict = new Dictionary<string, (string Sex, string Age, string Address, string MaritalStatus, bool IsPwd, bool IsSenior)>(StringComparer.OrdinalIgnoreCase);

        if (_connectivityService.IsCrsOnline)
        {
            // ── Online: fetch from CRS MySQL ────────────────────────────────
            try
            {
                using var conn = new MySqlConnector.MySqlConnection(_databaseConfig.CrsConnectionString);
                await conn.OpenAsync();

                // Build parameterised IN clause
                var paramNames  = beneficiaryIds.Select((_, i) => $"@id{i}").ToList();
                var inClause    = string.Join(",", paramNames);
                var sql         = $@"
                    SELECT beneficiary_id, sex, age, address, marital_status, is_pwd, is_senior
                    FROM   `crs_db`.`val_beneficiaries`
                    WHERE  beneficiary_id IN ({inClause});";

                using var cmd = new MySqlConnector.MySqlCommand(sql, conn);
                for (int i = 0; i < beneficiaryIds.Count; i++)
                    cmd.Parameters.AddWithValue($"@id{i}", beneficiaryIds[i]);

                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var bid = reader.IsDBNull(reader.GetOrdinal("beneficiary_id")) ? "" : reader.GetString("beneficiary_id");
                    if (string.IsNullOrEmpty(bid)) continue;

                    profileDict[bid] = (
                        Sex           : reader.IsDBNull(reader.GetOrdinal("sex"))            ? "" : reader.GetString("sex"),
                        Age           : reader.IsDBNull(reader.GetOrdinal("age"))            ? "" : reader.GetString("age"),
                        Address       : reader.IsDBNull(reader.GetOrdinal("address"))        ? "" : reader.GetString("address"),
                        MaritalStatus : reader.IsDBNull(reader.GetOrdinal("marital_status")) ? "" : reader.GetString("marital_status"),
                        IsPwd         : !reader.IsDBNull(reader.GetOrdinal("is_pwd"))  && reader.GetBoolean("is_pwd"),
                        IsSenior      : !reader.IsDBNull(reader.GetOrdinal("is_senior")) && reader.GetBoolean("is_senior")
                    );
                }
            }
            catch
            {
                // Fall through to cache on error
            }
        }

        // ── Offline / fallback: use local cache ──────────────────────────────
        if (profileDict.Count == 0)
        {
            var cache = await _context.CrsBeneficiaryCaches
                .Where(c => beneficiaryIds.Contains(c.BeneficiaryId))
                .ToListAsync();

            foreach (var c in cache)
            {
                profileDict[c.BeneficiaryId] = (
                    Sex           : c.Sex           ?? "",
                    Age           : c.Age.HasValue  ? c.Age.Value.ToString() : "",
                    Address       : c.Address       ?? "",
                    MaritalStatus : c.MaritalStatus ?? "",
                    IsPwd         : c.IsPwd,
                    IsSenior      : c.IsSenior
                );
            }
        }

        // Step 3: Build enriched master rows
        var masterRows = txSummaries.Select(t =>
        {
            profileDict.TryGetValue(t.BeneficiaryId, out var p);
            return new
            {
                BeneficiaryId     = t.BeneficiaryId,
                FullName          = string.IsNullOrWhiteSpace(t.FullName) ? "(unassigned)" : t.FullName,
                Sex               = p.Sex           ?? "",
                Age               = p.Age           ?? "",
                Address           = p.Address       ?? "",
                MaritalStatus     = p.MaritalStatus ?? "",
                Barangay          = t.Barangay,
                HouseholdNo       = t.HouseholdNo,
                IsPwd             = p.IsPwd   ? "Yes" : "No",
                IsSenior          = p.IsSenior ? "Yes" : "No",
                ServicesReceived  = t.ServicesReceived,
                TotalTransactions = t.TotalTransactions,
                TotalAmount       = t.TotalAmount,
                LastServiceDate   = t.LastServiceDate.HasValue
                    ? t.LastServiceDate.Value.ToString("MMM dd, yyyy") : "—",
                // raw booleans for KPI counters
                _IsPwd            = p.IsPwd,
                _IsSenior         = p.IsSenior,
                _Sex              = (p.Sex ?? "").ToLower()
            };
        }).OrderByDescending(r => r.TotalAmount).ToList();

        Beneficiary.BeneficiaryMasterList = new ObservableCollection<object>(masterRows.Cast<object>());

        // Step 4: KPIs
        Beneficiary.BmlTotalBeneficiaries = masterRows.Count;
        Beneficiary.BmlTotalAmount        = masterRows.Sum(r => r.TotalAmount);
        Beneficiary.BmlPwdCount           = masterRows.Count(r => r._IsPwd);
        Beneficiary.BmlSeniorCount        = masterRows.Count(r => r._IsSenior);

        // Step 5: Gender pie chart
        int male   = masterRows.Count(r => r._Sex == "male");
        int female = masterRows.Count(r => r._Sex == "female");
        int other  = masterRows.Count - male - female;

        var genderSeries = new ObservableCollection<ISeries>();
        if (male   > 0) genderSeries.Add(new PieSeries<int> { Name = "Male",   Values = new int[] { male },   DataLabelsFormatter = point => point.Model.ToString() });
        if (female > 0) genderSeries.Add(new PieSeries<int> { Name = "Female", Values = new int[] { female }, DataLabelsFormatter = point => point.Model.ToString() });
        if (other  > 0) genderSeries.Add(new PieSeries<int> { Name = "Other",  Values = new int[] { other },  DataLabelsFormatter = point => point.Model.ToString() });
        Beneficiary.BmlGenderSeries = genderSeries;

        // Step 6: Classification pie chart (PWD / Senior / Regular)
        int pwdOnly    = masterRows.Count(r => r._IsPwd && !r._IsSenior);
        int seniorOnly = masterRows.Count(r => r._IsSenior && !r._IsPwd);
        int both       = masterRows.Count(r => r._IsPwd && r._IsSenior);
        int regular    = masterRows.Count(r => !r._IsPwd && !r._IsSenior);

        var classSeries = new ObservableCollection<ISeries>();
        if (pwdOnly    > 0) classSeries.Add(new PieSeries<int> { Name = "PWD Only",       Values = new int[] { pwdOnly    }, DataLabelsFormatter = point => point.Model.ToString() });
        if (seniorOnly > 0) classSeries.Add(new PieSeries<int> { Name = "Senior Only",    Values = new int[] { seniorOnly }, DataLabelsFormatter = point => point.Model.ToString() });
        if (both       > 0) classSeries.Add(new PieSeries<int> { Name = "PWD & Senior",   Values = new int[] { both       }, DataLabelsFormatter = point => point.Model.ToString() });
        if (regular    > 0) classSeries.Add(new PieSeries<int> { Name = "Regular",        Values = new int[] { regular    }, DataLabelsFormatter = point => point.Model.ToString() });
        Beneficiary.BmlClassificationSeries = classSeries;

        // Step 7: Top 10 by total amount — bar chart
        var top10 = masterRows.Take(10).ToList();
        Beneficiary.BmlTopBeneficiaryLabels = new ObservableCollection<string>(
            top10.Select(r => r.FullName.Length > 20 ? r.FullName[..20] + "…" : r.FullName));
        Beneficiary.BmlTopBeneficiarySeries = new ObservableCollection<ISeries>
        {
            new ColumnSeries<double>
            {
                Name  = "Total Amount (₱)",
                Values = new ObservableCollection<double>(top10.Select(r => (double)r.TotalAmount))
            }
        };

        // Step 8: Monthly transaction trend
        var monthly = await _context.ConsolidatedTransactions
            .Where(ct => ct.BeneficiaryId != null && ct.TransactionDate.HasValue)
            .GroupBy(ct => new { ct.TransactionDate!.Value.Year, ct.TransactionDate.Value.Month })
            .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
            .Select(g => new
            {
                Label = $"{g.Key.Year}-{g.Key.Month:D2}",
                Count = g.Count(),
                Total = g.Sum(x => x.Amount ?? 0)
            })
            .ToListAsync();

        Beneficiary.BmlMonthlyTrendLabels = new ObservableCollection<string>(monthly.Select(m =>
        {
            if (DateTime.TryParse(m.Label + "-01", out var dt))
                return dt.ToString("MMM yy");
            return m.Label;
        }));
        Beneficiary.BmlMonthlyTrendSeries = new ObservableCollection<ISeries>
        {
            new ColumnSeries<int>
            {
                Name  = "Transactions",
                Values = new ObservableCollection<int>(monthly.Select(m => m.Count)),
                Fill   = new SolidColorPaint(SKColors.CornflowerBlue)
            }
        };
    }
}


