using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Microsoft.Data.Sqlite;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace GoodGovernanceApp.ViewModels;

public class CopyrightProfileViewModel : ViewModelBase
{
    private readonly DatabaseHelper _dbHelper;
    private string _photoAddress = string.Empty;
    private BitmapImage? _photoPreview;

    public string PhotoAddress
    {
        get => _photoAddress;
        set { _photoAddress = value; OnPropertyChanged(); }
    }

    public BitmapImage? PhotoPreview
    {
        get => _photoPreview;
        set { _photoPreview = value; OnPropertyChanged(); }
    }

    public ICommand BrowseCommand { get; }
    public ICommand SaveCommand { get; }

    public CopyrightProfileViewModel(DatabaseHelper dbHelper)
    {
        _dbHelper = dbHelper;
        BrowseCommand = new RelayCommand(_ => ExecuteBrowse());
        SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync());

        // Load existing copyright image on startup
        Application.Current.Dispatcher.InvokeAsync(async () =>
        {
            await InitializeAsync();
        });
    }

    private async Task InitializeAsync()
    {
        try
        {
            // Ensure table exists
            string createTableQuery = @"
                CREATE TABLE IF NOT EXISTS copyrightprofile (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    PhotoAddress NVARCHAR(500)
                );";

            await _dbHelper.ExecuteNonQueryAsync(createTableQuery);

            // Load existing profile
            string query = "SELECT PhotoAddress FROM copyrightprofile LIMIT 1;";
            var dataTable = await _dbHelper.ExecuteQueryAsync(query);

            if (dataTable.Rows.Count > 0)
            {
                var row = dataTable.Rows[0];
                PhotoAddress = row["PhotoAddress"]?.ToString() ?? "";

                LoadPhoto(PhotoAddress);
            }
        }
        catch (Exception ex)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                MessageBox.Show(
                    $"Copyright profile load error:\n\n" +
                    $"Type: {ex.GetType().Name}\n" +
                    $"Message: {ex.Message}\n\n" +
                    $"Inner: {ex.InnerException?.Message ?? "none"}\n\n" +
                    $"Stack: {ex.StackTrace}",
                    "Debug Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            });
        }
    }

    private void ExecuteBrowse()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Copyright Image",
            Filter = ImageHelper.OpenImageFileDialogFilter
        };

        if (dialog.ShowDialog() == true)
        {
            try
            {
                string selectedPath = dialog.FileName;

                // Use AppData\Roaming for storage
                string appDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "GoodGovernanceApp",
                    "Copyright");

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

                PhotoAddress = newPath;
                LoadPhoto(newPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy image: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void LoadPhoto(string path)
    {
        PhotoPreview = ImageHelper.LoadBitmapSafe(path, "pack://application:,,,/GoodGovernanceApp;component/Assets/Images/copyright.png");
    }

    private async Task ExecuteSaveAsync()
    {
        try
        {
            // Check if record exists
            string checkQuery = "SELECT COUNT(*) FROM copyrightprofile;";
            var countObj = await _dbHelper.ExecuteScalarAsync(checkQuery);
            int count = Convert.ToInt32(countObj ?? 0);

            if (count > 0)
            {
                // UPDATE first record
                string updateQuery = @"
                    UPDATE copyrightprofile 
                    SET PhotoAddress = @photoAddress 
                    ORDER BY id ASC LIMIT 1;";
                
                await _dbHelper.ExecuteNonQueryAsync(updateQuery,
                    new SqliteParameter("@photoAddress", PhotoAddress));
            }
            else
            {
                // INSERT
                string insertQuery = @"
                    INSERT INTO copyrightprofile (PhotoAddress)
                    VALUES (@photoAddress);";
                
                await _dbHelper.ExecuteNonQueryAsync(insertQuery,
                    new SqliteParameter("@photoAddress", PhotoAddress));
            }

            MessageBox.Show("Copyright profile saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving copyright profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
