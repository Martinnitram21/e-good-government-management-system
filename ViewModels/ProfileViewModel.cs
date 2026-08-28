using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.Services;
using GoodGovernanceApp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media.Imaging;

namespace GoodGovernanceApp.ViewModels;

public class ProfileViewModel : ViewModelBase
{
    private readonly AppDbContext _context;
    private readonly SessionService _sessionService;
    
    private string _username = string.Empty;
    private string _email    = string.Empty;
    private string _oldPassword     = string.Empty;
    private string _newPassword     = string.Empty;
    private string _confirmPassword = string.Empty;
    private string _role = string.Empty;
    private string _officeName = "None";
    
    private string? _profilePhotoPath;
    private BitmapImage? _profilePhotoSource;

    public string Username
    {
        get => _username;
        set { _username = value; OnPropertyChanged(); }
    }

    public string Email
    {
        get => _email;
        set { _email = value; OnPropertyChanged(); }
    }

    public string OldPassword
    {
        get => _oldPassword;
        set { _oldPassword = value; OnPropertyChanged(); }
    }

    public string NewPassword
    {
        get => _newPassword;
        set { _newPassword = value; OnPropertyChanged(); }
    }

    public string ConfirmPassword
    {
        get => _confirmPassword;
        set { _confirmPassword = value; OnPropertyChanged(); }
    }

    public string Role
    {
        get => _role;
        set { _role = value; OnPropertyChanged(); }
    }

    public string OfficeName
    {
        get => _officeName;
        set { _officeName = value; OnPropertyChanged(); }
    }

    // Keep for XAML compatibility
    public string DepartmentName
    {
        get => _officeName;
        set { _officeName = value; OnPropertyChanged(); }
    }

    public BitmapImage? ProfilePhotoSource
    {
        get => _profilePhotoSource;
        set { _profilePhotoSource = value; OnPropertyChanged(); }
    }

    public ICommand SaveChangesCommand { get; }
    public ICommand UploadPhotoCommand { get; }

    public ProfileViewModel(AppDbContext context, SessionService sessionService)
    {
        _context = context;
        _sessionService = sessionService;

        SaveChangesCommand = new RelayCommand(async _ => await ExecuteSaveChanges(), _ => CanSave());
        UploadPhotoCommand = new RelayCommand(_ => ExecuteUploadPhoto());

        LoadUserData();
    }

    private void ExecuteUploadPhoto()
    {
        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Profile Photo",
            Filter = ImageHelper.OpenImageFileDialogFilter
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                // ✅ Use AppData\Roaming instead of Program Files (no admin rights needed)
                string photosDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "GoodGovernanceApp",
                    "ProfilePhotos");

                if (!Directory.Exists(photosDir))
                    Directory.CreateDirectory(photosDir);

                // Copy selected file to the directory
                string ext = Path.GetExtension(openFileDialog.FileName);
                string newFileName = $"profile_{_sessionService.CurrentUser?.Id}_{DateTime.Now.Ticks}{ext}";
                string destinationPath = Path.Combine(photosDir, newFileName);

                File.Copy(openFileDialog.FileName, destinationPath, overwrite: true);

                // ✅ Save absolute path so it works from any run location
                _profilePhotoPath = destinationPath;
                ProfilePhotoSource = ImageHelper.LoadBitmapSafe(destinationPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not upload photo: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void LoadUserData()
    {
        var currentUser = _sessionService.CurrentUser;
        if (currentUser != null)
        {
            Username  = currentUser.Name;
            Email     = currentUser.Email;
            Role      = currentUser.Role;
            OfficeName = currentUser.Office?.Name ?? "General / Unassigned";
            
            if (!string.IsNullOrEmpty(currentUser.ProfilePhoto))
            {
                _profilePhotoPath = currentUser.ProfilePhoto;
                ProfilePhotoSource = ImageHelper.LoadBitmapSafe(currentUser.ProfilePhoto);
            }
        }
    }

    private bool CanSave()
    {
        if (string.IsNullOrWhiteSpace(Username)) return false;

        // If a new password is entered, old password must also be provided
        if (!string.IsNullOrEmpty(NewPassword))
        {
            if (string.IsNullOrEmpty(OldPassword))    return false;
            if (NewPassword != ConfirmPassword)       return false;
        }

        return true;
    }

    private async Task ExecuteSaveChanges()
    {
        try
        {
            var sessionUser = _sessionService.CurrentUser;
            if (sessionUser == null) return;

            var user = await _context.Users.FindAsync(sessionUser.Id);
            if (user == null)
            {
                MessageBox.Show("User not found in database.");
                return;
            }

            user.Name  = Username;
            user.Email = Email;

            if (!string.IsNullOrEmpty(NewPassword))
            {
                // Verify old password before allowing the change
                if (!PasswordHasher.VerifyPassword(OldPassword, user.Password))
                {
                    MessageBox.Show(
                        "❌ Old password is incorrect. Password was not changed.",
                        "Verification Failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    return;
                }

                user.Password = PasswordHasher.HashPassword(NewPassword);
            }

            if (_profilePhotoPath != null)
            {
                user.ProfilePhoto = _profilePhotoPath;
            }

            _context.Users.Update(user);
            await _context.SaveChangesAsync();

            // Update session
            sessionUser.Name  = Username;
            sessionUser.Email = Email;
            if (_profilePhotoPath != null) sessionUser.ProfilePhoto = _profilePhotoPath;

            OldPassword     = string.Empty;
            NewPassword     = string.Empty;
            ConfirmPassword = string.Empty;
            
            MessageBox.Show("Profile updated successfully!");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Update failed: {ex.Message}");
        }
    }
}
