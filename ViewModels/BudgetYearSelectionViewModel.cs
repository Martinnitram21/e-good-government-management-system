using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using Microsoft.Extensions.DependencyInjection;

namespace GoodGovernanceApp.ViewModels;

public class BudgetYearSelectionViewModel : ViewModelBase
{
    private readonly AppDbContext _context;

    private int _newBudgetYear = DateTime.Now.Year;
    private decimal _newBudgetAmount;
    private string _newBudgetDescription = string.Empty;
    private MasterBudget? _selectedMasterBudget;

    public ObservableCollection<MasterBudget> MasterBudgets { get; } = new();

    public int NewBudgetYear
    {
        get => _newBudgetYear;
        set { _newBudgetYear = value; OnPropertyChanged(); }
    }

    public decimal NewBudgetAmount
    {
        get => _newBudgetAmount;
        set { _newBudgetAmount = value; OnPropertyChanged(); }
    }

    public string NewBudgetDescription
    {
        get => _newBudgetDescription;
        set { _newBudgetDescription = value; OnPropertyChanged(); }
    }

    public MasterBudget? SelectedMasterBudget
    {
        get => _selectedMasterBudget;
        set { _selectedMasterBudget = value; OnPropertyChanged(); }
    }

    public ICommand AddMasterBudgetCommand { get; }

    private readonly GoodGovernanceApp.Services.SessionService _sessionService;

    public BudgetYearSelectionViewModel(AppDbContext context, GoodGovernanceApp.Services.SessionService sessionService)
    {
        _context = context;
        _sessionService = sessionService;

        AddMasterBudgetCommand = new RelayCommand(async _ => await AddMasterBudgetAsync());

        _ = LoadInitialDataAsync();
    }

    private async Task LoadInitialDataAsync()
    {
        var budgets = await _context.MasterBudgets.OrderByDescending(b => b.FiscalYear).ToListAsync();
        MasterBudgets.Clear();
        foreach (var b in budgets) MasterBudgets.Add(b);
        if (MasterBudgets.Any()) SelectedMasterBudget = MasterBudgets[0];
    }

    private async Task AddMasterBudgetAsync()
    {
        if (NewBudgetAmount <= 0) return;

        var yearStr = NewBudgetYear.ToString();
        var existing = await _context.MasterBudgets.FirstOrDefaultAsync(b => b.FiscalYear == yearStr);
        if (existing != null)
        {
            MessageBox.Show($"Budget for {NewBudgetYear} already exists.");
            return;
        }

        var budget = new MasterBudget
        {
            FiscalYear = yearStr,
            TotalAmount = Math.Round(NewBudgetAmount, 2),
            Description = NewBudgetDescription,
            CreatedById = _sessionService.CurrentUser?.Id ?? 1,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now,
            AllocatedBudget = 0.00m,
            RemainingBudget = Math.Round(NewBudgetAmount, 2),
            Status = "active"
        };

        _context.MasterBudgets.Add(budget);
        await _context.SaveChangesAsync();

        MasterBudgets.Insert(0, budget);
        SelectedMasterBudget = budget;
        NewBudgetAmount = 0;
        NewBudgetDescription = string.Empty;
    }
}
