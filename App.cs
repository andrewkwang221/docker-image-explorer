using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Windows.Shell;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace DockerImageExplorer;

public static class App
{
    [STAThread]
    public static void Main()
    {
        var english = System.Globalization.CultureInfo.GetCultureInfo("en-US");
        System.Globalization.CultureInfo.DefaultThreadCurrentCulture = english;
        System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = english;
        System.Threading.Thread.CurrentThread.CurrentCulture = english;
        System.Threading.Thread.CurrentThread.CurrentUICulture = english;
        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("en-US")));
        new Application().Run(new Viewer());
    }
}

public sealed partial class Viewer : Window
{
    readonly ListBox layers = new();
    readonly DataGrid files = new()
    {
        IsReadOnly = true,
        AutoGenerateColumns = false,
        SelectionMode = DataGridSelectionMode.Extended,
        SelectionUnit = DataGridSelectionUnit.FullRow,
        EnableRowVirtualization = true,
        CanUserAddRows = false,
        CanUserResizeRows = false,
    };
    readonly TextBox preview = new()
    {
        IsReadOnly = true,
        AcceptsReturn = true,
        TextWrapping = TextWrapping.NoWrap,
        VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
        HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
        FontFamily = new FontFamily("Consolas"),
        FontSize = 13,
    };
    readonly Image picture = new() { Stretch = Stretch.Uniform };
    readonly TabControl previewTabs = new();
    readonly TextBlock details = new() { TextWrapping = TextWrapping.Wrap, FontSize = 12 };
    readonly TextBox search = new()
    {
        Width = 220,
        ToolTip = "Search filename or path. Wildcards: *.png, filename.mp4, photo?.jpg",
    };
    readonly ComboBox type = new() { Width = 148, ItemsSource = ExplorerModel.FileTypes, SelectedIndex = 0 };
    readonly ComboBox action = new()
    {
        Width = 140,
        ItemsSource = new[] { "All changes", "Added", "Modified", "Deleted", "Unchanged" },
        SelectedIndex = 0,
    };
    readonly CheckBox merged = new() { Content = "Include unchanged files", IsChecked = true, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock status = new()
    {
        VerticalAlignment = VerticalAlignment.Center,
        TextTrimming = TextTrimming.CharacterEllipsis,
        FontSize = 12,
        Text = "Open a Docker save or OCI archive. Ctrl/Shift selects multiple items.",
    };
    readonly TextBlock itemCount = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0), FontSize = 12 };
    readonly TextBlock selectionCount = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 8, 0), FontSize = 12 };
    readonly TextBlock command = new() { TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), FontSize = 12 };
    readonly TextBox location = new() { IsReadOnly = true, VerticalContentAlignment = VerticalAlignment.Center };
    readonly Menu mainMenu = new();
    readonly MenuItem exportMenu = new() { Header = "_Export" };
    readonly Grid browser = new();
    readonly Border emptyState = new();
    readonly ProgressBar progress = new() { Height = 3, IsIndeterminate = true, Visibility = Visibility.Collapsed, BorderThickness = new Thickness(0) };
    readonly DataGridTextColumn pathColumn = new()
    {
        Header = "Path",
        Binding = new Binding("Path"),
        Width = 260,
        Visibility = Visibility.Collapsed,
    };
    ImageSession? session;
    Image titleIcon = null!;
    static BitmapFrame[]? iconFrames;
    bool busy;
    string currentPath = "";
    int previewVersion;

    public Viewer()
    {
        Icon = IconFrames().OrderByDescending(frame => frame.PixelWidth).First();
        Title = "DockerImageExplorer 1.0";
        Width = 1380;
        Height = 900;
        MinWidth = 900;
        MinHeight = 600;
        Theme.Apply(this);
        PreviewMouseLeftButtonDown += RouteDropdownClick;
        Deactivated += (_, _) => CloseDropdowns();
        progress.Foreground = Brush("Accent");
        progress.Background = Brush("Border");
        command.Foreground = Brush("Secondary");
        details.Foreground = Brush("Text");
        status.SetBinding(FrameworkElement.ToolTipProperty, new Binding(nameof(TextBlock.Text)) { Source = status });

        WindowStyle = WindowStyle.None;
        WindowChrome.SetWindowChrome(this, new WindowChrome
        {
            CaptionHeight = 32,
            ResizeBorderThickness = new Thickness(6),
            GlassFrameThickness = new Thickness(0),
            CornerRadius = new CornerRadius(0),
            UseAeroCaptionButtons = false,
        });
        var root = new DockPanel();
        Content = root;
        var titleBar = BuildTitleBar();
        DockPanel.SetDock(titleBar, Dock.Top);
        root.Children.Add(titleBar);
        BuildMenu();
        var openCommand = new RoutedCommand();
        CommandBindings.Add(new CommandBinding(openCommand, async (_, _) => await Work(Open)));
        InputBindings.Add(new KeyBinding(openCommand, Key.O, ModifierKeys.Control));

        var statusBar = new StatusBar { Background = Brush("Pane"), Foreground = Brush("Text") };
        var selectionItem = new StatusBarItem { Content = selectionCount };
        var countItem = new StatusBarItem { Content = itemCount };
        var messageItem = new StatusBarItem { Content = status };
        DockPanel.SetDock(selectionItem, Dock.Right);
        DockPanel.SetDock(countItem, Dock.Right);
        statusBar.Items.Add(selectionItem);
        statusBar.Items.Add(countItem);
        statusBar.Items.Add(messageItem);
        var statusHost = new Border
        {
            Background = Brush("Pane"),
            BorderBrush = Brush("Border"),
            BorderThickness = new Thickness(0, 1, 0, 0),
            Child = statusBar,
        };
        DockPanel.SetDock(statusHost, Dock.Bottom);
        root.Children.Add(statusHost);
        DockPanel.SetDock(progress, Dock.Bottom);
        root.Children.Add(progress);

        browser.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(280), MinWidth = 220 });
        browser.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
        browser.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MinWidth = 420 });
        root.Children.Add(browser);

        var left = new Grid { Background = Brush("Pane") };
        left.RowDefinitions.Add(new() { Height = GridLength.Auto });
        left.RowDefinitions.Add(new() { Height = new GridLength(1, GridUnitType.Star) });
        left.RowDefinitions.Add(new() { Height = GridLength.Auto });
        left.RowDefinitions.Add(new() { Height = new GridLength(140) });
        browser.Children.Add(left);
        var heading = new StackPanel { Margin = new Thickness(12, 12, 12, 4) };
        heading.Children.Add(new TextBlock { Text = "Layers", FontWeight = FontWeights.SemiBold });
        heading.Children.Add(new TextBlock { Text = "Newest first", Foreground = Brush("Secondary"), FontSize = 12 });
        left.Children.Add(heading);
        ScrollViewer.SetVerticalScrollBarVisibility(layers, ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(layers, ScrollBarVisibility.Disabled);
        layers.ItemTemplate = LayerTemplate();
        layers.AlternationCount = 100000;
        VirtualizingPanel.SetIsVirtualizing(layers, false);
        layers.Background = Brush("Pane");
        layers.Foreground = Brush("Text");
        layers.ItemContainerStyle = (Style)FindResource(typeof(ListBoxItem));
        Grid.SetRow(layers, 1);
        left.Children.Add(layers);
        var commandCaption = new TextBlock
        {
            Text = "Layer command",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(12, 8, 12, 4),
        };
        Grid.SetRow(commandCaption, 2);
        left.Children.Add(commandCaption);
        command.Margin = new Thickness(12, 0, 12, 12);
        var commandScroll = new ScrollViewer { Content = command, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        Grid.SetRow(commandScroll, 3);
        left.Children.Add(commandScroll);

        var columnSplitter = new GridSplitter
        {
            Width = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brush("Border"),
            ResizeDirection = GridResizeDirection.Columns,
        };
        Grid.SetColumn(columnSplitter, 1);
        browser.Children.Add(columnSplitter);

        var right = new Grid { Background = Brush("Pane") };
        Grid.SetColumn(right, 2);
        browser.Children.Add(right);
        foreach (var height in new[]
        {
            GridLength.Auto,
            GridLength.Auto,
            new GridLength(3, GridUnitType.Star),
            new GridLength(4),
            new GridLength(2, GridUnitType.Star),
        })
        {
            right.RowDefinitions.Add(new() { Height = height });
        }

        var nav = new DockPanel { Margin = new Thickness(8, 8, 8, 4) };
        right.Children.Add(nav);
        nav.Children.Add(IconButton("\uE72B", "Up", GoUp));
        nav.Children.Add(IconButton("\uE80F", "Root", () =>
        {
            currentPath = "";
            UpdateFiles();
        }));
        location.Margin = new Thickness(4, 0, 0, 0);
        nav.Children.Add(location);

        var filter = new WrapPanel { Margin = new Thickness(8, 0, 8, 8) };
        Grid.SetRow(filter, 1);
        right.Children.Add(filter);
        filter.Children.Add(new TextBlock
        {
            Text = "\uE721",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brush("Secondary"),
            Margin = new Thickness(4, 0, 6, 0),
        });
        search.Margin = new Thickness(0, 4, 8, 4);
        type.Margin = new Thickness(0, 4, 8, 4);
        action.Margin = new Thickness(0, 4, 8, 4);
        merged.Margin = new Thickness(0, 4, 0, 4);
        filter.Children.Add(search);
        filter.Children.Add(type);
        filter.Children.Add(action);
        filter.Children.Add(merged);

        var icon = new FrameworkElementFactory(typeof(Image));
        icon.SetValue(Image.WidthProperty, 16.0);
        icon.SetValue(Image.HeightProperty, 16.0);
        icon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        icon.SetBinding(Image.SourceProperty, new Binding("Icon"));
        files.Columns.Add(new DataGridTemplateColumn
        {
            Header = "",
            CellTemplate = new DataTemplate { VisualTree = icon },
            Width = 32,
        });
        var nameColumn = new DataGridTextColumn
        {
            Header = "Name",
            Binding = new Binding("Name"),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star),
            ElementStyle = CellText(false),
        };
        files.Columns.Add(nameColumn);

        var badge = new FrameworkElementFactory(typeof(Border));
        badge.SetValue(Border.CornerRadiusProperty, new CornerRadius(4));
        badge.SetValue(Border.PaddingProperty, new Thickness(6, 1, 6, 1));
        badge.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        badge.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
        var tagText = new FrameworkElementFactory(typeof(TextBlock));
        tagText.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(26, 26, 26)));
        tagText.SetValue(TextBlock.FontSizeProperty, 12.0);
        tagText.SetBinding(TextBlock.TextProperty, new Binding("Action"));
        badge.AppendChild(tagText);
        var badgeStyle = new Style(typeof(Border));
        foreach (var (value, color) in new[]
        {
            ("Added", "#D5F5E3"),
            ("Modified", "#FFF0BC"),
            ("Deleted", "#FFDADB"),
            ("Unchanged", "#E7EBF0"),
        })
        {
            var trigger = new DataTrigger { Binding = new Binding("Action"), Value = value };
            trigger.Setters.Add(new Setter(Border.BackgroundProperty, (Brush)new BrushConverter().ConvertFromString(color)!));
            badgeStyle.Triggers.Add(trigger);
        }

        badge.SetValue(Border.StyleProperty, badgeStyle);
        files.Columns.Add(new DataGridTemplateColumn
        {
            Header = "Change",
            CellTemplate = new DataTemplate { VisualTree = badge },
            Width = 112,
        });
        files.Columns.Add(new DataGridTextColumn
        {
            Header = "Type",
            Binding = new Binding("Type"),
            Width = 120,
            ElementStyle = CellText(false),
        });
        var sizeHeader = new Style(typeof(DataGridColumnHeader))
        {
            BasedOn = (Style)FindResource(typeof(DataGridColumnHeader)),
            Setters = { new Setter(Control.HorizontalContentAlignmentProperty, HorizontalAlignment.Right) },
        };
        files.Columns.Add(new DataGridTextColumn
        {
            Header = "Size",
            Binding = new Binding("Size"),
            Width = 90,
            ElementStyle = CellText(true),
            HeaderStyle = sizeHeader,
        });
        pathColumn.ElementStyle = CellText(false);
        files.Columns.Add(pathColumn);
        files.RowStyle = (Style)FindResource(typeof(DataGridRow));
        files.CellStyle = (Style)FindResource(typeof(DataGridCell));
        files.ColumnHeaderStyle = (Style)FindResource(typeof(DataGridColumnHeader));
        files.RowHeight = 28;
        files.ColumnHeaderHeight = 32;
        files.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;
        files.HorizontalGridLinesBrush = Brush("Border");
        files.RowBackground = Brushes.Transparent;
        files.AlternatingRowBackground = Brushes.Transparent;
        files.BorderThickness = new Thickness(0);
        files.Background = Brush("Pane");
        files.FontSize = 13;

        var fileLayer = new Grid();
        Grid.SetRow(fileLayer, 2);
        right.Children.Add(fileLayer);
        fileLayer.Children.Add(files);
        BuildEmptyState();
        fileLayer.Children.Add(emptyState);
        var splitter = new GridSplitter
        {
            Height = 4,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            Background = Brush("Border"),
            ResizeDirection = GridResizeDirection.Rows,
        };
        Grid.SetRow(splitter, 3);
        right.Children.Add(splitter);

        preview.Padding = new Thickness(12);
        preview.BorderThickness = new Thickness(0);
        preview.Background = Brushes.Transparent;
        picture.Margin = new Thickness(12);
        details.Margin = new Thickness(12, 8, 12, 4);
        var textHost = new Border
        {
            Background = Brush("Code"),
            BorderBrush = Brush("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(8, 0, 8, 8),
            Child = preview,
        };
        var imageHost = new Border
        {
            Background = Brush("PreviewSurface"),
            BorderBrush = Brush("Border"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            Margin = new Thickness(8, 0, 8, 8),
            Child = picture,
        };
        var previewPanel = new DockPanel();
        DockPanel.SetDock(details, Dock.Top);
        previewPanel.Children.Add(details);
        previewPanel.Children.Add(previewTabs);
        previewTabs.Items.Add(new TabItem { Header = "Text / Hex", Content = textHost });
        previewTabs.Items.Add(new TabItem { Header = "Image", Content = imageHost });
        previewTabs.Margin = new Thickness(8, 0, 8, 0);
        Grid.SetRow(previewPanel, 4);
        right.Children.Add(previewPanel);
        UpdateEmpty();

        SetupInteractions();
        layers.SelectionChanged += (_, _) =>
        {
            currentPath = "";
            UpdateFiles();
        };
        search.TextChanged += (_, _) => UpdateFiles();
        type.SelectionChanged += (_, _) => UpdateFiles();
        action.SelectionChanged += (_, _) => UpdateFiles();
        merged.Checked += (_, _) => UpdateFiles();
        merged.Unchecked += (_, _) => UpdateFiles();
        files.SelectionChanged += async (_, _) =>
        {
            selectionCount.Text = files.SelectedItems.Count == 0 ? "" : $"{files.SelectedItems.Count} selected";
            await ShowFile();
        };
        files.MouseDoubleClick += (_, _) =>
        {
            if (files.SelectedItem is ExplorerRow row && row.Item.IsDirectory)
            {
                if (row.Name == "..")
                    GoUp();
                else
                {
                    currentPath = row.Path;
                    ClearFilters();
                    UpdateFiles();
                }
            }
        };
        files.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter && files.SelectedItem is ExplorerRow row && row.Item.IsDirectory)
            {
                if (row.Name == "..")
                    GoUp();
                else
                {
                    currentPath = row.Path;
                    ClearFilters();
                    UpdateFiles();
                }

                e.Handled = true;
            }
        };
        Closing += (_, e) =>
        {
            if (busy)
            {
                e.Cancel = true;
                status.Text = "Wait for the current operation to finish.";
            }
            else
            {
                ResetMedia();
                session?.Dispose();
            }
        };
    }

    void ClearFilters()
    {
        search.Clear();
        type.SelectedIndex = 0;
        action.SelectedIndex = 0;
    }

    void GoUp()
    {
        currentPath = ExplorerModel.Parent(currentPath);
        ClearFilters();
        UpdateFiles();
        files.Focus();
    }

    Button IconButton(string glyph, string tip, Action action)
    {
        var button = new Button
        {
            Width = 32,
            Height = 32,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, 4, 0),
            ToolTip = tip,
            Content = new TextBlock
            {
                Text = glyph,
                FontFamily = new FontFamily("Segoe MDL2 Assets"),
                FontSize = 16,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        AutomationProperties.SetName(button, tip);
        button.Click += (_, _) => action();
        return button;
    }

    Border BuildTitleBar()
    {
        var bar = new Grid { Height = 32, Background = Brush("Pane") };
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        bar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var leading = new StackPanel { Orientation = Orientation.Horizontal };
        titleIcon = new Image
        {
            Width = 16,
            Height = 16,
            Margin = new Thickness(12, 0, 4, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
            IsHitTestVisible = false,
        };
        SetIcon(titleIcon, 16, 1);
        Loaded += (_, _) => SetIcon(titleIcon, 16, VisualTreeHelper.GetDpi(this).DpiScaleX);
        DpiChanged += (_, e) => SetIcon(titleIcon, 16, e.NewDpi.DpiScaleX);
        leading.Children.Add(titleIcon);
        mainMenu.Background = Brushes.Transparent;
        mainMenu.VerticalAlignment = VerticalAlignment.Center;
        WindowChrome.SetIsHitTestVisibleInChrome(mainMenu, true);
        leading.Children.Add(mainMenu);
        bar.Children.Add(leading);

        var title = new TextBlock
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            Foreground = Brush("Secondary"),
            FontSize = 12,
            Margin = new Thickness(16, 0, 16, 0),
            IsHitTestVisible = false,
        };
        title.SetBinding(TextBlock.TextProperty, new Binding(nameof(Title)) { Source = this });
        Grid.SetColumn(title, 1);
        bar.Children.Add(title);

        var maximizeIcon = new TextBlock
        {
            Text = "\uE922",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 10,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            IsHitTestVisible = false,
        };
        var maximize = CaptionButton(maximizeIcon, "Maximize", () =>
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized, close: false);
        StateChanged += (_, _) =>
        {
            var maximized = WindowState == WindowState.Maximized;
            maximizeIcon.Text = maximized ? "\uE923" : "\uE922";
            var label = maximized ? "Restore" : "Maximize";
            maximize.ToolTip = label;
            AutomationProperties.SetName(maximize, label);
        };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal };
        buttons.Children.Add(CaptionButton(CaptionGlyph("\uE921"), "Minimize", () => WindowState = WindowState.Minimized, close: false));
        buttons.Children.Add(maximize);
        buttons.Children.Add(CaptionButton(CaptionGlyph("\uE8BB"), "Close", Close, close: true));
        Grid.SetColumn(buttons, 2);
        bar.Children.Add(buttons);

        return new Border
        {
            Background = Brush("Pane"),
            BorderBrush = Brush("Border"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = bar,
        };
    }

    static TextBlock CaptionGlyph(string glyph) => new()
    {
        Text = glyph,
        FontFamily = new FontFamily("Segoe MDL2 Assets"),
        FontSize = 10,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
        IsHitTestVisible = false,
    };

    Button CaptionButton(TextBlock glyph, string name, Action action, bool close)
    {
        var hover = close ? new SolidColorBrush(Color.FromRgb(232, 17, 35)) : Brush("Pressed");
        if (hover.CanFreeze)
            hover.Freeze();
        var face = new FrameworkElementFactory(typeof(Border));
        face.Name = "Face";
        face.SetValue(Border.BackgroundProperty, Brushes.Transparent);
        var content = new FrameworkElementFactory(typeof(ContentPresenter));
        content.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
        content.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        face.AppendChild(content);
        var template = new ControlTemplate(typeof(Button)) { VisualTree = face };
        var over = new Trigger { Property = UIElement.IsMouseOverProperty, Value = true };
        over.Setters.Add(new Setter(Border.BackgroundProperty, hover, "Face"));
        template.Triggers.Add(over);
        glyph.Foreground = Brush("Text");
        var button = new Button
        {
            Width = 46,
            Height = 32,
            Content = glyph,
            ToolTip = name,
            Focusable = false,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            Template = template,
            Cursor = Cursors.Arrow,
        };
        if (close)
        {
            button.MouseEnter += (_, _) => glyph.Foreground = Brushes.White;
            button.MouseLeave += (_, _) => glyph.Foreground = Brush("Text");
        }

        AutomationProperties.SetName(button, name);
        WindowChrome.SetIsHitTestVisibleInChrome(button, true);
        button.Click += (_, _) => action();
        return button;
    }

    void BuildMenu()
    {
        var file = new MenuItem { Header = "_File" };
        var open = new MenuItem { Header = "Open archive", InputGestureText = "Ctrl+O" };
        open.Click += async (_, _) => await Work(Open);
        file.Items.Add(open);
        file.Items.Add(new Separator());
        var exit = new MenuItem { Header = "E_xit", InputGestureText = "Alt+F4" };
        exit.Click += (_, _) => Close();
        file.Items.Add(exit);
        mainMenu.Items.Add(file);

        AddExport("Export layer", () => Export(0));
        AddExport("Save layer", () => SaveDirect(0));
        exportMenu.Items.Add(new Separator());
        AddExport("Export filesystem", () => Export(1));
        AddExport("Save filesystem", () => SaveDirect(1));
        exportMenu.Items.Add(new Separator());
        AddExport("Export entire filesystem", () => Export(2));
        AddExport("Save entire filesystem", () => SaveDirect(2));
        mainMenu.Items.Add(exportMenu);

        var view = new MenuItem { Header = "_View" };
        var metadata = new MenuItem { Header = "Image metadata" };
        metadata.Click += async (_, _) => await Work(() =>
        {
            preview.Text = session?.Description ?? "Open an image first.";
            previewTabs.SelectedIndex = 0;
            return Task.CompletedTask;
        });
        view.Items.Add(metadata);
        mainMenu.Items.Add(view);

        var help = new MenuItem { Header = "_Help" };
        var about = new MenuItem { Header = "About Docker Image Explorer" };
        about.Click += (_, _) => ShowAbout();
        help.Items.Add(about);
        mainMenu.Items.Add(help);
    }

    void AddExport(string label, Func<Task> operation)
    {
        var item = new MenuItem { Header = label };
        item.Click += async (_, _) => await Work(operation);
        exportMenu.Items.Add(item);
    }

    void RouteDropdownClick(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source)
            return;

        foreach (var combo in new[] { type, action })
        {
            if (combo.IsDropDownOpen && !IsInsideCombo(combo, source))
                combo.IsDropDownOpen = false;
        }
    }

    void CloseDropdowns()
    {
        type.IsDropDownOpen = false;
        action.IsDropDownOpen = false;
    }

    static bool IsInsideCombo(ComboBox combo, DependencyObject source)
    {
        var popup = combo.Template?.FindName("PART_Popup", combo) as Popup;
        for (var node = source; node != null; node = ParentOf(node))
        {
            if (node == combo || (popup?.Child != null && node == popup.Child))
                return true;
        }

        return false;
    }

    static DependencyObject? ParentOf(DependencyObject node)
    {
        if (node is Visual)
        {
            var visual = VisualTreeHelper.GetParent(node);
            if (visual != null)
                return visual;
        }

        return LogicalTreeHelper.GetParent(node);
    }

    static BitmapFrame[] IconFrames()
    {
        if (iconFrames != null)
            return iconFrames;

        var decoder = BitmapDecoder.Create(
            new Uri("pack://application:,,,/DockerImageExplorer;component/AppIcon.ico", UriKind.Absolute),
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        iconFrames = decoder.Frames.ToArray();
        foreach (var frame in iconFrames)
        {
            if (frame.CanFreeze)
                frame.Freeze();
        }

        return iconFrames;
    }

    static BitmapFrame IconFrame(double dips, double dpiScale)
    {
        var pixels = Math.Max(1, (int)Math.Round(dips * dpiScale));
        return IconFrames()
            .OrderBy(frame => frame.PixelWidth >= pixels ? frame.PixelWidth : int.MaxValue)
            .ThenByDescending(frame => frame.PixelWidth)
            .First();
    }

    static void SetIcon(Image image, double dips, double dpiScale)
    {
        var source = IconFrame(dips, dpiScale);
        var pixels = Math.Max(1, (int)Math.Round(dips * dpiScale));
        image.Source = source;
        RenderOptions.SetBitmapScalingMode(
            image,
            source.PixelWidth == pixels ? BitmapScalingMode.NearestNeighbor : BitmapScalingMode.HighQuality);
    }

    void ShowAbout()
    {
        var dialog = new Window
        {
            Owner = this,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ShowInTaskbar = false,
            Background = Brush("Pane"),
            Foreground = Brush("Text"),
            FontFamily = FontFamily,
            FontSize = 14,
            UseLayoutRounding = true,
        };
        var body = new StackPanel
        {
            Width = 380,
            Margin = new Thickness(32, 28, 32, 28),
        };
        var aboutIcon = new Image
        {
            Width = 48,
            Height = 48,
            HorizontalAlignment = HorizontalAlignment.Center,
            Stretch = Stretch.Uniform,
            SnapsToDevicePixels = true,
            UseLayoutRounding = true,
        };
        SetIcon(aboutIcon, 48, VisualTreeHelper.GetDpi(this).DpiScaleX);
        dialog.Loaded += (_, _) => SetIcon(aboutIcon, 48, VisualTreeHelper.GetDpi(dialog).DpiScaleX);
        body.Children.Add(aboutIcon);
        body.Children.Add(new TextBlock
        {
            Text = "Docker Image Explorer",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 14, 0, 0),
        });
        body.Children.Add(new TextBlock
        {
            Text = "Version 1.0",
            Foreground = Brush("Secondary"),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 0),
        });
        body.Children.Add(new TextBlock
        {
            Text = "Browse Docker and OCI images layer by layer, and inspect the files each layer adds, changes, or removes.",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            Foreground = Brush("Secondary"),
            Margin = new Thickness(12, 16, 12, 0),
            LineHeight = 20,
        });
        var ok = new Button
        {
            Content = "OK",
            HorizontalAlignment = HorizontalAlignment.Center,
            MinWidth = 96,
            Margin = new Thickness(0, 20, 0, 0),
            IsDefault = true,
            IsCancel = true,
            Style = (Style)FindResource("PrimaryButton"),
        };
        ok.Click += (_, _) => dialog.Close();
        body.Children.Add(ok);

        var layout = new Grid();
        layout.Children.Add(body);
        var close = CaptionButton(CaptionGlyph("\uE8BB"), "Close", dialog.Close, close: true);
        close.HorizontalAlignment = HorizontalAlignment.Right;
        close.VerticalAlignment = VerticalAlignment.Top;
        layout.Children.Add(close);
        dialog.Content = new Border
        {
            Background = Brush("Pane"),
            BorderBrush = Brush("Border"),
            BorderThickness = new Thickness(1),
            Child = layout,
        };
        dialog.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState != MouseButtonState.Pressed || e.OriginalSource is not DependencyObject source || IsInsideButton(source))
                return;
            dialog.DragMove();
        };
        dialog.ShowDialog();
    }

    static bool IsInsideButton(DependencyObject source)
    {
        for (var node = source; node != null; node = ParentOf(node))
        {
            if (node is Button)
                return true;
        }

        return false;
    }

    int LayerNumber(Layer layer)
    {
        if (layer.Number > 0)
            return layer.Number;

        var index = session?.Layers.IndexOf(layer) ?? -1;
        return index >= 0 ? index + 1 : layers.SelectedIndex + 1;
    }

    static void ShowSavedLocation(string path)
    {
        var full = Path.GetFullPath(path);
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = File.Exists(full) ? "/select,\"" + full + "\"" : "\"" + full + "\"",
            UseShellExecute = true,
        });
    }

    void BuildEmptyState()
    {
        var stack = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        stack.Children.Add(new TextBlock
        {
            Text = "\uE8E5",
            FontFamily = new FontFamily("Segoe MDL2 Assets"),
            FontSize = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = Brush("Secondary"),
            Margin = new Thickness(0, 0, 0, 12),
        });
        stack.Children.Add(new TextBlock
        {
            Text = "Open a Docker save or OCI archive",
            FontSize = 16,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 16),
        });
        var open = new Button
        {
            Content = "Open archive",
            MinWidth = 128,
            HorizontalAlignment = HorizontalAlignment.Center,
            Style = (Style)FindResource("PrimaryButton"),
        };
        open.Click += async (_, _) => await Work(Open);
        stack.Children.Add(open);
        emptyState.Child = stack;
        emptyState.Background = Brush("Pane");
    }

    void UpdateEmpty()
    {
        bool open = session == null;
        emptyState.Visibility = open ? Visibility.Visible : Visibility.Collapsed;
        files.Visibility = open ? Visibility.Collapsed : Visibility.Visible;
    }

    DataTemplate LayerTemplate()
    {
        var root = new FrameworkElementFactory(typeof(DockPanel));
        root.SetBinding(FrameworkElement.ToolTipProperty, new Binding("Command") { Converter = Theme.OneLine });
        var index = new FrameworkElementFactory(typeof(TextBlock));
        index.SetValue(DockPanel.DockProperty, Dock.Left);
        index.SetValue(FrameworkElement.WidthProperty, 32.0);
        index.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        index.SetValue(TextBlock.FontSizeProperty, 12.0);
        index.SetValue(TextBlock.ForegroundProperty, Brush("Accent"));
        index.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
        index.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
        index.SetBinding(TextBlock.TextProperty, new Binding("Number"));
        var stack = new FrameworkElementFactory(typeof(StackPanel));
        stack.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 0, 0, 0));
        var title = new FrameworkElementFactory(typeof(TextBlock));
        title.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        title.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        title.SetBinding(TextBlock.TextProperty, new Binding("Title"));
        var line = new FrameworkElementFactory(typeof(TextBlock));
        line.SetValue(TextBlock.FontSizeProperty, 12.0);
        line.SetValue(TextBlock.ForegroundProperty, Brush("Secondary"));
        line.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        line.SetValue(TextBlock.TextWrappingProperty, TextWrapping.NoWrap);
        line.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 2, 0, 0));
        line.SetBinding(TextBlock.TextProperty, new Binding("Command") { Converter = Theme.OneLine });
        var counts = new FrameworkElementFactory(typeof(StackPanel));
        counts.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        counts.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 4, 0, 0));
        counts.AppendChild(CountLabel("Added", "added", Brush("AddedBrush")));
        counts.AppendChild(CountLabel("Modified", "modified", Brush("ModifiedBrush")));
        counts.AppendChild(CountLabel("Deleted", "deleted", Brush("DeletedBrush")));
        stack.AppendChild(title);
        stack.AppendChild(line);
        stack.AppendChild(counts);
        root.AppendChild(index);
        root.AppendChild(stack);
        return new DataTemplate { VisualTree = root };
    }

    FrameworkElementFactory CountLabel(string action, string caption, Brush color)
    {
        var panel = new FrameworkElementFactory(typeof(StackPanel));
        panel.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        panel.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 12, 0));
        var number = new FrameworkElementFactory(typeof(TextBlock));
        number.SetValue(TextBlock.FontWeightProperty, FontWeights.SemiBold);
        number.SetValue(TextBlock.FontSizeProperty, 12.0);
        number.SetValue(TextBlock.ForegroundProperty, color);
        number.SetValue(FrameworkElement.MarginProperty, new Thickness(0, 0, 4, 0));
        number.SetBinding(TextBlock.TextProperty, new Binding("Changes") { Converter = Theme.Count, ConverterParameter = action });
        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetValue(TextBlock.TextProperty, caption);
        label.SetValue(TextBlock.FontSizeProperty, 12.0);
        label.SetValue(TextBlock.ForegroundProperty, Brush("Secondary"));
        panel.AppendChild(number);
        panel.AppendChild(label);
        return panel;
    }

    static Style CellText(bool right)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        if (right)
        {
            style.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right));
            style.Setters.Add(new Setter(TextBlock.TextAlignmentProperty, TextAlignment.Right));
            style.Setters.Add(new Setter(FrameworkElement.MarginProperty, new Thickness(0, 0, 8, 0)));
        }

        return style;
    }

    Brush Brush(string key) => Theme.Get(this, key);

    async Task Work(Func<Task> operation)
    {
        if (busy)
            return;

        busy = true;
        mainMenu.IsEnabled = false;
        browser.IsEnabled = false;
        progress.Visibility = Visibility.Visible;
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            status.Text = "Operation failed.";
            MessageBox.Show(this, ex.Message, Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            busy = false;
            mainMenu.IsEnabled = true;
            browser.IsEnabled = true;
            progress.Visibility = Visibility.Collapsed;
        }
    }

    async Task Open()
    {
        var dialog = new OpenFileDialog { Filter = "Image archives|*.tar;*.tar.gz;*.tgz|All files|*.*" };
        if (dialog.ShowDialog(this) != true)
            return;

        status.Text = "Loading image…";
        var next = new ImageSession();
        try
        {
            await Task.Run(() => next.LoadArchive(dialog.FileName));
        }
        catch
        {
            next.Dispose();
            throw;
        }

        ++previewVersion;
        ResetMedia();
        mediaCopies.Clear();
        session?.Dispose();
        session = next;
        currentPath = "";
        UpdateEmpty();
        layers.ItemsSource = Enumerable.Reverse(next.Layers).ToList();
        layers.SelectedIndex = next.Layers.Count == 0 ? -1 : 0;
        Title = "DockerImageExplorer 1.0 — " + dialog.FileName;
        if (next.Layers.Count == 0)
        {
            files.ItemsSource = null;
            preview.Text = next.Description;
            details.Text = "Empty filesystem";
            picture.Source = null;
        }
    }

    void UpdateFiles()
    {
        ++previewVersion;
        ResetMedia();
        preview.Clear();
        picture.Source = null;
        details.Text = "Select a file to preview. Double-click a folder to open it. Ctrl/Shift selects multiple items.";
        if (layers.SelectedItem is not Layer layer)
        {
            pathColumn.Visibility = Visibility.Collapsed;
            itemCount.Text = "";
            return;
        }

        command.Text = layer.Command;
        bool searching = search.Text.Length > 0 || type.SelectedIndex > 0 || action.SelectedIndex > 0;
        pathColumn.Visibility = searching ? Visibility.Visible : Visibility.Collapsed;
        var items = ExplorerModel.Build(layer, merged.IsChecked != true);
        var prefix = currentPath.Length == 0 ? "" : currentPath + "/";
        var rows = items
            .Where(i => searching || i.Path.StartsWith(prefix, StringComparison.Ordinal) && !i.Path[prefix.Length..].Contains('/'))
            .Where(i => ExplorerModel.Matches(i, search.Text, type.SelectedItem as string ?? "All types", action.SelectedItem as string ?? "All changes"))
            .OrderByDescending(i => i.IsDirectory)
            .ThenBy(i => i.Path, StringComparer.Ordinal)
            .Select(i => new ExplorerRow(i))
            .ToList();
        if (currentPath.Length > 0)
            rows.Insert(0, new ExplorerRow(new ExplorerItem(currentPath + "/..", true, "", null)));

        files.ItemsSource = rows;
        location.Text = searching ? "/ — Search results across " + layer.Title : "/" + currentPath;
        itemCount.Text = rows.Count == 1 ? "1 item" : $"{rows.Count} items";
        status.Text = $"{layer.Title}: {layer.Changes.Count(c => c.Action == "Added")} added · {layer.Changes.Count(c => c.Action == "Modified")} modified · {layer.Changes.Count(c => c.Action == "Deleted")} deleted";
    }

    async Task ShowFile()
    {
        var version = ++previewVersion;
        ResetMedia();
        picture.Source = null;
        preview.Clear();
        if (files.SelectedItems.Count != 1 || files.SelectedItem is not ExplorerRow row || layers.SelectedItem is not Layer layer)
        {
            details.Text = $"{files.SelectedItems.Count} items selected";
            return;
        }

        details.Text = $"/{row.Path} · {row.Action} · {row.Type}";
        if (row.Item.IsDirectory)
        {
            preview.Text = "Double-click to open this folder. Export selected includes its contents recursively.";
            previewTabs.SelectedIndex = 0;
            return;
        }

        if (row.Item.File is not ImageFile original)
            return;

        var file = ImageSession.Resolve(original, layer.Files);
        details.Text += $" · {file.Size:N0} bytes · mode {Convert.ToString((int)original.Mode, 8)} · UID {original.Uid} · GID {original.Gid}\nModified: {original.Modified:u}"
            + (original.Link.Length > 0 ? " · Target: " + original.Link : "");
        if (file.Blob == null)
        {
            preview.Text = "This entry has no regular file content.";
            previewTabs.SelectedIndex = 0;
            return;
        }

        try
        {
            var data = await Task.Run(() =>
            {
                using var input = File.OpenRead(file.Blob);
                var bytes = new byte[(int)Math.Min(input.Length, 256 * 1024)];
                input.ReadExactly(bytes);
                return bytes;
            });
            if (version != previewVersion)
                return;

            bool imageType = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".ico" }.Contains(row.Item.Extension);
            bool binary = data.Take(8192).Contains((byte)0);
            string text;
            try
            {
                bool utf16 = data.Length >= 2 && (data[0] == 0xff && data[1] == 0xfe || data[0] == 0xfe && data[1] == 0xff);
                text = utf16
                    ? (data[0] == 0xff ? Encoding.Unicode : Encoding.BigEndianUnicode).GetString(data[2..])
                    : new UTF8Encoding(false, true).GetString(data);
                if (utf16)
                    binary = false;
            }
            catch (DecoderFallbackException)
            {
                binary = true;
                text = "";
            }

            preview.Text = binary
                ? "Binary content (first 4 KiB):\n" + string.Join('\n', data.Take(4096).Chunk(16).Select((chunk, i) => $"{i * 16:X8}  {Convert.ToHexString(chunk)}"))
                : text;
            if (file.Size > data.Length)
                preview.Text += "\n[Text preview limited to 256 KiB. Downloads preserve the full content.]";

            previewTabs.SelectedIndex = 0;
            if (ExplorerModel.Matches(row.Item, "", "Videos", "All changes") || ExplorerModel.Matches(row.Item, "", "Audio", "All changes"))
            {
                var owner = session!;
                var key = file.Blob + row.Item.Extension;
                if (!mediaCopies.TryGetValue(key, out var mediaPath))
                {
                    mediaPath = Path.Combine(owner.DirectoryPath, "preview-" + Guid.NewGuid().ToString("N") + row.Item.Extension);
                    await Task.Run(() => File.Copy(file.Blob, mediaPath));
                    if (version != previewVersion || session != owner)
                        return;

                    mediaCopies[key] = mediaPath;
                }

                if (version != previewVersion)
                    return;

                mediaStatus.Text = "Press Play to preview. Playback depends on codecs available in Windows.";
                media.Source = new Uri(mediaPath);
                media.Pause();
                previewTabs.SelectedIndex = 2;
                mediaTimer.Start();
            }
            else if (imageType)
            {
                if (file.Size > 32 * 1024 * 1024)
                {
                    preview.Text = "Image exceeds the 32 MiB preview limit. Download to view the full image.\n" + preview.Text;
                    return;
                }

                var image = await Task.Run(() =>
                {
                    using var input = File.OpenRead(file.Blob);
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.DecodePixelWidth = 1600;
                    bitmap.StreamSource = input;
                    bitmap.EndInit();
                    bitmap.Freeze();
                    return bitmap;
                });
                if (version == previewVersion)
                {
                    picture.Source = image;
                    previewTabs.SelectedIndex = 1;
                }
            }
        }
        catch (Exception ex)
        {
            if (version == previewVersion)
                preview.Text += "\nPreview unavailable: " + ex.Message;
        }
    }

    async Task SaveSelection(bool tar)
    {
        if (session == null || layers.SelectedItem is not Layer layer || files.SelectedItems.Count == 0)
            throw new InvalidOperationException("Select one or more files or folders first.");

        var selected = files.SelectedItems.Cast<ExplorerRow>().Where(r => r.Name != "..").Select(r => r.Item).ToArray();
        var content = ExplorerModel.Selection(layer, selected, ExplorerModel.Build(layer, false));
        if (content.Count == 0)
            throw new InvalidOperationException("The selection contains no exportable entries.");

        if (tar)
        {
            var dialog = new SaveFileDialog { Filter = "Tar archive|*.tar", FileName = "selected-files.tar" };
            if (dialog.ShowDialog(this) != true)
                return;

            var temp = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                await Task.Run(() =>
                {
                    ImageSession.ExportFilesystem(temp, content);
                    File.Move(temp, dialog.FileName, true);
                });
            }
            finally
            {
                if (File.Exists(temp))
                    File.Delete(temp);
            }

            status.Text = $"Saved {content.Count} entries to {dialog.FileName}";
            ShowSavedLocation(dialog.FileName);
        }
        else
        {
            var dialog = new OpenFolderDialog { Title = "Choose export location" };
            if (dialog.ShowDialog(this) != true)
                return;

            var destination = Path.Combine(
                dialog.FolderName,
                "DockerImageExplorer-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
            await Task.Run(() => ExplorerModel.Download(destination, content));
            status.Text = $"Exported {content.Count} entries to {destination}";
            ShowSavedLocation(destination);
        }
    }

    async Task SaveDirect(int kind)
    {
        if (session == null)
            throw new InvalidOperationException("Open an image first.");

        var layer = layers.SelectedItem as Layer;
        if (kind < 2 && layer == null)
            throw new InvalidOperationException("Select a layer first.");

        var dialog = new OpenFolderDialog { Title = "Choose save location" };
        if (dialog.ShowDialog(this) != true)
            return;

        var filesystem = kind == 2
            ? session.Layers.LastOrDefault()?.Files ?? new Dictionary<string, ImageFile>(StringComparer.Ordinal)
            : layer!.Files;
        var entries = kind == 0 ? ExplorerModel.ChangedFiles(layer!) : filesystem;
        var label = kind == 0
            ? $"layer-{LayerNumber(layer!)}"
            : kind == 1
                ? $"filesystem-layer-{LayerNumber(layer!)}"
                : "image-filesystem";
        var destination = Path.Combine(
            dialog.FolderName,
            "DockerImageExplorer-" + label + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
        status.Text = "Saving…";
        var result = await Task.Run(() => ExplorerModel.SaveWindows(destination, entries, filesystem));
        status.Text = result.Describe(destination);
        ShowSavedLocation(destination);
    }

    async Task Export(int kind)
    {
        if (session == null)
            throw new InvalidOperationException("Open an image first.");

        var layer = layers.SelectedItem as Layer;
        if (kind < 2 && layer == null)
            throw new InvalidOperationException("Select a layer first.");

        var dialog = new SaveFileDialog
        {
            Filter = kind == 3 ? "Image archive|*.*" : "Tar archive (*.tar)|*.tar",
            FileName = kind == 3
                ? Path.GetFileName(session.SourceArchive)
                : kind == 0
                    ? $"layer-{LayerNumber(layer!)}.tar"
                    : kind == 1
                        ? $"filesystem-layer-{LayerNumber(layer!)}.tar"
                        : "image-filesystem.tar",
        };
        if (dialog.ShowDialog(this) != true)
            return;

        status.Text = "Exporting…";
        // Write alongside the destination and replace only after successful completion.
        var temp = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await Task.Run(() =>
            {
                if (kind == 3)
                {
                    File.Copy(session.SourceArchive, temp);
                }
                else if (kind == 0)
                {
                    using var input = File.OpenRead(layer!.Archive);
                    int a = input.ReadByte();
                    int b = input.ReadByte();
                    input.Position = 0;
                    using var output = File.Create(temp);
                    if (a == 31 && b == 139)
                    {
                        using var gzip = new GZipStream(input, CompressionMode.Decompress);
                        gzip.CopyTo(output);
                    }
                    else
                    {
                        input.CopyTo(output);
                    }
                }
                else
                {
                    ImageSession.ExportFilesystem(
                        temp,
                        kind == 1 ? layer!.Files : session.Layers.LastOrDefault()?.Files ?? new Dictionary<string, ImageFile>());
                }

                File.Move(temp, dialog.FileName, true);
            });
            status.Text = $"Exported: {dialog.FileName}";
            ShowSavedLocation(dialog.FileName);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }
}
