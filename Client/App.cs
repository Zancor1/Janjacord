using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Themes.Fluent;
using Avalonia.Styling;
namespace JanjaCord.Client;
public sealed class App:Application
{
    public override void Initialize(){Styles.Add(new FluentTheme());RequestedThemeVariant=ThemeVariant.Dark;}
    public override void OnFrameworkInitializationCompleted(){if(ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop){desktop.ShutdownMode=ShutdownMode.OnExplicitShutdown;desktop.MainWindow=new Views.MainWindow();}base.OnFrameworkInitializationCompleted();}
}
