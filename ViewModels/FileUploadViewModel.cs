using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using GoodGovernanceApp.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace GoodGovernanceApp.ViewModels;

public class FileUploadViewModel : ViewModelBase
{
#pragma warning disable CS8618
    public FileUploadViewModel() { }
#pragma warning restore CS8618
    private readonly AppDbContext _context;
    private readonly FileService _fileService;

    private Office? _selectedOffice;
    private Parameter? _selectedParameter;
    private string _selectedFilePath = string.Empty;
    private string _fileName = string.Empty;
    private ObservableCollection<Category> _categories = new();  // ← add this
    private Category _selectedCategory = new();                  // ← add this










    public ObservableCollection<Office> Offices { get; } = new();
    public ObservableCollection<Parameter> Parameters { get; } = new();
    public ObservableCollection<UploadedFile> UploadedFiles { get; } = new();

    // Keep Departments property for XAML compatibility
    public ObservableCollection<Office> Departments => Offices;



    // ↓ add these two below
    public ObservableCollection<Category> Categories
    {
        get => _categories;
        set { _categories = value; OnPropertyChanged(); }
    }

    public Category SelectedCategory
    {
        get => _selectedCategory;
        set { _selectedCategory = value ?? new Category(); OnPropertyChanged(); }
    }





    public Office? SelectedDepartment
    {
        get => _selectedOffice;
        set { _selectedOffice = value; OnPropertyChanged(); }
    }

    public Parameter? SelectedParameter
    {
        get => _selectedParameter;
        set { _selectedParameter = value; OnPropertyChanged(); }
    }

    public string SelectedFilePath
    {
        get => _selectedFilePath;
        set 
        { 
            _selectedFilePath = value; 
            OnPropertyChanged(); 
            FileName = Path.GetFileName(value);
        }
    }

    public string FileName
    {
        get => _fileName;
        set { _fileName = value; OnPropertyChanged(); }
    }

    public ICommand SelectFileCommand { get; }
    public ICommand UploadFileCommand { get; }
    public ICommand DeleteFileCommand { get; }

    public FileUploadViewModel(AppDbContext context, FileService fileService)
    {
        _context = context;
        _fileService = fileService;

        SelectFileCommand = new RelayCommand(_ => ExecuteSelectFile());
        UploadFileCommand = new RelayCommand(async _ => await ExecuteUploadFile(), _ => CanUpload());
        DeleteFileCommand = new RelayCommand(async row => await ExecuteDeleteFile(row));

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var offices = await _context.Offices.ToListAsync();
            foreach (var d in offices) Offices.Add(d);

            var @params = await _context.Parameters.ToListAsync();
            foreach (var p in @params) Parameters.Add(p);

            var categories = await _context.Categories.ToListAsync();  // ← add this
            foreach (var c in categories) _categories.Add(c);

            await LoadUploadedFilesAsync();
        }
        catch { }
    }

    private async Task LoadUploadedFilesAsync()
    {
        UploadedFiles.Clear();
        var files = await _context.UploadedFiles
            .Include(f => f.Office)
            .Include(f => f.Parameter)
             .Include(f => f.Category)
            .OrderByDescending(f => f.UploadDate)
            .ToListAsync();
            
        foreach (var f in files) UploadedFiles.Add(f);
    }

    private void ExecuteSelectFile()
    {
        var dialog = new OpenFileDialog();
        if (dialog.ShowDialog() == true)
        {
            SelectedFilePath = dialog.FileName;
        }
    }

    private bool CanUpload()
    {
        return !string.IsNullOrWhiteSpace(SelectedFilePath) && 
               SelectedDepartment != null && 
               SelectedCategory != null;
    }

    private async Task ExecuteUploadFile()
    {
        try
        {
            string storedPath = await _fileService.SaveFileAsync(SelectedFilePath);
            var fileInfo = new FileInfo(SelectedFilePath);

            var uploadedFile = new UploadedFile
            {
                FileName = FileName,
                StoragePath = storedPath,
                FileType = fileInfo.Extension,
                FileSize = fileInfo.Length,
                OfficeId = SelectedDepartment!.Id,
                ParameterId = SelectedCategory!.Id,
                UploadDate = DateTime.Now
            };

            _context.UploadedFiles.Add(uploadedFile);
            await _context.SaveChangesAsync();

            UploadedFiles.Insert(0, uploadedFile);
            
            SelectedFilePath = string.Empty;
            FileName = string.Empty;
            
            MessageBox.Show("File uploaded successfully.");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Upload failed: {ex.Message}");
        }
    }

    private async Task ExecuteDeleteFile(object? parameter)
    {
        if (parameter is UploadedFile file)
        {
            var result = MessageBox.Show($"Are you sure you want to delete {file.FileName}?", "Confirm Delete", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                _fileService.DeleteFile(file.StoragePath);
                _context.UploadedFiles.Remove(file);
                await _context.SaveChangesAsync();
                UploadedFiles.Remove(file);
            }
        }
    }





}
