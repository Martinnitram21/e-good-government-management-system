using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.Utilities;
using GoodGovernanceApp.Views;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
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
    private bool _isRememberMe = false;
    private string _governanceName = "Good Governance Management System";
    private ImageSource? _logoSource;
    private string _address = string.Empty;
    private readonly GoodGovernanceApp.Services.SessionService _sessionService;
    private ImageSource? _systemPhotoSource;

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

    public bool IsRememberMe
    {
        get => _isRememberMe;
        set { _isRememberMe = value; OnPropertyChanged(); }
    }

    public string GovernanceName
    {
        get => _governanceName;
        set { _governanceName = value; OnPropertyChanged(); }
    }

    public ImageSource? LogoSource
    {
        get => _logoSource;
        set { _logoSource = value; OnPropertyChanged(); }
    }

    public string Address
    {
        get => _address;
        set { _address = value; OnPropertyChanged(); }
    }

    public ImageSource? SystemPhotoSource
    {
        get => _systemPhotoSource;
        set { _systemPhotoSource = value; OnPropertyChanged(); }
    }

    // ── Commands ─────────────────────────────────────────────────────────────
    public ICommand LoginCommand { get; }
    public ICommand OpenDbSettingsCommand { get; }
    public ICommand ForgotPasswordCommand { get; }
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
        OpenDbSettingsCommand = new RelayCommand(_ => ModalHelper.Show(new DatabaseSettingsWindow()));
        ForgotPasswordCommand = new RelayCommand(_ => MessageBox.Show(
            "Please contact your system administrator to reset your account password.",
            "Forgot Password",
            MessageBoxButton.OK,
            MessageBoxImage.Information));
        CheatCommand = new RelayCommand(_ => ExecuteCheat());

        LoadRememberedUsername();

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
            string inputLower = Username.Trim().ToLowerInvariant();
            User? user;

            try
            {
                using var scope = _serviceProvider.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                user = await FindUserAsync(context, inputLower);
            }
            catch (Exception ex) when (IsDatabaseConnectionFailure(ex))
            {
                // Use the local account cache only when MySQL cannot be reached.
                // The password and account status are still verified below.
                user = await FindCachedUserAsync(inputLower);

                if (user == null)
                {
                    ErrorMessage = "The database server is currently unavailable and this account is not available offline. Check Database Connection Settings and try again.";
                    return;
                }
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
            PersistRememberedUsername();
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
    private static async Task<User?> FindUserAsync(AppDbContext context, string normalizedLogin)
    {
        User? user = await context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Name.ToLower() == normalizedLogin);

        if (user == null)
        {
            user = await context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedLogin);
        }

        return user;
    }

    private static async Task<User?> FindCachedUserAsync(string normalizedLogin)
    {
        string databasePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "GoodGovernanceApp",
            "ggms.db");

        if (!File.Exists(databasePath))
            return null;

        var connectionBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            Mode = SqliteOpenMode.ReadOnly
        };

        await using var connection = new SqliteConnection(connectionBuilder.ConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, email, password, role, status
            FROM users
            WHERE lower(name) = $login OR lower(email) = $login
            LIMIT 1;";
        command.Parameters.AddWithValue("$login", normalizedLogin);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
            return null;

        return new User
        {
            Id = reader.GetInt64(0),
            Name = reader.GetString(1),
            Email = reader.GetString(2),
            Password = reader.GetString(3),
            Role = reader.IsDBNull(4) ? "user" : reader.GetString(4),
            Status = reader.IsDBNull(5) ? "active" : reader.GetString(5)
        };
    }

    private static bool IsDatabaseConnectionFailure(Exception exception)
    {
        for (Exception? current = exception; current != null; current = current.InnerException)
        {
            if (current is MySqlException)
                return true;

            if (current.Message.Contains("Unable to connect to any of the specified MySQL hosts", StringComparison.OrdinalIgnoreCase)
                || current.Message.Contains("maximum number of retries", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string RememberedUserFile => System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GoodGovernanceApp",
        "remembered-user.txt");

    private void LoadRememberedUsername()
    {
        try
        {
            if (!System.IO.File.Exists(RememberedUserFile)) return;

            Username = System.IO.File.ReadAllText(RememberedUserFile).Trim();
            IsRememberMe = !string.IsNullOrWhiteSpace(Username);
        }
        catch
        {
            // Remember-me is optional and must never prevent login.
        }
    }

    private void PersistRememberedUsername()
    {
        try
        {
            if (IsRememberMe)
            {
                string? directory = System.IO.Path.GetDirectoryName(RememberedUserFile);
                if (!string.IsNullOrEmpty(directory))
                    System.IO.Directory.CreateDirectory(directory);
                System.IO.File.WriteAllText(RememberedUserFile, Username.Trim());
            }
            else if (System.IO.File.Exists(RememberedUserFile))
            {
                System.IO.File.Delete(RememberedUserFile);
            }
        }
        catch
        {
            // Remember-me is optional and must never prevent login.
        }
    }

    private async Task LoadApplicationProfileAsync()
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

                if (!string.IsNullOrWhiteSpace(addr))
                    Address = addr;
            }

            await Task.Run(() =>
            {
                ImageSource? img = null;

                // 0. Bundled login seal wins when present.
                string? loginFile = ImageHelper.ResolveFilePath("login.png");
                if (!string.IsNullOrEmpty(loginFile))
                    img = ImageHelper.LoadLogoSafe(loginFile);
                if (img == null)
                    img = ImageHelper.LoadLogoSafe("pack://application:,,,/GoodGovernanceApp;component/Assets/Images/login.png");

                // 0b. Bundled Sulop seal fallback (if ever added).
                if (img == null)
                {
                    string? sealFile = ImageHelper.ResolveFilePath("sulop_seal.png");
                    if (!string.IsNullOrEmpty(sealFile))
                        img = ImageHelper.LoadLogoSafe(sealFile);
                    if (img == null)
                        img = ImageHelper.LoadLogoSafe("pack://application:,,,/GoodGovernanceApp;component/Assets/Images/sulop_seal.png");
                }

                // 1. Try DB path first if present
                if (img == null && !string.IsNullOrWhiteSpace(logoAddressFromDb))
                    img = ImageHelper.LoadLogoSafe(logoAddressFromDb);

                // 2. Try common filenames or any image in Assets/Images / AppData
                if (img == null)
                {
                    string? foundFile = ImageHelper.ResolveFilePath("company_profile_logo.jpg")
                        ?? ImageHelper.ResolveFilePath("logo.png")
                        ?? ImageHelper.ResolveFilePath("logo.jpg");

                    if (!string.IsNullOrEmpty(foundFile))
                        img = ImageHelper.LoadLogoSafe(foundFile);
                }

                // 3. Fallback to embedded pack URI or default icon
                if (img == null)
                {
                    img = ImageHelper.LoadLogoSafe("pack://application:,,,/GoodGovernanceApp;component/Assets/Images/company_profile_logo.jpg",
                                                    "pack://application:,,,/GoodGovernanceApp;component/Assets/Images/ggms.ico");
                }

                if (img != null)
                {
                    Application.Current?.Dispatcher.Invoke(() => LogoSource = img);
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[LoginViewModel] LoadApplicationProfileAsync Error: {ex.Message}");
        }
    }

    // ── System Photo ─────────────────────────────────────────────────────────
    private async Task LoadSystemPhotoAsync()
    {
        try
        {
            await Task.Run(() =>
            {
                ImageSource? img = null;

                string? foundFile = ImageHelper.ResolveFilePath("system_profile.png")
                    ?? ImageHelper.ResolveFilePath("system_profile.jpg")
                    ?? ImageHelper.ResolveFilePath("system.png");

                if (!string.IsNullOrEmpty(foundFile))
                    img = ImageHelper.LoadLogoSafe(foundFile);

                if (img == null)
                {
                    img = ImageHelper.LoadLogoSafe("pack://application:,,,/GoodGovernanceApp;component/Assets/Images/system_profile.png");
                }

                if (img != null)
                {
                    Application.Current?.Dispatcher.Invoke(() => SystemPhotoSource = img);
                }
            });
        }
        catch { }
    }
}
