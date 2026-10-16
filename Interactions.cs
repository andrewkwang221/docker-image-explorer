using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using System.Windows.Threading;

namespace DockerImageExplorer;

public sealed partial class Viewer
{
    readonly MediaElement media = new()
    {
        LoadedBehavior = MediaState.Manual,
        UnloadedBehavior = MediaState.Close,
        Stretch = Stretch.Uniform,
        Volume = 0.7,
    };
    readonly TextBlock mediaStatus = new()
    {
        Text = "Select a video or audio file.",
        Margin = new Thickness(6),
        TextWrapping = TextWrapping.Wrap,
    };
    readonly Slider seek = new()
    {
        Minimum = 0,
        Maximum = 1,
        Width = 230,
        Margin = new Thickness(6),
        IsEnabled = false,
    };
    readonly DispatcherTimer mediaTimer = new() { Interval = TimeSpan.FromMilliseconds(500) };
    readonly Dictionary<string, string> mediaCopies = new();

    static T? Ancestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source != null)
        {
            if (source is T found)
                return found;

            source = source is Visual || source is Visual3D
                ? VisualTreeHelper.GetParent(source)
                : LogicalTreeHelper.GetParent(source);
        }

        return null;
    }

    void NavigationKey(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Back || busy)
            return;

        var text = Ancestor<TextBox>(e.OriginalSource as DependencyObject);
        if (text is { IsReadOnly: false })
            return;

        GoUp();
        e.Handled = true;
    }

    ContextMenu Menu(params (string Label, Func<Task> Run)[] entries)
    {
        var menu = new ContextMenu();
        foreach (var entry in entries)
        {
            var item = new MenuItem { Header = entry.Label };
            item.Click += async (_, _) => await Work(entry.Run);
            menu.Items.Add(item);
        }

        return menu;
    }

    void SetupInteractions()
    {
        AddHandler(Keyboard.PreviewKeyDownEvent, new KeyEventHandler(NavigationKey), true);
        layers.ContextMenu = Menu(
            ("Export layer", () => Export(0)),
            ("Save layer", () => SaveDirect(0)),
            ("Export file system", () => Export(1)),
            ("Save file system", () => SaveDirect(1)),
            ("Export entire filesystem", () => Export(2)),
            ("Save entire filesystem", () => SaveDirect(2)));
        layers.PreviewMouseRightButtonDown += (_, e) =>
        {
            if (Ancestor<ListBoxItem>(e.OriginalSource as DependencyObject) is ListBoxItem item)
            {
                item.IsSelected = true;
                item.Focus();
            }
        };
        layers.ContextMenuOpening += (_, _) =>
        {
            foreach (MenuItem item in layers.ContextMenu.Items)
                item.IsEnabled = !busy && session != null && layers.SelectedItem is Layer;
        };

        files.ContextMenu = Menu(
            ("Save", () => SaveSelection(false)),
            ("Export", () => SaveSelection(true)));
        files.PreviewMouseRightButtonDown += (_, e) =>
        {
            var row = Ancestor<DataGridRow>(e.OriginalSource as DependencyObject);
            if (row != null)
            {
                if (!row.IsSelected)
                {
                    files.SelectedItems.Clear();
                    row.IsSelected = true;
                }

                row.Focus();
            }
            else
            {
                files.SelectedItems.Clear();
            }
        };
        files.ContextMenuOpening += (_, _) =>
        {
            foreach (MenuItem item in files.ContextMenu.Items)
                item.IsEnabled = !busy && session != null && files.SelectedItems.Cast<ExplorerRow>().Any(r => r.Name != "..");
        };

        var panel = new DockPanel
        {
            Margin = new Thickness(8, 0, 8, 8),
            Background = Theme.Get(this, "PreviewSurface"),
        };
        var controls = new WrapPanel { Margin = new Thickness(8) };
        DockPanel.SetDock(controls, Dock.Bottom);
        panel.Children.Add(controls);
        foreach (var (label, run) in new (string, Action)[]
        {
            ("Play", () =>
            {
                if (media.Source != null)
                    media.Play();
            }),
            ("Pause", () => media.Pause()),
            ("Stop", () => media.Stop()),
        })
        {
            var button = new Button
            {
                Content = label,
                Margin = new Thickness(0, 0, 8, 0),
            };
            button.Click += (_, _) => run();
            controls.Children.Add(button);
        }

        seek.Margin = new Thickness(0, 0, 8, 0);
        controls.Children.Add(seek);
        controls.Children.Add(new TextBlock { Text = "Volume", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });
        var volume = new Slider
        {
            Minimum = 0,
            Maximum = 1,
            Value = 0.7,
            Width = 90,
            VerticalAlignment = VerticalAlignment.Center,
        };
        volume.ValueChanged += (_, _) => media.Volume = volume.Value;
        controls.Children.Add(volume);
        DockPanel.SetDock(mediaStatus, Dock.Top);
        panel.Children.Add(mediaStatus);
        panel.Children.Add(media);
        previewTabs.Items.Add(new TabItem { Header = "Video / Audio", Content = panel });

        media.MediaOpened += (_, _) =>
        {
            if (media.NaturalDuration.HasTimeSpan)
            {
                seek.Maximum = Math.Max(1, media.NaturalDuration.TimeSpan.TotalSeconds);
                seek.IsEnabled = true;
                mediaStatus.Text = "Ready — " + media.NaturalDuration.TimeSpan.ToString(@"hh\:mm\:ss");
            }
            else
            {
                mediaStatus.Text = "Ready";
            }
        };
        media.MediaFailed += (_, e) =>
        {
            mediaStatus.Text = "Cannot play this media with the available Windows codecs. Export the file to view it in another player. " + e.ErrorException.Message;
            mediaTimer.Stop();
            seek.IsEnabled = false;
        };
        media.MediaEnded += (_, _) =>
        {
            media.Stop();
            seek.Value = 0;
        };
        seek.ValueChanged += (_, _) =>
        {
            if (seek.IsMouseCaptureWithin && media.Source != null && media.NaturalDuration.HasTimeSpan)
                media.Position = TimeSpan.FromSeconds(seek.Value);
        };
        mediaTimer.Tick += (_, _) =>
        {
            if (!seek.IsMouseCaptureWithin && media.Source != null && media.NaturalDuration.HasTimeSpan)
                seek.Value = Math.Min(seek.Maximum, media.Position.TotalSeconds);
        };
        previewTabs.SelectionChanged += (_, e) =>
        {
            if (e.Source == previewTabs && previewTabs.SelectedIndex != 2)
                media.Pause();
        };
    }

    void ResetMedia()
    {
        mediaTimer.Stop();
        media.Close();
        media.Source = null;
        seek.IsEnabled = false;
        seek.Value = 0;
        mediaStatus.Text = "Select a video or audio file.";
    }
}
