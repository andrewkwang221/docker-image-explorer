using System.Formats.Tar;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Data;
using Microsoft.Win32;
namespace DockerImageExplorer;
public static class App { [STAThread] public static void Main() {
    var english = System.Globalization.CultureInfo.GetCultureInfo("en-US");
    System.Globalization.CultureInfo.DefaultThreadCurrentCulture = english;
    System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = english;
    System.Threading.Thread.CurrentThread.CurrentCulture = english;
    System.Threading.Thread.CurrentThread.CurrentUICulture = english;
    FrameworkElement.LanguageProperty.OverrideMetadata(typeof(FrameworkElement),new FrameworkPropertyMetadata(System.Windows.Markup.XmlLanguage.GetLanguage("en-US")));
    new Application().Run(new Viewer());
} }
public sealed partial class Viewer : Window {
    readonly ListBox layers = new() { Margin = new Thickness(8) };
    readonly DataGrid files = new() { IsReadOnly = true, AutoGenerateColumns = false, SelectionMode = DataGridSelectionMode.Extended, SelectionUnit = DataGridSelectionUnit.FullRow, Margin = new Thickness(8), EnableRowVirtualization = true, CanUserAddRows = false };
    readonly TextBox preview = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, FontFamily = new FontFamily("Consolas"), Margin = new Thickness(8) };
    readonly Image picture = new() { Stretch = Stretch.Uniform, Margin = new Thickness(8) };
    readonly TabControl previewTabs = new();
    readonly TextBlock details = new() { Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap };
    readonly TextBox search = new() { Width = 190, Margin = new Thickness(4), ToolTip = "Search filename or path. Wildcards: *.png, filename.mp4, photo?.jpg" };
    readonly ComboBox type = new() { Width = 110, Margin = new Thickness(4), ItemsSource = ExplorerModel.FileTypes, SelectedIndex = 0 };
    readonly ComboBox action = new() { Width = 115, Margin = new Thickness(4), ItemsSource = new[] { "All changes", "Added", "Modified", "Deleted", "Unchanged" }, SelectedIndex = 0 };
    readonly CheckBox merged = new() { Content = "Include unchanged files", IsChecked = true, Margin = new Thickness(8) };
    readonly TextBlock status = new() { Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap, Text = "Open a Docker save or OCI archive. Ctrl/Shift selects multiple items." };
    readonly TextBlock command = new() { Margin = new Thickness(8), TextWrapping = TextWrapping.Wrap };
    readonly WrapPanel toolbar = new();
    readonly TextBox location = new() { IsReadOnly = true, Margin = new Thickness(4), VerticalContentAlignment = VerticalAlignment.Center };
    ImageSession? session; bool busy; string currentPath = ""; int previewVersion;
    public Viewer() {
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/DockerImageExplorer;component/AppIcon.ico")); Title = "DockerImageExplorer 1.0"; Width = 1380; Height = 900; MinWidth = 900; MinHeight = 600;
        Background = new SolidColorBrush(Color.FromRgb(245,247,250));
        var root = new DockPanel(); Content = root; DockPanel.SetDock(toolbar,Dock.Top); root.Children.Add(toolbar);
        Button("Open archive", Open); Button("Export selected", () => SaveSelection(false)); Button("Export selection as tar", () => SaveSelection(true));
        Button("Export layer", () => Export(0)); Button("Export filesystem", () => Export(1)); Button("Export entire filesystem", () => Export(2));
        Button("Image metadata", () => { preview.Text = session?.Description ?? "Open an image first."; previewTabs.SelectedIndex = 0; return Task.CompletedTask; });
        DockPanel.SetDock(status,Dock.Bottom); root.Children.Add(status);
        var grid = new Grid(); grid.ColumnDefinitions.Add(new() { Width = new GridLength(255) }); grid.ColumnDefinitions.Add(new() { Width = new GridLength(1,GridUnitType.Star) }); root.Children.Add(grid);
        var left = new Grid(); left.RowDefinitions.Add(new() { Height = GridLength.Auto }); left.RowDefinitions.Add(new() { Height = new GridLength(1,GridUnitType.Star) }); left.RowDefinitions.Add(new() { Height = new GridLength(140) }); grid.Children.Add(left);
        left.Children.Add(new TextBlock { Text = "IMAGE LAYERS · oldest first", Margin = new Thickness(12), FontWeight = FontWeights.Bold });
        ScrollViewer.SetVerticalScrollBarVisibility(layers,ScrollBarVisibility.Auto); ScrollViewer.SetHorizontalScrollBarVisibility(layers,ScrollBarVisibility.Auto); Grid.SetRow(layers,1); left.Children.Add(layers);
        var commandScroll = new ScrollViewer { Content = command, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }; Grid.SetRow(commandScroll,2); left.Children.Add(commandScroll);
        var right = new Grid(); Grid.SetColumn(right,1); grid.Children.Add(right);
        foreach (var height in new[] { GridLength.Auto, GridLength.Auto, new GridLength(3,GridUnitType.Star), new GridLength(5), new GridLength(2,GridUnitType.Star) }) right.RowDefinitions.Add(new() { Height = height });
        var filter = new WrapPanel { Margin = new Thickness(4) }; right.Children.Add(filter);
        filter.Children.Add(new TextBlock { Text = "Search", VerticalAlignment = VerticalAlignment.Center }); filter.Children.Add(search); filter.Children.Add(type); filter.Children.Add(action); filter.Children.Add(merged);
        var nav = new DockPanel(); Grid.SetRow(nav,1); right.Children.Add(nav);
        var up = new Button { Content = "↑ Up", Padding = new Thickness(12,4,12,4), Margin = new Thickness(4) }; up.Click += (_,_) => GoUp(); nav.Children.Add(up);
        var home = new Button { Content = "⌂ Root", Margin = new Thickness(4), Padding = new Thickness(12,4,12,4) }; home.Click += (_,_) => { currentPath = ""; UpdateFiles(); }; nav.Children.Add(home); nav.Children.Add(location);
        var icon = new FrameworkElementFactory(typeof(Image)); icon.SetValue(Image.WidthProperty,20.0); icon.SetValue(Image.HeightProperty,20.0); icon.SetBinding(Image.SourceProperty,new Binding("Icon"));
        files.Columns.Add(new DataGridTemplateColumn { Header = "", CellTemplate = new DataTemplate { VisualTree = icon }, Width = 34 });
        files.Columns.Add(new DataGridTextColumn { Header = "Name", Binding = new Binding("Name"), Width = new DataGridLength(1,DataGridLengthUnitType.Star) });
        var badge = new FrameworkElementFactory(typeof(Border)); badge.SetValue(Border.CornerRadiusProperty,new CornerRadius(4)); badge.SetValue(Border.PaddingProperty,new Thickness(6,2,6,2)); badge.SetValue(Border.MarginProperty,new Thickness(3));
        var tagText = new FrameworkElementFactory(typeof(TextBlock)); tagText.SetBinding(TextBlock.TextProperty,new Binding("Action")); badge.AppendChild(tagText);
        var badgeStyle = new Style(typeof(Border)); foreach (var (value,color) in new[] { ("Added","#D5F5E3"), ("Modified","#FFF0BC"), ("Deleted","#FFDADB"), ("Unchanged","#E7EBF0") }) { var trigger = new DataTrigger { Binding = new Binding("Action"), Value = value }; trigger.Setters.Add(new Setter(Border.BackgroundProperty,(Brush)new BrushConverter().ConvertFromString(color)!)); badgeStyle.Triggers.Add(trigger); } badge.SetValue(Border.StyleProperty,badgeStyle);
        files.Columns.Add(new DataGridTemplateColumn { Header = "Change", CellTemplate = new DataTemplate { VisualTree = badge }, Width = 110 });
        foreach (var (title,binding,width) in new[] { ("Type","Type",110), ("Bytes","Size",100), ("Path","Path",260) }) files.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(binding), Width = width });
        Grid.SetRow(files,2); right.Children.Add(files); var splitter = new GridSplitter { Height = 5, HorizontalAlignment = HorizontalAlignment.Stretch }; Grid.SetRow(splitter,3); right.Children.Add(splitter);
        var previewPanel = new DockPanel(); DockPanel.SetDock(details,Dock.Top); previewPanel.Children.Add(details); previewPanel.Children.Add(previewTabs); previewTabs.Items.Add(new TabItem { Header = "Text / Hex", Content = preview }); previewTabs.Items.Add(new TabItem { Header = "Image", Content = picture }); Grid.SetRow(previewPanel,4); right.Children.Add(previewPanel);
        SetupInteractions();
        layers.SelectionChanged += (_,_) => { currentPath = ""; UpdateFiles(); }; search.TextChanged += (_,_) => UpdateFiles(); type.SelectionChanged += (_,_) => UpdateFiles(); action.SelectionChanged += (_,_) => UpdateFiles(); merged.Checked += (_,_) => UpdateFiles(); merged.Unchecked += (_,_) => UpdateFiles();
        files.SelectionChanged += async (_,_) => await ShowFile(); files.MouseDoubleClick += (_,_) => { if (files.SelectedItem is ExplorerRow row && row.Item.IsDirectory) { if (row.Name == "..") GoUp(); else { currentPath = row.Path; ClearFilters(); UpdateFiles(); } } };
        files.PreviewKeyDown += (_,e) => { if (e.Key == Key.Enter && files.SelectedItem is ExplorerRow row && row.Item.IsDirectory) { if (row.Name == "..") GoUp(); else { currentPath = row.Path; ClearFilters(); UpdateFiles(); } e.Handled = true; } };
        Closing += (_,e) => { if (busy) { e.Cancel = true; status.Text = "Wait for the current operation to finish."; } else { ResetMedia(); session?.Dispose(); } };
    }
    void ClearFilters() { search.Clear(); type.SelectedIndex = 0; action.SelectedIndex = 0; }
    void GoUp() { currentPath = ExplorerModel.Parent(currentPath); ClearFilters(); UpdateFiles(); files.Focus(); }
    void Button(string text, Func<Task> operation) { var button = new Button { Content = text, Padding = new Thickness(10,6,10,6), Margin = new Thickness(3) }; button.Click += async (_,_) => await Work(operation); toolbar.Children.Add(button); }
    async Task Work(Func<Task> operation) { if (busy) return; busy = true; toolbar.IsEnabled = false; files.IsEnabled = false; layers.IsEnabled = false; try { await operation(); } catch (Exception ex) { status.Text = "Operation failed."; MessageBox.Show(this,ex.Message,Title,MessageBoxButton.OK,MessageBoxImage.Error); } finally { busy = false; toolbar.IsEnabled = true; files.IsEnabled = true; layers.IsEnabled = true; } }
    async Task Open() {
        var dialog = new OpenFileDialog { Filter = "Image archives|*.tar;*.tar.gz;*.tgz|All files|*.*" }; if (dialog.ShowDialog(this) != true) return;
        status.Text = "Loading image…"; var next = new ImageSession(); try { await Task.Run(() => next.LoadArchive(dialog.FileName)); } catch { next.Dispose(); throw; }
        ++previewVersion; ResetMedia(); mediaCopies.Clear(); session?.Dispose(); session = next; currentPath = ""; layers.ItemsSource = next.Layers; layers.SelectedIndex = next.Layers.Count - 1; Title = "DockerImageExplorer 1.0 — " + dialog.FileName;
        if (next.Layers.Count == 0) { files.ItemsSource = null; preview.Text = next.Description; details.Text = "Empty filesystem"; picture.Source = null; }
    }
    void UpdateFiles() {
        ++previewVersion; ResetMedia(); preview.Clear(); picture.Source = null; details.Text = "Select a file to preview. Double-click a folder to open it. Ctrl/Shift selects multiple items.";
        if (layers.SelectedItem is not Layer layer) return;
        command.Text = layer.Command;
        bool searching = search.Text.Length > 0 || type.SelectedIndex > 0 || action.SelectedIndex > 0;
        var items = ExplorerModel.Build(layer,merged.IsChecked != true);
        var prefix = currentPath.Length == 0 ? "" : currentPath + "/";
        var rows = items.Where(i => searching || i.Path.StartsWith(prefix,StringComparison.Ordinal) && !i.Path[prefix.Length..].Contains('/'))
            .Where(i => ExplorerModel.Matches(i,search.Text,type.SelectedItem as string ?? "All types",action.SelectedItem as string ?? "All changes"))
            .OrderByDescending(i => i.IsDirectory).ThenBy(i => i.Path,StringComparer.Ordinal).Select(i => new ExplorerRow(i)).ToList();
        if (currentPath.Length > 0) rows.Insert(0,new ExplorerRow(new ExplorerItem(currentPath + "/..",true,"",null)));
        files.ItemsSource = rows; location.Text = searching ? "/ — Search results across " + layer.Title : "/" + currentPath;
        status.Text = $"{layer.Title}: {layer.Changes.Count(c => c.Action == "Added")} added · {layer.Changes.Count(c => c.Action == "Modified")} modified · {layer.Changes.Count(c => c.Action == "Deleted")} deleted · {rows.Count} visible items";
    }
    async Task ShowFile() {
        var version = ++previewVersion; ResetMedia(); picture.Source = null; preview.Clear();
        if (files.SelectedItems.Count != 1 || files.SelectedItem is not ExplorerRow row || layers.SelectedItem is not Layer layer) { details.Text = $"{files.SelectedItems.Count} items selected"; return; }
        details.Text = $"/{row.Path} · {row.Action} · {row.Type}";
        if (row.Item.IsDirectory) { preview.Text = "Double-click to open this folder. Export selected includes its contents recursively."; previewTabs.SelectedIndex = 0; return; }
        if (row.Item.File is not ImageFile original) return;
        var file = ImageSession.Resolve(original,layer.Files);
        details.Text += $" · {file.Size:N0} bytes · mode {Convert.ToString((int)original.Mode,8)} · UID {original.Uid} · GID {original.Gid}\nModified: {original.Modified:u}" + (original.Link.Length > 0 ? " · Target: " + original.Link : "");
        if (file.Blob == null) { preview.Text = "This entry has no regular file content."; previewTabs.SelectedIndex = 0; return; }
        try {
            var data = await Task.Run(() => { using var input = File.OpenRead(file.Blob); var bytes = new byte[(int)Math.Min(input.Length,256*1024)]; input.ReadExactly(bytes); return bytes; });
            if (version != previewVersion) return;
            bool imageType = new[] { ".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".ico" }.Contains(row.Item.Extension);
            bool binary = data.Take(8192).Contains((byte)0); string text;
            try { text = data.Length >= 2 && (data[0] == 0xff && data[1] == 0xfe || data[0] == 0xfe && data[1] == 0xff) ? (data[0] == 0xff ? Encoding.Unicode : Encoding.BigEndianUnicode).GetString(data[2..]) : new UTF8Encoding(false,true).GetString(data); if (data.Length >= 2 && (data[0] == 0xff && data[1] == 0xfe || data[0] == 0xfe && data[1] == 0xff)) binary = false; } catch (DecoderFallbackException) { binary = true; text = ""; }
            preview.Text = binary ? "Binary content (first 4 KiB):\n" + string.Join('\n',data.Take(4096).Chunk(16).Select((chunk,i) => $"{i*16:X8}  {Convert.ToHexString(chunk)}")) : text;
            if (file.Size > data.Length) preview.Text += "\n[Text preview limited to 256 KiB. Downloads preserve the full content.]";
            previewTabs.SelectedIndex = 0;
            if (ExplorerModel.Matches(row.Item,"","Videos","All changes") || ExplorerModel.Matches(row.Item,"","Audio","All changes")) {
                var owner = session!; var key = file.Blob + row.Item.Extension;
                if (!mediaCopies.TryGetValue(key,out var mediaPath)) {
                    mediaPath = Path.Combine(owner.DirectoryPath,"preview-" + Guid.NewGuid().ToString("N") + row.Item.Extension);
                    await Task.Run(() => File.Copy(file.Blob,mediaPath));
                    if (version != previewVersion || session != owner) return;
                    mediaCopies[key] = mediaPath;
                }
                if (version != previewVersion) return;
                mediaStatus.Text = "Press Play to preview. Playback depends on codecs available in Windows.";
                media.Source = new Uri(mediaPath); media.Pause(); previewTabs.SelectedIndex = 2; mediaTimer.Start();
            } else if (imageType) {
                if (file.Size > 32*1024*1024) { preview.Text = "Image exceeds the 32 MiB preview limit. Download to view the full image.\n" + preview.Text; return; }
                var image = await Task.Run(() => { using var input = File.OpenRead(file.Blob); var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 1600; bitmap.StreamSource = input; bitmap.EndInit(); bitmap.Freeze(); return bitmap; });
                if (version == previewVersion) { picture.Source = image; previewTabs.SelectedIndex = 1; }
            }
        } catch (Exception ex) { if (version == previewVersion) preview.Text += "\nPreview unavailable: " + ex.Message; }
    }
    async Task SaveSelection(bool tar) {
        if (session == null || layers.SelectedItem is not Layer layer || files.SelectedItems.Count == 0) throw new InvalidOperationException("Select one or more files or folders first.");
        var selected = files.SelectedItems.Cast<ExplorerRow>().Where(r => r.Name != "..").Select(r => r.Item).ToArray();
        var content = ExplorerModel.Selection(layer,selected,ExplorerModel.Build(layer,false));
        if (content.Count == 0) throw new InvalidOperationException("The selection contains no exportable entries.");
        if (tar) {
            var dialog = new SaveFileDialog { Filter = "Tar archive|*.tar", FileName = "selected-files.tar" }; if (dialog.ShowDialog(this) != true) return;
            var temp = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try { await Task.Run(() => { ImageSession.ExportFilesystem(temp,content); File.Move(temp,dialog.FileName,true); }); } finally { if (File.Exists(temp)) File.Delete(temp); }
            status.Text = $"Saved {content.Count} entries to {dialog.FileName}";
        } else {
            var dialog = new OpenFolderDialog { Title = "Choose export location" }; if (dialog.ShowDialog(this) != true) return;
            var destination = Path.Combine(dialog.FolderName,"DockerImageExplorer-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N")[..6]);
            await Task.Run(() => ExplorerModel.Download(destination,content)); status.Text = $"Exported {content.Count} entries to {destination}";
        }
    }
    async Task Export(int kind) {
        if (session == null) throw new InvalidOperationException("Open an image first.");
        var layer = layers.SelectedItem as Layer;
        if (kind < 2 && layer == null) throw new InvalidOperationException("Select a layer first.");
        var dialog = new SaveFileDialog { Filter = kind == 3 ? "Image archive|*.*" : "Tar archive (*.tar)|*.tar", FileName = kind == 3 ? Path.GetFileName(session.SourceArchive) : kind == 0 ? $"layer-{layers.SelectedIndex + 1}.tar" : kind == 1 ? $"filesystem-layer-{layers.SelectedIndex + 1}.tar" : "image-filesystem.tar" };
        if (dialog.ShowDialog(this) != true) return;
        status.Text = "Exporting…";
        // Write alongside the destination and replace only after successful completion.
        var temp = dialog.FileName + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try {
            await Task.Run(() => { if (kind == 3) File.Copy(session.SourceArchive, temp); else if (kind == 0) { using var input = File.OpenRead(layer!.Archive); int a = input.ReadByte(), b = input.ReadByte(); input.Position = 0; using var output = File.Create(temp); if (a == 31 && b == 139) { using var gzip = new System.IO.Compression.GZipStream(input, System.IO.Compression.CompressionMode.Decompress); gzip.CopyTo(output); } else input.CopyTo(output); } else ImageSession.ExportFilesystem(temp, kind == 1 ? layer!.Files : session.Layers.LastOrDefault()?.Files ?? new Dictionary<string, ImageFile>()); File.Move(temp, dialog.FileName, true); });
            status.Text = $"Exported: {dialog.FileName}";
        } finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}







