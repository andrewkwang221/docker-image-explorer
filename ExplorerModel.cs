using System.IO;
using System.Formats.Tar;
namespace DockerImageExplorer;
public record ExplorerItem(string Path, bool IsDirectory, string Action, ImageFile? File) {
    public string Name => Path.Split('/')[^1];
    public string Extension => IsDirectory ? "" : System.IO.Path.GetExtension(Name).ToLowerInvariant();
    public string Type => IsDirectory ? "Folder" : File?.Type is TarEntryType.SymbolicLink or TarEntryType.HardLink ? "Link" : Extension.Length > 0 ? Extension[1..].ToUpperInvariant() + " file" : "File";
    public string Size => IsDirectory ? "" : $"{File?.Size ?? 0:N0}";
}
public static class ExplorerModel {
    public static List<ExplorerItem> Build(Layer layer, bool changesOnly) {
        var actions = layer.Changes.GroupBy(c => c.Path).ToDictionary(g => g.Key, g => g.Last(), StringComparer.Ordinal);
        var result = new Dictionary<string, ExplorerItem>(StringComparer.Ordinal);
        foreach (var file in layer.Files.Values) {
            var action = actions.TryGetValue(file.Path, out var change) && change.Action != "Deleted" ? change.Action : "Unchanged";
            if (!changesOnly || action != "Unchanged") result[file.Path] = new(file.Path, file.Type == TarEntryType.Directory, action, file);
        }
        foreach (var change in actions.Values.Where(c => c.Action == "Deleted"))
            if (!result.ContainsKey(change.Path)) result[change.Path] = new(change.Path, change.File?.Type == TarEntryType.Directory, "Deleted", change.File);
        foreach (var item in result.Values.ToArray()) {
            var index = item.Path.LastIndexOf('/');
            while (index > 0) { var parent = item.Path[..index]; result.TryAdd(parent, new(parent, true, "", null)); index = parent.LastIndexOf('/'); }
        }
        return result.Values.ToList();
    }
    public static readonly string[] FileTypes = ["All types", "Folders", "Files", "Images", "Videos", "Audio", "Text", "Documents", "Archives", "Source code", "Configuration", "Executables", "Fonts", "Links", "Other"];
    static readonly Dictionary<string,string[]> Categories = new() {
        ["Images"] = [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tif", ".tiff", ".ico", ".svg", ".webp", ".avif", ".heic", ".psd"],
        ["Videos"] = [".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".m4v", ".mpeg", ".mpg", ".ts", ".flv", ".3gp"],
        ["Audio"] = [".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a", ".wma", ".opus", ".aiff", ".mid"],
        ["Text"] = [".txt", ".log", ".md", ".rst", ".csv", ".tsv"],
        ["Documents"] = [".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".odt", ".ods", ".odp", ".rtf", ".epub"],
        ["Archives"] = [".zip", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".zst", ".7z", ".rar", ".jar", ".deb", ".rpm"],
        ["Source code"] = [".cs", ".py", ".js", ".ts", ".tsx", ".jsx", ".java", ".c", ".cpp", ".h", ".hpp", ".go", ".rs", ".rb", ".php", ".sh", ".ps1", ".html", ".css", ".sql", ".vue", ".svelte"],
        ["Configuration"] = [".json", ".yaml", ".yml", ".xml", ".toml", ".ini", ".conf", ".config", ".env", ".properties"],
        ["Executables"] = [".exe", ".dll", ".so", ".bin", ".bat", ".cmd", ".com", ".msi", ".appimage"],
        ["Fonts"] = [".ttf", ".otf", ".woff", ".woff2", ".eot"]
    };
    public static string Parent(string path) => path.Contains('/') ? path[..path.LastIndexOf('/')] : "";
    public static bool Matches(ExplorerItem item, string query, string type, string action) {
        bool nameMatch;
        if (query.Contains('*') || query.Contains('?')) {
            var pattern = "^" + System.Text.RegularExpressions.Regex.Escape(query).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            nameMatch = System.Text.RegularExpressions.Regex.IsMatch(query.Contains('/') ? item.Path : item.Name,pattern,System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        } else nameMatch = item.Path.Contains(query,StringComparison.OrdinalIgnoreCase);
        bool typeMatch = type switch {
            "All types" => true, "Folders" => item.IsDirectory, "Files" => !item.IsDirectory,
            "Links" => item.Type == "Link", "Other" => !item.IsDirectory && item.Type != "Link" && !Categories.Values.Any(exts => exts.Contains(item.Extension)),
            _ => !item.IsDirectory && Categories.TryGetValue(type,out var extensions) && extensions.Contains(item.Extension)
        };
        return nameMatch && typeMatch && (action == "All changes" || item.Action == action);
    }
    public static Dictionary<string, ImageFile> Selection(Layer layer, IEnumerable<ExplorerItem> selected, IEnumerable<ExplorerItem> all) {
        var roots = selected.ToArray(); var result = new Dictionary<string, ImageFile>(StringComparer.Ordinal);
        foreach (var item in all) {
            if (item.File == null || !roots.Any(r => item.Path == r.Path || r.IsDirectory && item.Path.StartsWith(r.Path + "/", StringComparison.Ordinal))) continue;
            result[item.Path] = item.File;
        }
        // Resolved links retain full content even if their targets were not selected.
        foreach (var item in result.Values.ToArray()) {
            var resolved = ImageSession.Resolve(item, layer.Files);
            if (item.Type == TarEntryType.HardLink && resolved.Blob != null) result[item.Path] = item with { Blob = resolved.Blob, Size = resolved.Size };
        }
        return result;
    }
    public static void Download(string destination, IReadOnlyDictionary<string, ImageFile> selected) {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var components = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in selected.Values) {
            var parts = file.Path.Split('/');
            if (parts.Any(p => p.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || p.EndsWith('.') || p.EndsWith(' ') || IsReserved(p)))
                throw new IOException("This selection contains names Windows cannot represent. Use Save selection as tar instead.");
            var prefix = "";
            foreach (var part in parts) {
                prefix = prefix.Length == 0 ? part : prefix + "/" + part;
                if (components.TryGetValue(prefix, out var casing) && casing != prefix) throw new IOException("Case-sensitive names conflict on Windows. Use Save selection as tar instead.");
                components[prefix] = prefix;
            }
            var target = Path.GetFullPath(Path.Combine(destination, file.Path.Replace('/', Path.DirectorySeparatorChar)));
            if (!target.StartsWith(Path.GetFullPath(destination) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !targets.Add(target)) throw new IOException("Conflicting Windows paths. Use Save selection as tar instead.");
            if (file.Type is not (TarEntryType.Directory or TarEntryType.RegularFile or TarEntryType.V7RegularFile or TarEntryType.HardLink) || file.Type == TarEntryType.HardLink && file.Blob == null)
                throw new IOException("Links or special files cannot be downloaded as ordinary Windows files. Use Save selection as tar instead.");
        }
        foreach (var file in selected.Values.OrderBy(f => f.Path.Length)) {
            var target = Path.Combine(destination, file.Path.Replace('/', Path.DirectorySeparatorChar));
            if (file.Type == TarEntryType.Directory) Directory.CreateDirectory(target);
            else { Directory.CreateDirectory(Path.GetDirectoryName(target)!); if (file.Blob == null) File.WriteAllBytes(target, []); else File.Copy(file.Blob, target, false); }
        }
    }
    static bool IsReserved(string name) { var stem = name.Split('.')[0].ToUpperInvariant(); return new[] { "CON", "PRN", "AUX", "NUL" }.Contains(stem) || stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT")) && "123456789".Contains(stem[3]); }
}


