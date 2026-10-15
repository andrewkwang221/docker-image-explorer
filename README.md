# DockerImageExplorer 1.0

English-only Windows x64 desktop application. No Docker Engine, Docker CLI, or installed .NET runtime is required for the portable build.

## Run

Extract DockerImageExplorer-Windows-x64.zip into a folder, then double-click DockerImageExplorer.exe. Keep all bundled files together.

## Use

1. Open a Docker save or single-platform OCI archive (.tar, .tar.gz, .tgz).
2. Choose a layer from the scrollable left panel. Layer commands have their own scroll area so long commands do not squeeze the layer list.
3. Browse the folder hierarchy on the right. Double-click folders; use Backspace, the .. folder, Up, or Root to navigate. Enter opens the selected folder. File icons follow Windows file associations. The app has an embedded folder-and-magnifier icon. Added, Modified, Deleted, and Unchanged badges show changes in the selected layer. Inferred parent folders have no badge. Clear Include unchanged files to browse only this layer's changes and their parent folders.
4. Search in one box using a filename (filename.mp4), a wildcard (*.png, photo?.jpg), or a partial name/path. Combine it with file type or change badge filters. Filters combine and search across the whole selected layer, regardless of the current folder. Clear filters to return to folder browsing.
5. Select a file for preview. PNG, JPEG, GIF (first frame), BMP, TIFF, and ICO render in the Image tab. UTF-8 and BOM-marked UTF-16 text render in Text / Hex; other binary data shows hex. Text previews are limited to 256 KiB; image inputs are limited to 32 MiB and decoded to a 1600-pixel width. Unsupported image formats remain available as text/hex and downloads.
6. Ctrl-click or Shift-click to select multiple files/folders. Export selected writes ordinary files into a newly created subfolder at your chosen destination, preserving paths from the image root. Selected folders include all descendants, even when filters hide them. Deleted entries download their previous content. Export selection as tar preserves Linux names, links, permissions, and special entries. Windows downloads reject names and entries Windows cannot represent, with an explanation to use tar instead.
7. Export layer writes the original changes tar, including deletion whiteouts. Export filesystem writes the merged filesystem at the selected layer. Export entire filesystem writes the final merged filesystem.

Image metadata displays the image configuration JSON. Resolved hard links are materialized as regular files in merged exports to preserve content across replacements. Downloads and previews never execute files from the image.

## Supported inputs and limits

- Docker save archives containing exactly one image, and single-manifest OCI image archives.
- Uncompressed or gzip-compressed archives and layers.
- Multi-image archives, nested/multi-platform OCI indexes, zstd layers, container `export` archives, registry downloads, and encrypted layers are unsupported and produce an error.
- The app stores archive contents in a temporary folder while open; large images require adequate disk space. Layer snapshots use memory proportional to the number of entries across layers.
- Modified means an existing path was supplied again in a layer, including metadata-only changes.
- Preview renders text, hex, or supported images; it does not execute image files or follow symbolic links on Windows.

## Build from source

Install the .NET 8 SDK on Windows, then:

```powershell
dotnet build DockerImageExplorer.csproj -c Release
dotnet run --project Tests/Tests.csproj -c Release
dotnet publish DockerImageExplorer.csproj -c Release -r win-x64 --self-contained true -o publish
```

The supplied portable package bundles the SDK's .NET 8.0.31 runtime and Windows Desktop runtime; launch was verified on this machine without an installed .NET runtime. The SDK is not included.

## Run from source

With the .NET 8 SDK installed on Windows:

```powershell
dotnet run --project DockerImageExplorer.csproj -c Release
```

## Validation

Release build: zero warnings or errors. Automated tests cover Docker and OCI archive loading, gzip archives/layers, immutable lower-layer snapshots, normal and opaque whiteouts, same-layer additions, modification classification, history alignment, content, merged export, unsafe path rejection, folder reconstruction, badges, recursive selection deduplication, downloads, combined filters, and Windows name validation. A portable application launch was verified. Full interactive UI testing has not been performed.



The file-type filter includes images, videos, audio, text, documents, archives, source code, configuration, executables, fonts, links, and other files. Classification uses file extensions. Video and audio entries open in the Video / Audio tab with Play, Pause, Stop, seeking, and volume. Playback uses Windows media codecs; unsupported codecs show an explanatory message and the files can still be exported. Playback pauses when you leave the media tab and closes when selection or image changes. Wildcard matching is case-insensitive and applies to the whole filename (or whole image path when the pattern includes /). Partial text searches match names and paths.


Right-click a layer for Export layer, Export file system, and Export entire filesystem. Right-click a file/folder for Export and Export as tar. Existing multiple selection is preserved when right-clicking a selected row; otherwise the clicked row becomes selected. The final layer is marked (Final). Backspace navigates up throughout the browser while remaining a text-editing key in the search box.

UI regression checks verify routed Backspace, search exclusion, parent folder, context-menu labels, toolbar labels, and media controls. Run them on Windows with dotnet run --project UITests/UITests.csproj -c Release. Actual video/audio codec coverage has not been exhaustively tested.


## Version 1.0 installer

Run DockerImageExplorer-1.0-Setup.exe. Supports Windows 10/11 x64 and installs for the current user without administrator privileges. The default location is %LOCALAPPDATA%\Programs\DockerImageExplorer. Includes the private .NET runtime, a Start menu shortcut, an optional desktop shortcut, and registration in Windows Installed Apps for uninstalling. The installer and application are English only. The release is unsigned.

The portable build remains available as DockerImageExplorer-1.0-Windows-x64.zip. Application assembly/file version is 1.0.0.0; product/display version is 1.0.

To rebuild the installer, publish the app to Portable, install the NSIS compiler, and run:

```powershell
dotnet publish DockerImageExplorer.csproj -c Release -r win-x64 --self-contained true -o Portable
./Installer/Build-Installer.ps1 -Compiler 'C:\Program Files (x86)\NSIS\makensis.exe'
```

The installer packs the root Portable files and omits translated runtime satellite directories. Uninstall removes only the packaged files; unrelated user-created files inside the installation folder are preserved. Close the app before installing an update or uninstalling.

Installer test mode (/S /TESTMODE /D=absolute-test-directory) suppresses shortcut and registry registration so extraction, launch, and uninstall can be verified in an isolated folder. Test-mode uninstall also requires /TESTMODE.

## Git project

This download is a local Git repository containing the version 1.0 source, an initial commit on main, and the v1.0.0 tag. Extract the ZIP, including its hidden .git directory, then open the extracted DockerImageExplorer-Git folder.

```powershell
git status
git log --oneline
git tag
dotnet build DockerImageExplorer.csproj -c Release
dotnet run --project DockerImageExplorer.csproj -c Release
dotnet run --project Tests/Tests.csproj -c Release
dotnet run --project UITests/UITests.csproj -c Release
```

Build on Windows with the .NET 8 SDK. Published app/runtime binaries and build outputs are excluded from the repository. Installer sources are included; publish into Portable and run Installer/Build-Installer.ps1 with an installed NSIS compiler to rebuild Setup. No remote repository is configured.
