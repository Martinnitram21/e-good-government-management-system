using System;
using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Kernel.Sketches;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.ViewModels;

namespace GoodGovernanceApp.ViewModels.Reports;

public class SystemReportsViewModel : ViewModelBase
{
    private ObservableCollection<SystemLog> _userActivityLogs = new();
    public ObservableCollection<SystemLog> UserActivityLogs { get => _userActivityLogs; set { _userActivityLogs = value; OnPropertyChanged(); } }

    private ObservableCollection<Parameter> _parametersList = new();
    public ObservableCollection<Parameter> ParametersList { get => _parametersList; set { _parametersList = value; OnPropertyChanged(); } }

    private ObservableCollection<object> _systemOverview = new();
    public ObservableCollection<object> SystemOverview { get => _systemOverview; set { _systemOverview = value; OnPropertyChanged(); } }

    private ObservableCollection<object> _citizenFeedbackRows = new();
    public ObservableCollection<object> CitizenFeedbackRows { get => _citizenFeedbackRows; set { _citizenFeedbackRows = value; OnPropertyChanged(); } }

    private double _avgFeedbackScore;
    public double AvgFeedbackScore { get => _avgFeedbackScore; set { _avgFeedbackScore = value; OnPropertyChanged(); } }

    private int _totalFeedbackCount;
    public int TotalFeedbackCount { get => _totalFeedbackCount; set { _totalFeedbackCount = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _feedbackScoreSeries = new();
    public ObservableCollection<ISeries> FeedbackScoreSeries { get => _feedbackScoreSeries; set { _feedbackScoreSeries = value; OnPropertyChanged(); } }

    private ObservableCollection<string> _feedbackScoreLabels = new();
    public ObservableCollection<string> FeedbackScoreLabels
    {
        get => _feedbackScoreLabels;
        set
        {
            _feedbackScoreLabels = value;
            OnPropertyChanged();
            FeedbackScoreXAxes = new ObservableCollection<ICartesianAxis> { new Axis { Labels = value } };
        }
    }

    private ObservableCollection<ICartesianAxis> _feedbackScoreXAxes = new ObservableCollection<ICartesianAxis> { new Axis() };
    public ObservableCollection<ICartesianAxis> FeedbackScoreXAxes { get => _feedbackScoreXAxes; set { _feedbackScoreXAxes = value; OnPropertyChanged(); } }
}
