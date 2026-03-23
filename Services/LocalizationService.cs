using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace DicomViewer.Services;

/// <summary>
/// Singleton localization service that provides translated strings.
/// Supports runtime language switching with property change notifications.
/// Use the indexer to retrieve translated strings: LocalizationService.Instance["Key"]
/// In XAML: {Binding [Key], Source={x:Static svc:LocalizationService.Instance}}
/// </summary>
public class LocalizationService : INotifyPropertyChanged
{
    private static readonly Lazy<LocalizationService> _instance = new(() => new LocalizationService());
    public static LocalizationService Instance => _instance.Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    private string _currentLanguage = "en";
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new();

    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage == value) return;
            _currentLanguage = value;
            // Notify all bindings that translations have changed
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
        }
    }

    public string this[string key]
    {
        get
        {
            if (_translations.TryGetValue(_currentLanguage, out var langDict) &&
                langDict.TryGetValue(key, out var value))
                return value;

            // Fallback to English
            if (_currentLanguage != "en" &&
                _translations.TryGetValue("en", out var enDict) &&
                enDict.TryGetValue(key, out var enValue))
                return enValue;

            return key; // Return key itself as last resort
        }
    }

    private LocalizationService()
    {
        LoadTranslations();
    }

    public void SetLanguage(string language)
    {
        CurrentLanguage = language;
    }

    public string[] SupportedLanguages => new[] { "en", "pl" };

    public string GetLanguageDisplayName(string code) => code switch
    {
        "en" => this["Lang_English"],
        "pl" => this["Lang_Polish"],
        _ => code
    };

    private void LoadTranslations()
    {
        _translations["en"] = GetEnglishStrings();
        _translations["pl"] = GetPolishStrings();
    }

    private static Dictionary<string, string> GetEnglishStrings() => new()
    {
        // Language names
        ["Lang_English"] = "English",
        ["Lang_Polish"] = "Polish",

        // Toolbar & Tools
        ["Open"] = "Open",
        ["OpenFiles"] = "Open File(s)...",
        ["OpenDirectory"] = "Open Directory...",
        ["Arrow"] = "Arrow",
        ["TextLabel"] = "Text Label",
        ["FreehandDraw"] = "Freehand Draw",
        ["Annotate"] = "Annotate",
        ["Rectangle"] = "Rectangle",
        ["Ellipse"] = "Ellipse",
        ["Line"] = "Line",
        ["Draw"] = "Draw",
        ["Fit"] = "Fit",
        ["Reset"] = "Reset",
        ["Settings"] = "Settings",

        // Tooltips
        ["Tip_OpenFileOrDir"] = "Open File or Directory (Ctrl+O)",
        ["Tip_PanZoom"] = "Pan / Zoom",
        ["Tip_WindowLevel"] = "Window/Level",
        ["Tip_AnnotateTools"] = "Annotate Tools",
        ["Tip_DrawShapes"] = "Draw Shapes",
        ["Tip_AnnotationColor"] = "Annotation Color",
        ["Tip_RotateCCW"] = "Rotate Counter-Clockwise",
        ["Tip_RotateCW"] = "Rotate Clockwise",
        ["Tip_FlipH"] = "Flip Horizontal",
        ["Tip_FlipV"] = "Flip Vertical",
        ["Tip_InvertColors"] = "Invert Colors (I)",
        ["Tip_WindowPresets"] = "Window Presets",
        ["Tip_LogViewer"] = "Log Viewer (Ctrl+L)",
        ["Tip_ZoomOut"] = "Zoom Out (-)",
        ["Tip_ZoomIn"] = "Zoom In (+)",
        ["Tip_FitToWindow"] = "Fit to Window (F)",
        ["Tip_ResetView"] = "Reset View (R)",
        ["Tip_ToggleOverlay"] = "Toggle Overlay",
        ["Tip_ToggleFilmstrip"] = "Toggle Filmstrip",
        ["Tip_ToggleFilePanel"] = "Toggle File Panel",
        ["Tip_Minimise"] = "Minimise",
        ["Tip_MaximiseRestore"] = "Maximise / Restore",
        ["Tip_Close"] = "Close",
        ["Tip_FirstFrame"] = "First Frame",
        ["Tip_PreviousFrame"] = "Previous Frame",
        ["Tip_PlayPause"] = "Play / Pause (Space)",
        ["Tip_NextFrame"] = "Next Frame",
        ["Tip_LastFrame"] = "Last Frame",
        ["Tip_LoopPlayback"] = "Loop Playback",
        ["Tip_CopyErrorDetails"] = "Copy error details",
        ["Tip_Dismiss"] = "Dismiss",
        ["Tip_CollapseFilmstrip"] = "Collapse Filmstrip",
        ["Tip_ExpandFilmstrip"] = "Expand Filmstrip",
        ["Tip_HideFilePanel"] = "Hide File Panel",
        ["Tip_ShowFilePanel"] = "Show File Panel",

        // Colors
        ["Yellow"] = "Yellow",
        ["Cyan"] = "Cyan",
        ["Red"] = "Red",
        ["Green"] = "Green",
        ["Blue"] = "Blue",
        ["Magenta"] = "Magenta",
        ["Orange"] = "Orange",
        ["White"] = "White",

        // Window/Level Presets
        ["Brain"] = "Brain",
        ["Lung"] = "Lung",
        ["Bone"] = "Bone",
        ["Abdomen"] = "Abdomen",
        ["Mediastinum"] = "Mediastinum",
        ["Liver"] = "Liver",
        ["SoftTissue"] = "Soft Tissue",
        ["Stroke"] = "Stroke",
        ["Spine"] = "Spine",
        ["Angio"] = "Angio/CTA",
        ["ChestWide"] = "Chest Wide",
        ["Presets"] = "Presets",

        // Main Viewer
        ["NoFileLoaded"] = "No DICOM file loaded",
        ["OpenOrDragDrop"] = "Open a file or drag & drop to begin",
        ["CtrlODragDrop"] = "Ctrl+O to open  |  Drag & drop supported",
        ["LoadingDicomFile"] = "Loading DICOM File",
        ["DropFilesHere"] = "Drop DICOM files here to open",
        ["DicomViewer"] = "DICOM Viewer",

        // Browser Panel
        ["Browser"] = "BROWSER",
        ["FilesAndDirectories"] = "Files & directories",
        ["Add"] = "Add",
        ["NoDirectoryLoaded"] = "No directory loaded",
        ["UseOpenDirectory"] = "Use Open > Open Directory",

        // Open Files Panel
        ["OpenFilesPanel"] = "OPEN FILES",

        // Filmstrip
        ["Filmstrip"] = "FILMSTRIP",
        ["NoFileOpenFrames"] = "No file loaded \u2014 open a DICOM file to see frames",
        ["Frame"] = "Frame",

        // Playback & Status
        ["FPS"] = "FPS:",
        ["StatusReady"] = "Ready - Open a DICOM file to begin",
        ["StatusNoTool"] = "No tool selected \u2014 scroll to navigate frames",
        ["StatusPanZoom"] = "Pan / Zoom \u2014 drag to pan, scroll to zoom",
        ["StatusWindowLevel"] = "Window/Level \u2014 drag left/right: center, up/down: width",
        ["StatusArrow"] = "Arrow \u2014 click and drag to point at structures",
        ["StatusText"] = "Text \u2014 click to place a text label",
        ["StatusFreehand"] = "Freehand \u2014 draw freely on the image",
        ["StatusRectangle"] = "Rectangle \u2014 click and drag to draw a rectangle",
        ["StatusEllipse"] = "Ellipse \u2014 click and drag to draw an ellipse",
        ["StatusLine"] = "Line \u2014 click and drag to draw a line",
        ["StatusNoToolSelected"] = "No tool selected",
        ["AnnotationColor"] = "Annotation color:",
        ["ReadingHeaders"] = "Reading DICOM headers...",
        ["ParsingMetadata"] = "Parsing metadata",
        ["FramesFound"] = "frame(s) found...",
        ["BuildingThumbnails"] = "Building thumbnails...",
        ["Ready"] = "Ready",
        ["Error"] = "Error:",
        ["Playing"] = "Playing",
        ["Paused"] = "Paused",
        ["Buffering"] = "Buffering",
        ["Buffered"] = "Buffered",
        ["Preset"] = "Preset:",
        ["Opening"] = "Opening:",
        ["WL_Center"] = "Center:",
        ["WL_Width"] = "Width:",

        // Settings Window
        ["Settings_Title"] = "Settings",
        ["Settings_Language"] = "LANGUAGE",
        ["Settings_DisplayLanguage"] = "Display Language",
        ["Settings_DefaultFolder"] = "DEFAULT FOLDER",
        ["Settings_StartupDirectory"] = "Startup Directory",
        ["Settings_NotSet"] = "Not set",
        ["Settings_Browse"] = "Browse",
        ["Settings_DirectoryAutoLoad"] = "This directory will be loaded automatically on startup.",
        ["Settings_Interface"] = "INTERFACE",
        ["Settings_ShowTooltips"] = "Show Tooltips on Hover",
        ["Settings_ShowTooltipsDesc"] = "Display helpful tips when hovering over buttons and controls.",
        ["Settings_ShowNotifications"] = "Show Notifications",
        ["Settings_ShowNotificationsDesc"] = "Display warnings and errors as overlay notifications in the main window.",
        ["Settings_WindowMode"] = "Startup Window Mode",
        ["Settings_WindowModeDesc"] = "Choose how the window opens on launch. Press F11 to toggle fullscreen.",
        ["Settings_Windowed"] = "Windowed",
        ["Settings_Maximized"] = "Maximized",
        ["Settings_Fullscreen"] = "Fullscreen",
        ["Settings_General"] = "General",
        ["Settings_Controls"] = "Controls",
        ["Settings_Save"] = "Save",
        ["Settings_SaveClose"] = "Save & Close",
        ["Settings_Saved"] = "Settings saved",
        ["Settings_UnsavedChanges"] = "Unsaved changes",

        // Settings - Controls Tab
        ["Controls_KeyboardShortcuts"] = "KEYBOARD SHORTCUTS",
        ["Controls_ResetAll"] = "Reset All",
        ["Controls_RebindHint"] = "Click a shortcut to rebind it, then press the new key combination.",
        ["Controls_Playback"] = "Playback",
        ["Controls_PlayPause"] = "Play / Pause",
        ["Controls_PreviousFrame"] = "Previous Frame",
        ["Controls_NextFrame"] = "Next Frame",
        ["Controls_FirstFrame"] = "First Frame",
        ["Controls_LastFrame"] = "Last Frame",
        ["Controls_View"] = "View",
        ["Controls_ZoomIn"] = "Zoom In",
        ["Controls_ZoomOut"] = "Zoom Out",
        ["Controls_FitToWindow"] = "Fit to Window",
        ["Controls_ResetView"] = "Reset View",
        ["Controls_ToggleInvert"] = "Toggle Invert",
        ["Controls_ToggleFullscreen"] = "Toggle Fullscreen",
        ["Controls_AnnotationTools"] = "Annotation Tools",
        ["Controls_ArrowTool"] = "Arrow Tool",
        ["Controls_TextTool"] = "Text Tool",
        ["Controls_FreehandTool"] = "Freehand Tool",
        ["Controls_CycleColor"] = "Cycle Color",
        ["Controls_DeselectTool"] = "Deselect Tool",
        ["Controls_File"] = "File",
        ["Controls_OpenFile"] = "Open File",
        ["Controls_OpenLogs"] = "Open Logs",
        ["Controls_Edit"] = "Edit",
        ["Controls_Undo"] = "Undo",
        ["Controls_Redo"] = "Redo",
        ["Controls_PressKey"] = "Press key...",
        ["Controls_ConflictsWith"] = "Conflicts with:",

        // Log Viewer
        ["Log_Title"] = "Log Viewer",
        ["Log_Log"] = "LOG",
        ["Log_All"] = "ALL",
        ["Log_ShowAll"] = "Show all",
        ["Log_InfoAbove"] = "Info and above",
        ["Log_WarningsErrors"] = "Warnings and errors",
        ["Log_ErrorsOnly"] = "Errors only",
        ["Log_OpenFolder"] = "Open log folder",
        ["Log_ClearView"] = "Clear log view",
        ["Log_Refresh"] = "Refresh",

        // Notifications
        ["Notif_Info"] = "INFO",
        ["Notif_Warning"] = "WARNING",
        ["Notif_Error"] = "ERROR",

        // Error Messages
        ["Err_FailedToOpen"] = "Failed to open",
        ["Err_FileNotFound"] = "File not found:",
        ["Err_FileEmpty"] = "File is empty (0 bytes):",
        ["Err_VeryLargeFile"] = "Very large file",
        ["Err_UnrecognizedExtension"] = "Unrecognized extension",
        ["Err_AttemptingDicomParse"] = "attempting DICOM parse",
        ["Err_AccessDenied"] = "Access denied:",
        ["Err_IOError"] = "IO error reading:",
        ["Err_CouldNotOpenLogFolder"] = "Could not open log folder:",
        ["Err_VideoNotSupported"] = "Video playback requires FFmpeg",
        ["Err_VideoNotSupported_Details"] = "Video files (.mp4, .avi, .mkv, .mov, .wmv) require FFmpeg libraries to be installed. Please install FFmpeg and ensure it is on your PATH.",
        ["Info_RestartRequired"] = "Restart required",
        ["Info_RestartRequired_Lang"] = "Some UI elements require a restart to fully apply the new language.",
    };

    private static Dictionary<string, string> GetPolishStrings() => new()
    {
        // Language names
        ["Lang_English"] = "Angielski",
        ["Lang_Polish"] = "Polski",

        // Toolbar & Tools
        ["Open"] = "Otw\u00f3rz",
        ["OpenFiles"] = "Otw\u00f3rz plik(i)...",
        ["OpenDirectory"] = "Otw\u00f3rz katalog...",
        ["Arrow"] = "Strza\u0142ka",
        ["TextLabel"] = "Etykieta tekstowa",
        ["FreehandDraw"] = "Rysowanie odr\u0119czne",
        ["Annotate"] = "Adnotacje",
        ["Rectangle"] = "Prostok\u0105t",
        ["Ellipse"] = "Elipsa",
        ["Line"] = "Linia",
        ["Draw"] = "Rysowanie",
        ["Fit"] = "Dopasuj",
        ["Reset"] = "Resetuj",
        ["Settings"] = "Ustawienia",

        // Tooltips
        ["Tip_OpenFileOrDir"] = "Otw\u00f3rz plik lub katalog (Ctrl+O)",
        ["Tip_PanZoom"] = "Przesuwanie / Powi\u0119kszanie",
        ["Tip_WindowLevel"] = "Okno/Poziom",
        ["Tip_AnnotateTools"] = "Narz\u0119dzia adnotacji",
        ["Tip_DrawShapes"] = "Rysuj kszta\u0142ty",
        ["Tip_AnnotationColor"] = "Kolor adnotacji",
        ["Tip_RotateCCW"] = "Obr\u00f3\u0107 w lewo",
        ["Tip_RotateCW"] = "Obr\u00f3\u0107 w prawo",
        ["Tip_FlipH"] = "Odbij poziomo",
        ["Tip_FlipV"] = "Odbij pionowo",
        ["Tip_InvertColors"] = "Odwr\u00f3\u0107 kolory (I)",
        ["Tip_WindowPresets"] = "Presety okna",
        ["Tip_LogViewer"] = "Podgl\u0105d log\u00f3w (Ctrl+L)",
        ["Tip_ZoomOut"] = "Oddal (-)",
        ["Tip_ZoomIn"] = "Przybli\u017c (+)",
        ["Tip_FitToWindow"] = "Dopasuj do okna (F)",
        ["Tip_ResetView"] = "Resetuj widok (R)",
        ["Tip_ToggleOverlay"] = "Prze\u0142\u0105cz nak\u0142adk\u0119",
        ["Tip_ToggleFilmstrip"] = "Prze\u0142\u0105cz pasek klatek",
        ["Tip_ToggleFilePanel"] = "Prze\u0142\u0105cz panel plik\u00f3w",
        ["Tip_Minimise"] = "Minimalizuj",
        ["Tip_MaximiseRestore"] = "Maksymalizuj / Przywr\u00f3\u0107",
        ["Tip_Close"] = "Zamknij",
        ["Tip_FirstFrame"] = "Pierwsza klatka",
        ["Tip_PreviousFrame"] = "Poprzednia klatka",
        ["Tip_PlayPause"] = "Odtwarzaj / Wstrzymaj (Spacja)",
        ["Tip_NextFrame"] = "Nast\u0119pna klatka",
        ["Tip_LastFrame"] = "Ostatnia klatka",
        ["Tip_LoopPlayback"] = "Odtwarzanie w p\u0119tli",
        ["Tip_CopyErrorDetails"] = "Kopiuj szczeg\u00f3\u0142y b\u0142\u0119du",
        ["Tip_Dismiss"] = "Odrzu\u0107",
        ["Tip_CollapseFilmstrip"] = "Zwi\u0144 pasek klatek",
        ["Tip_ExpandFilmstrip"] = "Rozwi\u0144 pasek klatek",
        ["Tip_HideFilePanel"] = "Ukryj panel plik\u00f3w",
        ["Tip_ShowFilePanel"] = "Poka\u017c panel plik\u00f3w",

        // Colors
        ["Yellow"] = "\u017b\u00f3\u0142ty",
        ["Cyan"] = "Cyjan",
        ["Red"] = "Czerwony",
        ["Green"] = "Zielony",
        ["Blue"] = "Niebieski",
        ["Magenta"] = "Magenta",
        ["Orange"] = "Pomara\u0144czowy",
        ["White"] = "Bia\u0142y",

        // Window/Level Presets
        ["Brain"] = "M\u00f3zg",
        ["Lung"] = "P\u0142uca",
        ["Bone"] = "Ko\u015b\u0107",
        ["Abdomen"] = "Brzuch",
        ["Mediastinum"] = "\u015ar\u00f3dpiersie",
        ["Liver"] = "W\u0105troba",
        ["SoftTissue"] = "Tk. mi\u0119kkie",
        ["Stroke"] = "Udar",
        ["Spine"] = "Kr\u0119gos\u0142up",
        ["Angio"] = "Angio/CTA",
        ["ChestWide"] = "Kl. piersiowa",
        ["Presets"] = "Presety",

        // Main Viewer
        ["NoFileLoaded"] = "Nie za\u0142adowano pliku DICOM",
        ["OpenOrDragDrop"] = "Otw\u00f3rz plik lub przeci\u0105gnij i upu\u015b\u0107, aby rozpocz\u0105\u0107",
        ["CtrlODragDrop"] = "Ctrl+O aby otworzy\u0107  |  Obs\u0142uga przeci\u0105gania i upuszczania",
        ["LoadingDicomFile"] = "\u0141adowanie pliku DICOM",
        ["DropFilesHere"] = "Upu\u015b\u0107 pliki DICOM tutaj, aby otworzy\u0107",
        ["DicomViewer"] = "Przegl\u0105darka DICOM",

        // Browser Panel
        ["Browser"] = "PRZEGL\u0104DARKA",
        ["FilesAndDirectories"] = "Pliki i katalogi",
        ["Add"] = "Dodaj",
        ["NoDirectoryLoaded"] = "Nie za\u0142adowano katalogu",
        ["UseOpenDirectory"] = "U\u017cyj Otw\u00f3rz > Otw\u00f3rz katalog",

        // Open Files Panel
        ["OpenFilesPanel"] = "OTWARTE PLIKI",

        // Filmstrip
        ["Filmstrip"] = "PASEK KLATEK",
        ["NoFileOpenFrames"] = "Nie za\u0142adowano pliku \u2014 otw\u00f3rz plik DICOM, aby zobaczy\u0107 klatki",
        ["Frame"] = "Klatka",

        // Playback & Status
        ["FPS"] = "KL/S:",
        ["StatusReady"] = "Gotowy - Otw\u00f3rz plik DICOM, aby rozpocz\u0105\u0107",
        ["StatusNoTool"] = "Nie wybrano narz\u0119dzia \u2014 przewi\u0144, aby nawigowa\u0107 po klatkach",
        ["StatusPanZoom"] = "Przesuwanie / Powi\u0119kszanie \u2014 przeci\u0105gnij, aby przesuwa\u0107, przewi\u0144, aby powi\u0119kszy\u0107",
        ["StatusWindowLevel"] = "Okno/Poziom \u2014 przeci\u0105gnij lewo/prawo: \u015brodek, g\u00f3ra/d\u00f3\u0142: szeroko\u015b\u0107",
        ["StatusArrow"] = "Strza\u0142ka \u2014 kliknij i przeci\u0105gnij, aby wskaza\u0107 struktury",
        ["StatusText"] = "Tekst \u2014 kliknij, aby umie\u015bci\u0107 etykiet\u0119 tekstow\u0105",
        ["StatusFreehand"] = "Odr\u0119czne \u2014 rysuj swobodnie na obrazie",
        ["StatusRectangle"] = "Prostok\u0105t \u2014 kliknij i przeci\u0105gnij, aby narysowa\u0107 prostok\u0105t",
        ["StatusEllipse"] = "Elipsa \u2014 kliknij i przeci\u0105gnij, aby narysowa\u0107 elips\u0119",
        ["StatusLine"] = "Linia \u2014 kliknij i przeci\u0105gnij, aby narysowa\u0107 lini\u0119",
        ["StatusNoToolSelected"] = "Nie wybrano narz\u0119dzia",
        ["AnnotationColor"] = "Kolor adnotacji:",
        ["ReadingHeaders"] = "Odczytywanie nag\u0142\u00f3wk\u00f3w DICOM...",
        ["ParsingMetadata"] = "Parsowanie metadanych",
        ["FramesFound"] = "znaleziono klatki...",
        ["BuildingThumbnails"] = "Budowanie miniatur...",
        ["Ready"] = "Gotowy",
        ["Error"] = "B\u0142\u0105d:",
        ["Playing"] = "Odtwarzanie",
        ["Paused"] = "Wstrzymano",
        ["Buffering"] = "Buforowanie",
        ["Buffered"] = "Zbuforowano",
        ["Preset"] = "Preset:",
        ["Opening"] = "Otwieranie:",
        ["WL_Center"] = "Środek:",
        ["WL_Width"] = "Szerokość:",

        // Settings Window
        ["Settings_Title"] = "Ustawienia",
        ["Settings_Language"] = "J\u0118ZYK",
        ["Settings_DisplayLanguage"] = "J\u0119zyk wy\u015bwietlania",
        ["Settings_DefaultFolder"] = "DOMY\u015aLNY FOLDER",
        ["Settings_StartupDirectory"] = "Katalog startowy",
        ["Settings_NotSet"] = "Nie ustawiono",
        ["Settings_Browse"] = "Przegl\u0105daj",
        ["Settings_DirectoryAutoLoad"] = "Ten katalog zostanie za\u0142adowany automatycznie przy uruchomieniu.",
        ["Settings_Interface"] = "INTERFEJS",
        ["Settings_ShowTooltips"] = "Poka\u017c podpowiedzi po najechaniu",
        ["Settings_ShowTooltipsDesc"] = "Wy\u015bwietlaj pomocne wskaz\u00f3wki po najechaniu na przyciski i kontrolki.",
        ["Settings_ShowNotifications"] = "Poka\u017c powiadomienia",
        ["Settings_ShowNotificationsDesc"] = "Wy\u015bwietlaj ostrze\u017cenia i b\u0142\u0119dy jako powiadomienia w g\u0142\u00f3wnym oknie.",
        ["Settings_WindowMode"] = "Tryb okna przy uruchomieniu",
        ["Settings_WindowModeDesc"] = "Wybierz spos\u00f3b otwierania okna przy uruchomieniu. Naci\u015bnij F11, aby prze\u0142\u0105czy\u0107 pe\u0142ny ekran.",
        ["Settings_Windowed"] = "Okno",
        ["Settings_Maximized"] = "Zmaksymalizowane",
        ["Settings_Fullscreen"] = "Pe\u0142ny ekran",
        ["Settings_General"] = "Og\u00f3lne",
        ["Settings_Controls"] = "Sterowanie",
        ["Settings_Save"] = "Zapisz",
        ["Settings_SaveClose"] = "Zapisz i zamknij",
        ["Settings_Saved"] = "Ustawienia zapisane",
        ["Settings_UnsavedChanges"] = "Niezapisane zmiany",

        // Settings - Controls Tab
        ["Controls_KeyboardShortcuts"] = "SKR\u00d3TY KLAWISZOWE",
        ["Controls_ResetAll"] = "Resetuj wszystko",
        ["Controls_RebindHint"] = "Kliknij skr\u00f3t, aby go zmieni\u0107, a nast\u0119pnie naci\u015bnij now\u0105 kombinacj\u0119 klawiszy.",
        ["Controls_Playback"] = "Odtwarzanie",
        ["Controls_PlayPause"] = "Odtwarzaj / Wstrzymaj",
        ["Controls_PreviousFrame"] = "Poprzednia klatka",
        ["Controls_NextFrame"] = "Nast\u0119pna klatka",
        ["Controls_FirstFrame"] = "Pierwsza klatka",
        ["Controls_LastFrame"] = "Ostatnia klatka",
        ["Controls_View"] = "Widok",
        ["Controls_ZoomIn"] = "Przybli\u017c",
        ["Controls_ZoomOut"] = "Oddal",
        ["Controls_FitToWindow"] = "Dopasuj do okna",
        ["Controls_ResetView"] = "Resetuj widok",
        ["Controls_ToggleInvert"] = "Prze\u0142\u0105cz inwersj\u0119",
        ["Controls_ToggleFullscreen"] = "Prze\u0142\u0105cz pe\u0142ny ekran",
        ["Controls_AnnotationTools"] = "Narz\u0119dzia adnotacji",
        ["Controls_ArrowTool"] = "Narz\u0119dzie strza\u0142ki",
        ["Controls_TextTool"] = "Narz\u0119dzie tekstu",
        ["Controls_FreehandTool"] = "Narz\u0119dzie odr\u0119czne",
        ["Controls_CycleColor"] = "Zmie\u0144 kolor",
        ["Controls_DeselectTool"] = "Odznacz narz\u0119dzie",
        ["Controls_File"] = "Plik",
        ["Controls_OpenFile"] = "Otw\u00f3rz plik",
        ["Controls_OpenLogs"] = "Otw\u00f3rz logi",
        ["Controls_Edit"] = "Edycja",
        ["Controls_Undo"] = "Cofnij",
        ["Controls_Redo"] = "Pon\u00f3w",
        ["Controls_PressKey"] = "Naci\u015bnij klawisz...",
        ["Controls_ConflictsWith"] = "Konflikty z:",

        // Log Viewer
        ["Log_Title"] = "Podgl\u0105d log\u00f3w",
        ["Log_Log"] = "LOG",
        ["Log_All"] = "WSZYSTKO",
        ["Log_ShowAll"] = "Poka\u017c wszystko",
        ["Log_InfoAbove"] = "Informacje i powy\u017cej",
        ["Log_WarningsErrors"] = "Ostrze\u017cenia i b\u0142\u0119dy",
        ["Log_ErrorsOnly"] = "Tylko b\u0142\u0119dy",
        ["Log_OpenFolder"] = "Otw\u00f3rz folder log\u00f3w",
        ["Log_ClearView"] = "Wyczy\u015b\u0107 podgl\u0105d log\u00f3w",
        ["Log_Refresh"] = "Od\u015bwie\u017c",

        // Notifications
        ["Notif_Info"] = "INFO",
        ["Notif_Warning"] = "OSTRZE\u017bENIE",
        ["Notif_Error"] = "B\u0141\u0104D",

        // Error Messages
        ["Err_FailedToOpen"] = "Nie uda\u0142o si\u0119 otworzy\u0107",
        ["Err_FileNotFound"] = "Nie znaleziono pliku:",
        ["Err_FileEmpty"] = "Plik jest pusty (0 bajt\u00f3w):",
        ["Err_VeryLargeFile"] = "Bardzo du\u017cy plik",
        ["Err_UnrecognizedExtension"] = "Nierozpoznane rozszerzenie",
        ["Err_AttemptingDicomParse"] = "pr\u00f3ba parsowania DICOM",
        ["Err_AccessDenied"] = "Odmowa dost\u0119pu:",
        ["Err_IOError"] = "B\u0142\u0105d odczytu:",
        ["Err_CouldNotOpenLogFolder"] = "Nie mo\u017cna otworzy\u0107 folderu log\u00f3w:",
        ["Err_VideoNotSupported"] = "Odtwarzanie wideo wymaga FFmpeg",
        ["Err_VideoNotSupported_Details"] = "Pliki wideo (.mp4, .avi, .mkv, .mov, .wmv) wymagaj\u0105 zainstalowanych bibliotek FFmpeg. Zainstaluj FFmpeg i upewnij si\u0119, \u017ce jest w zmiennej PATH.",
        ["Info_RestartRequired"] = "Wymagany restart",
        ["Info_RestartRequired_Lang"] = "Niekt\u00f3re elementy interfejsu wymagaj\u0105 restartu, aby w pe\u0142ni zastosowa\u0107 nowy j\u0119zyk.",
    };
}
