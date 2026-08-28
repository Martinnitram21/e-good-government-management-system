using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Microsoft.Data.Sqlite;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace GoodGovernanceApp.ViewModels;

public class ApplicationProfileViewModel : ViewModelBase
{
    private readonly DatabaseHelper _dbHelper;
    private ApplicationProfileModel _profile = new();
    private BitmapImage? _logoPreview;

    public string GoveName
    {
        get => _profile.GoveName;
        set { _profile.GoveName = value; OnPropertyChanged(); }
    }

    public string Address
    {
        get => _profile.Address;
        set { _profile.Address = value; OnPropertyChanged(); }
    }

    public string LogoAddress
    {
        get => _profile.LogoAddress;
        set { _profile.LogoAddress = value; OnPropertyChanged(); }
    }

    public BitmapImage? LogoPreview
    {
        get => _logoPreview;
        set { _logoPreview = value; OnPropertyChanged(); }
    }

    public ICommand BrowseCommand { get; }
    public ICommand SaveCommand { get; }

    public ApplicationProfileViewModel(DatabaseHelper dbHelper)
    {
        _dbHelper = dbHelper;
        BrowseCommand = new RelayCommand(_ => ExecuteBrowse());
        SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync());

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Ensure table exists
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS goveprofile (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    GoveName NVARCHAR(255),
                    Address NVARCHAR(255),
                    LogoAddress NVARCHAR(500)
                );";

            await _dbHelper.ExecuteNonQueryAsync(createTableQuery);

            // Load existing profile
            string query = "SELECT GoveName, Address, LogoAddress FROM goveprofile LIMIT 1;";
            var dataTable = await _dbHelper.ExecuteQueryAsync(query);

            if (dataTable.Rows.Count > 0)
            {
                var row = dataTable.Rows[0];
                GoveName = row["GoveName"]?.ToString() ?? "";
                Address = row["Address"]?.ToString() ?? "";
                LogoAddress = row["LogoAddress"]?.ToString() ?? "";

                LoadLogoImage(LogoAddress);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading application profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExecuteBrowse()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Logo",
            Filter = ImageHelper.OpenImageFileDialogFilter
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string selectedPath = dialog.FileName;

                // ✅ Use AppData\Roaming instead of Program Files
                string appDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "GoodGovernanceApp",
                    "Logos");

                if (!Directory.Exists(appDataFolder))
                    Directory.CreateDirectory(appDataFolder);

                string fileName = Path.GetFileName(selectedPath);
                string newPath = Path.Combine(appDataFolder, fileName);

                // If file already exists, append guid to avoid overwrite
                if (File.Exists(newPath))
                {
                    string name = Path.GetFileNameWithoutExtension(fileName);
                    string ext = Path.GetExtension(fileName);
                    newPath = Path.Combine(appDataFolder, $"{name}_{Guid.NewGuid().ToString().Substring(0, 8)}{ext}");
                }

                File.Copy(selectedPath, newPath, overwrite: true);

                // ✅ Save absolute path — no more relative path issues
                LogoAddress = newPath;
                LoadLogoImage(newPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy image: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void LoadLogoImage(string path)
    {
        LogoPreview = ImageHelper.LoadBitmapSafe(path, "pack://application:,,,/GoodGovernanceApp;component/Assets/Images/company_profile_logo.jpg");
    }

    private async Task ExecuteSaveAsync()
    {
        try
        {
            // Check if record exists
            string checkQuery = "SELECT COUNT(*) FROM goveprofile;";
            var countObj = await _dbHelper.ExecuteScalarAsync(checkQuery);
            int count = Convert.ToInt32(countObj ?? 0);

            if (count > 0)
            {
                // UPDATE first record specifically
                string updateQuery = @"
                    UPDATE goveprofile 
                    SET GoveName = @goveName, Address = @address, LogoAddress = @logoAddress 
                    WHERE id IN (SELECT id FROM goveprofile ORDER BY id ASC LIMIT 1);";
                
                await _dbHelper.ExecuteNonQueryAsync(updateQuery,
                    new SqliteParameter("@goveName", GoveName),
                    new SqliteParameter("@address", Address),
                    new SqliteParameter("@logoAddress", LogoAddress));
            }
            else
            {
                // INSERT
                string insertQuery = @"
                    INSERT INTO goveprofile (GoveName, Address, LogoAddress)
                    VALUES (@goveName, @address, @logoAddress);";
                
                await _dbHelper.ExecuteNonQueryAsync(insertQuery,
                    new SqliteParameter("@goveName", GoveName),
                    new SqliteParameter("@address", Address),
                    new SqliteParameter("@logoAddress", LogoAddress));
            }

            MessageBox.Show("Application profile saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            
            // Re-throw Event or Close dialog (Usually dialog bounds can be closed, but we just save for now)
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving application profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
