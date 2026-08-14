using System;
using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Kernel.Sketches;
using GoodGovernanceApp.ViewModels;

namespace GoodGovernanceApp.ViewModels.Reports;

public class FinancialReportsViewModel : ViewModelBase
{
    private decimal _totalBudget;
    public decimal TotalBudget { get => _totalBudget; set { _totalBudget = value; OnPropertyChanged(); } }

    private decimal _totalExpenses;
    public decimal TotalExpenses { get => _totalExpenses; set { _totalExpenses = value; OnPropertyChanged(); } }

    private int _activeUsers;
    public int ActiveUsers { get => _activeUsers; set { _activeUsers = value; OnPropertyChanged(); } }

    private int _totalProjects;
    public int TotalProjects { get => _totalProjects; set { _totalProjects = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _deptBudgetSeries = new();
    public ObservableCollection<ISeries> DeptBudgetSeries { get => _deptBudgetSeries; set { _deptBudgetSeries = value; OnPropertyChanged(); } }



    private ObservableCollection<object> _budgetSummaries = new();
    public ObservableCollection<object> BudgetSummaries { get => _budgetSummaries; set { _budgetSummaries = value; OnPropertyChanged(); } }

    private ObservableCollection<object> _departmentalBudgets = new();
    public ObservableCollection<object> DepartmentalBudgets { get => _departmentalBudgets; set { _departmentalBudgets = value; OnPropertyChanged(); } }

    private ObservableCollection<object> _budgetUtilization = new();
    public ObservableCollection<object> BudgetUtilization { get => _budgetUtilization; set { _budgetUtilization = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _budgetUtilSeries = new();
    public ObservableCollection<ISeries> BudgetUtilSeries { get => _budgetUtilSeries; set { _budgetUtilSeries = value; OnPropertyChanged(); } }

    private ObservableCollection<string> _budgetUtilLabels = new();
    public ObservableCollection<string> BudgetUtilLabels
    {
        get => _budgetUtilLabels;
        set
        {
            _budgetUtilLabels = value;
            OnPropertyChanged();
            BudgetUtilXAxes = new ObservableCollection<ICartesianAxis> { new Axis { Labels = value } };
        }
    }

    private ObservableCollection<ICartesianAxis> _budgetUtilXAxes = new ObservableCollection<ICartesianAxis> { new Axis() };
    public ObservableCollection<ICartesianAxis> BudgetUtilXAxes { get => _budgetUtilXAxes; set { _budgetUtilXAxes = value; OnPropertyChanged(); } }
}
