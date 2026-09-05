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

        if (!tabs.Items.OfType<TabItem>().Any(x =>
            (x.Header?.ToString() ?? "").Contains("Herramientas", StringComparison.OrdinalIgnoreCase)))
        {
            var tools = new TabItem
            {
                Header = "🧰 Herramientas",
                Content = new ToolsHubControl()
            };
            tabs.Items.Insert(Math.Min(1, tabs.Items.Count), tools);
        }

        foreach (var text in FindLogicalChildren<TextBlock>(window))
        {
            if (text.Text == "KIT HERRAMIENTAS")
                text.Text = "NEXOKIT";
            else if (text.Text.Contains("Desktop 0.9.0", StringComparison.OrdinalIgnoreCase))
                text.Text = "Desktop 1.0.0 · R10 · herramientas integradas · Wi‑Fi Sensing";
        }

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

    private static IEnumerable<T> FindLogicalChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is T typed) yield return typed;
            if (child is DependencyObject dependency)
                foreach (var nested in FindLogicalChildren<T>(dependency))
                    yield return nested;
        }
    }
}
