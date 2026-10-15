using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DockerImageExplorer;

var dir = Path.Combine(Path.GetTempPath(), "div-test-" + Guid.NewGuid());
Directory.CreateDirectory(dir);

void Assert(bool value, string message)
{
    if (!value)
        throw new Exception(message);
}

void Tar(string path, params (string, string)[] rows)
{
    using var output = File.Create(path);
    using var writer = new TarWriter(output);
    foreach (var (name, data) in rows)
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(data));
        writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = content });
    }
}

try
{
    var a = Path.Combine(dir, "a.tar");
    var b = Path.Combine(dir, "b.tar");
    Tar(a, ("etc/config", "old"), ("etc/deleted", "gone"), ("cache/old", "lower"));
    Tar(b, ("cache/new", "upper"), ("cache/.wh..wh..opq", ""), ("etc/config", "new"), ("etc/.wh.deleted", ""));
    var archive = Path.Combine(dir, "image.tar");
    using (var output = File.Create(archive))
    using (var writer = new TarWriter(output))
    {
        writer.WriteEntry(a, "a.tar");
        writer.WriteEntry(b, "b.tar");
        using var config = new MemoryStream(Encoding.UTF8.GetBytes("{\"history\":[{\"created_by\":\"base\"},{\"empty_layer\":true},{\"created_by\":\"update\"}]}"));
        writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "config.json") { DataStream = config });
        using var manifest = new MemoryStream(Encoding.UTF8.GetBytes("[{\"Config\":\"config.json\",\"Layers\":[\"a.tar\",\"b.tar\"]}]"));
        writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "manifest.json") { DataStream = manifest });
    }

    using var session = new ImageSession();
    session.LoadArchive(archive);
    var first = session.Layers[0];
    var last = session.Layers[1];
    Assert(last.Title == "Layer 2 (Final)" && first.Title == "Layer 1", "Final layer label failed");
    Assert(first.Files.ContainsKey("etc/deleted"), "Lower snapshot was modified");
    Assert(!last.Files.ContainsKey("etc/deleted") && !last.Files.ContainsKey("cache/old"), "Whiteout deletion failed");
    Assert(last.Files.ContainsKey("cache/new"), "Opaque deletion removed same-layer addition");
    Assert(last.Changes.Any(c => c.Path == "etc/config" && c.Action == "Modified"), "Modification not classified");
    Assert(last.Command == "update", "Empty history alignment failed");
    Assert(File.ReadAllText(last.Files["etc/config"].Blob!) == "new", "Preview data incorrect");

    var browser = ExplorerModel.Build(last, false);
    Assert(browser.Any(i => i.Path == "etc" && i.IsDirectory), "Implicit folder missing");
    Assert(browser.Any(i => i.Path == "etc/config" && i.Action == "Modified"), "Explorer modification badge missing");
    Assert(browser.Any(i => i.Path == "etc/deleted" && i.Action == "Deleted"), "Explorer deletion missing");
    var selected = ExplorerModel.Selection(last, new[] { browser.Single(i => i.Path == "etc"), browser.Single(i => i.Path == "etc/config") }, browser);
    Assert(selected.Count == 2, "Recursive selection or overlapping selection deduplication failed");
    var downloads = Path.Combine(dir, "downloads");
    ExplorerModel.Download(downloads, selected);
    Assert(
        File.ReadAllText(Path.Combine(downloads, "etc", "config")) == "new"
            && File.ReadAllText(Path.Combine(downloads, "etc", "deleted")) == "gone",
        "Selected downloads incorrect");

    var imageItem = new ExplorerItem("assets/photo.PNG", false, "Added", null);
    Assert(ExplorerModel.Matches(imageItem, "*.png", "Images", "Added"), "Combined filters failed");
    Assert(!ExplorerModel.Matches(imageItem, "*.txt", "Images", "Added"), "Extension filter failed");
    Assert(ExplorerModel.Matches(new ExplorerItem("media/filename.mp4", false, "Modified", null), "filename.mp4", "Videos", "Modified"), "Full filename or video category failed");
    Assert(ExplorerModel.Matches(imageItem, "photo.?NG", "Images", "Added"), "Question-mark wildcard failed");
    Assert(ExplorerModel.Parent("a/b/c") == "a/b" && ExplorerModel.Parent("a") == "" && ExplorerModel.Parent("") == "", "Parent navigation failed");

    var invalid = last.Files["etc/config"] with { Path = "CON.txt" };
    bool invalidRejected = false;
    try
    {
        ExplorerModel.Download(Path.Combine(dir, "invalid"), new Dictionary<string, ImageFile> { [invalid.Path] = invalid });
    }
    catch (IOException)
    {
        invalidRejected = true;
    }

    Assert(invalidRejected && !Directory.Exists(Path.Combine(dir, "invalid")), "Windows path validation must happen before writing");
    var exported = Path.Combine(dir, "export.tar");
    ImageSession.ExportFilesystem(exported, last.Files);
    using (var input = File.OpenRead(exported))
    using (var reader = new TarReader(input))
    {
        var names = new List<string>();
        TarEntry? entry;
        while ((entry = reader.GetNextEntry()) != null)
            names.Add(entry.Name);

        Assert(names.SequenceEqual(new[] { "cache/new", "etc/config" }), "Final filesystem export incorrect");
    }

    var gzip = archive + ".gz";
    using (var output = File.Create(gzip))
    using (var compressed = new GZipStream(output, CompressionMode.Compress))
    using (var input = File.OpenRead(archive))
        input.CopyTo(compressed);

    using var gzSession = new ImageSession();
    gzSession.LoadArchive(gzip);
    Assert(gzSession.Layers.Count == 2, "Gzip archive failed");
    foreach (var unsafePath in new[] { "../escape", "/absolute", "a/../../escape", "a\\b" })
    {
        bool rejected = false;
        try
        {
            ImageSession.Normalize(unsafePath);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }

        Assert(rejected, "Unsafe path accepted");
    }

    var ociArchive = Path.Combine(dir, "oci.tar");
    using (var output = File.Create(ociArchive))
    using (var writer = new TarWriter(output))
    {
        void Json(string name, object value)
        {
            using var data = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(value));
            writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, name) { DataStream = data });
        }

        Json("index.json", new { manifests = new[] { new { digest = "sha256:manifest" } } });
        Json("blobs/sha256/manifest", new
        {
            config = new { digest = "sha256:config" },
            layers = new[] { new { digest = "sha256:layer", mediaType = "application/vnd.oci.image.layer.v1.tar+gzip" } },
        });
        Json("blobs/sha256/config", new { history = new[] { new { created_by = "oci-base" } } });
        var compressedLayer = Path.Combine(dir, "layer.gz");
        using (var dest = File.Create(compressedLayer))
        using (var compressed = new GZipStream(dest, CompressionMode.Compress))
        using (var input = File.OpenRead(a))
            input.CopyTo(compressed);

        writer.WriteEntry(compressedLayer, "blobs/sha256/layer");
    }

    using var ociSession = new ImageSession();
    ociSession.LoadArchive(ociArchive);
    Assert(
        ociSession.Layers.Count == 1 && ociSession.Layers[0].Files.Count == 3 && ociSession.Layers[0].Command == "oci-base",
        "OCI gzip layers failed");

    var baseLayer = Path.Combine(dir, "base.tar");
    var topLayer = Path.Combine(dir, "top.tar");
    void WriteLayer(string path, params (TarEntryType Type, string Name, string? Data, string? Link)[] rows)
    {
        using var output = File.Create(path);
        using var writer = new TarWriter(output);
        foreach (var (type, name, data, link) in rows)
        {
            var entry = new PaxTarEntry(type, name);
            if (link != null)
                entry.LinkName = link;

            if (data == null)
            {
                writer.WriteEntry(entry);
                continue;
            }

            using var content = new MemoryStream(Encoding.UTF8.GetBytes(data));
            entry.DataStream = content;
            writer.WriteEntry(entry);
        }
    }

    WriteLayer(baseLayer, (TarEntryType.RegularFile, "usr/bin/tool", "ok", null));
    WriteLayer(
        topLayer,
        (TarEntryType.SymbolicLink, "usr/bin/python", null, "/usr/bin/tool"),
        (TarEntryType.SymbolicLink, "bin", null, "/usr/bin"),
        (TarEntryType.SymbolicLink, "missing", null, "/no/such"));
    var linkArchive = Path.Combine(dir, "links.tar");
    using (var output = File.Create(linkArchive))
    using (var writer = new TarWriter(output))
    {
        writer.WriteEntry(baseLayer, "base.tar");
        writer.WriteEntry(topLayer, "top.tar");
        using var config = new MemoryStream(Encoding.UTF8.GetBytes("{\"history\":[{\"created_by\":\"base\"},{\"created_by\":\"links\"}]}"));
        writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "config.json") { DataStream = config });
        using var manifest = new MemoryStream(Encoding.UTF8.GetBytes("[{\"Config\":\"config.json\",\"Layers\":[\"base.tar\",\"top.tar\"]}]"));
        writer.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, "manifest.json") { DataStream = manifest });
    }

    using var linkSession = new ImageSession();
    linkSession.LoadArchive(linkArchive);
    var linked = linkSession.Layers[1];
    var savedFiles = new Dictionary<string, ImageFile>(linked.Files, StringComparer.Ordinal)
    {
        ["run/pipe"] = new("run/pipe", TarEntryType.Fifo, null, "", 0, 0, 0, 0, DateTimeOffset.UnixEpoch),
    };
    var saved = Path.Combine(dir, "saved-full");
    var report = ExplorerModel.SaveWindows(saved, savedFiles, savedFiles);
    Assert(report.SymbolicLinks == 3 && report.SpecialFiles == 1, "Windows save did not count links and special files");
    Assert(File.ReadAllText(Path.Combine(saved, "usr", "bin", "python")) == "ok", "File symlink was not saved as the target file");
    Assert(Directory.Exists(Path.Combine(saved, "bin")), "Directory symlink was not saved as a Windows directory");
    Assert(File.ReadAllText(Path.Combine(saved, "bin", "tool")) == "ok", "Directory symlink did not point at the saved folder");

    Assert(File.ReadAllText(Path.Combine(saved, "missing")).Contains("no/such"), "Dangling symbolic link was not saved");
    Assert(File.ReadAllText(Path.Combine(saved, "run", "pipe")) == "fifo", "Special Linux file was not saved");
    var changes = ExplorerModel.ChangedFiles(linked);
    Assert(!changes.ContainsKey("usr/bin/tool"), "Layer save included a lower-layer file");
    var savedLayer = Path.Combine(dir, "saved-layer");
    ExplorerModel.SaveWindows(savedLayer, changes, linked.Files);
    Assert(File.ReadAllText(Path.Combine(savedLayer, "usr", "bin", "python")) == "ok", "Layer save dropped symbolic link content");
    bool saveRejected = false;
    try
    {
        var illegal = linked.Files["usr/bin/tool"] with { Path = "CON.txt" };
        ExplorerModel.SaveWindows(Path.Combine(dir, "saved-illegal"), new Dictionary<string, ImageFile> { [illegal.Path] = illegal }, linked.Files);
    }
    catch (IOException)
    {
        saveRejected = true;
    }

    Assert(saveRejected && !Directory.Exists(Path.Combine(dir, "saved-illegal")), "Windows save accepted a name Windows cannot store");
    Console.WriteLine("PASS: Docker and OCI archives, gzip archives/layers, layer snapshots, whiteouts, change classification, history, content, filesystem export, path validation, Windows save of symbolic links.");
}
finally
{
    Directory.Delete(dir, true);
}
