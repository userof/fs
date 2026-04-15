using Microsoft.Extensions.DependencyInjection;

namespace FileManager.Services;

/// <summary>
/// Central DI container for the application.
/// </summary>
public static class AppServices
{
    private static ServiceProvider? _provider;

    public static ServiceProvider Provider =>
        _provider ?? throw new System.InvalidOperationException("Services not configured. Call Configure() first.");

    public static void Configure()
    {
        var services = new ServiceCollection();

        // Services (singletons)
        services.AddSingleton<IFileSystemService, FileSystemService>();
        services.AddSingleton<IProfileService, ProfileService>();
        services.AddSingleton<IPriorityService, PriorityService>();

        // ViewModels (transient — new instance each time)
        services.AddTransient<ViewModels.FilePanelViewModel>();
        services.AddTransient<ViewModels.LevelViewModel>();
        services.AddTransient<ViewModels.MainWindowViewModel>();

        _provider = services.BuildServiceProvider();
    }

    public static T Get<T>() where T : notnull => Provider.GetRequiredService<T>();
}
