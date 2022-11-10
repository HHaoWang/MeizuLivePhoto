using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Xaml;
using WinRT;

namespace MeizuLivePhotoViewer;

public sealed partial class MainWindow
{
    private WindowsSystemDispatcherQueueHelper _dispatcherQueueHelper;
    private DesktopAcrylicController _acrylicController;
    private SystemBackdropConfiguration _configurationSource;

    public MainWindow()
    {
        InitializeComponent();
        TrySetAcrylicBackdrop();
        AppTitleTextBlock.Text = "魅族动态照片查看器";
    }

    private void ExtendAcrylicIntoTitleBar()
    {
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
    }

    private bool TrySetAcrylicBackdrop()
    {
        if (!DesktopAcrylicController.IsSupported())
            return false; // Acrylic is not supported on this system

        _dispatcherQueueHelper = new WindowsSystemDispatcherQueueHelper();
        _dispatcherQueueHelper.EnsureWindowsSystemDispatcherQueueController();

        // Hooking up the policy object
        _configurationSource = new SystemBackdropConfiguration();
        Activated += Window_Activated;
        Closed += Window_Closed;
        ((FrameworkElement) Content).ActualThemeChanged += Window_ThemeChanged;

        // Initial configuration state.
        _configurationSource.IsInputActive = true;
        SetConfigurationSourceTheme();

        _acrylicController = new DesktopAcrylicController();

        // Enable the system backdrop.
        // Note: Be sure to have "using WinRT;" to support the Window.As<...>() call.
        _acrylicController.AddSystemBackdropTarget(this
            .As<ICompositionSupportsSystemBackdrop>());
        _acrylicController.SetSystemBackdropConfiguration(_configurationSource);
        return true; // succeeded
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        _configurationSource.IsInputActive = args.WindowActivationState != WindowActivationState.Deactivated;

        ExtendAcrylicIntoTitleBar();
    }

    private void Window_Closed(object sender, WindowEventArgs args)
    {
        // Make sure any Mica/Acrylic controller is disposed so it doesn't try to
        // use this closed window.
        if (_acrylicController != null)
        {
            _acrylicController.Dispose();
            _acrylicController = null;
        }

        Activated -= Window_Activated;
        _configurationSource = null;
    }

    private void Window_ThemeChanged(FrameworkElement sender, object args)
    {
        if (_configurationSource != null)
        {
            SetConfigurationSourceTheme();
        }
    }

    private void SetConfigurationSourceTheme()
    {
        _configurationSource.Theme = ((FrameworkElement) Content).ActualTheme switch
        {
            ElementTheme.Dark => SystemBackdropTheme.Dark,
            ElementTheme.Light => SystemBackdropTheme.Light,
            ElementTheme.Default => SystemBackdropTheme.Default,
            _ => _configurationSource.Theme
        };
    }
}