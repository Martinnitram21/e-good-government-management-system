using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using System.Collections.ObjectModel;

namespace GoodGovernanceApp.ViewModels;

public class DashboardViewModel : ViewModelBase
{
    private AppDbContext _context;
    private decimal _totalBudget;
    private decimal _totalIncome;
    private decimal _totalExpense;
    private int _activeUsersCount;

    int currentYear = DateTime.Now.Year;

    // Custom chart data
    private double _incomeHeight;
    private double _expenseHeight;
    private List<DeptAllocationData> _deptAllocations = new List<DeptAllocationData>();


    private ObservableCollection<ISeries> _deptPieSeries = new ObservableCollection<ISeries>();
    private ObservableCollection<ISeries> _deptProject  = new ObservableCollection<ISeries>();

    public ObservableCollection<ISeries> DeptProjectPieSeries
    {
        get => _deptProject;
        set { _deptProject = value; OnPropertyChanged(); }
    }

    public ObservableCollection<ISeries> DeptPieSeries
    {
        get => _deptPieSeries;
        set { _deptPieSeries = value; OnPropertyChanged(); }
    }

    public decimal TotalBudget
    {
        get => _totalBudget;
        set { _totalBudget = value; OnPropertyChanged(); }
    }



    public decimal TotalIncome
    {
        get => _totalIncome;
        set { _totalIncome = value; OnPropertyChanged(); UpdateChartHeights(); }
    }

    public decimal TotalExpense
    {
        get => _totalExpense;
        set { _totalExpense = value; OnPropertyChanged(); UpdateChartHeights(); }
    }

    public int ActiveUsersCount
    {
        get => _activeUsersCount;
        set { _activeUsersCount = value; OnPropertyChanged(); }
    }

    public double IncomeHeight
    {
        get => _incomeHeight;
        set { _incomeHeight = value; OnPropertyChanged(); }
    }

    public double ExpenseHeight
    {
        get => _expenseHeight;
        set { _expenseHeight = value; OnPropertyChanged(); }
    }

    public List<DeptAllocationData> DeptAllocations
    {
        get => _deptAllocations;
        set { _deptAllocations = value; OnPropertyChanged(); }
    }

    public DashboardViewModel(AppDbContext context)
    {
        _context = context;
        try
        {
            _ = LoadAnalyticsAsync();
        }
        catch { }
    }

    private async Task LoadAnalyticsAsync()
    {
        if (_context == null) return;

        try
        {
            TotalBudget = await _context.MasterBudgets.SumAsync(b => b.TotalAmount);
            
            TotalIncome = await _context.TblTransactions
                                        .Where(t => t.TransactionType == "Income")
                                        .SumAsync(t => t.Amount);

            TotalExpense = await _context.TblTransactions
                                         .Where(t => t.TransactionType == "Expense" || t.TransactionType == "disbursement")
                                         .SumAsync(t => t.Amount);

            ActiveUsersCount = await _context.Users
                                             .CountAsync(u => u.Status == "active");


            var currentYear = DateTime.Now.Year;

            var projectData = await _context.ProjectDetails
                .Where(p => p.Status == "active")
                .Join(_context.MasterBudgets,
                    p => p.MasterBudgetId,
                    y => y.Id,
                    (p, y) => new { p, y })
                .Where(x => x.y.FiscalYear == currentYear.ToString())
                .GroupBy(x => x.p.Name)
                .Select(g => new DeptAllocationData
                {
                    Name = g.Key,
                    Amount = (double)g.Sum(x => x.p.Budget ?? 0)
                })
                .ToListAsync();

            var seriesproject = new ObservableCollection<ISeries>();

            foreach (var d in projectData)  // <-- use projectData now
            {
                seriesproject.Add(new PieSeries<double>
                {
                    Name = d.Name,
                    Values = new double[] { d.Amount },
                    DataLabelsFormatter = point => point.Model.ToString()
                });
            }
            DeptProjectPieSeries = seriesproject;


            var officeData = await _context.BudgetAllocations
            .Include(a => a.Office)
            .GroupBy(a => a.Office!.Name)
            .Select(g => new DeptAllocationData { Name = g.Key, Amount = (double)g.Sum(a => a.AllocatedAmount) })
            .ToListAsync();

            var series = new ObservableCollection<ISeries>(); foreach (var d in officeData) { series.Add(new PieSeries<double>
            {
                Name = d.Name,
                Values = new double[] { d.Amount }, DataLabelsFormatter = point => point.Model.ToString()
            }); } DeptPieSeries = series;




            UpdateChartHeights();
        }
        catch { }
    }

    private void UpdateChartHeights()
    {
        double incomeVal = (double)TotalIncome;
        double expenseVal = (double)TotalExpense;
        double max = Math.Max(incomeVal, expenseVal);
        
        if (max <= 0) 
        {
            IncomeHeight = 20; 
            ExpenseHeight = 20;
            return;
        }

        IncomeHeight = incomeVal / max * 250;
        ExpenseHeight = expenseVal / max * 250;
    }
}


