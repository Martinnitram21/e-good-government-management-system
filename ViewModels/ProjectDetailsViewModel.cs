using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using Microsoft.Extensions.DependencyInjection;

namespace GoodGovernanceApp.ViewModels;

public class ProjectDetailsViewModel : ViewModelBase
{
    private readonly AppDbContext _context;
    private List<ProjectDetail> _allProjects = new();
    private Office? _selectedDepartment;
    private ProjectDetail? _selectedRole;

    public ObservableCollection<ProjectDetail> SelectedDepartmentRoles { get; } = new();

    public Office? SelectedDepartment
    {
        get => _selectedDepartment;
        set
        {
            _selectedDepartment = value;
            OnPropertyChanged();
            _ = LoadAndFilterAsync(value?.OfficeCode); // ✅ changed from FilterByOfficeCode
        }
    }

    public ProjectDetail? SelectedRole
    {
        get => _selectedRole;
        set
        {
            _selectedRole = value;
            OnPropertyChanged();
        }
    }

    public ProjectDetailsViewModel(AppDbContext context)
    {
        _context = context;
        _ = LoadAndFilterAsync(null); // ✅ changed from LoadDataAsync
    }

    // ✅ replaced LoadDataAsync + FilterByOfficeCode with this single method
    private async Task LoadAndFilterAsync(string? officeCode)
    {
        _allProjects = await _context.ProjectDetails.Where(p => p.Status == "active").ToListAsync();

        SelectedDepartmentRoles.Clear();

        var filtered = string.IsNullOrEmpty(officeCode)
            ? _allProjects
            : _allProjects.Where(p => p.OfficeCode == officeCode).ToList();

        foreach (var project in filtered)
            SelectedDepartmentRoles.Add(project);
    }
}