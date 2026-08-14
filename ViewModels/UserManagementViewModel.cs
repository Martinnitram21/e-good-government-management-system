using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using System.IO;
using System.Windows.Media.Imaging;
using System.Linq;
using System;

namespace GoodGovernanceApp.ViewModels;

public class UserManagementViewModel : ViewModelBase
{
    private readonly AppDbContext _context;

    // ── Users ─────────────────────────────────────────────────────────────────
    private ObservableCollection<User> _users = new();
    private User _selectedUser = new();
    private string _searchText = string.Empty;
    private ICollectionView _usersView;
    private bool _isEditing;
    private string _newPasswordInput = string.Empty;   // value from the PasswordBox
    private ObservableCollection<Office> _offices = new();
    private ObservableCollection<DepartmentRole> _allRoles = new();
    private ObservableCollection<DepartmentRole> _filteredRoles = new();
    private BitmapImage? _selectedUserProfilePhotoSource;

    // ── ValidateUsers ────────────────────────────────────────────────────────
    private ObservableCollection<ValidateUser> _validateUsers = new();
    private ValidateUser? _selectedValidateUser;
    private ICollectionView? _validateUsersView;
    private bool _filterAll = true;
    private bool _filterPending;
    private bool _filterAccepted;
    private bool _filterRejected;

    // ── Users Properties ─────────────────────────────────────────────────────
    public ObservableCollection<User> Users
    {
        get => _users;
        set { _users = value; OnPropertyChanged(); }
    }

    public ObservableCollection<Office> Offices
    {
        get => _offices;
        set { _offices = value; OnPropertyChanged(); }
    }

    public ObservableCollection<DepartmentRole> OfficeRoles
    {
        get => _filteredRoles;
        set { _filteredRoles = value; OnPropertyChanged(); }
    }

    public User SelectedUser
    {
        get => _selectedUser;
        set
        {
            _selectedUser = value ?? new User();
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedOfficeId));
            RefreshFilteredRoles();
            RefreshProfilePhoto();
        }
    }

    public BitmapImage? SelectedUserProfilePhotoSource
    {
        get => _selectedUserProfilePhotoSource;
        set { _selectedUserProfilePhotoSource = value; OnPropertyChanged(); }
    }

    public long? SelectedOfficeId
    {
        get => SelectedUser.OfficeId;
        set
        {
            SelectedUser.OfficeId = value;
            OnPropertyChanged();
            RefreshFilteredRoles();
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            _searchText = value;
            OnPropertyChanged();
            _usersView?.Refresh();
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            _isEditing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotEditing));
        }
    }

    public bool IsNotEditing => !IsEditing;

    /// <summary>Plain-text password typed in the PasswordBox. Reset after save/cancel.</summary>
    public string NewPasswordInput
    {
        get => _newPasswordInput;
        set { _newPasswordInput = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> Roles { get; } = new() { "SuperAdmin", "Admin", "Evaluator", "User" };

    // ── ValidateUsers Properties ─────────────────────────────────────────────
    public ObservableCollection<ValidateUser> ValidateUsers
    {
        get => _validateUsers;
        set { _validateUsers = value; OnPropertyChanged(); }
    }

    public ICollectionView? ValidateUsersView
    {
        get => _validateUsersView;
        set { _validateUsersView = value; OnPropertyChanged(); }
    }

    public ValidateUser? SelectedValidateUser
    {
        get => _selectedValidateUser;
        set { _selectedValidateUser = value; OnPropertyChanged(); }
    }

    public bool FilterAll
    {
        get => _filterAll;
        set { _filterAll = value; OnPropertyChanged(); _validateUsersView?.Refresh(); }
    }

    public bool FilterPending
    {
        get => _filterPending;
        set { _filterPending = value; OnPropertyChanged(); _validateUsersView?.Refresh(); }
    }

    public bool FilterAccepted
    {
        get => _filterAccepted;
        set { _filterAccepted = value; OnPropertyChanged(); _validateUsersView?.Refresh(); }
    }

    public bool FilterRejected
    {
        get => _filterRejected;
        set { _filterRejected = value; OnPropertyChanged(); _validateUsersView?.Refresh(); }
    }

    // ── Commands ──────────────────────────────────────────────────────────────
    public ICommand AddCommand { get; }
    public ICommand EditCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand RefreshValidateUsersCommand { get; }
    public ICommand UploadPhotoCommand { get; }

    // ── Constructor ───────────────────────────────────────────────────────────
    public UserManagementViewModel(AppDbContext context)
    {
        try
        {
            _context = context;
            LoadData();
        }
        catch (Exception ex)
        {


            _context = null!;
        }

        _usersView = CollectionViewSource.GetDefaultView(Users);
        _usersView.Filter = FilterUsers;

        AddCommand    = new RelayCommand(ExecuteAdd);
        EditCommand   = new RelayCommand(ExecuteEdit, CanExecuteEdit);
        SaveCommand   = new RelayCommand(ExecuteSave, CanExecuteSave);
        DeleteCommand = new RelayCommand(ExecuteDelete, CanExecuteDelete);
        CancelCommand = new RelayCommand(ExecuteCancel);
        RefreshValidateUsersCommand = new RelayCommand(_ => LoadValidateUsers());
        UploadPhotoCommand = new RelayCommand(_ => ExecuteUploadPhoto(), _ => IsEditing);
    }

    // ── Data Loading ──────────────────────────────────────────────────────────
    private void LoadData()
    {
        try
        {
            _context.Users
                .Include(u => u.Office)
                .Include(u => u.ValidationInfo)
                .Load();

            Users.Clear();
            foreach (var user in _context.Users.Local)
                Users.Add(user);

            _usersView = CollectionViewSource.GetDefaultView(Users);
            _usersView.Filter = FilterUsers;

            var offcs = _context.Offices.OrderBy(d => d.Name).ToList();
            Offices.Clear();
            foreach (var d in offcs) Offices.Add(d);

            var roles = _context.DepartmentRoles.OrderBy(r => r.Name).ToList();
            _allRoles.Clear();
            foreach (var r in roles) _allRoles.Add(r);

            RefreshFilteredRoles();
            LoadValidateUsers();
        }
         catch 
        {

        }
    }

    private void LoadValidateUsers()
    {
        try
        {
            var list = _context.ValidateUsers
                .Include(v => v.Category)
                .Include(v => v.AppUser)
                .OrderByDescending(v => v.CreatedAt)
                .ToList();

            ValidateUsers.Clear();
            foreach (var v in list)
                ValidateUsers.Add(v);

            var view = CollectionViewSource.GetDefaultView(ValidateUsers);
            view.Filter = FilterValidateUsers;
            ValidateUsersView = view;
        }
        catch { }
    }

    // ── Filters ───────────────────────────────────────────────────────────────
    private bool FilterUsers(object obj)
    {
        if (obj is User user)
        {
            if (string.IsNullOrWhiteSpace(SearchText)) return true;
            return user.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ||
                   user.Role.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private bool FilterValidateUsers(object obj)
    {
        if (obj is not ValidateUser v) return false;
        if (FilterAll)      return true;
        if (FilterPending)  return v.Status == "pending";
        if (FilterAccepted) return v.Status == "accepted";
        if (FilterRejected) return v.Status == "rejected";
        return true;
    }

    private void RefreshFilteredRoles()
    {
        _filteredRoles.Clear();
        if (SelectedUser?.OfficeId != null)
        {
            var filtered = _allRoles.Where(r => r.OfficeId == SelectedUser.OfficeId).ToList();
            foreach (var r in filtered) _filteredRoles.Add(r);
        }
        OnPropertyChanged(nameof(OfficeRoles));
    }

    private void RefreshProfilePhoto()
    {
        SelectedUserProfilePhotoSource = null;
        if (SelectedUser != null && !string.IsNullOrEmpty(SelectedUser.ProfilePhoto) && File.Exists(SelectedUser.ProfilePhoto))
        {
            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad; // allows file to be released
                bitmap.UriSource = new Uri(SelectedUser.ProfilePhoto, UriKind.Absolute);
                bitmap.EndInit();
                SelectedUserProfilePhotoSource = bitmap;
            }
            catch { /* Ignore load errors */ }
        }
    }

    private void ExecuteUploadPhoto()
    {
        if (!IsEditing) return;

        var openFileDialog = new OpenFileDialog
        {
            Title = "Select Profile Photo",
            Filter = "Image Files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*"
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string photosDir = Path.Combine(appDir, "ProfilePhotos");
                if (!Directory.Exists(photosDir)) Directory.CreateDirectory(photosDir);

                string ext = Path.GetExtension(openFileDialog.FileName);
                string newFileName = $"profile_{SelectedUser.Id}_{DateTime.Now.Ticks}{ext}";
                string destinationPath = Path.Combine(photosDir, newFileName);

                File.Copy(openFileDialog.FileName, destinationPath, overwrite: true);

                SelectedUser.ProfilePhoto = destinationPath;
                RefreshProfilePhoto();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not upload photo: {ex.Message}", "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    // ── User Commands ─────────────────────────────────────────────────────────
    private void ExecuteAdd(object? parameter)
    {
        SelectedUser = new User();
        IsEditing = true;
    }

    private bool CanExecuteEdit(object? parameter) => SelectedUser.Id != 0 && !IsEditing;

    private void ExecuteEdit(object? parameter) => IsEditing = true;

    private bool CanExecuteSave(object? parameter)
        => IsEditing && !string.IsNullOrWhiteSpace(SelectedUser.Name) && !string.IsNullOrWhiteSpace(SelectedUser.Role);

    private void ExecuteSave(object? parameter)
    {
        if (SelectedUser.Id == 0)
        {
            // ── New user ──────────────────────────────────────────────────
            string plainPw = string.IsNullOrWhiteSpace(NewPasswordInput)
                ? "defaultpass"
                : NewPasswordInput;

            SelectedUser.Password = PasswordHasher.HashPassword(plainPw);
            _context.Users.Add(SelectedUser);
        }
        else
        {
            // ── Existing user — only update password if something was typed ─
            if (!string.IsNullOrWhiteSpace(NewPasswordInput))
                SelectedUser.Password = PasswordHasher.HashPassword(NewPasswordInput);
        }

        _context.SaveChanges();
        NewPasswordInput = string.Empty;   // clear backing field (box is cleared by code-behind)
        IsEditing = false;
        LoadData();
    }

    private bool CanExecuteDelete(object? parameter) => SelectedUser.Id != 0 && !IsEditing;

    private void ExecuteDelete(object? parameter)
    {
        if (SelectedUser.Id != 0)
        {
            try
            {
                _context.Users.Remove(SelectedUser);
                _context.SaveChanges();
                SelectedUser = new User();
                LoadData();
            }
            catch (DbUpdateException)
            {
                // Revert the deletion state in the EF tracker so the app doesn't crash on next action
                foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged))
                    entry.State = EntityState.Unchanged;

                System.Windows.MessageBox.Show(
                    "This user cannot be deleted because they have associated records (e.g., budgets, requests) in the system.\n\n" +
                    "To prevent them from accessing the system, please Edit the user and change their Status to 'inactive'.",
                    "Deletion Blocked",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
            }
        }
    }

    private void ExecuteCancel(object? parameter)
    {
        NewPasswordInput = string.Empty;
        IsEditing = false;
        SelectedUser = new User();
        foreach (var entry in _context.ChangeTracker.Entries().Where(e => e.State != EntityState.Unchanged))
            entry.State = EntityState.Unchanged;
    }
}
