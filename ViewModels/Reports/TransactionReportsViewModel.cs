using System;
using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Kernel.Sketches;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.ViewModels;

namespace GoodGovernanceApp.ViewModels.Reports;

public class TransactionReportsViewModel : ViewModelBase
{
    private int _consolidatedTotalCount;
    public int ConsolidatedTotalCount { get => _consolidatedTotalCount; set { _consolidatedTotalCount = value; OnPropertyChanged(); } }

    private decimal _consolidatedTotalAmount;
    public decimal ConsolidatedTotalAmount { get => _consolidatedTotalAmount; set { _consolidatedTotalAmount = value; OnPropertyChanged(); } }

    private decimal _consolidatedAvgAmount;
    public decimal ConsolidatedAvgAmount { get => _consolidatedAvgAmount; set { _consolidatedAvgAmount = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _consolidatedTypeSeries = new();
    public ObservableCollection<ISeries> ConsolidatedTypeSeries { get => _consolidatedTypeSeries; set { _consolidatedTypeSeries = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _consolidatedMonthlySeries = new();
    public ObservableCollection<ISeries> ConsolidatedMonthlySeries { get => _consolidatedMonthlySeries; set { _consolidatedMonthlySeries = value; OnPropertyChanged(); } }

    private ObservableCollection<string> _consolidatedMonthlyLabels = new();
    public ObservableCollection<string> ConsolidatedMonthlyLabels
    {
        get => _consolidatedMonthlyLabels;
        set
        {
            _consolidatedMonthlyLabels = value;
            OnPropertyChanged();
            ConsolidatedMonthlyXAxes = new ObservableCollection<ICartesianAxis> { new Axis { Labels = value } };
        }
    }

    private ObservableCollection<ICartesianAxis> _consolidatedMonthlyXAxes = new ObservableCollection<ICartesianAxis> { new Axis() };
    public ObservableCollection<ICartesianAxis> ConsolidatedMonthlyXAxes { get => _consolidatedMonthlyXAxes; set { _consolidatedMonthlyXAxes = value; OnPropertyChanged(); } }

    private ObservableCollection<TblTransaction> _transactionHistory = new();
    public ObservableCollection<TblTransaction> TransactionHistory { get => _transactionHistory; set { _transactionHistory = value; OnPropertyChanged(); } }

    private ObservableCollection<object> _publicServiceRows = new();
    public ObservableCollection<object> PublicServiceRows { get => _publicServiceRows; set { _publicServiceRows = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _publicServiceSeries = new();
    public ObservableCollection<ISeries> PublicServiceSeries { get => _publicServiceSeries; set { _publicServiceSeries = value; OnPropertyChanged(); } }

    private ObservableCollection<string> _publicServiceLabels = new();
    public ObservableCollection<string> PublicServiceLabels
    {
        get => _publicServiceLabels;
        set
        {
            _publicServiceLabels = value;
            OnPropertyChanged();
            PublicServiceXAxes = new ObservableCollection<ICartesianAxis> { new Axis { Labels = value } };
        }
    }

    private ObservableCollection<ICartesianAxis> _publicServiceXAxes = new ObservableCollection<ICartesianAxis> { new Axis() };
    public ObservableCollection<ICartesianAxis> PublicServiceXAxes { get => _publicServiceXAxes; set { _publicServiceXAxes = value; OnPropertyChanged(); } }
}
