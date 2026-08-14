using System;
using System.Collections.ObjectModel;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.Kernel.Sketches;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.ViewModels;

namespace GoodGovernanceApp.ViewModels.Reports;

public class BeneficiaryReportsViewModel : ViewModelBase
{
    private int _crsTotalCount;
    public int CrsTotalCount { get => _crsTotalCount; set { _crsTotalCount = value; OnPropertyChanged(); } }

    private int _crsPwdCount;
    public int CrsPwdCount { get => _crsPwdCount; set { _crsPwdCount = value; OnPropertyChanged(); } }

    private int _crsSeniorCount;
    public int CrsSeniorCount { get => _crsSeniorCount; set { _crsSeniorCount = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _crsGenderSeries = new();
    public ObservableCollection<ISeries> CrsGenderSeries { get => _crsGenderSeries; set { _crsGenderSeries = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _crsAgeGroupSeries = new();
    public ObservableCollection<ISeries> CrsAgeGroupSeries { get => _crsAgeGroupSeries; set { _crsAgeGroupSeries = value; OnPropertyChanged(); } }



    private ObservableCollection<string> _crsAgeGroupLabels = new() { "0–17", "18–35", "36–60", "60+" };
    public ObservableCollection<string> CrsAgeGroupLabels
    {
        get => _crsAgeGroupLabels;
        set
        {
            _crsAgeGroupLabels = value;
            OnPropertyChanged();
            CrsAgeGroupXAxes = new ObservableCollection<ICartesianAxis> { new Axis { Labels = value } };
        }
    }

    private ObservableCollection<ICartesianAxis> _crsAgeGroupXAxes = new ObservableCollection<ICartesianAxis> { new Axis { Labels = new[] { "0–17", "18–35", "36–60", "60+" } } };
    public ObservableCollection<ICartesianAxis> CrsAgeGroupXAxes { get => _crsAgeGroupXAxes; set { _crsAgeGroupXAxes = value; OnPropertyChanged(); } }

    private ObservableCollection<object> _beneficiariesPerProject = new();
    public ObservableCollection<object> BeneficiariesPerProject { get => _beneficiariesPerProject; set { _beneficiariesPerProject = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _bppBarSeries = new();
    public ObservableCollection<ISeries> BppBarSeries { get => _bppBarSeries; set { _bppBarSeries = value; OnPropertyChanged(); } }

    private ObservableCollection<string> _bppBarLabels = new();
    public ObservableCollection<string> BppBarLabels
    {
        get => _bppBarLabels;
        set
        {
            _bppBarLabels = value;
            OnPropertyChanged();
            BppBarXAxes = new ObservableCollection<ICartesianAxis> { new Axis { Labels = value } };
        }
    }

    private ObservableCollection<ICartesianAxis> _bppBarXAxes = new ObservableCollection<ICartesianAxis> { new Axis() };
    public ObservableCollection<ICartesianAxis> BppBarXAxes { get => _bppBarXAxes; set { _bppBarXAxes = value; OnPropertyChanged(); } }

    private ObservableCollection<object> _individualBeneficiaries = new();
    public ObservableCollection<object> IndividualBeneficiaries { get => _individualBeneficiaries; set { _individualBeneficiaries = value; OnPropertyChanged(); } }

    private ObservableCollection<object> _beneficiaryMasterList = new();
    public ObservableCollection<object> BeneficiaryMasterList { get => _beneficiaryMasterList; set { _beneficiaryMasterList = value; OnPropertyChanged(); } }

    private int _bmlTotalBeneficiaries;
    public int BmlTotalBeneficiaries { get => _bmlTotalBeneficiaries; set { _bmlTotalBeneficiaries = value; OnPropertyChanged(); } }

    private decimal _bmlTotalAmount;
    public decimal BmlTotalAmount { get => _bmlTotalAmount; set { _bmlTotalAmount = value; OnPropertyChanged(); } }

    private int _bmlPwdCount;
    public int BmlPwdCount { get => _bmlPwdCount; set { _bmlPwdCount = value; OnPropertyChanged(); } }

    private int _bmlSeniorCount;
    public int BmlSeniorCount { get => _bmlSeniorCount; set { _bmlSeniorCount = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _bmlGenderSeries = new();
    public ObservableCollection<ISeries> BmlGenderSeries { get => _bmlGenderSeries; set { _bmlGenderSeries = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _bmlClassificationSeries = new();
    public ObservableCollection<ISeries> BmlClassificationSeries { get => _bmlClassificationSeries; set { _bmlClassificationSeries = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _bmlTopBeneficiarySeries = new();
    public ObservableCollection<ISeries> BmlTopBeneficiarySeries { get => _bmlTopBeneficiarySeries; set { _bmlTopBeneficiarySeries = value; OnPropertyChanged(); } }

    private ObservableCollection<string> _bmlTopBeneficiaryLabels = new();
    public ObservableCollection<string> BmlTopBeneficiaryLabels
    {
        get => _bmlTopBeneficiaryLabels;
        set
        {
            _bmlTopBeneficiaryLabels = value;
            OnPropertyChanged();
            BmlTopBeneficiaryXAxes = new ObservableCollection<ICartesianAxis> { new Axis { Labels = value } };
        }
    }

    private ObservableCollection<ICartesianAxis> _bmlTopBeneficiaryXAxes = new ObservableCollection<ICartesianAxis> { new Axis() };
    public ObservableCollection<ICartesianAxis> BmlTopBeneficiaryXAxes { get => _bmlTopBeneficiaryXAxes; set { _bmlTopBeneficiaryXAxes = value; OnPropertyChanged(); } }

    private ObservableCollection<ISeries> _bmlMonthlyTrendSeries = new();
    public ObservableCollection<ISeries> BmlMonthlyTrendSeries { get => _bmlMonthlyTrendSeries; set { _bmlMonthlyTrendSeries = value; OnPropertyChanged(); } }

    private ObservableCollection<string> _bmlMonthlyTrendLabels = new();
    public ObservableCollection<string> BmlMonthlyTrendLabels
    {
        get => _bmlMonthlyTrendLabels;
        set
        {
            _bmlMonthlyTrendLabels = value;
            OnPropertyChanged();
            BmlMonthlyTrendXAxes = new ObservableCollection<ICartesianAxis> { new Axis { Labels = value } };
        }
    }

    private ObservableCollection<ICartesianAxis> _bmlMonthlyTrendXAxes = new ObservableCollection<ICartesianAxis> { new Axis() };
    public ObservableCollection<ICartesianAxis> BmlMonthlyTrendXAxes { get => _bmlMonthlyTrendXAxes; set { _bmlMonthlyTrendXAxes = value; OnPropertyChanged(); } }
}
