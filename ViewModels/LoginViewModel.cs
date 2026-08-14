using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.Utilities;
using GoodGovernanceApp.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Linq;

namespace GoodGovernanceApp.ViewModels;

public class LoginViewModel : ViewModelBase
{
    // ── Fields ───────────────────────────────────────────────────────────────
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isLoggingIn = false;
    private bool _isPasswordVisible = false;
    private string _governanceName = "Good Governance Management System";
    private BitmapImage? _logoSource;
    private string _address = string.Empty;
    private readonly GoodGovernanceApp.Services.SessionService _sessionService;
    private BitmapImage? _systemPhotoSource;

    // ── Properties ───────────────────────────────────────────────────────────
    public string Username
    {
        get => _username;
        set { _username = value; OnPropertyChanged(); ClearError(); }
    }

    public string Password
    {
        get => _password;
        set { _password = value; OnPropertyChanged(); ClearError(); }
    }

    public string ErrorMessage
    {
        get => _errorMessage;
        set { _errorMessage = value; OnPropertyChanged(); }
    }

    public bool IsLoggingIn
    {
        get => _isLoggingIn;
        set { _isLoggingIn = value; OnPropertyChanged(); }
    }

    public bool IsPasswordVisible
    {
        get => _isPasswordVisible;
        set { _isPasswordVisible = value; OnPropertyChanged(); }
    }

    public string GovernanceName
    {
        get => _governanceName;
        set { _governanceName = value; OnPropertyChanged(); }
    }

    public BitmapImage? LogoSource
    {
        get => _logoSource;
        set { _logoSource = value; OnPropertyChanged(); }
    }

    public string Address
    {
        get => _address;
        set { _address = value; OnPropertyChanged(); }
    }

    public BitmapImage? SystemPhotoSource
    {
        get => _systemPhotoSource;
        set { _systemPhotoSource = value; OnPropertyChanged(); }
    }

    // ── Commands ─────────────────────────────────────────────────────────────
    public ICommand LoginCommand { get; }
    public ICommand OpenDbSettingsCommand { get; }
    public ICommand CheatCommand { get; }

    private readonly IServiceProvider _serviceProvider;
    private readonly DatabaseHelper _dbHelper;

    // ── Constructor ───────────────────────────────────────────────────────────
    public LoginViewModel(GoodGovernanceApp.Services.SessionService sessionService, IServiceProvider serviceProvider, DatabaseHelper dbHelper)
    {
        _sessionService = sessionService;
        _serviceProvider = serviceProvider;
        _dbHelper = dbHelper;

        LoginCommand = new RelayCommand(async p => await ExecuteLoginAsync(p), CanExecuteLogin);
        OpenDbSettingsCommand = new RelayCommand(_ => new DatabaseSettingsWindow().ShowDialog());
        CheatCommand = new RelayCommand(_ => ExecuteCheat());

        _ = LoadApplicationProfileAsync();
        _ = LoadSystemPhotoAsync();
    }

    // ── Cheat ─────────────────────────────────────────────────────────────────
    private void ExecuteCheat()
    {
        MessageBox.Show("Username: superadmin\nPassword: password", "Cheat Codes", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── CanExecute ────────────────────────────────────────────────────────────
    private bool CanExecuteLogin(object? _)
        => !string.IsNullOrWhiteSpace(Username)
        && !string.IsNullOrWhiteSpace(Password)
        && !IsLoggingIn;

    private void ClearError() => ErrorMessage = string.Empty;

    // ── Login Logic ───────────────────────────────────────────────────────────
    private async Task ExecuteLoginAsync(object? parameter)
    {
        IsLoggingIn = true;
        ErrorMessage = string.Empty;

        try
        {
            User? user = null;

            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            string inputLower = Username.Trim().ToLowerInvariant();

            // First try: match by `name`
            user = await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Name.ToLower() == inputLower);

            // Second try: match by `email`
            if (user == null)
            {
                user = await context.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.Email.ToLower() == inputLower);
            }

            if (user == null)
            {
                ErrorMessage = "❌ No account found with that username or email.";
                return;
            }

            bool passwordOk = PasswordHasher.VerifyPassword(Password, user.Password);

            if (!passwordOk)
            {
                ErrorMessage = "❌ Incorrect password. Please try again.";
                return;
            }

            if (user.Status?.Equals("inactive", StringComparison.OrdinalIgnoreCase) == true)
            {
                ErrorMessage = "⚠ Your account is inactive. Please contact an administrator.";
                return;
            }

            if (user.Status?.Equals("suspended", StringComparison.OrdinalIgnoreCase) == true)
            {
                ErrorMessage = "🚫 Your account has been suspended. Please contact an administrator.";
                return;
            }

            // ── OTP Check removed per user request ───────────────────────────

            // ── Login success ────────────────────────────────────────────────
            _sessionService.CurrentUser = user;

            var mainWindow = _serviceProvider.GetService(typeof(MainWindow)) as MainWindow;
            mainWindow!.Show();

            if (parameter is Window window)
                window.Close();
        }
        catch (Exception ex)
        {
            try 
            { 
                System.IO.File.AppendAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "db_error_log.txt"), 
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} - Login Error:\n{ex}\n\n"); 
            } catch { }

            string realError = ex.Message;
            Exception? inner = ex.InnerException;
            while (inner != null)
            {
                realError += $" -> {inner.Message}";
                inner = inner.InnerException;
            }
            ErrorMessage = $"❌ Login error: {realError}";
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    // ── Application Profile ───────────────────────────────────────────────────
    private async Task LoadApplicationProfileAsync()
    {
        try
        {
            string query = "SELECT GoveName, LogoAddress, Address FROM goveprofile LIMIT 1;";
            var dataTable = await _dbHelper.ExecuteQueryAsync(query);

            if (dataTable.Rows.Count > 0)
            {
                var row = dataTable.Rows[0];
                string govName = row["GoveName"]?.ToString() ?? "";
                string addr = row["Address"]?.ToString() ?? "";

                if (!string.IsNullOrWhiteSpace(govName))
                    GovernanceName = govName;

                if (!string.IsNullOrWhiteSpace(addr))
                    Address = addr;
            }

            // Load logo from Assets/Images instead
            await Task.Run(() =>
            {
                var imagesDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images");
                if (System.IO.Directory.Exists(imagesDir))
                {
                    var file = System.IO.Directory.EnumerateFiles(imagesDir, "*company*.*").FirstOrDefault();
                    if (file == null)
                    {
                        file = System.IO.Directory.EnumerateFiles(imagesDir, "*logo*.*").FirstOrDefault();
                    }
                    if (file == null) 
                    {
                        file = System.IO.Directory.EnumerateFiles(imagesDir, "*gov*.*").FirstOrDefault();
                    }

                    if (file != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var bi = new BitmapImage();
                            bi.BeginInit();
                            bi.CacheOption = BitmapCacheOption.OnLoad;
                            bi.UriSource = new Uri(file, UriKind.Absolute);
                            bi.EndInit();
                            LogoSource = bi;
                        });
                    }
                }
            });
        }
        catch (Exception ex) // ✅ CHANGE THIS TOO — never silently swallow errors
        {
            System.Diagnostics.Debug.WriteLine($"[MethodName] Error: {ex.Message}");
        }
    }
      
    

    // ── System Photo ─────────────────────────────────────────────────────────
    private async Task LoadSystemPhotoAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                var imagesDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Images");
                if (System.IO.Directory.Exists(imagesDir))
                {
                    var file = System.IO.Directory.EnumerateFiles(imagesDir, "*system_profile*.*").FirstOrDefault();
                    if (file == null) 
                    {
                        file = System.IO.Directory.EnumerateFiles(imagesDir, "*system*.*").FirstOrDefault();
                    }

                    if (file != null)
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.UriSource = new Uri(file, UriKind.Absolute);
                            bitmap.EndInit();
                            SystemPhotoSource = bitmap;
                        });
                    }
                }
            });
        }
        catch { }
    }
}
