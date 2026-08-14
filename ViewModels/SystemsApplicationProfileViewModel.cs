using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Microsoft.Data.Sqlite;
using GoodGovernanceApp.Data;
using Microsoft.Extensions.DependencyInjection;

namespace GoodGovernanceApp.ViewModels;

public class SystemsApplicationProfileViewModel : ViewModelBase
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

    public SystemsApplicationProfileViewModel(DatabaseHelper dbHelper)
    {
        _dbHelper = dbHelper;
        BrowseCommand = new RelayCommand(_ => ExecuteBrowse());
        SaveCommand = new RelayCommand(async _ => await ExecuteSaveAsync());

        // Fix: ensure async init runs on UI thread properly
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
                CREATE TABLE IF NOT EXISTS systemsprofile (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    PhotoAddress NVARCHAR(500)
                );";

            await _dbHelper.ExecuteNonQueryAsync(createTableQuery);

            // Load existing profile
            string query = "SELECT PhotoAddress FROM systemsprofile LIMIT 1;";
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
                    $"Profile load error:\n\n" +
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
            Title = "Select Logo",
            Filter = "Image Files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg"
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

                File.Copy(selectedPath, newPath);

                // ✅ Save absolute path — no more relative path issues
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
        // Resolve relative path if needed
        if (!Path.IsPathRooted(path))
            path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);

        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            try
            {
                var bi = new BitmapImage();
                bi.BeginInit();
                bi.CacheOption = BitmapCacheOption.OnLoad;
                bi.UriSource = new Uri(path, UriKind.Absolute);
                bi.EndInit();
                PhotoPreview = bi;
            }
            catch
            {
                PhotoPreview = null;
            }
        }
        else
        {
            PhotoPreview = null;
        }
    }

    private async Task ExecuteSaveAsync()
    {
        try
        {
            // Check if record exists
            string checkQuery = "SELECT COUNT(*) FROM systemsprofile;";
            var countObj = await _dbHelper.ExecuteScalarAsync(checkQuery);
            int count = Convert.ToInt32(countObj ?? 0);

            if (count > 0)
            {
                // UPDATE first record specifically
                string updateQuery = @"
                    UPDATE systemsprofile 
                    SET PhotoAddress = @photoAddress 
                    WHERE id IN (SELECT id FROM systemsprofile ORDER BY id ASC LIMIT 1);";
                
                await _dbHelper.ExecuteNonQueryAsync(updateQuery,
                    new SqliteParameter("@photoAddress", PhotoAddress));
            }
            else
            {
                // INSERT
                string insertQuery = @"
                    INSERT INTO systemsprofile (PhotoAddress)
                    VALUES (@photoAddress);";
                
                await _dbHelper.ExecuteNonQueryAsync(insertQuery,
                    new SqliteParameter("@photoAddress", PhotoAddress));
            }

            MessageBox.Show("Systems Application Profile saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error saving systems application profile: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
