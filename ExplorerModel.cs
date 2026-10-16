using System.Formats.Tar;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace DockerImageExplorer;

public record ExplorerItem(string Path, bool IsDirectory, string Action, ImageFile? File)
{
    public string Name => Path.Split('/')[^1];
    public string Extension => IsDirectory ? "" : System.IO.Path.GetExtension(Name).ToLowerInvariant();
    public string Type => IsDirectory
        ? "Folder"
        : File?.Type is TarEntryType.SymbolicLink or TarEntryType.HardLink
            ? "Link"
            : Extension.Length > 0 ? Extension[1..].ToUpperInvariant() + " file" : "File";
    public string Size => IsDirectory ? "" : $"{File?.Size ?? 0:N0}";
}

public static class ExplorerModel
{
    public static readonly string[] FileTypes =
    [
        "All types",
        "Folders",
        "Files",
        "Images",
        "Videos",
        "Audio",
        "Text",
        "Documents",
        "Archives",
        "Source code",
        "Configuration",
        "Executables",
        "Fonts",
        "Links",
        "Other",
    ];

    static readonly Dictionary<string, string[]> Categories = new()
    {
        ["Images"] = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".ico", ".svg", ".webp", ".avif", ".heic", ".psd"],
        ["Videos"] = [".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".m4v", ".mpeg", ".mpg", ".ts", ".flv", ".3gp"],
        ["Audio"] = [".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma", ".opus", ".aiff", ".mid"],
        ["Text"] = [".txt", ".log", ".md", ".rst", ".csv", ".tsv"],
        ["Documents"] = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp", ".rtf", ".epub"],
        ["Archives"] = [".zip", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".zst", ".7z", ".rar", ".jar", ".deb", ".rpm"],
        ["Source code"] =
        [
            ".cs", ".py", ".js", ".ts", ".tsx", ".jsx", ".java", ".c", ".cpp", ".h", ".hpp",
            ".go", ".rs", ".rb", ".php", ".sh", ".ps1", ".html", ".css", ".sql", ".vue", ".svelte",
        ],
        ["Configuration"] = [".json", ".yaml", ".yml", ".xml", ".toml", ".ini", ".conf", ".config", ".env", ".properties"],
        ["Executables"] = [".exe", ".dll", ".so", ".bin", ".bat", ".cmd", ".com", ".msi", ".appimage"],
        ["Fonts"] = [".ttf", ".otf", ".woff", ".woff2", ".eot"],
    };

    public static List<ExplorerItem> Build(Layer layer, bool changesOnly)
    {
        var actions = layer.Changes.GroupBy(c => c.Path).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var result = new Dictionary<string, ExplorerItem>(StringComparer.Ordinal);
        foreach (var file in layer.Files.Values)
        {
            var action = actions.TryGetValue(file.Path, out var change) && change.Action != "Deleted" ? change.Action : "Unchanged";
            if (!changesOnly || action != "Unchanged")
                result[file.Path] = new(file.Path, file.Type == TarEntryType.Directory, action, file);
        }

        foreach (var change in actions.Values.Where(c => c.Action == "Deleted"))
        {
            if (!result.ContainsKey(change.Path))
                result[change.Path] = new(change.Path, change.File?.Type == TarEntryType.Directory, "Deleted", change.File);
        }

        foreach (var item in result.Values.ToArray())
        {
            var index = item.Path.LastIndexOf('/');
            while (index > 0)
            {
                var parent = item.Path[..index];
                result.TryAdd(parent, new(parent, true, "", null));
                index = parent.LastIndexOf('/');
            }
        }

        return result.Values.ToList();
    }

    public static string Parent(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";

    public static bool Matches(ExplorerItem item, string query, string type, string action)
    {
        bool nameMatch;
        if (query.Contains('*') || query.Contains('?'))
        {
            var pattern = "^" + Regex.Escape(query).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            var target = query.Contains('/') ? item.Path : item.Name;
            nameMatch = Regex.IsMatch(
                target,
                pattern,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100));
        }
        else
        {
            nameMatch = item.Path.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        bool typeMatch = type switch
        {
            "All types" => true,
            "Folders" => item.IsDirectory,
            "Files" => !item.IsDirectory,
            "Links" => item.Type == "Link",
            "Other" => !item.IsDirectory
                && item.Type != "Link"
                && !Categories.Values.Any(exts => exts.Contains(item.Extension)),
            _ => !item.IsDirectory
                && Categories.TryGetValue(type, out var extensions)
                && extensions.Contains(item.Extension),
        };

        return nameMatch && typeMatch && (action == "All changes" || item.Action == action);
    }

    public static Dictionary<string, ImageFile> Selection(Layer layer, IEnumerable<ExplorerItem> selected, IEnumerable<ExplorerItem> all)
    {
        var roots = selected.ToArray();
        var result = new Dictionary<string, ImageFile>(StringComparer.Ordinal);
        foreach (var item in all)
        {
            if (item.File == null || !roots.Any(r => item.Path == r.Path || r.IsDirectory && item.Path.StartsWith(r.Path + "/", StringComparison.Ordinal)))
                continue;

            result[item.Path] = item.File;
        }

        // Resolved links retain full content even if their targets were not selected.
        foreach (var item in result.Values.ToArray())
        {
            var resolved = ImageSession.Resolve(item, layer.Files);
            if (item.Type == TarEntryType.HardLink && resolved.Blob != null)
                result[item.Path] = item with { Blob = resolved.Blob, Size = resolved.Size };
        }

        return result;
    }

    public static Dictionary<string, ImageFile> ChangedFiles(Layer layer)
    {
        var result = new Dictionary<string, ImageFile>(StringComparer.Ordinal);
        foreach (var change in layer.Changes)
        {
            if (change.Action == "Deleted" || change.File == null)
                result.Remove(change.Path);
            else
                result[change.Path] = change.File;
        }

        return result;
    }

    public readonly record struct WindowsSaveResult(int Entries, int SymbolicLinks, int SpecialFiles)
    {
        public string Describe(string destination)
        {
            var text = $"Saved {Entries} entries to {destination}";
            if (SymbolicLinks > 0)
                text += ". Symbolic links were saved as Windows links or as the files and folders they point to.";
            if (SpecialFiles > 0)
                text += $" {SpecialFiles} special Linux entries were saved as text notes.";

            return text;
        }
    }

    public static WindowsSaveResult SaveWindows(
        string destination,
        IReadOnlyDictionary<string, ImageFile> entries,
        IReadOnlyDictionary<string, ImageFile> filesystem)
    {
        var files = entries.Values.Where(file => file.Path.Length > 0).ToArray();
        RejectUnsupportedWindowsNames(destination, files);
        Directory.CreateDirectory(destination);

        var symlinkPaths = new HashSet<string>(
            files.Where(file => file.Type == TarEntryType.SymbolicLink).Select(file => file.Path),
            StringComparer.Ordinal);
        var symbolicLinks = 0;
        var specialFiles = 0;

        foreach (var file in files.Where(file => file.Type == TarEntryType.Directory && !LivesUnderSymlink(file.Path, symlinkPaths)))
            Directory.CreateDirectory(ToWindows(destination, file.Path));

        foreach (var file in files.Where(file => IsStoredFile(file) && !LivesUnderSymlink(file.Path, symlinkPaths)))
            WriteStoredFile(destination, file, filesystem);

        var pendingLinks = files.Where(file => file.Type == TarEntryType.SymbolicLink).ToList();
        while (pendingLinks.Count > 0)
        {
            var ready = pendingLinks.Where(link =>
            {
                var resolved = FollowSymlink(link.Path, link.Link, filesystem);
                return resolved == null || pendingLinks.All(other => other.Path != resolved);
            }).ToList();
            if (ready.Count == 0)
                ready = [pendingLinks[0]];

            foreach (var file in ready)
            {
                WriteSymbolicLink(destination, file, entries, filesystem);
                pendingLinks.Remove(file);
                symbolicLinks++;
            }
        }

        foreach (var file in files.Where(file => file.Type == TarEntryType.Directory && LivesUnderSymlink(file.Path, symlinkPaths)))
            Directory.CreateDirectory(ToWindows(destination, file.Path));

        foreach (var file in files.Where(file => IsStoredFile(file) && LivesUnderSymlink(file.Path, symlinkPaths)))
            WriteStoredFile(destination, file, filesystem);

        foreach (var file in files.Where(file => !IsStoredFile(file) && file.Type is not (TarEntryType.Directory or TarEntryType.SymbolicLink)))
        {
            var windows = ToWindows(destination, file.Path);
            EnsureParent(windows);
            File.WriteAllText(windows, DescribeSpecial(file));
            specialFiles++;
        }

        return new WindowsSaveResult(files.Length, symbolicLinks, specialFiles);
    }

    public static void Download(string destination, IReadOnlyDictionary<string, ImageFile> selected)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var components = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in selected.Values)
        {
            var parts = file.Path.Split('/');
            if (parts.Any(p => p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || p.EndsWith('.') || p.EndsWith(' ') || IsReserved(p)))
                throw new IOException("This selection contains names Windows cannot represent. Use Save selection as tar instead.");

            var prefix = "";
            foreach (var part in parts)
            {
                prefix = prefix.Length == 0 ? part : prefix + "/" + part;
                if (components.TryGetValue(prefix, out var casing) && casing != prefix)
                    throw new IOException("Case-sensitive names conflict on Windows. Use Save selection as tar instead.");

                components[prefix] = prefix;
            }

            var target = Path.GetFullPath(Path.Combine(destination, file.Path.Replace('/', Path.DirectorySeparatorChar)));
            var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !targets.Add(target))
                throw new IOException("Conflicting Windows paths. Use Save selection as tar instead.");

            if (file.Type is not (TarEntryType.Directory or TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.HardLink)
                || (file.Type == TarEntryType.HardLink && file.Blob == null))
                throw new IOException("Links or special files cannot be downloaded as ordinary Windows files. Use Save selection as tar instead.");
        }

        foreach (var file in selected.Values.OrderBy(f => f.Path.Length))
        {
            var target = Path.Combine(destination, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (file.Type == TarEntryType.Directory)
            {
                Directory.CreateDirectory(target);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                if (file.Blob == null)
                    File.WriteAllBytes(target, []);
                else
                    File.Copy(file.Blob, target, false);
            }
        }
    }

    static void RejectUnsupportedWindowsNames(string destination, IReadOnlyList<ImageFile> files)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var components = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in files)
        {
            var parts = file.Path.Split('/');
            if (parts.Any(part => part.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || part.EndsWith('.') || part.EndsWith(' ') || IsReserved(part)))
                throw new IOException($"Windows cannot store \"{file.Path}\". Use Export instead.");

            var prefix = "";
            foreach (var part in parts)
            {
                prefix = prefix.Length == 0 ? part : prefix + "/" + part;
                if (components.TryGetValue(prefix, out var casing) && casing != prefix)
                    throw new IOException($"Case-sensitive names conflict on Windows at \"{file.Path}\". Use Export instead.");

                components[prefix] = prefix;
            }

            var target = Path.GetFullPath(ToWindows(destination, file.Path));
            var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase) || !targets.Add(target))
                throw new IOException($"Conflicting Windows paths at \"{file.Path}\". Use Export instead.");
        }
    }

    static void WriteStoredFile(string destination, ImageFile file, IReadOnlyDictionary<string, ImageFile> filesystem)
    {
        var windows = ToWindows(destination, file.Path);
        EnsureParent(windows);
        var resolved = ImageSession.Resolve(file, filesystem);
        if (resolved.Blob != null)
            File.Copy(resolved.Blob, windows, false);
        else if (file.Type == TarEntryType.HardLink)
            File.WriteAllText(windows, file.Link);
        else
            File.WriteAllBytes(windows, []);
    }

    static void WriteSymbolicLink(
        string destination,
        ImageFile link,
        IReadOnlyDictionary<string, ImageFile> entries,
        IReadOnlyDictionary<string, ImageFile> filesystem)
    {
        var windows = ToWindows(destination, link.Path);
        EnsureParent(windows);
        var resolved = FollowSymlink(link.Path, link.Link, filesystem);
        if (resolved != null
            && IsDirectoryTarget(resolved, filesystem)
            && DirectoryWillExist(resolved, entries)
            && TryLinkDirectory(windows, ToWindows(destination, resolved)))
            return;

        if (resolved != null && filesystem.TryGetValue(resolved, out var target) && target.Type != TarEntryType.Directory)
        {
            var content = ImageSession.Resolve(target, filesystem);
            var targetWindows = DirectoryWillExist(resolved, entries) || entries.ContainsKey(resolved)
                ? ToWindows(destination, resolved)
                : null;
            if (content.Blob != null && targetWindows != null && File.Exists(targetWindows) && TryLinkFile(windows, targetWindows))
                return;

            if (content.Blob != null)
            {
                File.Copy(content.Blob, windows, false);
                return;
            }
        }

        File.WriteAllText(windows, link.Link);
    }

    static bool IsStoredFile(ImageFile file) =>
        file.Type is TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.ContiguousFile
            or TarEntryType.SparseFile or TarEntryType.HardLink;

    static bool LivesUnderSymlink(string path, HashSet<string> symlinkPaths)
    {
        var index = path.LastIndexOf('/');
        while (index > 0)
        {
            if (symlinkPaths.Contains(path[..index]))
                return true;

            index = path.LastIndexOf('/', index - 1);
        }

        return false;
    }

    static bool IsDirectoryTarget(string path, IReadOnlyDictionary<string, ImageFile> filesystem)
    {
        if (filesystem.TryGetValue(path, out var file))
            return file.Type == TarEntryType.Directory;

        var prefix = path + "/";
        return filesystem.Keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal));
    }

    static bool DirectoryWillExist(string path, IReadOnlyDictionary<string, ImageFile> entries)
    {
        if (entries.TryGetValue(path, out var file) && file.Type == TarEntryType.Directory)
            return true;

        var prefix = path + "/";
        return entries.Keys.Any(key => key.StartsWith(prefix, StringComparison.Ordinal));
    }

    static string? FollowSymlink(string path, string link, IReadOnlyDictionary<string, ImageFile> filesystem)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var location = path;
        var text = link;
        string? resolved = null;
        for (var hop = 0; hop < 32; hop++)
        {
            resolved = CombineLinuxPath(location, text);
            if (resolved == null || !seen.Add(resolved))
                return null;

            if (!filesystem.TryGetValue(resolved, out var target) || target.Type != TarEntryType.SymbolicLink)
                return resolved;

            location = resolved;
            text = target.Link;
        }

        return null;
    }

    static string? CombineLinuxPath(string linkPath, string linkText)
    {
        if (linkText.Contains('\\') || linkText.Contains('\0'))
            return null;

        var raw = linkText.StartsWith('/')
            ? linkText
            : (linkPath.Contains('/') ? linkPath[..linkPath.LastIndexOf('/')] + "/" : "") + linkText;
        var parts = new List<string>();
        foreach (var part in raw.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == ".")
                continue;

            if (part == "..")
            {
                if (parts.Count == 0)
                    return null;

                parts.RemoveAt(parts.Count - 1);
                continue;
            }

            parts.Add(part);
        }

        return string.Join('/', parts);
    }

    static string DescribeSpecial(ImageFile file) => file.Type switch
    {
        TarEntryType.Fifo => "fifo",
        TarEntryType.CharacterDevice => $"character device {file.DeviceMajor}:{file.DeviceMinor}",
        TarEntryType.BlockDevice => $"block device {file.DeviceMajor}:{file.DeviceMinor}",
        _ => file.Type.ToString(),
    };

    static string ToWindows(string destination, string imagePath) =>
        Path.Combine(destination, imagePath.Replace('/', Path.DirectorySeparatorChar));

    static void EnsureParent(string windowsPath)
    {
        var parent = Path.GetDirectoryName(windowsPath);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);
    }

    static bool TryLinkFile(string linkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, Path.GetRelativePath(Path.GetDirectoryName(linkPath)!, targetPath));
            return true;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return CreateHardLink(linkPath, targetPath, IntPtr.Zero);
    }

    static bool TryLinkDirectory(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, Path.GetRelativePath(Path.GetDirectoryName(linkPath)!, targetPath));
            return true;
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }

        return TryCreateJunction(linkPath, targetPath);
    }

    static bool TryCreateJunction(string junctionPath, string targetPath)
    {
        if (File.Exists(junctionPath) || Directory.Exists(junctionPath))
            return false;

        try
        {
            Directory.CreateDirectory(junctionPath);
            if (SetJunction(junctionPath, Path.GetFullPath(targetPath)))
                return true;

            Directory.Delete(junctionPath);
            return false;
        }
        catch (IOException)
        {
            if (Directory.Exists(junctionPath))
                Directory.Delete(junctionPath, false);

            return false;
        }
        catch (UnauthorizedAccessException)
        {
            if (Directory.Exists(junctionPath))
                Directory.Delete(junctionPath, false);

            return false;
        }
    }

    static bool SetJunction(string junctionPath, string targetPath)
    {
        if (!targetPath.EndsWith('\\'))
            targetPath += '\\';

        var substitute = Encoding.Unicode.GetBytes(@"\??\" + targetPath);
        var print = Encoding.Unicode.GetBytes(targetPath);
        var dataLength = 8 + substitute.Length + 2 + print.Length + 2;
        var buffer = new byte[8 + dataLength];
        WriteUInt(buffer, 0, 0xA0000003);
        WriteUShort(buffer, 4, (ushort)dataLength);
        WriteUShort(buffer, 8, 0);
        WriteUShort(buffer, 10, (ushort)substitute.Length);
        WriteUShort(buffer, 12, (ushort)(substitute.Length + 2));
        WriteUShort(buffer, 14, (ushort)print.Length);
        Buffer.BlockCopy(substitute, 0, buffer, 16, substitute.Length);
        Buffer.BlockCopy(print, 0, buffer, 16 + substitute.Length + 2, print.Length);

        using var handle = CreateFile(
            junctionPath,
            0x40000000,
            FileShare.ReadWrite,
            IntPtr.Zero,
            FileMode.Open,
            0x02200000,
            IntPtr.Zero);
        if (handle.IsInvalid)
            return false;

        return DeviceIoControl(handle, 0x000900A4, buffer, buffer.Length, IntPtr.Zero, 0, out _, IntPtr.Zero);
    }

    static void WriteUInt(byte[] buffer, int offset, uint value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
        buffer[offset + 2] = (byte)(value >> 16);
        buffer[offset + 3] = (byte)(value >> 24);
    }

    static void WriteUShort(byte[] buffer, int offset, ushort value)
    {
        buffer[offset] = (byte)value;
        buffer[offset + 1] = (byte)(value >> 8);
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateHardLink(string fileName, string existingFileName, IntPtr securityAttributes);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern SafeFileHandle CreateFile(
        string fileName,
        uint access,
        FileShare share,
        IntPtr securityAttributes,
        FileMode creationDisposition,
        uint flags,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint controlCode,
        byte[] inBuffer,
        int inBufferSize,
        IntPtr outBuffer,
        int outBufferSize,
        out int bytesReturned,
        IntPtr overlapped);

    static bool IsReserved(string name)
    {
        var stem = name.Split('.')[0].ToUpperInvariant();
        return new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem)
            || stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && "123456789".Contains(stem[3]);
    }
}
