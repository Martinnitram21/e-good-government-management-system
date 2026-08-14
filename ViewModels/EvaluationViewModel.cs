using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Microsoft.EntityFrameworkCore;
using GoodGovernanceApp.Data;
using GoodGovernanceApp.Models;
using Microsoft.Extensions.DependencyInjection;

namespace GoodGovernanceApp.ViewModels;

public class EvaluationViewModel : ViewModelBase
{
#pragma warning disable CS8618
    public EvaluationViewModel() { }
#pragma warning restore CS8618
    private readonly AppDbContext _context;
    private readonly Services.SessionService _sessionService;
    private UploadedFile? _selectedFile;
    private int _score;
    private string _comments = string.Empty;

    public ObservableCollection<UploadedFile> PendingFiles { get; } = new();
    public ObservableCollection<Evaluation> PastEvaluations { get; } = new();

    public UploadedFile? SelectedFile
    {
        get => _selectedFile;
        set 
        { 
            _selectedFile = value; 
            OnPropertyChanged(); 
            if (value != null)
            {
                Score = 0;
                Comments = string.Empty;
            }
        }
    }

    public int Score
    {
        get => _score;
        set { _score = value; OnPropertyChanged(); }
    }

    public string Comments
    {
        get => _comments;
        set { _comments = value; OnPropertyChanged(); }
    }

    public ICommand OpenFileCommand { get; }
    public ICommand SubmitEvaluationCommand { get; }

    public EvaluationViewModel(AppDbContext context, Services.SessionService sessionService)
    {
        _context = context;
        _sessionService = sessionService;

        OpenFileCommand = new RelayCommand(_ => ExecuteOpenFile(), _ => SelectedFile != null);
        SubmitEvaluationCommand = new RelayCommand(async _ => await SubmitEvaluationAsync(), _ => CanSubmit());

        _ = LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // Load files that have not been evaluated by anyone (or just general list)
            var evaluatedFileIds = await _context.Evaluations.Select(e => e.UploadedFileId).ToListAsync();
            
            var files = await _context.UploadedFiles
                .Include(f => f.Office)
                    .Include(f => f.Category)
                .Include(f => f.Parameter)
                .Where(f => !evaluatedFileIds.Contains(f.Id))
                .ToListAsync();

            PendingFiles.Clear();
            foreach (var f in files) PendingFiles.Add(f);

            var past = await _context.Evaluations
                .Include(e => e.UploadedFile)
                .ThenInclude(f => f.Category)
                .Include(e => e.Evaluator)
                .OrderByDescending(e => e.EvaluationDate)
                .Take(50)
                .ToListAsync();

            PastEvaluations.Clear();
            foreach (var e in past) PastEvaluations.Add(e);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading data: {ex.Message}");
        }
    }

    private void ExecuteOpenFile()
    {
        if (SelectedFile == null) return;

        try
        {
            if (File.Exists(SelectedFile.StoragePath))
            {
                Process.Start(new ProcessStartInfo(SelectedFile.StoragePath) { UseShellExecute = true });
            }
            else
            {
                MessageBox.Show("Physical file not found on disk.");
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open file: {ex.Message}");
        }
    }

    private bool CanSubmit()
    {
        return SelectedFile != null && Score >= 0 && Score <= 100;
    }

    private async Task SubmitEvaluationAsync()
    {
        try
        {
            var evaluator = _sessionService.CurrentUser;
            
            if (evaluator == null)
            {
                MessageBox.Show("No active user session found. Please log in again.");
                return;
            }

            var evaluation = new Evaluation
            {
                UploadedFileId = SelectedFile!.Id,
                EvaluatorId = evaluator.Id,
                Score = Score,
                Comments = Comments,
                EvaluationDate = DateTime.Now
            };

            _context.Evaluations.Add(evaluation);
            await _context.SaveChangesAsync();

            MessageBox.Show("Evaluation submitted successfully.");
            
            PendingFiles.Remove(SelectedFile);
            PastEvaluations.Insert(0, evaluation);
            
            SelectedFile = null;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Submission failed: {ex.Message}");
        }
    }
}
