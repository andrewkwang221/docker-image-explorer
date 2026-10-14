# DockerImageExplorer 1.0

Initial Windows x64 release.

- Opens Docker save and single-platform OCI archives without Docker Engine.
- Browses per-layer added, modified, deleted, and unchanged files with folder navigation.
- Includes wildcard search, file-type filters, multi-selection, context-menu exports, and final-layer labeling.
- Previews text, binary data, images, and Windows-codec-supported video/audio.
- Exports selected files/folders, tar selections, layer archives, and reconstructed filesystems.
- Includes English-only portable and per-user installer packages with a private .NET runtime.

Version metadata: product 1.0; assembly/file 1.0.0.0.

Validation: release build and core/UI regression checks passed. Isolated installer testing verified every installed payload file against its source, confirmed the installed application's version and launch, and verified that uninstall removed the app while preserving an unrelated file. This test mode intentionally suppresses Windows registry and shortcut changes. Actual media decoding depends on installed Windows codecs.

The installer is unsigned.
