using System.Collections;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KitHerramientas.Desktop.Services;
using Microsoft.Win32;

namespace KitHerramientas.Desktop.Controls;

public sealed class ToolsHubControl : UserControl
{
    private readonly TextBox _metaTarget = new() { MinWidth = 260, Height = 34, Margin = new Thickness(0, 0, 8, 0) };
    private readonly PasswordBox _metaApiKey = new() { MinWidth = 260, Height = 34, Margin = new Thickness(0, 0, 8, 0) };
    private readonly CheckBox _metaBusiness = new() { Content = "Incluir Business / About / Transparencia", IsChecked = true, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _metaStatus = new() { Foreground = new SolidColorBrush(Color.FromRgb(170, 179, 194)), TextWrapping = TextWrapping.Wrap };
    private readonly TextBox _metaOutput = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontFamily = new FontFamily("Consolas"),
        Background = new SolidColorBrush(Color.FromRgb(13, 18, 26)),
        Foreground = new SolidColorBrush(Color.FromRgb(216, 225, 235)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(41, 50, 65)),
        Padding = new Thickness(12)
    };
    private MetaScanBundle? _lastMetaResult;

    private readonly TextBox _osintSearch = new() { MinWidth = 300, Height = 34, Margin = new Thickness(0, 0, 8, 0) };
    private readonly DataGrid _osintGrid = new()
    {
        AutoGenerateColumns = false,
        IsReadOnly = true,
        HeadersVisibility = DataGridHeadersVisibility.Column,
        SelectionMode = DataGridSelectionMode.Single,
        Background = new SolidColorBrush(Color.FromRgb(18, 24, 33)),
        Foreground = Brushes.White,
        RowBackground = new SolidColorBrush(Color.FromRgb(23, 28, 37)),
        AlternatingRowBackground = new SolidColorBrush(Color.FromRgb(20, 26, 35)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(41, 50, 65)),
        GridLinesVisibility = DataGridGridLinesVisibility.Horizontal
    };
    private readonly TextBlock _osintStatus = new() { Foreground = new SolidColorBrush(Color.FromRgb(170, 179, 194)), TextWrapping = TextWrapping.Wrap };
    private List<DiscoveredTool> _osintTools = new();

    public ToolsHubControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(14, 17, 23));
        Content = BuildUi();
        Loaded += (_, _) => ReloadOsintTools();
    }

    private UIElement BuildUi()
    {
        var root = new Grid { Margin = new Thickness(14) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(12) });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var heading = new StackPanel();
        heading.Children.Add(new TextBlock
        {
            Text = "HERRAMIENTAS INTEGRADAS",
            Foreground = Brushes.White,
            FontSize = 22,
            FontWeight = FontWeights.Bold
        });
        heading.Children.Add(new TextBlock
        {
            Text = "Usá los proyectos desde NexoKit. Actualizaciones sólo mantiene los repos al día.",
            Foreground = new SolidColorBrush(Color.FromRgb(154, 166, 182)),
            Margin = new Thickness(0, 4, 0, 0)
        });
        root.Children.Add(heading);

        var tabs = new TabControl
        {
            Background = new SolidColorBrush(Color.FromRgb(14, 17, 23)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(41, 50, 65))
        };
        Grid.SetRow(tabs, 2);
        tabs.Items.Add(new TabItem { Header = "🌐 Meta Scan", Content = BuildMetaScanTab() });
        tabs.Items.Add(new TabItem { Header = "🔎 OSINT Hub", Content = BuildOsintTab() });
        tabs.Items.Add(new TabItem { Header = "📡 Wi‑Fi Sensing", Content = BuildWifiTab() });
        root.Children.Add(tabs);
        return root;
    }

    private UIElement BuildMetaScanTab()
    {
        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var info = Card();
        var infoPanel = new StackPanel();
        infoPanel.Children.Add(new TextBlock { Text = "META SCAN · FACEBOOK OSINT", Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = 17 });
        infoPanel.Children.Add(new TextBlock
        {
            Text = "Integración del repo HackUnderway/meta_scan. Consulta datos públicos mediante Facebook Pages Scraper en RapidAPI. La API key queda sólo en memoria durante esta sesión.",
            Foreground = new SolidColorBrush(Color.FromRgb(170, 179, 194)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 5, 0, 0)
        });
        info.Child = infoPanel;
        grid.Children.Add(info);

        var controls = new Grid();
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var targetPanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        targetPanel.Children.Add(Label("Usuario o URL de Facebook"));
        targetPanel.Children.Add(_metaTarget);
        controls.Children.Add(targetPanel);

        var keyPanel = new StackPanel { Margin = new Thickness(0, 0, 8, 0) };
        keyPanel.Children.Add(Label("RapidAPI key"));
        keyPanel.Children.Add(_metaApiKey);
        Grid.SetColumn(keyPanel, 1);
        controls.Children.Add(keyPanel);

        var search = ActionButton("BUSCAR", 110);
        search.Margin = new Thickness(0, 20, 8, 0);
        search.Click += MetaScan_Click;
        Grid.SetColumn(search, 2);
        controls.Children.Add(search);

        var repo = ActionButton("REPO", 90, secondary: true);
        repo.Margin = new Thickness(0, 20, 0, 0);
        repo.Click += (_, _) => OpenUrl("https://github.com/HackUnderway/meta_scan");
        Grid.SetColumn(repo, 3);
        controls.Children.Add(repo);
        Grid.SetRow(controls, 2);
        grid.Children.Add(controls);

        var resultGrid = new Grid();
        resultGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        resultGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        resultGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        resultGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
        resultGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var statusRow = new Grid();
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        statusRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        statusRow.Children.Add(_metaStatus);
        _metaBusiness.Margin = new Thickness(10, 0, 0, 0);
        Grid.SetColumn(_metaBusiness, 1);
        statusRow.Children.Add(_metaBusiness);
        resultGrid.Children.Add(statusRow);

        Grid.SetRow(_metaOutput, 2);
        resultGrid.Children.Add(_metaOutput);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
        var copy = ActionButton("COPIAR", 100, secondary: true);
        copy.Click += (_, _) => { if (!string.IsNullOrWhiteSpace(_metaOutput.Text)) Clipboard.SetText(_metaOutput.Text); };
        var json = ActionButton("EXPORTAR JSON", 130, secondary: true);
        json.Margin = new Thickness(8, 0, 0, 0);
        json.Click += ExportMetaJson_Click;
        var txt = ActionButton("EXPORTAR TXT", 120, secondary: true);
        txt.Margin = new Thickness(8, 0, 0, 0);
        txt.Click += ExportMetaTxt_Click;
        actions.Children.Add(copy);
        actions.Children.Add(json);
        actions.Children.Add(txt);
        Grid.SetRow(actions, 4);
        resultGrid.Children.Add(actions);

        Grid.SetRow(resultGrid, 4);
        grid.Children.Add(resultGrid);
        return grid;
    }

    private UIElement BuildOsintTab()
    {
        var grid = new Grid { Margin = new Thickness(12) };
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var info = Card();
        info.Child = new TextBlock
        {
            Text = "OSINT HUB · catálogo navegable basado en las fuentes de Descubrir. No ejecuta herramientas externas automáticamente: las abre o las agrega a seguimiento cuando vos lo elegís.",
            Foreground = new SolidColorBrush(Color.FromRgb(200, 208, 220)),
            TextWrapping = TextWrapping.Wrap
        };
        grid.Children.Add(info);

        var controls = new Grid();
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        controls.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        _osintSearch.TextChanged += (_, _) => ApplyOsintFilter();
        controls.Children.Add(_osintSearch);

        var refresh = ActionButton("ACTUALIZAR CATÁLOGO", 170);
        refresh.Margin = new Thickness(8, 0, 0, 0);
        refresh.Click += RefreshCatalog_Click;
        Grid.SetColumn(refresh, 1);
        controls.Children.Add(refresh);

        var open = ActionButton("ABRIR", 90, secondary: true);
        open.Margin = new Thickness(8, 0, 0, 0);
        open.Click += OpenSelectedOsint_Click;
        Grid.SetColumn(open, 2);
        controls.Children.Add(open);

        var follow = ActionButton("+ SEGUIR", 100, secondary: true);
        follow.Margin = new Thickness(8, 0, 0, 0);
        follow.Click += FollowSelectedOsint_Click;
        Grid.SetColumn(follow, 3);
        controls.Children.Add(follow);
        Grid.SetRow(controls, 2);
        grid.Children.Add(controls);

        _osintGrid.Columns.Add(new DataGridTextColumn { Header = "Repositorio", Binding = new System.Windows.Data.Binding(nameof(DiscoveredTool.Repository)), Width = new DataGridLength(2, DataGridLengthUnitType.Star) });
        _osintGrid.Columns.Add(new DataGridTextColumn { Header = "Categoría", Binding = new System.Windows.Data.Binding(nameof(DiscoveredTool.Category)), Width = new DataGridLength(1.4, DataGridLengthUnitType.Star) });
        _osintGrid.Columns.Add(new DataGridTextColumn { Header = "Fuente", Binding = new System.Windows.Data.Binding(nameof(DiscoveredTool.Source)), Width = new DataGridLength(1.3, DataGridLengthUnitType.Star) });
        _osintGrid.Columns.Add(new DataGridTextColumn { Header = "Estado", Binding = new System.Windows.Data.Binding(nameof(DiscoveredTool.State)), Width = 90 });
        Grid.SetRow(_osintGrid, 4);
        grid.Children.Add(_osintGrid);

        Grid.SetRow(_osintStatus, 6);
        grid.Children.Add(_osintStatus);
        return grid;
    }

    private UIElement BuildWifiTab()
    {
        var grid = new Grid { Margin = new Thickness(18) };
        var card = Card();
        var panel = new StackPanel { MaxWidth = 760 };
        panel.Children.Add(new TextBlock { Text = "WI‑FI SENSING", Foreground = Brushes.White, FontSize = 24, FontWeight = FontWeights.Bold });
        panel.Children.Add(new TextBlock
        {
            Text = "Tu módulo de router + teléfono/PC sigue siendo parte de NexoKit. Desde acá lo abrís como herramienta, mientras el módulo avanzado CSI queda opcional para hardware compatible.",
            Foreground = new SolidColorBrush(Color.FromRgb(170, 179, 194)),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 8, 0, 18)
        });
        var open = ActionButton("ABRIR WI‑FI SENSING", 210);
        open.Click += (_, _) => SelectMainTab("Wi‑Fi Sensing");
        var source = ActionButton("VER RUVIEW", 130, secondary: true);
        source.Margin = new Thickness(8, 0, 0, 0);
        source.Click += (_, _) => OpenUrl("https://github.com/ruvnet/RuView");
        var row = new StackPanel { Orientation = Orientation.Horizontal };
        row.Children.Add(open);
        row.Children.Add(source);
        panel.Children.Add(row);
        card.Child = panel;
        grid.Children.Add(card);
        return grid;
    }

    private async void MetaScan_Click(object sender, RoutedEventArgs e)
    {
        _metaStatus.Text = "Consultando Meta Scan…";
        _metaOutput.Text = "";
        try
        {
            _lastMetaResult = await MetaScanService.ScanAsync(
                _metaTarget.Text,
                _metaApiKey.Password,
                _metaBusiness.IsChecked == true);
            _metaOutput.Text = MetaScanService.FormatSummary(_lastMetaResult);
            _metaStatus.Text = _lastMetaResult.Errors.Count == 0
                ? $"LISTO · @{_lastMetaResult.Username}"
                : $"LISTO CON {_lastMetaResult.Errors.Count} aviso(s) parcial(es)";
        }
        catch (Exception ex)
        {
            _metaStatus.Text = $"ERROR · {ex.Message}";
        }
    }

    private void ExportMetaJson_Click(object sender, RoutedEventArgs e)
    {
        if (_lastMetaResult is null) { _metaStatus.Text = "Primero hacé una consulta."; return; }
        var dialog = new SaveFileDialog { Filter = "JSON|*.json", FileName = $"meta_scan_{_lastMetaResult.Username}_{DateTime.Now:yyyyMMdd_HHmmss}.json" };
        if (dialog.ShowDialog() != true) return;
        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(_lastMetaResult, new JsonSerializerOptions { WriteIndented = true }));
        _metaStatus.Text = $"JSON guardado: {dialog.FileName}";
    }

    private void ExportMetaTxt_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_metaOutput.Text)) { _metaStatus.Text = "Primero hacé una consulta."; return; }
        var name = _lastMetaResult?.Username ?? "resultado";
        var dialog = new SaveFileDialog { Filter = "Texto|*.txt", FileName = $"meta_scan_{name}_{DateTime.Now:yyyyMMdd_HHmmss}.txt" };
        if (dialog.ShowDialog() != true) return;
        File.WriteAllText(dialog.FileName, _metaOutput.Text);
        _metaStatus.Text = $"TXT guardado: {dialog.FileName}";
    }

    private void ReloadOsintTools()
    {
        _osintTools = CatalogDiscoveryService.LoadTools();
        ApplyOsintFilter();
        _osintStatus.Text = _osintTools.Count == 0
            ? "Todavía no hay catálogo local. Tocá ACTUALIZAR CATÁLOGO."
            : $"{_osintTools.Count} herramientas OSINT cargadas.";
    }

    private void ApplyOsintFilter()
    {
        var q = _osintSearch.Text.Trim();
        IEnumerable<DiscoveredTool> items = _osintTools;
        if (!string.IsNullOrWhiteSpace(q))
            items = items.Where(x => x.Repository.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                     x.Category.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                                     x.Source.Contains(q, StringComparison.OrdinalIgnoreCase));
        _osintGrid.ItemsSource = items.Take(500).ToList();
    }

    private async void RefreshCatalog_Click(object sender, RoutedEventArgs e)
    {
        _osintStatus.Text = "Actualizando fuentes OSINT…";
        try
        {
            var sources = CatalogDiscoveryService.LoadSources();
            var tools = CatalogDiscoveryService.LoadTools();
            var added = 0;
            foreach (var source in sources.Where(x => x.Enabled))
            {
                try
                {
                    var snapshot = await CatalogDiscoveryService.FetchAsync(source.Repository);
                    added += CatalogDiscoveryService.MergeSnapshot(tools, source, snapshot);
                }
                catch (Exception ex)
                {
                    source.Status = $"ERROR · {ex.Message}";
                }
            }
            CatalogDiscoveryService.SaveSources(sources);
            CatalogDiscoveryService.SaveTools(tools);
            _osintTools = tools;
            ApplyOsintFilter();
            _osintStatus.Text = $"Catálogo actualizado · {_osintTools.Count} recursos · {added} nuevos.";
        }
        catch (Exception ex)
        {
            _osintStatus.Text = $"Error actualizando catálogo: {ex.Message}";
        }
    }

    private void OpenSelectedOsint_Click(object sender, RoutedEventArgs e)
    {
        if (_osintGrid.SelectedItem is not DiscoveredTool tool)
        {
            _osintStatus.Text = "Seleccioná una herramienta.";
            return;
        }
        OpenUrl(tool.Url);
    }

    private void FollowSelectedOsint_Click(object sender, RoutedEventArgs e)
    {
        if (_osintGrid.SelectedItem is not DiscoveredTool tool)
        {
            _osintStatus.Text = "Seleccioná una herramienta.";
            return;
        }

        var watches = RepositoryUpdateService.Load();
        if (watches.Any(x => x.Repository.Equals(tool.Repository, StringComparison.OrdinalIgnoreCase)))
        {
            _osintStatus.Text = $"{tool.Repository} ya está en Actualizaciones.";
            return;
        }

        var watch = new RepositoryWatch
        {
            Repository = tool.Repository,
            IntervalHours = 12,
            Enabled = true,
            AutoDownload = false,
            Status = "AGREGADO DESDE HERRAMIENTAS · SOLO AVISA"
        };
        watches.Add(watch);
        RepositoryUpdateService.Save(watches);

        if (Window.GetWindow(this) is MainWindow window && window.FindName("RepositoryGrid") is DataGrid grid && grid.ItemsSource is IList<RepositoryWatch> live)
        {
            if (!live.Any(x => x.Repository.Equals(tool.Repository, StringComparison.OrdinalIgnoreCase)))
                live.Add(watch);
            grid.Items.Refresh();
        }

        _osintStatus.Text = $"{tool.Repository} agregado a Actualizaciones en modo SOLO AVISA.";
    }

    private void SelectMainTab(string contains)
    {
        if (Window.GetWindow(this) is not MainWindow window) return;
        var tab = FindLogicalChild<TabControl>(window);
        if (tab is null) return;
        foreach (var item in tab.Items.OfType<TabItem>())
        {
            if ((item.Header?.ToString() ?? "").Contains(contains, StringComparison.OrdinalIgnoreCase))
            {
                tab.SelectedItem = item;
                return;
            }
        }
    }

    private static T? FindLogicalChild<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is T typed) return typed;
            if (child is DependencyObject dependency)
            {
                var found = FindLogicalChild<T>(dependency);
                if (found is not null) return found;
            }
        }
        return null;
    }

    private static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    private static Border Card() => new()
    {
        Background = new SolidColorBrush(Color.FromRgb(23, 28, 37)),
        BorderBrush = new SolidColorBrush(Color.FromRgb(41, 50, 65)),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(10),
        Padding = new Thickness(14)
    };

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        Foreground = new SolidColorBrush(Color.FromRgb(154, 166, 182)),
        Margin = new Thickness(0, 0, 0, 4)
    };

    private static Button ActionButton(string text, double width, bool secondary = false) => new()
    {
        Content = text,
        Width = width,
        Height = 34,
        Background = new SolidColorBrush(secondary ? Color.FromRgb(29, 38, 51) : Color.FromRgb(37, 48, 68)),
        Foreground = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.FromRgb(51, 65, 85))
    };
}
