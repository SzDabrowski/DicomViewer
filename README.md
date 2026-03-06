# DICOM Viewer — Avalonia UI + fo-dicom

A professional, dark-themed DICOM image viewer built with .NET 8, Avalonia UI, and fo-dicom.

---

## Features

### Toolbar (Top Bar)
| Feature | Description |
|---|---|
| **Pan** | Click & drag to move the image |
| **Zoom** | Drag up/down or use scroll wheel |
| **Window/Level** | Drag left/right = center, up/down = width |
| **Measure** | Click and drag to measure distances |
| **Annotate** | Add text annotations |
| **Rotate CW/CCW** | 90° rotation buttons |
| **Flip H/V** | Mirror the image |
| **Invert** | Invert pixel values |
| **Presets** | Brain, Lung, Bone, Abdomen, Liver, Mediastinum |

### Main Viewer
- High-performance custom Avalonia `Control` for DICOM rendering
- Smooth pan, zoom, rotate with mouse interaction
- Measurement overlays drawn directly on canvas
- Pixel value overlay (W/L, zoom, rotation)
- Crosshair guide lines

### Film Strip (Right of viewer)
- Vertical column of mini-frames with opacity
- Active frame highlighted with blue border
- Scrollable for long series

### Cine Control Bar (Bottom of viewer)
- Play / Pause / First / Last / Prev / Next buttons
- Frame scrubber slider
- Loop toggle
- FPS control (1–60)
- Window Center & Width sliders

### File Panel (Right Side)
- List of all open DICOM files
- Per-file: filename, patient name, modality badge, frame count, study date
- Click to activate; ✕ to close
- "+ Add" button to open more files

---

## Quick Start

### Requirements
- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- Windows, macOS, or Linux

### Build & Run

```bash
# Restore packages
dotnet restore

# Run in development
dotnet run

# Build release
dotnet publish -c Release -r win-x64 --self-contained
```

### Open files
- **File → Open** (Ctrl+O) — opens file picker
- **Drag & drop** .dcm files directly onto the viewer

---

## Keyboard Shortcuts

| Key | Action |
|---|---|
| `Space` | Play / Pause |
| `←` / `→` | Previous / Next frame |
| `Home` | First frame |
| `End` | Last frame |
| `+` / `-` | Zoom in / out |
| `F` | Fit to window |
| `R` | Reset view |
| `I` | Invert colors |
| `Ctrl+O` | Open file |

---

## Project Structure

```
DicomViewer/
├── Controls/
│   └── DicomViewerControl.cs     # Custom Avalonia rendering control
├── Converters/
│   └── ValueConverters.cs        # XAML value converters
├── Models/
│   └── DicomFile.cs              # Data models
├── Services/
│   └── DicomService.cs           # fo-dicom file loading
├── Styles/
│   └── DicomViewerStyles.axaml   # Custom dark theme styles
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── MainWindowViewModel.cs    # Main app state & commands
│   ├── DicomFileViewModel.cs     # Per-file view model
│   └── ThumbnailViewModel.cs     # Film strip thumbnails
├── Views/
│   ├── MainWindow.axaml          # Main UI layout
│   └── MainWindow.axaml.cs      # File dialogs & keyboard input
├── App.axaml
├── App.axaml.cs
├── Program.cs
└── ViewLocator.cs
```

---

## Dependencies

| Package | Version | Purpose |
|---|---|---|
| `Avalonia` | 11.2.1 | Cross-platform UI framework |
| `Avalonia.Desktop` | 11.2.1 | Desktop (WinExe) support |
| `Avalonia.Themes.Fluent` | 11.2.1 | Fluent design theme |
| `Avalonia.ReactiveUI` | 11.2.1 | ReactiveUI integration |
| `fo-dicom` | 5.1.2 | DICOM file parsing & rendering |
| `CommunityToolkit.Mvvm` | 8.3.2 | MVVM source generators |

---

## Extending the Viewer

### Adding a new window preset
In `MainWindowViewModel.cs`, add a case to `ApplyWindowPreset`:
```csharp
"Spine" => (50.0, 350.0),
```

### Adding a new tool
1. Add entry to `MouseTool` enum
2. Add button in `MainWindow.axaml` toolbar
3. Add case in `DicomViewerControl.OnPointerMoved`

### Custom rendering overlays
Override `Render()` in `DicomViewerControl.cs` to add DICOM overlays, GSPS annotations, or segmentation masks.
