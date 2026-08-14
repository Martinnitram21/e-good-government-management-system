using System;
using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using GoodGovernanceApp.ViewModels;

namespace GoodGovernanceApp.ViewModels.Reports;

public class ProjectReportsViewModel : ViewModelBase
{
    private ObservableCollection<object> _projectStatusRows = new();
    public ObservableCollection<object> ProjectStatusRows { get => _projectStatusRows; set { _projectStatusRows = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _projectStatusSeries = new();
    public ObservableCollection<ISeries> ProjectStatusSeries { get => _projectStatusSeries; set { _projectStatusSeries = value; OnPropertyChanged(); } }

    private int _activeProjectCount;
    public int ActiveProjectCount { get => _activeProjectCount; set { _activeProjectCount = value; OnPropertyChanged(); } }

    private int _closedProjectCount;
    public int ClosedProjectCount { get => _closedProjectCount; set { _closedProjectCount = value; OnPropertyChanged(); } }
}
