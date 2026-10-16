using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Text.Json;

namespace DockerImageExplorer;

public record ImageFile(
    string Path,
    TarEntryType Type,
    string? Blob,
    string Link,
    long Size,
    UnixFileMode Mode,
    int Uid,
    int Gid,
    DateTimeOffset Modified,
    int DeviceMajor = 0,
    int DeviceMinor = 0);

public record Change(string Action, string Path, ImageFile? File);

public sealed class Layer
{
    public required string Archive { get; init; }
    public required string Title { get; set; }
    public int Number { get; init; }
    public required string Command { get; init; }
    public List<Change> Changes { get; } = [];
    public Dictionary<string, ImageFile> Files { get; set; } = new(StringComparer.Ordinal);

    public override string ToString() => Title;
}

public sealed class ImageSession : IDisposable
{
    public string DirectoryPath { get; } = Path.Combine(Path.GetTempPath(), "DockerImageExplorer", Guid.NewGuid().ToString("N"));
    public List<Layer> Layers { get; } = [];
    public string Description { get; private set; } = "";
    public string SourceArchive { get; private set; } = "";

    public ImageSession() => Directory.CreateDirectory(DirectoryPath);

    static Stream OpenTar(string path)
    {
        var stream = File.OpenRead(path);
        int first = stream.ReadByte();
        int second = stream.ReadByte();
        stream.Position = 0;
        return first == 0x1f && second == 0x8b
            ? new GZipStream(stream, CompressionMode.Decompress)
            : stream;
    }

    public static string Normalize(string path)
    {
        if (path.Contains('\\') || path.Contains('\0') || path.StartsWith('/'))
            throw new InvalidDataException($"Unsafe archive path: {path}");

        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries).Where(p => p != ".").ToArray();
        if (parts.Any(p => p == ".."))
            throw new InvalidDataException($"Unsafe archive path: {path}");

        return string.Join('/', parts);
    }

    public void LoadArchive(string archive)
    {
        SourceArchive = Path.GetFullPath(archive);
        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var stream = OpenTar(archive))
        using (var reader = new TarReader(stream))
        {
            TarEntry? entry;
            while ((entry = reader.GetNextEntry()) != null)
            {
                var name = Normalize(entry.Name);
                if (entry.DataStream == null)
                    continue;

                var blob = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N"));
                using (var output = File.Create(blob))
                    entry.DataStream.CopyTo(output);

                if (!entries.TryAdd(name, blob))
                    throw new InvalidDataException("Duplicate archive entry: " + name);
            }
        }

        if (!entries.TryGetValue("manifest.json", out var manifestPath))
        {
            if (!entries.TryGetValue("index.json", out var indexPath))
                throw new InvalidDataException("Expected a Docker save or OCI image archive (not a container export).");

            using var index = JsonDocument.Parse(File.ReadAllText(indexPath));
            var descriptors = index.RootElement.GetProperty("manifests");
            if (descriptors.GetArrayLength() != 1)
                throw new InvalidDataException("Select an archive containing one image/platform.");

            string Blob(JsonElement descriptor)
            {
                var digest = descriptor.GetProperty("digest").GetString()!;
                var name = Normalize("blobs/" + digest.Replace(':', '/'));
                return entries.TryGetValue(name, out var value)
                    ? value
                    : throw new InvalidDataException("Missing OCI blob: " + digest);
            }

            using var oci = JsonDocument.Parse(File.ReadAllText(Blob(descriptors[0])));
            var root = oci.RootElement;
            if (!root.TryGetProperty("layers", out var ociLayers))
                throw new InvalidDataException("Nested OCI indexes are unsupported. Export a single-platform OCI image.");

            var configBlob = Blob(root.GetProperty("config"));
            entries["viewer-config.json"] = configBlob;
            var layerNames = new List<string>();
            foreach (var descriptor in ociLayers.EnumerateArray())
            {
                if (descriptor.TryGetProperty("mediaType", out var media) && (media.GetString() ?? "").Contains("zstd"))
                    throw new InvalidDataException("Zstandard OCI layers are unsupported; use tar or gzip layers.");

                var name = "viewer-layer-" + layerNames.Count;
                entries[name] = Blob(descriptor);
                layerNames.Add(name);
            }

            manifestPath = Path.Combine(DirectoryPath, "viewer-manifest.json");
            File.WriteAllText(manifestPath, JsonSerializer.Serialize(new[] { new { Config = "viewer-config.json", Layers = layerNames } }));
        }

        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        if (manifest.RootElement.GetArrayLength() != 1)
            throw new InvalidDataException("Archive must contain exactly one image. Save each image separately.");

        var image = manifest.RootElement[0];
        using var config = JsonDocument.Parse(File.ReadAllText(entries[Normalize(image.GetProperty("Config").GetString()!)]));
        Description = JsonSerializer.Serialize(config.RootElement, new JsonSerializerOptions { WriteIndented = true });
        var commands = new List<string>();
        if (config.RootElement.TryGetProperty("history", out var history))
        {
            foreach (var item in history.EnumerateArray())
            {
                if (!item.TryGetProperty("empty_layer", out var empty) || !empty.GetBoolean())
                    commands.Add(item.TryGetProperty("created_by", out var command) ? command.GetString() ?? "" : "");
            }
        }

        var state = new Dictionary<string, ImageFile>(StringComparer.Ordinal);
        foreach (var item in image.GetProperty("Layers").EnumerateArray())
        {
            var path = entries[Normalize(item.GetString()!)];
            var layer = new Layer
            {
                Archive = path,
                Number = Layers.Count + 1,
                Title = $"Layer {Layers.Count + 1}",
                Command = commands.ElementAtOrDefault(Layers.Count) ?? "",
            };
            ApplyLayer(path, state, layer.Changes);
            layer.Files = new(state, StringComparer.Ordinal);
            Layers.Add(layer);
        }

        if (Layers.Count > 0)
            Layers[^1].Title += " (Final)";
    }

    public void ApplyLayer(string archive, Dictionary<string, ImageFile> state, List<Change> changes)
    {
        var additions = new List<ImageFile>();
        var removals = new List<(string Path, bool Opaque)>();
        using var stream = OpenTar(archive);
        using var reader = new TarReader(stream);
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) != null)
        {
            var name = Normalize(entry.Name);
            if (name.Length == 0)
                continue;

            var slash = name.LastIndexOf('/');
            var basename = name[(slash + 1)..];
            var parent = slash < 0 ? "" : name[..(slash + 1)];
            if (basename == ".wh..wh..opq")
            {
                removals.Add((parent.TrimEnd('/'), true));
                continue;
            }

            if (basename.StartsWith(".wh.", StringComparison.Ordinal))
            {
                removals.Add((parent + basename[4..], false));
                continue;
            }

            string? blob = null;
            if (entry.DataStream != null)
            {
                blob = Path.Combine(DirectoryPath, Guid.NewGuid().ToString("N"));
                using var output = File.Create(blob);
                entry.DataStream.CopyTo(output);
            }

            additions.Add(new(
                name,
                entry.EntryType,
                blob,
                entry.LinkName,
                entry.Length,
                entry.Mode,
                entry.Uid,
                entry.Gid,
                entry.ModificationTime,
                entry is PosixTarEntry posix ? posix.DeviceMajor : 0,
                entry is PosixTarEntry posix2 ? posix2.DeviceMinor : 0));
        }

        // Whiteouts apply only to the lower filesystem, regardless of tar entry order.
        foreach (var (path, opaque) in removals)
        {
            var keys = state.Keys
                .Where(k => (!opaque && k == path) || (path.Length == 0 ? opaque : k.StartsWith(path + "/", StringComparison.Ordinal)))
                .ToArray();
            foreach (var key in keys)
            {
                changes.Add(new("Deleted", key, state[key]));
                state.Remove(key);
            }
        }

        foreach (var file in additions)
        {
            // A child requires directory ancestors: remove incompatible lower entries.
            var parent = file.Path.LastIndexOf('/');
            while (parent > 0)
            {
                var ancestor = file.Path[..parent];
                if (state.TryGetValue(ancestor, out var lower) && lower.Type != TarEntryType.Directory)
                {
                    changes.Add(new("Deleted", ancestor, lower));
                    state.Remove(ancestor);
                }

                parent = ancestor.LastIndexOf('/');
            }

            if (file.Type != TarEntryType.Directory)
            {
                var children = state.Keys.Where(k => k.StartsWith(file.Path + "/", StringComparison.Ordinal)).ToArray();
                foreach (var key in children)
                {
                    changes.Add(new("Deleted", key, state[key]));
                    state.Remove(key);
                }
            }

            changes.Add(new(state.ContainsKey(file.Path) ? "Modified" : "Added", file.Path, file));
            if (file.Type == TarEntryType.HardLink)
            {
                var target = Resolve(file, state);
                state[file.Path] = target.Blob == null ? file : file with { Blob = target.Blob, Size = target.Size };
            }
            else
            {
                state[file.Path] = file;
            }
        }
    }

    public static ImageFile Resolve(ImageFile file, IReadOnlyDictionary<string, ImageFile> files)
    {
        var seen = new HashSet<string>();
        while (file.Type == TarEntryType.HardLink && seen.Add(file.Path))
        {
            if (file.Blob != null)
                break;

            if (!files.TryGetValue(Normalize(file.Link), out var target))
                break;

            file = target;
        }

        return file;
    }

    public static void ExportFilesystem(string destination, IReadOnlyDictionary<string, ImageFile> files)
    {
        using var stream = File.Create(destination);
        using var writer = new TarWriter(stream, TarEntryFormat.Pax);
        foreach (var file in files.Values.OrderBy(f => f.Path, StringComparer.Ordinal))
        {
            var resolved = Resolve(file, files);
            var type = file.Type == TarEntryType.HardLink && resolved.Blob != null ? TarEntryType.RegularFile : file.Type;
            var entry = new PaxTarEntry(type == TarEntryType.V7RegularFile ? TarEntryType.RegularFile : type, file.Path)
            {
                Mode = file.Mode,
                Uid = file.Uid,
                Gid = file.Gid,
                ModificationTime = file.Modified,
            };
            if (type is TarEntryType.CharacterDevice or TarEntryType.BlockDevice)
            {
                entry.DeviceMajor = file.DeviceMajor;
                entry.DeviceMinor = file.DeviceMinor;
            }

            if (type is TarEntryType.SymbolicLink or TarEntryType.HardLink)
                entry.LinkName = file.Link;

            using var content = resolved.Blob == null ? null : File.OpenRead(resolved.Blob);
            if (content != null)
                entry.DataStream = content;

            writer.WriteEntry(entry);
        }
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(DirectoryPath, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
