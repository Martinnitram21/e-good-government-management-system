using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GoodGovernanceApp.ViewModels
{
    public class AuditTrailDisplayModel
    {
        public DateTime? CreatedAt { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string ModelType { get; set; } = string.Empty;
        public long? ModelId { get; set; }
        public string Description { get; set; } = string.Empty;
    }

    public class AuditLogViewModel : ViewModelBase
    {
        private readonly AppDbContext _dbContext;

        private ObservableCollection<AuditTrailDisplayModel> _auditLogs = new();
        public ObservableCollection<AuditTrailDisplayModel> AuditLogs
        {
            get => _auditLogs;
            set { _auditLogs = value; OnPropertyChanged(); }
        }

        private ObservableCollection<AuditTrailDisplayModel> _filteredAuditLogs = new();
        public ObservableCollection<AuditTrailDisplayModel> FilteredAuditLogs
        {
            get => _filteredAuditLogs;
            set { _filteredAuditLogs = value; OnPropertyChanged(); }
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                FilterLogs();
            }
        }

        public ICommand RefreshCommand { get; }

        public AuditLogViewModel(AppDbContext dbContext)
        {
            _dbContext = dbContext;
            RefreshCommand = new RelayCommand(async _ => await LoadLogsAsync());

            _ = LoadLogsAsync();
        }

        private async Task LoadLogsAsync()
        {
            IsLoading = true;
            try
            {
                var query = await _dbContext.AuditTrails
                    .OrderByDescending(a => a.CreatedAt)
                    .Take(500) // Limit to the last 500 records for performance
                    .ToListAsync();

                // Fetch users to map user IDs to Usernames
                var userIds = query.Where(a => a.UserId.HasValue).Select(a => a.UserId!.Value).Distinct().ToList();
                var users = await _dbContext.Users
                    .Where(u => userIds.Contains(u.Id))
                    .ToDictionaryAsync(u => u.Id, u => u.Name ?? "Unknown");

                var displayModels = query.Select(a => new AuditTrailDisplayModel
                {
                    CreatedAt = a.CreatedAt,
                    Username = a.UserId.HasValue && users.ContainsKey(a.UserId.Value) ? users[a.UserId.Value] : "System",
                    Action = a.Action ?? "Unknown",
                    ModelType = a.ModelType ?? string.Empty,
                    ModelId = a.ModelId,
                    Description = a.Description ?? string.Empty
                }).ToList();

                AuditLogs = new ObservableCollection<AuditTrailDisplayModel>(displayModels);
                FilterLogs();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Error loading audit logs:\n{ex.Message}",
                    "Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void FilterLogs()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredAuditLogs = new ObservableCollection<AuditTrailDisplayModel>(AuditLogs);
                return;
            }

            var lowerSearch = SearchText.ToLowerInvariant();
            var filtered = AuditLogs.Where(a =>
                a.Username.ToLowerInvariant().Contains(lowerSearch) ||
                a.Action.ToLowerInvariant().Contains(lowerSearch) ||
                a.ModelType.ToLowerInvariant().Contains(lowerSearch) ||
                a.Description.ToLowerInvariant().Contains(lowerSearch)
            ).ToList();

            FilteredAuditLogs = new ObservableCollection<AuditTrailDisplayModel>(filtered);
        }
    }
}
