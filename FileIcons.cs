using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace DockerImageExplorer;

public static class FileIcons
{
    const uint FileAttributeDirectory = 0x10;
    const uint FileAttributeNormal = 0x80;
    const uint Icon = 0x100;
    const uint SmallIcon = 0x1;
    const uint UseFileAttributes = 0x10;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct Info
    {
        public IntPtr Icon;
        public int Index;
        public uint Attributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string DisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string TypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    static extern IntPtr SHGetFileInfo(string path, uint attributes, out Info info, uint size, uint flags);

    [DllImport("user32.dll")]
    static extern bool DestroyIcon(IntPtr icon);

    static readonly Dictionary<string, ImageSource?> cache = new();

    public static ImageSource? Get(ExplorerItem item)
    {
        var key = item.IsDirectory ? "folder" : item.Type == "Link" ? ".lnk" : item.Extension;
        if (cache.TryGetValue(key, out var existing))
            return existing;

        SHGetFileInfo(
            item.IsDirectory ? "folder" : "file" + key,
            item.IsDirectory ? FileAttributeDirectory : FileAttributeNormal,
            out var info,
            (uint)Marshal.SizeOf<Info>(),
            Icon | SmallIcon | UseFileAttributes);

        ImageSource? image = null;
        if (info.Icon != IntPtr.Zero)
        {
            try
            {
                image = Imaging.CreateBitmapSourceFromHIcon(info.Icon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                image.Freeze();
            }
            finally
            {
                DestroyIcon(info.Icon);
            }
        }

        cache[key] = image;
        return image;
    }
}

public record ExplorerRow(ExplorerItem Item)
{
    public string Name => Item.Name;
    public string Path => Item.Path;
    public string Action => Item.Action;
    public string Type => Item.Type;
    public string Size => Item.Size;
    public ImageSource? Icon => FileIcons.Get(Item);
}
