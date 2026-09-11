using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using GoodGovernanceApp.ViewModels;

namespace GoodGovernanceApp.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _loadingAutoHideTimer = new();
    private bool _isLoadingOverlayVisible;

    public MainWindow()
    {
        InitializeComponent();

        _loadingAutoHideTimer.Interval = TimeSpan.FromMilliseconds(900);
        _loadingAutoHideTimer.Tick += (_, _) =>
        {
            _loadingAutoHideTimer.Stop();
            HideGlobalLoadingOverlay();
        };

        AddHandler(
            ButtonBase.PreviewMouseLeftButtonDownEvent,
            new MouseButtonEventHandler(NavigationButtonPreviewMouseLeftButtonDown),
            true);

        Loaded += (_, _) =>
        {
            ShowGlobalLoadingOverlay("PREPARING YOUR WORKSPACE...");
            ScheduleGlobalLoadingOverlayHide(1100);
        };

        Closed += (_, _) => _loadingAutoHideTimer.Stop();
    }

    public MainWindow(MainViewModel viewModel) : this()
    {
        DataContext = viewModel;
    }

    private void NavigationButtonPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not MainViewModel viewModel ||
            FindVisualParent<Button>(e.OriginalSource as DependencyObject) is not { IsEnabled: true } button)
        {
            return;
        }

        bool isNavigation = ReferenceEquals(button.Command, viewModel.NavigateTileCommand) ||
                            ReferenceEquals(button.Command, viewModel.ShowDashboardCommand);
        if (!isNavigation)
        {
            return;
        }

        string section = button.CommandParameter as string ?? "Dashboard";
        if (section is "ConsolidatedTransactions" or "BudgetAllocation")
        {
            // These routes open modal selectors rather than replacing the main
            // content immediately, so they should not invoke the page loader.
            return;
        }

        ShowGlobalLoadingOverlay($"LOADING {FormatSectionName(section).ToUpperInvariant()}...");
        ScheduleGlobalLoadingOverlayHide(750);
    }

    private static T FindVisualParent<T>(DependencyObject current)
        where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static string FormatSectionName(string section)
    {
        if (string.IsNullOrWhiteSpace(section) || section == "Home")
        {
            return "Dashboard";
        }

        var formatted = new System.Text.StringBuilder(section.Length + 8);
        for (int index = 0; index < section.Length; index++)
        {
            char character = section[index];
            if (index > 0 && char.IsUpper(character) && !char.IsWhiteSpace(section[index - 1]))
            {
                formatted.Append(' ');
            }
            formatted.Append(character);
        }

        return formatted.ToString();
    }

    private void ShowGlobalLoadingOverlay(string message)
    {
        _loadingAutoHideTimer.Stop();
        GlobalLoadingMessageText.Text = message;
        GlobalLoadingOverlay.Visibility = Visibility.Visible;
        GlobalLoadingOverlay.IsHitTestVisible = true;

        if (!_isLoadingOverlayVisible)
        {
            _isLoadingOverlayVisible = true;
            GlobalLoadingOverlay.BeginAnimation(
                OpacityProperty,
                new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
            GlobalLoadingPanelScale.BeginAnimation(
                ScaleTransform.ScaleXProperty,
                new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(190)));
            GlobalLoadingPanelScale.BeginAnimation(
                ScaleTransform.ScaleYProperty,
                new DoubleAnimation(0.97, 1, TimeSpan.FromMilliseconds(190)));
            GlobalLoadingPanelTranslate.BeginAnimation(
                TranslateTransform.YProperty,
                new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(190)));
        }
    }

    private void ScheduleGlobalLoadingOverlayHide(int delayMilliseconds)
    {
        _loadingAutoHideTimer.Stop();
        _loadingAutoHideTimer.Interval = TimeSpan.FromMilliseconds(delayMilliseconds);
        _loadingAutoHideTimer.Start();
    }

    private void HideGlobalLoadingOverlay()
    {
        if (!_isLoadingOverlayVisible)
        {
            GlobalLoadingOverlay.Visibility = Visibility.Collapsed;
            return;
        }

        _isLoadingOverlayVisible = false;
        GlobalLoadingOverlay.IsHitTestVisible = false;

        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) =>
        {
            if (!_isLoadingOverlayVisible)
            {
                GlobalLoadingOverlay.Visibility = Visibility.Collapsed;
            }
        };

        GlobalLoadingOverlay.BeginAnimation(OpacityProperty, fade);
        GlobalLoadingPanelScale.BeginAnimation(
            ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.98, TimeSpan.FromMilliseconds(170)));
        GlobalLoadingPanelScale.BeginAnimation(
            ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.98, TimeSpan.FromMilliseconds(170)));
        GlobalLoadingPanelTranslate.BeginAnimation(
            TranslateTransform.YProperty,
            new DoubleAnimation(8, TimeSpan.FromMilliseconds(170)));
    }
}
