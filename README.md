# DICOM Viewer — Avalonia UI + fo-dicom

A professional, dark-themed medical image viewer built with .NET 8, Avalonia UI, and fo-dicom. Supports DICOM, standard images, and video files with annotation tools, series stacking, and multi-language UI.

---

## Features

### File Format Support
| Format | Extensions | Notes |
|---|---|---|
| **DICOM** | `.dcm`, `.dicom`, extensionless | Full metadata, multi-frame, compressed codecs (JPEG 2000, JPEG-LS, etc.) |
| **Images** | `.jpg`, `.jpeg`, `.png`, `.bmp`, `.tiff`, `.tif`, `.gif`, `.webp` | Loaded via SixLabors.ImageSharp |
| **Video** | `.avi`, `.mp4`, `.mkv`, `.mov`, `.wmv` | Frame extraction via FFMediaToolkit (requires FFmpeg) |

### Toolbar
| Tool | Description |
|---|---|
| **Pan** | Click & drag to move the image |
| **Zoom** | Drag up/down or use scroll wheel |
| **Window/Level** | Drag left/right = center, up/down = width (Hounsfield Units for CT) |
| **Arrow** | Draw arrow annotations |
| **Text** | Add text label annotations |
| **Freehand** | Free-form drawing |
| **Rectangle / Ellipse / Line** | Shape annotations |
| **Rotate CW/CCW** | 90-degree rotation |
| **Flip H/V** | Mirror the image horizontally/vertically |
| **Invert** | Invert pixel values |
| **W/L Presets** | Brain, Lung, Bone, Abdomen, Liver, Mediastinum, Soft Tissue, Stroke, Spine, Angio, Chest Wide |

### Main Viewer
- High-performance custom Avalonia `Control` with direct pixel rendering
- Smooth pan, zoom, rotate with mouse interaction
- Annotation overlays drawn directly on canvas with color selection
- Undo/Redo support for annotations (Ctrl+Z / Ctrl+Y)
- Async frame loading with cancellation for fast scrolling through compressed DICOM
- Color and grayscale DICOM rendering (RGB, YBR, Palette Color)

### Series Stacking
- Automatic grouping of single-frame DICOM files by `SeriesInstanceUID`
- Sorted by `InstanceNumber` or `SliceLocation`
- Browse through slices as if they were frames of a multi-frame file

### Film Strip (Bottom)
- Horizontal strip of mini-frame thumbnails (up to 2000 frames)
- Active frame highlighted; auto-scrolls to current position
- Click any thumbnail to jump to that frame

### Cine Playback (Bottom Bar)
- Play / Pause / First / Last / Prev / Next controls
- Frame scrubber slider
- Loop toggle
- FPS control (1-60)
- Window Center & Width sliders

### File Browser (Right Panel)
- Directory tree with recursive scanning for supported files
- File type icons (DICOM, image, video)
- Click a folder to load all DICOM files as stacked series
- Open files list with per-file metadata (patient, modality, frame count, study date)

### Additional Features
- **Notifications** — Toast-style notifications with auto-dismiss and manual close
- **Logging** — Built-in log viewer window with level filtering and search
- **Settings** — Configurable default directory, tooltips, startup window mode, key bindings
- **Localization** — Runtime language switching (English and additional languages)
- **Drag & Drop** — Drop files directly onto the viewer to open them

---

## Quick Start

### Requirements
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Windows, macOS, or Linux
- (Optional) [FFmpeg](https://ffmpeg.org/) shared libraries for video file support

### Build & Run

```bash
# Restore packages
dotnet restore

# Run in development
dotnet run

# Build release
dotnet publish -c Release -r win-x64 --self-contained
```

### Run Tests

```bash
dotnet test DicomViewer.Tests/
```

128 unit and integration tests covering helpers, models, playback logic, and file type detection.

### Open Files
- **File > Open** (Ctrl+O) — opens file picker for individual files
- **File > Open Directory** — scans folder recursively, auto-stacks DICOM series
- **Drag & drop** files directly onto the viewer
- **File browser** — click files or folders in the right panel

---

## Keyboard Shortcuts

All shortcuts are customizable via Settings.

| Key | Action |
|---|---|
| `Space` | Play / Pause |
| `Left` / `Right` | Previous / Next frame |
| `Home` / `End` | First / Last frame |
| `+` / `-` | Zoom in / out |
| `F` | Fit to window |
| `R` | Reset view |
| `I` | Invert colors |
| `F11` | Toggle fullscreen |
| `Ctrl+O` | Open file |
| `Ctrl+Z` | Undo annotation |
| `Ctrl+Y` | Redo annotation |
| `A` | Arrow tool |
| `T` | Text tool |
| `D` | Freehand tool |
| `C` | Cycle annotation color |
| `Escape` | Deselect tool |

---

## Project Structure

```
DicomViewer/
├── Constants/
│   ├── DicomDefaults.cs          # Centralized DICOM default values
│   └── UIConstants.cs            # Named constants (thumbnail size, FPS, etc.)
├── Controls/
│   ├── DicomCanvas.cs            # Custom Avalonia rendering control
│   ├── AnnotationRenderer.cs     # Annotation drawing (extracted from canvas)
│   └── CanvasInputHandler.cs     # Mouse/keyboard input handling (extracted)
├── Converters/
│   └── ValueConverters.cs        # XAML value converters
├── Helpers/
│   ├── FileTypeDetector.cs       # Single source of truth for file extensions
│   ├── GeometryHelper.cs         # Shared geometry math (point-to-segment distance)
│   └── PixelConversion.cs        # RGB-to-grayscale and gray-to-ushort helpers
├── Models/
│   ├── Annotations.cs            # Annotation types (Arrow, Text, Freehand, etc.)
│   └── DicomFile.cs              # DicomFile model, DicomSeriesStack, AnnotationColors
├── Services/
│   ├── DicomService.cs           # DICOM loading, pixel extraction, series stacking
│   ├── ImageService.cs           # Standard image loading (ImageSharp)
│   ├── VideoService.cs           # Video frame extraction (FFMediaToolkit)
│   ├── LocalizationService.cs    # Runtime language switching
│   ├── LoggingService.cs         # File + in-memory logging with level filtering
│   └── SettingsService.cs        # JSON settings persistence
├── ViewModels/
│   ├── ViewModelBase.cs          # Base class with IDisposable + localization
│   ├── MainWindowViewModel.cs    # Main app state, commands, file management
│   ├── PlaybackController.cs     # Frame playback logic (extracted for testability)
│   ├── DicomFileViewModel.cs     # Per-file view model
│   ├── ThumbnailViewModel.cs     # Async thumbnail loading with bitmap disposal
│   ├── DirectoryTreeViewModel.cs # File browser tree nodes
│   ├── LogViewerViewModel.cs     # Log window filtering and display
│   ├── SettingsViewModel.cs      # Settings dialog state
│   └── NotificationViewModel.cs  # Toast notification model
├── Views/
│   ├── MainWindow.axaml(.cs)     # Main UI layout and input wiring
│   ├── SettingsWindow.axaml(.cs) # Settings dialog
│   └── LogWindow.axaml(.cs)      # Log viewer window
├── Styles/
│   └── DicomViewerStyles.axaml   # Custom dark theme styles
├── DicomViewer.Tests/
│   ├── Unit/                     # Unit tests (helpers, models, playback)
│   └── Integration/              # Integration tests (file loading pipeline)
├── App.axaml
├── Program.cs
└── ViewLocator.cs
```

---

## Architecture

The codebase follows **MVVM** (Model-View-ViewModel) with these design principles:

- **Single Responsibility** — Large classes are split into focused units (e.g., `DicomCanvas` delegates to `AnnotationRenderer` and `CanvasInputHandler`; `PlaybackController` is extracted from `MainWindowViewModel`)
- **DRY** — Shared logic centralized in `Helpers/` (geometry, pixel conversion, file detection) and `Constants/` (DICOM defaults, UI magic numbers)
- **Resource Safety** — `ViewModelBase` implements `IDisposable` to prevent event subscription memory leaks; `WriteableBitmap` instances are properly disposed on replacement
- **Async with Cancellation** — DICOM frame loading supports `CancellationToken` so fast scrolling skips intermediate frames instead of queueing them

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Avalonia` | 11.3.12 | Cross-platform UI framework |
| `Avalonia.Desktop` | 11.3.12 | Desktop (WinExe) support |
| `Avalonia.Themes.Fluent` | 11.3.9 | Fluent design theme |
| `fo-dicom` | 5.2.5 | DICOM file parsing & rendering |
| `fo-dicom.Codecs` | 5.16.5.1 | JPEG 2000, JPEG-LS, RLE codec support |
| `fo-dicom.Imaging.ImageSharp` | 5.2.5 | ImageSharp integration for fo-dicom |
| `FFMediaToolkit` | 4.8.1 | Video frame extraction (requires FFmpeg) |
| `CommunityToolkit.Mvvm` | 8.3.2 | MVVM source generators (`[ObservableProperty]`, `[RelayCommand]`) |
| `IconPacks.Avalonia.Codicons` | 1.3.1 | VS Code icon set for toolbar and file browser |
| `xunit` | 2.9+ | Test framework (test project only) |

---

## Extending the Viewer

### Adding a new W/L preset
In `MainWindowViewModel.ApplyWindowPreset`, add a case with Hounsfield Unit values:
```csharp
"PetrousBone" => (700.0, 4000.0),
```
The values are automatically converted from HU to normalized pixel space using the active file's modality range.

### Adding a new annotation tool
1. Add a class inheriting from `Annotation` in `Models/Annotations.cs`
2. Add the tool to the `MouseTool` enum in `MainWindowViewModel.cs`
3. Handle creation in `CanvasInputHandler.cs` pointer events
4. Add rendering in `AnnotationRenderer.Render()`
5. Add toolbar button in `MainWindow.axaml`

### Adding a new file format
1. Create a new service (e.g., `NiftiService.cs`) implementing load logic
2. Add extensions to `FileTypeDetector.cs`
3. Add loading branch in `MainWindow.UpdateCanvasImageAsync()`

### Custom rendering overlays
Extend `DicomCanvas.Render()` or add a new renderer class following the `AnnotationRenderer` pattern.
