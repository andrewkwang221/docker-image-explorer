using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using DockerImageExplorer;

class Program
{
    [STAThread]
    static void Main()
    {
        var app = new Application();
        var viewer = new Viewer();
        var flags = BindingFlags.NonPublic | BindingFlags.Instance;
        T Field<T>(string name) => (T)typeof(Viewer).GetField(name, flags)!.GetValue(viewer)!;
        void Assert(bool value, string message)
        {
            if (!value)
                throw new Exception(message);
        }

        var layers = Field<ListBox>("layers");
        var files = Field<DataGrid>("files");
        var search = Field<TextBox>("search");
        var layer = new Layer { Title = "Layer 1 (Final)", Archive = "", Command = "" };
        layers.ItemsSource = new[] { layer };
        layers.SelectedIndex = 0;
        typeof(Viewer).GetField("currentPath", flags)!.SetValue(viewer, "a/b");
        typeof(Viewer).GetMethod("UpdateFiles", flags)!.Invoke(viewer, null);
        Assert(files.Items.Cast<ExplorerRow>().Single().Name == "..", "Parent entry missing");

        using var source = new HwndSource(new HwndSourceParameters("Navigation test") { Width = 1, Height = 1, WindowStyle = 0 });
        var back = new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Back) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
        files.RaiseEvent(back);
        Assert(Field<string>("currentPath") == "a" && back.Handled, "Backspace did not navigate from explorer");

        search.Text = "abc";
        search.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, Key.Back) { RoutedEvent = Keyboard.PreviewKeyDownEvent });
        Assert(Field<string>("currentPath") == "a", "Backspace in search navigated unexpectedly");
        Assert(
            layers.ContextMenu.Items.Cast<MenuItem>().Select(i => (string)i.Header).SequenceEqual(new[]
            {
                "Export layer",
                "Save layer",
                "Export file system",
                "Save file system",
                "Export entire filesystem",
                "Save entire filesystem",
            }),
            "Layer menu incorrect");
        Assert(
            files.ContextMenu.Items.Cast<MenuItem>().Select(i => (string)i.Header).SequenceEqual(new[] { "Save", "Export" }),
            "Explorer menu incorrect");

        var menuLabels = Field<MenuItem>("exportMenu").Items.OfType<MenuItem>().Select(i => (string)i.Header).ToList();
        Assert(
            menuLabels.Contains("Save layer")
                && menuLabels.Contains("Save filesystem")
                && menuLabels.Contains("Save entire filesystem")
                && !menuLabels.Contains("Copy image archive"),
            "Export menu incorrect");
        var fileMenu = Field<Menu>("mainMenu").Items.OfType<MenuItem>().First();
        Assert(fileMenu.Items.OfType<MenuItem>().Any(i => (string)i.Header == "Open archive"), "Open command missing");
        Assert(Field<TabControl>("previewTabs").Items.Cast<TabItem>().Any(t => (string)t.Header == "Video / Audio"), "Media preview missing");
        Assert(Field<MediaElement>("media").LoadedBehavior == MediaState.Manual, "Media playback should be controlled manually");
        viewer.Close();
        app.Shutdown();
        Console.WriteLine("PASS: routed Backspace navigation, editable search exclusion, parent folder, context menus, Export menu labels, media controls.");
    }
}
