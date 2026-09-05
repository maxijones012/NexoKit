using System.Windows;
using System.Windows.Controls;
using KitHerramientas.Desktop.Controls;

namespace KitHerramientas.Desktop;

public partial class MainWindow
{
    static MainWindow()
    {
        EventManager.RegisterClassHandler(
            typeof(MainWindow),
            FrameworkElement.LoadedEvent,
            new RoutedEventHandler(AttachToolsTab));
    }

    private static void AttachToolsTab(object sender, RoutedEventArgs e)
    {
        if (sender is not MainWindow window) return;
        var tabs = FindMainTabControl(window);
        if (tabs is null) return;

        if (tabs.Items.OfType<TabItem>().Any(x =>
            (x.Header?.ToString() ?? "").Contains("Herramientas", StringComparison.OrdinalIgnoreCase)))
            return;

        var tools = new TabItem
        {
            Header = "🧰 Herramientas",
            Content = new ToolsHubControl()
        };

        var insertAt = Math.Min(1, tabs.Items.Count);
        tabs.Items.Insert(insertAt, tools);
        window.Title = "NexoKit — Desktop R10 · Herramientas integradas";
    }

    private static TabControl? FindMainTabControl(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is TabControl tab) return tab;
            if (child is DependencyObject dependency)
            {
                var found = FindMainTabControl(dependency);
                if (found is not null) return found;
            }
        }
        return null;
    }
}
