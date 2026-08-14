using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GoodGovernanceApp.ViewModels;

public class ParametersViewModel : ViewModelBase
{
    private readonly AppDbContext _context;
    
    // Parameters
    private ObservableCollection<Parameter> _parameters = new();
    private Parameter _selectedParameter = new();
    private bool _isEditingParameter;

    // Categories
    private ObservableCollection<Category> _categories = new();
    private Category _selectedCategory = new();
    private bool _isEditingCategory;

    public ObservableCollection<Parameter> Parameters
    {
        get => _parameters;
        set { _parameters = value; OnPropertyChanged(); }
    }

    public Parameter SelectedParameter
    {
        get => _selectedParameter;
        set { _selectedParameter = value ?? new Parameter(); OnPropertyChanged(); }
    }

    public bool IsEditingParameter
    {
        get => _isEditingParameter;
        set { _isEditingParameter = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotEditingParameter)); }
    }
    public bool IsNotEditingParameter => !IsEditingParameter;

    public ObservableCollection<Category> Categories
    {
        get => _categories;
        set { _categories = value; OnPropertyChanged(); }
    }

    public Category SelectedCategory
    {
        get => _selectedCategory;
        set { _selectedCategory = value ?? new Category(); OnPropertyChanged(); }
    }

    public bool IsEditingCategory
    {
        get => _isEditingCategory;
        set { _isEditingCategory = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotEditingCategory)); }
    }
    public bool IsNotEditingCategory => !IsEditingCategory;


    // Commands - Parameters
    public ICommand AddParameterCommand { get; }
    public ICommand SaveParameterCommand { get; }
    public ICommand DeleteParameterCommand { get; }
    public ICommand CancelParameterCommand { get; }

    // Commands - Categories
    public ICommand AddCategoryCommand { get; }
    public ICommand SaveCategoryCommand { get; }
    public ICommand DeleteCategoryCommand { get; }
    public ICommand CancelCategoryCommand { get; }

    public ParametersViewModel(AppDbContext context)
    {
        _context = context;
        AddParameterCommand = new RelayCommand(p => { SelectedParameter = new Parameter(); IsEditingParameter = true; });
        CancelParameterCommand = new RelayCommand(p => { CancelChanges(); IsEditingParameter = false; SelectedParameter = new Parameter(); });
        SaveParameterCommand = new RelayCommand(ExecuteSaveParameter, p => IsEditingParameter && !string.IsNullOrWhiteSpace(SelectedParameter.Name));
        DeleteParameterCommand = new RelayCommand(ExecuteDeleteParameter, p => SelectedParameter.Id != 0 && !IsEditingParameter);

        AddCategoryCommand = new RelayCommand(p => { SelectedCategory = new Category(); IsEditingCategory = true; });
        CancelCategoryCommand = new RelayCommand(p => { CancelChanges(); IsEditingCategory = false; SelectedCategory = new Category(); });
        SaveCategoryCommand = new RelayCommand(ExecuteSaveCategory, p => IsEditingCategory && !string.IsNullOrWhiteSpace(SelectedCategory.Name));
        DeleteCategoryCommand = new RelayCommand(ExecuteDeleteCategory, p => SelectedCategory.Id != 0 && !IsEditingCategory);




        try
        {

            _context.Parameters.Load();
            Parameters = _context.Parameters.Local.ToObservableCollection();

            _context.Categories.Load();
            Categories = _context.Categories.Local.ToObservableCollection();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"LoadData error: {ex.Message}");
            // or
            System.Windows.MessageBox.Show($"Error loading data: {ex.Message}");

        }


    }

    private void LoadData()
    {
        try
        {
            _context.Parameters.Load();
            Parameters = _context.Parameters.Local.ToObservableCollection();

            _context.Categories.Load();
            Categories = _context.Categories.Local.ToObservableCollection();


            Console.WriteLine($"Loaded {Parameters.Count} parameters and {Categories.Count} categories.");
        }
        catch { }
    }

    private void ExecuteSaveParameter(object? obj)
    {
        if (SelectedParameter.Id == 0) _context.Parameters.Add(SelectedParameter);
        _context.SaveChanges();
        IsEditingParameter = false;
        LoadData();
    }

    private void ExecuteDeleteParameter(object? obj)
    {
        _context.Parameters.Remove(SelectedParameter);
        _context.SaveChanges();
        SelectedParameter = new Parameter();
        LoadData();
    }

    private void ExecuteSaveCategory(object? obj)
    {
        if (SelectedCategory.Id == 0) _context.Categories.Add(SelectedCategory);
        _context.SaveChanges();
        IsEditingCategory = false;
        LoadData();
    }

    private void ExecuteDeleteCategory(object? obj)
    {
        _context.Categories.Remove(SelectedCategory);
        _context.SaveChanges();
        SelectedCategory = new Category();
        LoadData();
    }

    private void CancelChanges()
    {
        foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged))
        {
            entry.State = EntityState.Unchanged;
        }
    }
}
