using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.Views;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows.Data;
using System.Windows.Input;
using GoodGovernanceApp.Services;

namespace GoodGovernanceApp.ViewModels;

public class ConsolidatedTransactionsPageViewModel : ViewModelBase
{
    private readonly AppDbContext _dbContext;
    private readonly ICrsBeneficiaryService _crsBeneficiaryService;

    private ObservableCollection<ConsolidatedTransactionsViewModel> _consolidatedRows = new();
    private ICollectionView _consolidatedTransactionsView = null!;

    public ObservableCollection<ConsolidatedTransactionsViewModel> ConsolidatedRows
    {
        get => _consolidatedRows;
        private set 
        { 
            _consolidatedRows = value; 
            OnPropertyChanged(); 
            _consolidatedTransactionsView = CollectionViewSource.GetDefaultView(_consolidatedRows);
            _consolidatedTransactionsView.Filter = ApplyConsolidatedFilter;
            OnPropertyChanged(nameof(ConsolidatedTransactionsView));
        }
    }

    public ICollectionView ConsolidatedTransactionsView => _consolidatedTransactionsView;

    // ── Filter fields ────────────────────────────────────
    private string _consolidatedFilterFreeText     = string.Empty;
    private string _consolidatedFilterName         = string.Empty;
    private string _consolidatedFilterCivilId      = string.Empty;
    private string _consolidatedFilterTransactionType = string.Empty;
    private string _consolidatedFilterStatus       = string.Empty;

    // ── Loading indicator ─────────────────────────────────────────────────────
    private bool _isLoading;

    public bool IsLoading
    {
        get => _isLoading;
        private set { _isLoading = value; OnPropertyChanged(); }
    }

    // ── Filter properties ────────────────────────────────────────
    public string ConsolidatedFilterFreeText
    {
        get => _consolidatedFilterFreeText;
        set { _consolidatedFilterFreeText = value; OnPropertyChanged(); _consolidatedTransactionsView?.Refresh(); }
    }

    public string ConsolidatedFilterName
    {
        get => _consolidatedFilterName;
        set { _consolidatedFilterName = value; OnPropertyChanged(); _consolidatedTransactionsView?.Refresh(); }
    }

    public string ConsolidatedFilterCivilId
    {
        get => _consolidatedFilterCivilId;
        set { _consolidatedFilterCivilId = value; OnPropertyChanged(); _consolidatedTransactionsView?.Refresh(); }
    }

    public string ConsolidatedFilterTransactionType
    {
        get => _consolidatedFilterTransactionType;
        set { _consolidatedFilterTransactionType = value; OnPropertyChanged(); _consolidatedTransactionsView?.Refresh(); }
    }

    public string ConsolidatedFilterStatus
    {
        get => _consolidatedFilterStatus;
        set { _consolidatedFilterStatus = value; OnPropertyChanged(); _consolidatedTransactionsView?.Refresh(); }
    }

    // ── Status dropdown choices ────────────────────
    public ObservableCollection<string> StatusOptions { get; } =
        new() { string.Empty, "Active", "Inactive", "Pending", "Completed" };

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand RefreshCommand                 { get; }
    public ICommand ClearConsolidatedFilterCommand { get; }
    public ICommand OpenAnalyticsCommand           { get; }
    public ICommand OpenOfficeAnalyticsCommand     { get; }
    public ICommand OpenProjectAnalyticsCommand    { get; }

    public ConsolidatedTransactionsPageViewModel(AppDbContext dbContext, ICrsBeneficiaryService crsBeneficiaryService)
    {
        if (App.AppHost == null) return;

        _dbContext = dbContext;
        _crsBeneficiaryService = crsBeneficiaryService;

        _consolidatedRows = new ObservableCollection<ConsolidatedTransactionsViewModel>();
        _consolidatedTransactionsView = CollectionViewSource.GetDefaultView(_consolidatedRows);
        _consolidatedTransactionsView.Filter = ApplyConsolidatedFilter;

        RefreshCommand                 = new RelayCommand(async _ => await LoadConsolidatedTransactionsAsync());
        ClearConsolidatedFilterCommand = new RelayCommand(_ => ClearConsolidatedFilters());
        
        OpenAnalyticsCommand = new RelayCommand(row =>
        {
            if (row is ConsolidatedTransactionsViewModel t && !string.IsNullOrWhiteSpace(t.BeneficiaryId))
            {
                var vm = new BeneficiaryAnalyticsViewModel(_dbContext, _crsBeneficiaryService, t.BeneficiaryId, t.FullName);
                var window = new BeneficiaryAnalyticsWindow(vm);
                window.ShowDialog();
            }
        });

        OpenOfficeAnalyticsCommand = new RelayCommand(row =>
        {
            if (row is ConsolidatedTransactionsViewModel t && !string.IsNullOrWhiteSpace(t.OfficeId))
            {
                var vm = new OfficeAnalyticsViewModel(_dbContext, t.OfficeId, t.OfficeName);
                var window = new OfficeAnalyticsWindow(vm);
                window.ShowDialog();
            }
        });

        OpenProjectAnalyticsCommand = new RelayCommand(row =>
        {
            if (row is ConsolidatedTransactionsViewModel t && !string.IsNullOrWhiteSpace(t.ProjectDetailsId))
            {
                var vm = new ProjectAnalyticsViewModel(_dbContext, t.ProjectDetailsId, t.ProjectName);
                var window = new ProjectAnalyticsWindow(vm);
                window.ShowDialog();
            }
        });

        _ = LoadConsolidatedTransactionsAsync();
    }

    private async Task LoadConsolidatedTransactionsAsync()
    {
        IsLoading = true;

        try
        {
            var rowsList = await ConsolidatedTransactionsViewModel.GetTransactionsAsync(_dbContext);
            ConsolidatedRows = new ObservableCollection<ConsolidatedTransactionsViewModel>(rowsList);
        }
        catch (Exception ex)
        {
            var msg = $"[ConsolidatedTransactionsPageViewModel] Consolidated load error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine(msg);
            LogToFile(msg);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                System.Windows.MessageBox.Show(msg, "Database Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error));
        }
        finally
        {
            IsLoading = false;
        }
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

    private bool ApplyConsolidatedFilter(object obj)
    {
        if (obj is not ConsolidatedTransactionsViewModel row) return false;

        // Full name filter
        if (!string.IsNullOrWhiteSpace(ConsolidatedFilterName) &&
            !row.FullName.Contains(ConsolidatedFilterName, StringComparison.OrdinalIgnoreCase) &&
            !row.FirstName.Contains(ConsolidatedFilterName, StringComparison.OrdinalIgnoreCase) &&
            !row.LastName.Contains(ConsolidatedFilterName, StringComparison.OrdinalIgnoreCase))
            return false;

        // Civil registry ID filter
        if (!string.IsNullOrWhiteSpace(ConsolidatedFilterCivilId) &&
            !row.CivilRegistryId.Contains(ConsolidatedFilterCivilId, StringComparison.OrdinalIgnoreCase))
            return false;

        // Transaction type filter
        if (!string.IsNullOrWhiteSpace(ConsolidatedFilterTransactionType) &&
            !row.TransactionType.Contains(ConsolidatedFilterTransactionType, StringComparison.OrdinalIgnoreCase))
            return false;

        // Status filter
        if (!string.IsNullOrWhiteSpace(ConsolidatedFilterStatus) &&
            !row.Status.Equals(ConsolidatedFilterStatus, StringComparison.OrdinalIgnoreCase))
            return false;

        // Free-text search across all key columns
        if (!string.IsNullOrWhiteSpace(ConsolidatedFilterFreeText))
        {
            string q = ConsolidatedFilterFreeText;
            bool hit = row.BeneficiaryId.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.CivilRegistryId.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.FullName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.FirstName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.MiddleName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.LastName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.Barangay.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.HouseholdNo.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.ProjectDetailsId.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.ProjectName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (row.ProjectCode + " - " + row.ProjectName).Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.OfficeId.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.OfficeName.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || (row.OfficeId + " - " + row.OfficeName).Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.TransactionType.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.Status.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || row.Amount.ToString().Contains(q, StringComparison.OrdinalIgnoreCase);
            if (!hit) return false;
        }

        return true;
    }

    private void ClearConsolidatedFilters()
    {
        ConsolidatedFilterFreeText        = string.Empty;
        ConsolidatedFilterName            = string.Empty;
        ConsolidatedFilterCivilId         = string.Empty;
        ConsolidatedFilterTransactionType = string.Empty;
        ConsolidatedFilterStatus          = string.Empty;
    }

    /// <summary>
    /// Called by MainViewModel after the search dialog closes.
    /// Pre-fills the matching filter field so the grid shows only relevant rows.
    /// </summary>
    public void ApplyInitialSearch(string mode, string value)
    {
        ClearConsolidatedFilters();

        switch (mode)
        {
            case "BeneficiaryId":
            case "ProjectCode":
            case "OfficeCode":
            case "Barangay":
            case "HouseholdNo":
                ConsolidatedFilterFreeText = value;
                break;
            case "FullName":
                ConsolidatedFilterName = value;
                break;
            case "ViewAll":
            default:
                // No filter – show everything
                break;
        }
    }
}
