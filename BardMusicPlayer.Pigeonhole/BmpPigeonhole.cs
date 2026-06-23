/*
 * Copyright(c) 2026 GiR-Zippo, 2021 MoogleTroupe
 * Licensed under the GPL v3 license. See https://github.com/GiR-Zippo/LightAmp/blob/main/LICENSE for full license information.
 */

using BardMusicPlayer.Pigeonhole.JsonSettings.Autosave;
using BardMusicPlayer.Quotidian;
using System.Collections.Generic;

namespace BardMusicPlayer.Pigeonhole
{
    public class BmpPigeonhole : JsonSettings.JsonSettings
    {
        private static BmpPigeonhole _instance;

        /// <summary>
        /// Initializes the pigeonhole file
        /// </summary>
        /// <param name="filename">full path to the json pigeonhole file</param>
        public static void Initialize(string filename)
        {
            if (Initialized) return;
            _instance = Load<BmpPigeonhole>(filename).EnableAutosave();
        }

        /// <summary>
        /// 
        /// </summary>
        public static bool Initialized => _instance != null;

        /// <summary>
        /// Gets this pigeonhole instance
        /// </summary>
        public static BmpPigeonhole Instance => _instance ?? throw new BmpException("This pigeonhole must be initialized first.");

        #region Playlist Settings

        /// <summary>
        /// Sets PlayAllTracks
        /// </summary>
        public virtual bool PlayAllTracks { get; set; } = false;

        /// <summary>
        /// Sets PlaylistDelay
        /// </summary>
        public virtual float PlaylistDelay { get; set; } = 1;

        /// <summary>
        /// Sets PlayAllTracks
        /// </summary>
        public virtual bool PlaylistAutoPlay { get; set; } = false;

        /// <summary>
        /// last loaded song
        /// </summary>
        public virtual string LastLoadedCatalog { get; set; } = "";

        #endregion

        /// <summary>
        /// sets/gets if the host should be switches according to the group lead
        /// </summary>
        public virtual bool AutoselectHost { get; set; } = true;

        /// <summary>
        /// last loaded song
        /// </summary>
        public virtual string SongDirectory { get; set; } = "songs/";

        /// <summary>
        /// Displays Local songs as collapsible folders instead of the
        /// original flat recursive list.
        /// </summary>
        public virtual bool SongBrowserUseTreeView { get; set; } = false;

        /// <summary>
        /// Distinguishes the initial default expansion from a user choosing
        /// Collapse All, which legitimately stores an empty folder list.
        /// </summary>
        public virtual bool SongBrowserTreeExpansionInitialized { get; set; } = false;

        /// <summary>
        /// Pipe-separated normalized folder paths. The pipe character cannot
        /// be used in Windows folder names, making it safe as a delimiter.
        /// </summary>
        public virtual string SongBrowserExpandedFolders { get; set; } = "";

        /// <summary>
        /// Pipe-separated Local Song Browser tag filters. An empty value means
        /// All tags. Multiple entries use AND matching.
        /// </summary>
        public virtual string SongBrowserSelectedTags { get; set; } = "";

        /// <summary>
        /// Local Song Browser rating filter.
        /// -1 = all ratings, 0 = unrated, 1 through 5 = exact rating.
        /// </summary>
        public virtual int SongBrowserRatingFilter { get; set; } = -1;

        /// <summary>
        /// When enabled, the Local Song Browser displays only songs whose
        /// persistent Favorite metadata is set.
        /// </summary>
        public virtual bool SongBrowserFavoritesOnly { get; set; } = false;

        /// <summary>
        /// hold long notes
        /// </summary>
        public virtual bool HoldNotes { get; set; } = true;

        /// <summary>
        /// save the chatlog
        /// </summary>
        public virtual bool SaveChatLog { get; set; } = false;

        /// <summary>
        /// Sets the autostart method
        /// </summary>
        public virtual int AutostartMethod { get; set; } = 2;

        /// <summary>
        /// Sets UnequipPause
        /// </summary>
        public virtual bool UnequipPause { get; set; } = true;

        /// <summary>
        /// last selected midi input device
        /// </summary>
        public virtual int MidiInputDev { get; set; } = -1;

        /// <summary>
        /// are we using the LA for live midi input play
        /// </summary>
        public virtual bool LiveMidiPlayDelay { get; set; } = false;

        /// <summary>
        /// force the playback
        /// </summary>
        public virtual bool ForcePlayback { get; set; } = false;

        /// <summary>
        /// brings the bmp to front
        /// </summary>
        public virtual bool BringBMPtoFront { get; set; } = false;

        /// <summary>
        /// Enables the multibox feature
        /// </summary>
        public virtual bool EnableMultibox { get; set; } = true;

        /// <summary>
        /// BMP window location
        /// </summary>
        public virtual global::System.Drawing.Point BmpLocation { get; set; } = System.Drawing.Point.Empty;

        public virtual global::System.Drawing.Size BmpSize { get; set; } = System.Drawing.Size.Empty;

        /// <summary>
        /// Sets/Gets last used skin
        /// </summary>
        public virtual string LastSkin { get; set; } = "";

        /// <summary>
        /// Tracks the one-time default-theme migration. This prevents the
        /// application from forcing BlackGlassNeon again after the user
        /// deliberately selects another theme or the built-in Default theme.
        /// </summary>
        public virtual bool DefaultThemeInitialized { get; set; } = false;

        /// <summary>
        /// Folder scanned for external XAML themes.
        /// </summary>
        public virtual string ThemeDirectory { get; set; } = "Themes";

        #region Visualizer Settings

        public virtual bool VisualizerShowPianoRoll { get; set; } = true;
        public virtual bool VisualizerShowKeyboard { get; set; } = true;
        public virtual double VisualizerPianoRollHeight { get; set; } = 150.0;
        public virtual double VisualizerNoteThickness { get; set; } = 5.0;
        public virtual bool VisualizerShowNoteNames { get; set; } = false;
        public virtual bool VisualizerShowBarLines { get; set; } = true;
        public virtual bool VisualizerShowBeatLines { get; set; } = true;
        public virtual bool VisualizerAllTracks { get; set; } = true;

        #endregion

        #region Main Window Dashboard

        /// <summary>
        /// Pipe-separated section order used by the four dashboard slots.
        /// Valid values are Tabs, Player, PianoRoll and Keyboard.
        /// </summary>
        public virtual string DashboardSectionOrder { get; set; } =
            "Tabs|Player|PianoRoll|Keyboard";

        public virtual bool DashboardLayoutLocked { get; set; } = false;
        public virtual bool DashboardToolbarHidden { get; set; } = false;

        /// <summary>
        /// Dock side for the combined Playlist / History sidebar.
        /// Valid values are Left and Right.
        /// </summary>
        public virtual string SidebarDockSide { get; set; } = "Left";

        public virtual bool SidebarVisible { get; set; } = true;
        public virtual double SidebarWidth { get; set; } = 290;
        public virtual double SidebarPlaylistHeight { get; set; } = 260;
        public virtual double SidebarHistoryHeight { get; set; } = 180;

        // Independent Playlist / History docking. The old combined-sidebar
        // settings above are retained for one-time migration.
        public virtual bool IndependentSidePanelsInitialized { get; set; } = false;

        public virtual string PlaylistDockSide { get; set; } = "Left";
        public virtual bool PlaylistPanelVisible { get; set; } = true;
        public virtual double PlaylistPanelWidth { get; set; } = 290;
        public virtual double PlaylistPanelHeight { get; set; } = 410;

        public virtual string HistoryDockSide { get; set; } = "Left";
        public virtual bool HistoryPanelVisible { get; set; } = true;
        public virtual double HistoryPanelWidth { get; set; } = 290;
        public virtual double HistoryPanelHeight { get; set; } = 250;

        // A width of 0 means stretch to the available dashboard width.
        public virtual double DashboardTabsWidth { get; set; } = 0;
        public virtual double DashboardTabsHeight { get; set; } = 380;
        public virtual double DashboardTabsOffsetX { get; set; } = 0;

        public virtual double DashboardPlayerWidth { get; set; } = 0;
        public virtual double DashboardPlayerHeight { get; set; } = 210;
        public virtual double DashboardPlayerOffsetX { get; set; } = 0;

        public virtual double DashboardPianoRollWidth { get; set; } = 0;
        public virtual double DashboardPianoRollHeight { get; set; } = 250;
        public virtual double DashboardPianoRollOffsetX { get; set; } = 0;

        public virtual double DashboardKeyboardWidth { get; set; } = 0;
        public virtual double DashboardKeyboardHeight { get; set; } = 190;
        public virtual double DashboardKeyboardOffsetX { get; set; } = 0;

        #endregion

        #region Theme Overrides

        public virtual string ThemeAccentColor { get; set; } = "";
        public virtual double ThemeGlowStrength { get; set; } = 1.0;
        public virtual bool ThemeAnimationsEnabled { get; set; } = true;
        public virtual double ThemeCornerRadius { get; set; } = 10.0;
        public virtual double ThemeUiScale { get; set; } = 1.0;
        public virtual double ThemeFontSize { get; set; } = 12.0;
        public virtual double ThemeDarkenAmount { get; set; } = 0.0;

        #endregion

        #region Song Favorites and Ratings

        public virtual Dictionary<string, bool> SongFavorites { get; set; } =
            new Dictionary<string, bool>();

        public virtual Dictionary<string, int> SongRatings { get; set; } =
            new Dictionary<string, int>();

        public virtual Dictionary<string, int> SongPlayCounts { get; set; } =
            new Dictionary<string, int>();

        public virtual Dictionary<string, string> SongLastPlayedUtc { get; set; } =
            new Dictionary<string, string>();

        public virtual Dictionary<string, string> SongTags { get; set; } =
            new Dictionary<string, string>();

        /// <summary>
        /// User-created tag names available in the Now Playing tag picker and
        /// Song Browser filter. Built-in defaults are merged at runtime.
        /// </summary>
        public virtual List<string> SongTagCatalog { get; set; } =
            new List<string>
            {
                "Solo",
                "Octet",
                "Metal",
                "Jazz",
                "Needs Editing"
            };

        /// <summary>
        /// Maps normalized source file paths to the metadata key used for the
        /// loaded BmpSong. This lets Song Browser filter files without opening
        /// and parsing every MIDI during each refresh.
        /// </summary>
        public virtual Dictionary<string, string> SongPathAliases { get; set; } =
            new Dictionary<string, string>();

        #endregion

        #region OBS Text Output

        public virtual bool ObsOutputEnabled { get; set; } = false;

        /// <summary>
        /// Relative paths are resolved beside LightAmp.exe.
        /// </summary>
        public virtual string ObsOutputDirectory { get; set; } = "OBS";

        public virtual string ObsNowPlayingTemplate { get; set; } = "{song}";
        public virtual string ObsHistoryTemplate { get; set; } = "{song}";
        public virtual int ObsHistoryLength { get; set; } = 5;
        public virtual bool ObsHistoryShowTimestamp { get; set; } = false;
        public virtual bool ObsClearNowPlayingOnStop { get; set; } = false;

        /// <summary>
        /// Most-recent-first rendered history entries.
        /// </summary>
        public virtual List<string> ObsSongHistory { get; set; } =
            new List<string>();

        #endregion

        /// <summary>
        /// open local orchestra after hooking new proc
        /// </summary>
        public virtual bool LocalOrchestra { get; set; } = true;

        /// <summary>
        /// Enable the 16 voice limit in Synthesizer
        /// </summary>
        public virtual bool EnableSynthVoiceLimiter { get; set; } = false;

        /// <summary>
        /// milliseconds till ready check confirmation.
        /// </summary>
        public virtual int EnsembleReadyDelay { get; set; } = 500;

        /// <summary>
        /// playback delay enabled
        /// </summary>
        public virtual bool EnsemblePlayDelay { get; set; } = false;

        /// <summary>
        /// autoequip bards after song loaded
        /// </summary>
        public virtual bool AutoEquipBards { get; set; } = false;

        /// <summary>
        /// keep the ensmble track settings
        /// </summary>
        public virtual bool EnsembleKeepTrackSetting { get; set; } = true;

        /// <summary>
        /// ignores the progchange
        /// </summary>
        public virtual bool IgnoreProgChange { get; set; } = false;

        /// <summary>
        /// start the performer by it's own ready signal
        /// </summary>
        public virtual bool EnsembleStartIndividual { get; set; } = true;

        /// <summary>
        /// milliseconds between game process scans / seer scanner startups.
        /// </summary>
        public virtual int SeerGameScanCooldown { get; set; } = 20;

        /// <summary>
        /// Contains the last path of an opened midi file
        /// </summary>
        public virtual string LastOpenedMidiPath { get; set; } = "";

        /// <summary>
        /// Compatmode for MidiBard
        /// </summary>
        public virtual bool MidiBardCompatMode { get; set; } = false;

        /// <summary>
        /// unkown but used
        /// </summary>
        public virtual bool UsePluginForKeyOutput { get; set; } = false;

        /// <summary>
        /// Use the Hypnotoad for instruemtn eq
        /// </summary>
        public virtual bool UsePluginForInstrumentOpen { get; set; } = false;

        /// <summary>
        /// Defaults to log level Info
        /// </summary>
        public virtual BmpLog.Verbosity DefaultLogLevel { get; set; } = BmpLog.Verbosity.Info;

        /// <summary>
        /// Use the NoteOffset instead the instrument offset
        /// </summary>
        public virtual bool UseNoteOffset { get; set; } = false;

        /// <summary>
        /// Loads the perfomer settings automatically
        /// </summary>
        public virtual bool AutoLoadPerformers { get; set; } = false;

        /// <summary>
        /// Enable channel to prog events
        /// </summary>
        public virtual bool ChannelToProgram { get; set; } = false;

        /// <summary>
        /// The last loaded perfomer profile
        /// </summary>
        public virtual string LastLoadedPerformerProfile { get; set; } = "";

        /// <summary>
        /// Use the LyricsOffset to keep lyrics in sync with the ensemble
        /// </summary>
        public virtual bool UseLyricsOffset { get; set; } = false;

        /// <summary>
        /// Player HomeWorld cache
        /// </summary>
        public virtual string PlayerHomeWorldCache { get; set; } = "";

        /// <summary>
        /// Autoaccepts the party invite from local account
        /// </summary>
        public virtual bool AutoAcceptPartyInvite { get; set; } = false;

        /// <summary>
        /// Songhistory in use or not
        /// </summary>
        public virtual bool EnableSongHistory { get; set; } = false;

        /// <summary>
        /// The ApiKey for the BMP Upload
        /// </summary>
        public virtual string BMPApiKey { get; set; } = "";
    }
}
