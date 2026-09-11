using GoodGovernanceApp.Data;
using GoodGovernanceApp.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Org.BouncyCastle.Utilities.Net;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace GoodGovernanceApp.ViewModels;

/// <summary>
/// Represents a Metro tile navigation item.
/// </summary>
public class NavigationItem : ViewModelBase
{
    public string Name      { get; set; } = string.Empty;
    public string Icon      { get; set; } = "Apps";
    public string ViewToken { get; set; } = string.Empty;

    /// <summary>Hex color string for the tile background, e.g. "#FF009688"</summary>
    public string TileColor { get; set; } = "#FF009688";

    /// <summary>Optional tile group label shown in the dashboard.</summary>
    public string Group     { get; set; } = string.Empty;

    /// <summary>Short subtitle shown under the tile name on the dashboard.</summary>
    public string Description { get; set; } = string.Empty;

    private bool _isSelected;
    /// <summary>True when this item's view is currently open — drives sidebar highlight.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; OnPropertyChanged(); }
    }
}

public class AppNotification
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public System.DateTime Date { get; set; }
    public string ViewToken { get; set; } = string.Empty;
    public string Icon { get; set; } = "Bell";
    public string Color { get; set; } = "#3B82F6";
    public string FormattedDate => Date.ToString("MMM dd, hh:mm tt");
}

public class MainViewModel : ViewModelBase, IDisposable
{
    // ── services ─────────────────────────────────────────────────────────────
    private readonly Services.SessionService _sessionService;
    private readonly Timer _clockTimer;

    // ── backing fields ────────────────────────────────────────────────────────
    private NavigationItem _selectedNavItem = null!;
    private object         _currentView     = null!;
    private bool           _isShowingDashboard = true;
    private bool           _isSidebarOpen   = false;
    private string         _governanceName  = "Good Governance Management System";
    private string         _currentDate     = string.Empty;
    private bool           _isDatabaseConnected = true;
    private string?        _profilePhotoPath;
    private BitmapImage?   _profilePhotoSource;
    private string         _currentSectionTitle = "Home";
    private System.Windows.Media.ImageSource?   _systemPhotoSource;
    private System.Windows.Media.ImageSource? _systemGovPhotoSource;
    private System.Windows.Media.ImageSource?   _copyrightPhotoSource;
    private bool           _hasNewNotifications = false;

    // ── public properties ─────────────────────────────────────────────────────
    public string CurrentUserName => _sessionService.CurrentUser?.Name ?? "Guest";
    public string CurrentUserRole => _sessionService.CurrentUser?.Role ?? "None";

    public string CurrentDate
    {
        get => _currentDate;
        private set { _currentDate = value; OnPropertyChanged(); }
    }

    public bool IsDatabaseConnected
    {
        get => _isDatabaseConnected;
        set { _isDatabaseConnected = value; OnPropertyChanged(); }
    }

    public bool IsShowingDashboard
    {
        get => _isShowingDashboard;
        set
        {
            if (_isShowingDashboard == value) return;
            _isShowingDashboard = value;
            if (value)
                IsSidebarOpen = false;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsShowingView));
            CommandManager.InvalidateRequerySuggested();
        }
    }

    /// <summary>Inverse of IsShowingDashboard – used in XAML visibility bindings.</summary>
    public bool IsShowingView => !_isShowingDashboard;

    public bool IsSidebarOpen
    {
        get => _isSidebarOpen;
        set { _isSidebarOpen = value; OnPropertyChanged(); }
    }

    public string CurrentSectionTitle
    {
        get => _currentSectionTitle;
        set { _currentSectionTitle = value; OnPropertyChanged(); }
    }

    public BitmapImage? ProfilePhotoSource
    {
        get => _profilePhotoSource;
        set { _profilePhotoSource = value; OnPropertyChanged(); }
    }

    public System.Windows.Media.ImageSource? SystemPhotoSource
    {
        get => _systemPhotoSource;
        set { _systemPhotoSource = value; OnPropertyChanged(); }
    }

    public System.Windows.Media.ImageSource? GovPhotoSource
    {
        get => _systemGovPhotoSource;
        set { _systemGovPhotoSource = value; OnPropertyChanged(); }
    }

    public System.Windows.Media.ImageSource? CopyrightPhotoSource
    {
        get => _copyrightPhotoSource;
        set { _copyrightPhotoSource = value; OnPropertyChanged(); }
    }

    public string GovernanceName
    {
        get => _governanceName;
        set { _governanceName = value; OnPropertyChanged(); }
    }

    public NavigationItem SelectedNavItem
    {
        get => _selectedNavItem;
        set
        {
            _selectedNavItem = value;
            OnPropertyChanged();
        }
    }

    public object CurrentView
    {
        get => _currentView;
        set { _currentView = value; OnPropertyChanged(); }
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }
    public IReadOnlyList<NavigationItem> SidebarPrimaryItems { get; }
    public IReadOnlyList<NavigationItem> SidebarTrailingItems { get; }
    public IReadOnlyList<NavigationItem> DashboardPrimaryItems { get; }
    public IReadOnlyList<NavigationItem> DashboardMiddleItems { get; }
    public IReadOnlyList<NavigationItem> DashboardBottomItems { get; }
    public ObservableCollection<AppNotification> Notifications { get; } = new();

    public bool HasNewNotifications
    {
        get => _hasNewNotifications;
        set { _hasNewNotifications = value; OnPropertyChanged(); }
    }

    private bool _isNotificationPopupOpen;
    public bool IsNotificationPopupOpen
    {
        get => _isNotificationPopupOpen;
        set { _isNotificationPopupOpen = value; OnPropertyChanged(); }
    }

    private bool _isOnline = false;
    public bool IsOnline
    {
        get => _isOnline;
        set
        {
            if (_isOnline == value) return;
            _isOnline = value;
            IsDatabaseConnected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RemoteStatusText));
            OnPropertyChanged(nameof(ActiveDatabaseStatusText));
            OnPropertyChanged(nameof(ActiveConnectionIsOnline));
        }
    }

    private bool _isNetworkOnline;
    public bool IsNetworkOnline
    {
        get => _isNetworkOnline;
        private set
        {
            if (_isNetworkOnline == value) return;
            _isNetworkOnline = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NetworkStatusText));
            OnPropertyChanged(nameof(ActiveDatabaseStatusText));
            OnPropertyChanged(nameof(ActiveConnectionIsOnline));
        }
    }

    private bool _isCrsOnline;
    public bool IsCrsOnline
    {
        get => _isCrsOnline;
        private set
        {
            if (_isCrsOnline == value) return;
            _isCrsOnline = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(CrsStatusText));
        }
    }

    private bool _connectionCheckCompleted;
    public bool ConnectionCheckCompleted
    {
        get => _connectionCheckCompleted;
        private set
        {
            if (_connectionCheckCompleted == value) return;
            _connectionCheckCompleted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(RemoteStatusText));
            OnPropertyChanged(nameof(NetworkStatusText));
            OnPropertyChanged(nameof(CrsStatusText));
            OnPropertyChanged(nameof(ActiveDatabaseStatusText));
            OnPropertyChanged(nameof(ActiveConnectionIsOnline));
        }
    }

    // ── Single active-database status ─────────────────────────────────────
    // The footer shows ONLY the database the system is currently using.
    // Mode comes from AppSettings:DatabaseMode (Online vs Network/LAN).
    private string _activeDatabaseMode = "ONLINE";
    public string ActiveDatabaseMode
    {
        get => _activeDatabaseMode;
        private set { _activeDatabaseMode = value; OnPropertyChanged(); }
    }

    public bool ActiveConnectionIsOnline => IsActiveModeNetwork ? IsNetworkOnline : IsOnline;

    public bool IsActiveModeNetwork
    {
        get
        {
            string mode = App.Config?["AppSettings:DatabaseMode"] ?? ActiveDatabaseMode ?? "Online";
            return mode.Equals("Network", System.StringComparison.OrdinalIgnoreCase)
                || mode.Equals("LAN", System.StringComparison.OrdinalIgnoreCase)
                || mode.Equals("LanConnection", System.StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string ResolveActiveModeLabel()
    {
        string mode = App.Config?["AppSettings:DatabaseMode"] ?? "Online";
        if (mode.Equals("Network", System.StringComparison.OrdinalIgnoreCase)
            || mode.Equals("LAN", System.StringComparison.OrdinalIgnoreCase)
            || mode.Equals("LanConnection", System.StringComparison.OrdinalIgnoreCase))
            return "NETWORK";
        // Online connection is the active database.
        return "ONLINE";
    }

    // Kept for backward-compat (background checks still run for sync/reports),
    // but the footer no longer displays these individually.
    public string SqliteStatusText => "DISABLED";
    public string RemoteStatusText => GetConnectionStatusText(IsOnline);
    public string NetworkStatusText => GetConnectionStatusText(IsNetworkOnline);
    public string CrsStatusText => GetConnectionStatusText(IsCrsOnline);
    public string ActiveDatabaseStatusText => GetConnectionStatusText(ActiveConnectionIsOnline);

    private string GetConnectionStatusText(bool isConnected) =>
        !ConnectionCheckCompleted ? "CHECKING" : isConnected ? "ONLINE" : "OFFLINE";

    private bool _isSyncing = false;
    public bool IsSyncing
    {
        get => _isSyncing;
        set { _isSyncing = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); }
    }

    private string _syncStatus = "Waiting...";
    public string SyncStatus
    {
        get => _syncStatus;
        set { _syncStatus = value; OnPropertyChanged(); }
    }

    private int _syncErrorCount = 0;
    public int SyncErrorCount
    {
        get => _syncErrorCount;
        set { _syncErrorCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSyncErrors)); }
    }

    public bool HasSyncErrors => SyncErrorCount > 0;

    private List<string> _lastSyncErrors = new();
    public List<string> LastSyncErrors
    {
        get => _lastSyncErrors;
        set { _lastSyncErrors = value; OnPropertyChanged(); }
    }

    // ── dashboard KPI strip (cheap COUNT/SUM scalars, display-only) ──────────
    private string _kpiBudgetText = "₱0.00";
    public string KpiBudgetText
    {
        get => _kpiBudgetText;
        set { _kpiBudgetText = value; OnPropertyChanged(); }
    }

    private string _kpiTransactionText = "0";
    public string KpiTransactionText
    {
        get => _kpiTransactionText;
        set { _kpiTransactionText = value; OnPropertyChanged(); }
    }

    private string _kpiUserText = "0";
    public string KpiUserText
    {
        get => _kpiUserText;
        set { _kpiUserText = value; OnPropertyChanged(); }
    }

    private string _kpiPendingText = "0";
    public string KpiPendingText
    {
        get => _kpiPendingText;
        set { _kpiPendingText = value; OnPropertyChanged(); }
    }

    private string _kpiUpdatedText = "Updated —";
    public string KpiUpdatedText
    {
        get => _kpiUpdatedText;
        set { _kpiUpdatedText = value; OnPropertyChanged(); }
    }

    // ── commands ──────────────────────────────────────────────────────────────
    public ICommand LogoutCommand        { get; }
    public ICommand OpenAppProfileCommand{ get; }
    public ICommand OpenSystemsProfileCommand { get; }
    public ICommand NavigateTileCommand  { get; }
    public ICommand ShowDashboardCommand { get; }
    public ICommand ToggleSidebarCommand { get; }
    public ICommand NotificationClickedCommand { get; }
    public ICommand OpenNotificationsCommand   { get; }
    public ICommand SyncNowCommand             { get; }
    public ICommand ViewSyncLogCommand         { get; }
    public ICommand MinimizeMainWindowCommand  { get; }
    public ICommand CloseMainWindowCommand     { get; }

    private readonly DatabaseHelper _dbHelper;
    private readonly IServiceProvider _serviceProvider;
    private readonly GoodGovernanceApp.Services.IConnectivityService _connectivityService;
    private readonly GoodGovernanceApp.Services.ISyncService _syncService;
    private readonly Action<bool> _connectionStatusHandler;
    private readonly Action<bool> _syncStatusHandler;
    private readonly Action<string> _syncProgressHandler;
    private readonly Action<List<string>> _syncErrorsHandler;
    private bool _disposed;

    // ── constructor ───────────────────────────────────────────────────────────
    public MainViewModel(Services.SessionService sessionService, DatabaseHelper dbHelper, IServiceProvider serviceProvider, GoodGovernanceApp.Services.IConnectivityService connectivityService, GoodGovernanceApp.Services.ISyncService syncService)
    {
        _sessionService = sessionService;
        _dbHelper = dbHelper;
        _serviceProvider = serviceProvider;
        _connectivityService = connectivityService;
        _syncService = syncService;

        // Live clock
        CurrentDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy  •  hh:mm tt");
        _clockTimer = new Timer(_ =>
        {
            DispatchToUi(() =>
                CurrentDate = DateTime.Now.ToString("dddd, MMMM dd, yyyy  •  hh:mm tt"));
        }, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));

        // Sync Events
        _connectionStatusHandler = isOnline =>
        {
            DispatchToUi(() => RefreshConnectionStatus(isOnline));
        };
        _syncStatusHandler = isSyncing =>
        {
            DispatchToUi(() => IsSyncing = isSyncing);
        };
        _syncProgressHandler = msg =>
        {
            DispatchToUi(() => SyncStatus = msg);
        };
        _syncErrorsHandler = errors =>
        {
            DispatchToUi(() =>
            {
                SyncErrorCount = errors.Count;
                LastSyncErrors = errors;
            });
        };
        _connectivityService.OnConnectionStatusChanged += _connectionStatusHandler;
        _syncService.OnSyncStatusChanged += _syncStatusHandler;
        _syncService.OnSyncProgress += _syncProgressHandler;
        _syncService.OnSyncErrorsCollected += _syncErrorsHandler;

        // Seed the initial state from whatever ConnectivityService already knows
        RefreshConnectionStatus(_connectivityService.IsOnline);
        SyncStatus = "Ready — Online to Network sync";

        // Ask ConnectivityService to push its current status to all subscribers immediately
        _connectivityService.SyncCurrentStatus();

        // Commands
        // e-KARD pattern: app runs fully on Online/Remote; footer Sync button syncs
        // Online (Remote) <-> Network (LAN). Requires both reachable.
        SyncNowCommand = new RelayCommand(async _ => await _syncService.SyncNowAsync(), _ => IsOnline && IsNetworkOnline && !IsSyncing);
        ViewSyncLogCommand = new RelayCommand(_ =>
        {
            string logPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Logs", "sync_log.txt");
            if (System.IO.File.Exists(logPath))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(logPath) { UseShellExecute = true });
            else
                MessageBox.Show("No sync log file found yet. A log will be created after the first sync.", "Sync Log", MessageBoxButton.OK, MessageBoxImage.Information);
        });
        OpenAppProfileCommand = new RelayCommand(ExecuteOpenAppProfile);
        OpenSystemsProfileCommand = new RelayCommand(ExecuteOpenSystemsProfile);
        LogoutCommand         = new RelayCommand(ExecuteLogout);
        NavigateTileCommand   = new RelayCommand(ExecuteNavigateTile);
        ShowDashboardCommand  = new RelayCommand(_ =>
        {
            IsShowingDashboard   = true;
            CurrentSectionTitle  = "Home";
            CurrentView          = null!;
            UpdateSidebarSelection("Dashboard");
        });
        ToggleSidebarCommand  = new RelayCommand(
            _ => IsSidebarOpen = !IsSidebarOpen,
            _ => IsShowingView);
        MinimizeMainWindowCommand = new RelayCommand(_ => SetMainWindowState(WindowState.Minimized));
        CloseMainWindowCommand = new RelayCommand(_ =>
        {
            Application.Current?.Windows.OfType<Views.MainWindow>().FirstOrDefault()?.Close();
        });
        NotificationClickedCommand  = new RelayCommand(ExecuteNotificationClicked);
        OpenNotificationsCommand    = new RelayCommand(_ => IsNotificationPopupOpen = true);

        _ = InitializeShellAsync();

        // ── Build navigation items with Metro tile colors ──────────────────
        var allItems = new List<NavigationItem>
        {
            new() { Name="Dashboard",        Icon="ViewDashboard",    ViewToken="Dashboard",      TileColor="#171717", Group="Main",       Description="System overview." },
            new() { Name="Analytics",        Icon="ChartBar",         ViewToken="Analytics",      TileColor="#B38B00", Group="Main",       Description="Budget and financial insights." },
            new() { Name="My Profile",       Icon="AccountEdit",      ViewToken="Profile",        TileColor="#2D2D2D", Group="Main",       Description="Signed-in account." },
            new() { Name="Users",            Icon="AccountGroup",     ViewToken="Users",          TileColor="#D90000", Group="Management", Description="Manage system accounts." },
            new() { Name="Parameters",       Icon="CogBox",           ViewToken="Parameters",     TileColor="#B38B00", Group="System",     Description="System variables." },
            new() { Name="Transactions",     Icon="Finance",          ViewToken="Transactions",   TileColor="#171717", Group="Finance",    Description="Financial records." },
            new() { Name="Consolidated",     Icon="TableMultiple",    ViewToken="ConsolidatedTransactions", TileColor="#D90000", Group="Finance", Description="All transactions." },
            new() { Name="Budget Allocation",Icon="ScaleBalance",     ViewToken="BudgetAllocation",TileColor="#2D2D2D",Group="Finance",   Description="Manage allocations." },
            new() { Name="CRS Beneficiaries",Icon="AccountMultiple",  ViewToken="CrsBeneficiary", TileColor="#B38B00", Group="Management", Description="Registry & residents." },
            new() { Name="Reports",          Icon="FileChart",        ViewToken="Reports",        TileColor="#D90000", Group="Reports",    Description="Analytics & data." },

            new() { Name="File Center",      Icon="CloudUpload",      ViewToken="FileUpload",     TileColor="#171717", Group="System",     Description="Documents & files." },
            new() { Name="Evaluation Center",Icon="FileCertificate",  ViewToken="Evaluation",     TileColor="#B38B00", Group="Reports",    Description="Performance metrics." },
            new() { Name="Audit Log",        Icon="FormatListBulleted",ViewToken="AuditLog",      TileColor="#2D2D2D", Group="System",     Description="Activity trail." },
            new() { Name="Settings & Backups",Icon="DatabaseSettings",ViewToken="Settings",       TileColor="#D90000", Group="System",     Description="System configuration." },
        };

        var role = _sessionService.CurrentUser?.Role;
        IEnumerable<NavigationItem> filtered;

        if (string.Equals(role, "super_admin", System.StringComparison.OrdinalIgnoreCase) || 
            string.Equals(role, "SuperAdmin", System.StringComparison.OrdinalIgnoreCase) || 
            role == "Admin")
        {
            filtered = allItems;
        }
        else if (role == "Evaluator")
        {
            filtered = allItems.Where(i => i.ViewToken is "Dashboard" or "Analytics" or "Profile" or "Evaluation");
        }
        else // Standard User
        {
            filtered = allItems.Where(i => i.ViewToken is "Dashboard" or "Analytics" or "Profile" or "Transactions" or "ConsolidatedTransactions" or "FileUpload");
        }

        // One sequence drives both navigation surfaces. Settings and Profile
        // are deliberately the final two modules; Logout is rendered after
        // them by both the dashboard and sidebar.
        var visibleItems = filtered
            .OrderBy(item => item.ViewToken switch
            {
                "Settings" => 1,
                "Profile"  => 2,
                _          => 0
            })
            .ToList();
        NavigationItems = new ObservableCollection<NavigationItem>(visibleItems);

        SidebarPrimaryItems = visibleItems
            .Where(item => item.ViewToken is not "Settings"
                           and not "Profile"
                           and not "ConsolidatedTransactions"
                           and not "BudgetAllocation")
            .ToList();
        SidebarTrailingItems = visibleItems
            .Where(item => item.ViewToken is "Settings" or "Profile")
            .ToList();

        // Split the role-filtered modules into the three bands used by the
        // Metro dashboard. The Dashboard entry remains available in the sidebar
        // as the route back to this navigation home; Analytics is a normal tile.
        var dashboardItems = visibleItems
            .Where(item => item.ViewToken != "Dashboard")
            .ToList();
        DashboardPrimaryItems = dashboardItems.Take(4).ToList();
        DashboardMiddleItems = dashboardItems.Skip(4).Take(3).ToList();
        DashboardBottomItems = dashboardItems.Skip(7).ToList();

        // Start on the dashboard tile grid
        IsShowingDashboard  = true;
        CurrentSectionTitle = "Home";
        UpdateSidebarSelection("Dashboard");
    }

    private async Task InitializeShellAsync()
    {
        try
        {
            // Stagger shell reads so login doesn't fire 5 connections at once
            // (startup storm fix). Images first, then profile + notifications with
            // a short gap. Same data, just spread out.
            await Task.WhenAll(LoadSystemPhotoAsync(), LoadCopyrightPhotoAsync());
            await Task.Delay(500);
            await Task.WhenAll(LoadGovProfileAsync(), LoadProfilePhotoAsync());
            await Task.Delay(500);
            await LoadNotificationsAsync();
            await Task.Delay(1000);
            await LoadKpisAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] Shell initialization skipped: {ex}");
        }
    }

    // ── dashboard KPI scalars — four single-row queries, no table materialization.
    // Runs once per shell init (staggered above); failures keep the "0" defaults.
    private async System.Threading.Tasks.Task LoadKpisAsync()
    {
        try
        {
            var usersObj = await _dbHelper.ExecuteScalarAsync("SELECT COUNT(*) FROM users;");
            DispatchToUi(() => KpiUserText = FormatCount(usersObj));

            var txObj = await _dbHelper.ExecuteScalarAsync("SELECT COUNT(*) FROM tbl_transaction;");
            DispatchToUi(() => KpiTransactionText = FormatCount(txObj));

            var pendingObj = await _dbHelper.ExecuteScalarAsync("SELECT COUNT(*) FROM validate_users WHERE status = 'pending';");
            DispatchToUi(() => KpiPendingText = FormatCount(pendingObj));

            var budgetObj = await _dbHelper.ExecuteScalarAsync("SELECT COALESCE(SUM(total_budget), 0) FROM master_budget;");
            DispatchToUi(() =>
            {
                if (budgetObj != null && decimal.TryParse(budgetObj.ToString(), out var total))
                    KpiBudgetText = "₱" + total.ToString("N2");
                KpiUpdatedText = "Updated " + DateTime.Now.ToString("h:mm tt");
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] KPI load skipped: {ex.Message}");
        }
    }

    private static string FormatCount(object? value)
    {
        if (value != null && long.TryParse(value.ToString(), out var n))
            return n.ToString("N0");
        return "0";
    }

    private static void DispatchToUi(Action action)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.HasShutdownStarted)
            return;

        if (dispatcher.CheckAccess())
            action();
        else
            _ = dispatcher.BeginInvoke(action);
    }

    private void RefreshConnectionStatus(bool remoteOnline)
    {
        IsOnline = remoteOnline;
        IsNetworkOnline = _connectivityService.IsNetworkOnline;
        IsCrsOnline = _connectivityService.IsCrsOnline;
        ConnectionCheckCompleted = _connectivityService.HasChecked;
        ActiveDatabaseMode = ResolveActiveModeLabel();
        OnPropertyChanged(nameof(ActiveDatabaseStatusText));
        OnPropertyChanged(nameof(ActiveConnectionIsOnline));
        OnPropertyChanged(nameof(IsActiveModeNetwork));
        CommandManager.InvalidateRequerySuggested();
    }

    // ── custom chrome (borderless window) ─────────────────────────────────
    private static void SetMainWindowState(WindowState state)
    {
        var win = Application.Current?.Windows.OfType<Views.MainWindow>().FirstOrDefault();
        if (win != null) win.WindowState = state;
    }

    // ── tile navigation ───────────────────────────────────────────────────────
    private void ExecuteNavigateTile(object? parameter)
    {
        if (parameter is not string viewToken) return;

        if (viewToken.Equals("Dashboard", StringComparison.OrdinalIgnoreCase))
        {
            IsShowingDashboard = true;
            CurrentSectionTitle = "Home";
            CurrentView = null!;
            UpdateSidebarSelection("Dashboard");
            return;
        }

        if (viewToken == "BudgetAllocation")
        {
            var dialog = new Views.BudgetYearSelectionWindow { DataContext = _serviceProvider.GetRequiredService<BudgetYearSelectionViewModel>() };
            var result = GoodGovernanceApp.Utilities.ModalHelper.Show(dialog);
            if (result == true && dialog.DataContext is BudgetYearSelectionViewModel vm && vm.SelectedMasterBudget != null)
            {
                NavigateTo("BudgetAllocation", vm.SelectedMasterBudget);
                CurrentSectionTitle = $"Budget Allocation - {vm.SelectedMasterBudget.FiscalYear}";
                IsShowingDashboard = false;
            }
            return;
        }

        if (viewToken == "ConsolidatedTransactions")
        {
            var searchDialog = new Views.ConsolidatedSearchWindow();
            searchDialog.Owner = Application.Current.Windows.OfType<Views.MainWindow>().FirstOrDefault();
            var searchResult = GoodGovernanceApp.Utilities.ModalHelper.Show(searchDialog);
            if (searchResult == true)
            {
                var searchParam = (Mode: searchDialog.SearchMode, Value: searchDialog.SearchValue);
                NavigateTo("ConsolidatedTransactions", searchParam);
                CurrentSectionTitle = searchDialog.SearchMode switch
                {
                    "BeneficiaryId" => $"Consolidated – ID: {searchDialog.SearchValue}",
                    "FullName"      => $"Consolidated – Name: {searchDialog.SearchValue}",
                    "ProjectCode"   => $"Consolidated – Project: {searchDialog.SearchValue}",
                    "OfficeCode"    => $"Consolidated – Office: {searchDialog.SearchValue}",
                    "Barangay"      => $"Consolidated – Brgy: {searchDialog.SearchValue}",
                    "HouseholdNo"   => $"Consolidated – Household: {searchDialog.SearchValue}",
                    _               => "Consolidated Transactions"
                };
                IsShowingDashboard = false;
            }
            return;
        }

        NavigateTo(viewToken);
        CurrentSectionTitle = NavigationItems.FirstOrDefault(i => i.ViewToken == viewToken)?.Name ?? viewToken;
        IsShowingDashboard  = false;
    }

    private void ExecuteNotificationClicked(object? parameter)
    {
        if (parameter is string viewToken)
        {
            ExecuteNavigateTile(viewToken);
            HasNewNotifications = false;
            IsNotificationPopupOpen = false;
        }
    }

    // ── public navigation (called by other ViewModels too) ────────────────────
    public void NavigateTo(string? viewToken, object? parameter = null)
    {
        UpdateSidebarSelection(viewToken);
        switch (viewToken)
        {
            case "Dashboard":
            case "Home":
                IsShowingDashboard  = true;
                CurrentSectionTitle = "Home";
                CurrentView         = null!;
                break;
            case "Analytics":
                CurrentView = _serviceProvider.GetRequiredService<DashboardViewModel>();
                break;
            case "Profile":
                CurrentView = _serviceProvider.GetRequiredService<ProfileViewModel>();
                break;
            case "Users":
                CurrentView = _serviceProvider.GetRequiredService<UserManagementViewModel>();
                break;
            case "Parameters":
                CurrentView = _serviceProvider.GetRequiredService<ParametersViewModel>();
                break;
            case "Transactions":
                CurrentView = _serviceProvider.GetRequiredService<BudgetTransactionsViewModel>();
                break;
            case "ConsolidatedTransactions":
                var ctVm = _serviceProvider.GetRequiredService<ConsolidatedTransactionsPageViewModel>();
                if (parameter is (string mode, string value))
                {
                    ctVm.ApplyInitialSearch(mode, value);
                }
                CurrentView = ctVm;
                break;
            case "Reports":
                CurrentView = _serviceProvider.GetRequiredService<ReportsViewModel>();
                break;
            case "BudgetAllocation":
                var allocVm = _serviceProvider.GetRequiredService<BudgetAllocationViewModel>();
                if (parameter is Models.MasterBudget selectedBudget)
                {
                    allocVm.InitializeWithBudget(selectedBudget);
                }
                else if (parameter is string officeCode)
                {
                    allocVm.ActivateForOffice(officeCode);
                }
                CurrentView = allocVm;
                break;
            case "CrsBeneficiary":
                CurrentView = _serviceProvider.GetRequiredService<CrsBeneficiaryViewModel>();
                break;
            case "Settings":
                CurrentView = _serviceProvider.GetRequiredService<SettingsViewModel>();
                break;
            case "Departments":
                CurrentView = _serviceProvider.GetRequiredService<DepartmentManagementViewModel>();
                break;
            case "FileUpload":
                CurrentView = _serviceProvider.GetRequiredService<FileUploadViewModel>();
                break;
            case "Evaluation":
                CurrentView = _serviceProvider.GetRequiredService<EvaluationViewModel>();
                break;
            case "AuditLog":
                CurrentView = _serviceProvider.GetRequiredService<AuditLogViewModel>();
                break;
            default:
                CurrentView = new System.Windows.Controls.TextBlock
                {
                    Text = viewToken + " – coming soon",
                    FontSize = 24,
                    Foreground = System.Windows.Media.Brushes.Black,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment   = VerticalAlignment.Center
                };
                break;
        }
    }

    // ── sidebar highlight ─────────────────────────────────────────────────────
    // Marks the open view's nav item selected (IsSelected drives the highlight
    // in SidebarControl). "Home" maps to the Dashboard item. Unknown tokens
    // clear the highlight. Also mirrors into SelectedNavItem.
    private void UpdateSidebarSelection(string? viewToken)
    {
        string token = string.Equals(viewToken, "Home", StringComparison.OrdinalIgnoreCase)
            ? "Dashboard"
            : viewToken ?? string.Empty;

        NavigationItem? match = null;
        foreach (var item in NavigationItems)
        {
            bool selected = item.ViewToken.Equals(token, StringComparison.OrdinalIgnoreCase);
            item.IsSelected = selected;
            if (selected) match = item;
        }
        _selectedNavItem = match!;
        OnPropertyChanged(nameof(SelectedNavItem));
    }

    // ── profile photo ─────────────────────────────────────────────────────────
    private async System.Threading.Tasks.Task LoadProfilePhotoAsync()
    {
        try
        {
            string query = $"SELECT profile_photo FROM users WHERE Id = {_sessionService.CurrentUser?.Id};";
            var dataTable = await _dbHelper.ExecuteQueryAsync(query);

            if (dataTable.Rows.Count > 0)
            {
                var path = dataTable.Rows[0]["profile_photo"]?.ToString();
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.UriSource   = new Uri(path, UriKind.Absolute);
                    bitmap.EndInit();
                    ProfilePhotoSource = bitmap;
                }
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(
                $"Could not load profile photo: {ex.Message}", "Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async System.Threading.Tasks.Task LoadApplicationProfileAsync()
    {
        try
        {
            string query = "SELECT GoveName, Address FROM goveprofile LIMIT 1;";
            var dataTable = await _dbHelper.ExecuteQueryAsync(query);

            if (dataTable.Rows.Count > 0)
            {
                var goveName = dataTable.Rows[0]["GoveName"]?.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(goveName))
                    GovernanceName = goveName;
            }
        }
        catch { }
    }

    // ── system photo ──────────────────────────────────────────────────────────
    private async System.Threading.Tasks.Task LoadSystemPhotoAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                var img = ImageHelper.LoadLogoSafe("system_profile.png")
                       ?? ImageHelper.LoadLogoSafe("pack://application:,,,/GoodGovernanceApp;component/Assets/Images/system_profile.png");

                if (img != null)
                {
                    DispatchToUi(() => SystemPhotoSource = img);
                }
            });
        }
        catch { }
    }

    // ── copyright photo ──────────────────────────────────────────────────────
    private async System.Threading.Tasks.Task LoadCopyrightPhotoAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                var img = ImageHelper.LoadLogoSafe("copyright.png")
                       ?? ImageHelper.LoadLogoSafe("pack://application:,,,/GoodGovernanceApp;component/Assets/Images/copyright.png");

                if (img != null)
                {
                    DispatchToUi(() => CopyrightPhotoSource = img);
                }
            });
        }
        catch { }
    }

    private async Task LoadGovProfileAsync()
    {
        try
        {
            string logoAddressFromDb = string.Empty;
            string query = "SELECT GoveName, LogoAddress, Address FROM goveprofile LIMIT 1;";
            var dataTable = await _dbHelper.ExecuteQueryAsync(query);

            if (dataTable.Rows.Count > 0)
            {
                var row = dataTable.Rows[0];
                string govName = row["GoveName"]?.ToString() ?? "";
                string addr = row["Address"]?.ToString() ?? "";
                logoAddressFromDb = row["LogoAddress"]?.ToString() ?? "";
                if (!string.IsNullOrWhiteSpace(govName))
                    GovernanceName = govName;
            }

            await Task.Run(() =>
            {
                System.Windows.Media.ImageSource? img = null;

                if (!string.IsNullOrWhiteSpace(logoAddressFromDb))
                    img = ImageHelper.LoadLogoSafe(logoAddressFromDb);

                if (img == null)
                    img = ImageHelper.LoadLogoSafe("company_profile_logo.jpg")
                       ?? ImageHelper.LoadLogoSafe("logo.png")
                       ?? ImageHelper.LoadLogoSafe("pack://application:,,,/GoodGovernanceApp;component/Assets/Images/company_profile_logo.jpg");

                if (img != null)
                {
                    DispatchToUi(() => GovPhotoSource = img);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadGovProfileAsync Error: {ex.Message}");
        }
    }

    private async Task LoadNotificationsAsync()
    {
        try
        {
            var notifications = new List<AppNotification>();

            // Query TblTransaction (Budget Transactions)
            string budgetQuery = "SELECT id, amount, description, created_at FROM tbl_transaction ORDER BY created_at DESC LIMIT 5;";
            var budgetData = await _dbHelper.ExecuteQueryAsync(budgetQuery);
            foreach (System.Data.DataRow row in budgetData.Rows)
            {
                if (row["created_at"] is DateTime date)
                {
                    notifications.Add(new AppNotification
                    {
                        Title = "New Budget Transaction",
                        Message = $"₱{Convert.ToDecimal(row["amount"]):N2} - {row["description"]}",
                        Date = date,
                        ViewToken = "Transactions",
                        Icon = "Finance",
                        Color = "#EAB308"
                    });
                }
            }

            // Query ConsolidatedTransactions
            string consolidatedQuery = "SELECT id, amount, transaction_type, created_at FROM consolidated_transactions ORDER BY created_at DESC LIMIT 5;";
            var consolidatedData = await _dbHelper.ExecuteQueryAsync(consolidatedQuery);
            foreach (System.Data.DataRow row in consolidatedData.Rows)
            {
                if (row["created_at"] is DateTime date)
                {
                    notifications.Add(new AppNotification
                    {
                        Title = "Consolidated Record Added",
                        Message = $"₱{Convert.ToDecimal(row["amount"]):N2} - {row["transaction_type"]}",
                        Date = date,
                        ViewToken = "ConsolidatedTransactions",
                        Icon = "TableMultiple",
                        Color = "#5E35B1"
                    });
                }
            }

            var sortedNotifications = notifications.OrderByDescending(n => n.Date).Take(10).ToList();

            DispatchToUi(() =>
            {
                Notifications.Clear();
                foreach (var n in sortedNotifications)
                {
                    Notifications.Add(n);
                }
                
                if (Notifications.Any())
                {
                    HasNewNotifications = true;
                    // Automatically open the popup to notify the user
                    IsNotificationPopupOpen = true;
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] LoadNotificationsAsync Error: {ex.Message}");
        }
    }

    // ── logout ────────────────────────────────────────────────────────────────
    private void ExecuteLogout(object? parameter)
    {
        Dispose();
        _sessionService.ClearSession();

        var loginWindow = _serviceProvider.GetService(typeof(Views.LoginWindow)) as Views.LoginWindow;
        loginWindow!.Show();

        var window = parameter as System.Windows.Window
                     ?? Application.Current.Windows.OfType<Views.MainWindow>().FirstOrDefault();
        window?.Close();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _clockTimer.Dispose();
        _connectivityService.OnConnectionStatusChanged -= _connectionStatusHandler;
        _syncService.OnSyncStatusChanged -= _syncStatusHandler;
        _syncService.OnSyncProgress -= _syncProgressHandler;
        _syncService.OnSyncErrorsCollected -= _syncErrorsHandler;
        GC.SuppressFinalize(this);
    }

    // ── app profile window ────────────────────────────────────────────────────
    private void ExecuteOpenAppProfile(object? parameter)
    {
        var window = new Views.ApplicationProfileWindow
        {
            DataContext = _serviceProvider.GetRequiredService<GoodGovernanceApp.ViewModels.ApplicationProfileViewModel>()
        };
        GoodGovernanceApp.Utilities.ModalHelper.Show(window);
    }

    private void ExecuteOpenSystemsProfile(object? parameter)
    {
        var window = new Views.SystemsApplicationProfile { DataContext = _serviceProvider.GetRequiredService<SystemsApplicationProfileViewModel>() };
        GoodGovernanceApp.Utilities.ModalHelper.Show(window);
    }
}
