/*
 * Copyright(c) 2026 GiR-Zippo
 * Licensed under the GPL v3 license. See https://github.com/GiR-Zippo/LightAmp/blob/main/LICENSE for full license information.
 */

using BardMusicPlayer.DalamudBridge;
using BardMusicPlayer.Maestro;
using BardMusicPlayer.Pigeonhole;
using BardMusicPlayer.Quotidian;
using BardMusicPlayer.Quotidian.Structs;
using BardMusicPlayer.Seer;
using BardMusicPlayer.Transmogrify.Song;
using BardMusicPlayer.Ui.Controls;
using BardMusicPlayer.Ui.Functions;
using BardMusicPlayer.Ui.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Media.Animation;

namespace BardMusicPlayer.Ui.Classic
{
    /// <summary>
    /// Interaktionslogik für Classic_MainView.xaml
    /// </summary>
    public sealed partial class Classic_MainView : UserControl
    {
        private int MaxTracks = 1;
        private bool _directLoaded { get; set; } = false; //indicates if a song was loaded directly or from playlist
        private bool _showPlaylistGrid { get; set; } = true; //indicates if we showing the playlists or history
        private bool _loadingSongMetadata;
        private bool _playCountRecordedForCurrentSong;
        private long _obsLastElapsedSecond = -1;
        private TimeSpan _obsCurrentElapsed = TimeSpan.Zero;
        private const string DashboardDragDataFormat =
            "LightAmpDashboardSection";

        private static readonly string[] DefaultDashboardOrder =
        {
            "Tabs",
            "Player",
            "PianoRoll",
            "Keyboard"
        };

        private Point _dashboardDragStart;
        private string _dashboardDragSectionKey = string.Empty;
        private bool _loadingDashboardLayout;
        private bool _loadingSidebarLayout;

        private readonly DispatcherTimer _layoutHotkeyToastTimer =
            new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1300)
            };

        public Classic_MainView()
        {
            _loadingDashboardLayout = true;
            _loadingSidebarLayout = true;
            InitializeComponent();
            ApplyConfiguredXamlTheme();

            _layoutHotkeyToastTimer.Tick +=
                LayoutHotkeyToastTimer_Tick;

            ApplyDashboardLayoutFromSettings();
            ApplySidebarLayoutFromSettings();
            _loadingDashboardLayout = false;
            _loadingSidebarLayout = false;

            this.SongName.Text = PlaybackFunctions.GetSongName();
            BmpMaestro.Instance.OnPlaybackTimeChanged   += Instance_PlaybackTimeChanged;
            BmpMaestro.Instance.OnSongMaxTime           += Instance_PlaybackMaxTime;
            BmpMaestro.Instance.OnSongLoaded            += Instance_OnSongLoaded;
            BmpMaestro.Instance.OnPlaybackStarted       += Instance_PlaybackStarted;
            BmpMaestro.Instance.OnPlaybackStopped       += Instance_PlaybackStopped;
            BmpMaestro.Instance.OnTrackNumberChanged    += Instance_TrackNumberChanged;
            BmpMaestro.Instance.OnOctaveShiftChanged    += Instance_OctaveShiftChanged;
            BmpMaestro.Instance.OnSpeedChanged          += Instance_OnSpeedChange;

            BmpSeer.Instance.ChatLog                    += Instance_ChatLog;

            PlaylistCtl.OnLoadSongFromPlaylist          += Instance_PlaylistLoadSong;
            PlaylistCtl.OnSetPlaybuttonState            += Instance_SetPlaybuttonState;
            PlaylistCtl.OnLoadSongFromPlaylistToPreview += Instance_PlaylistLoadSongToPreview;

            PlayedHistoryCtl.OnLoadSongFromHistory      += Instance_PlaylistLoadSong;

            SongBrowser.OnLoadSongFromBrowser           += Instance_SongBrowserLoadedSong;
            SongBrowser.OnAddSongFromBrowser            += Instance_SongBrowserAddSongToPlaylist;
            SongBrowser.OnLoadSongFromBrowserToPreview  += Instance_SongBrowserLoadSongToPreview;

            XIVBrowser.OnLoadSongFromBrowser            += Instance_BMLBrowserLoadedSong;
            XIVBrowser.OnAddSongFromBrowser             += Instance_BMLBrowserAddSongToPlaylist;
            XIVBrowser.OnLoadSongFromBrowserToPreview   += Instance_BMLBrowserLoadSongToPreview;

            NetworkCtl.OnLoadSongFromNetwork            += Instance_NetworkLoadedSong;            

            BmpSeer.Instance.MidibardPlaylistEvent      += Instance_MidibardPlaylistEvent;

            Globals.Globals.OnConfigReload              += Globals_OnConfigReload;
            SettingsControl.LoadConfig();
            ApplyFeatureSettings();
            UpdateSongMetadataUi();
            ObsOutputService.ApplySettings();
            UpdateObsNowPlaying();

            Songbrowser_Source_box.SelectedIndex        = 0;
        }



        private void ApplyConfiguredXamlTheme()
        {
            string configuredTheme =
                BmpPigeonhole.Instance.LastSkin;

            if (string.IsNullOrWhiteSpace(configuredTheme)
                ||
                !configuredTheme.EndsWith(
                    ".xaml",
                    StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            try
            {
                string resolvedTheme =
                    configuredTheme;

                if (!System.IO.Path.IsPathRooted(resolvedTheme))
                {
                    resolvedTheme =
                        System.IO.Path.Combine(
                            AppDomain.CurrentDomain.BaseDirectory,
                            resolvedTheme);
                }

                resolvedTheme =
                    System.IO.Path.GetFullPath(resolvedTheme);

                if (!File.Exists(resolvedTheme))
                    return;

                Globals.Globals.SetTheme(
                    this,
                    resolvedTheme);
            }
            catch
            {
                // A broken optional external theme must never stop the Classic
                // interface from opening with its built-in resources.
            }
        }

        private void Globals_OnConfigReload(object sender, EventArgs e)
        {
            SettingsControl.LoadConfig(true);
            //Enable / Disable buttons when UseNoteOffset
            octave_txtNum.IsEnabled = !BmpPigeonhole.Instance.UseNoteOffset;
            octave_cmdUp.IsEnabled = !BmpPigeonhole.Instance.UseNoteOffset;
            octave_cmdDown.IsEnabled = !BmpPigeonhole.Instance.UseNoteOffset;

            ApplyConfiguredXamlTheme();

            ApplyDashboardLayoutFromSettings();
            ApplySidebarLayoutFromSettings();
            ApplyFeatureSettings();
            ObsOutputService.ApplySettings();
            UpdateObsNowPlaying();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            KeyHeat.InitUi();
            ApplyFeatureSettings();
            UpdateSongMetadataUi();
            ObsOutputService.ApplySettings();
            UpdateObsNowPlaying();
        }

        #region EventHandler
        private void Instance_PlaybackTimeChanged(object sender, Maestro.Events.CurrentPlayPositionEvent e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.PlaybackTimeChanged(e)));
        }

        private void Instance_PlaybackMaxTime(object sender, Maestro.Events.MaxPlayTimeEvent e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.PlaybackMaxTime(e)));
        }

        private void Instance_OnSongLoaded(object sender, Maestro.Events.SongLoadedEvent e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.OnSongLoaded(e)));
        }

        private void Instance_PlaybackStarted(object sender, bool e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.PlaybackStarted()));
        }

        private void Instance_PlaybackStopped(object sender, bool e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.PlaybackStopped()));
        }

        private void Instance_TrackNumberChanged(object sender, Maestro.Events.TrackNumberChangedEvent e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.TracknumberChanged(e)));
        }

        private void Instance_OctaveShiftChanged(object sender, Maestro.Events.OctaveShiftChangedEvent e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.OctaveShiftChanged(e)));
        }

        private void Instance_OnSpeedChange(object sender, Maestro.Events.SpeedShiftEvent e)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.SpeedShiftChange(e)));
        }

        private void Instance_ChatLog(Seer.Events.ChatLog seerEvent)
        {
            this.Dispatcher.BeginInvoke(new Action(() => this.AppendChatLog(seerEvent)));
        }

        /// <summary>
        /// triggered by the playlist when a song is loaded
        /// </summary>
        private void Instance_PlaylistLoadSong(object sender, BmpSong song)
        {
            //Inform the PlayedHistory
            if (BmpPigeonhole.Instance.EnableSongHistory)
                PlayedHistory.SongHistory.Add(song);

            PlaybackFunctions.LoadSongFromPlaylist(song);
            InstrumentInfo.Content = PlaybackFunctions.GetInstrumentNameForHostPlayer();
            _directLoaded = false;
        }

        /// <summary>
        /// triggered by the playlist
        /// </summary>
        private void Instance_SetPlaybuttonState(object sender, bool state)
        {
            Play_Button_State(state);
        }

        /// <summary>
        /// triggered by the playlist when a song will be previewed
        /// </summary>
        private void Instance_PlaylistLoadSongToPreview(object sender, BmpSong song)
        {
            SirenPreview.SirenLoadSong(song);
        }

        /// <summary>
        /// triggered by the playlist when a song will be previewed
        /// </summary>
        private void Instance_SwitchPlaylistAndHistory(object sender, bool unused)
        {
            if (!BmpPigeonhole.Instance.EnableSongHistory)
                return;

            this.Dispatcher.BeginInvoke(
                new Action(
                    delegate
                    {
                        PlaylistGrid.Visibility =
                            Visibility.Visible;

                        HistoryGrid.Visibility =
                            Visibility.Visible;

                        BmpPigeonhole.Instance.PlaylistPanelVisible =
                            true;

                        BmpPigeonhole.Instance.HistoryPanelVisible =
                            true;

                        ApplySidebarLayoutFromSettings();
                    }));
        }

        /// <summary>
        /// triggered by the songbrowser or history if a file should be loaded
        /// </summary>
        private void Instance_SongBrowserLoadedSong(object sender, string filename)
        {
            if (PlaybackFunctions.LoadSong(filename))
            {
                SongMetadataService.AssociatePath(
                    PlaybackFunctions.CurrentSong,
                    filename);

                //Inform the PlayedHistory
                if (BmpPigeonhole.Instance.EnableSongHistory)
                    PlayedHistory.SongHistory.Add(PlaybackFunctions.CurrentSong);

                InstrumentInfo.Content = PlaybackFunctions.GetInstrumentNameForHostPlayer();
                _directLoaded = true;
            }
        }

        /// <summary>
        /// triggered by the songbrowser if a file should be added to the playlist
        /// </summary>
        private void Instance_SongBrowserAddSongToPlaylist(object sender, string filename)
        {
            PlaylistCtl.AddSongToPlaylist(filename);
        }

        /// <summary>
        /// triggered by the songbrowser if a file should be load into siren
        /// </summary>
        private void Instance_SongBrowserLoadSongToPreview(object sender, string filename)
        {
            SirenPreview.SirenLoadSong(BmpSong.OpenFile(filename).Result);
        }

        /// <summary>
        /// triggered by the BMLBrowser if a song should be loaded
        /// </summary>
        private void Instance_BMLBrowserLoadedSong(object sender, BmpSong song)
        {
            //Inform the PlayedHistory
            if (BmpPigeonhole.Instance.EnableSongHistory)
                PlayedHistory.SongHistory.Add(song);

            PlaybackFunctions.LoadSongFromPlaylist(song);
            InstrumentInfo.Content = PlaybackFunctions.GetInstrumentNameForHostPlayer();
            _directLoaded = true;
        }

        /// <summary>
        /// triggered by the BMLBrowser if a song should be added to the playlist
        /// </summary>
        private void Instance_BMLBrowserAddSongToPlaylist(object sender, BmpSong song)
        {
            PlaylistCtl.AddSongToPlaylist(song);
        }

        /// <summary>
        /// Triggered by the BMLBrowser when a song should be previewed
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="song"></param>
        private void Instance_BMLBrowserLoadSongToPreview(object sender, BmpSong song)
        {
            SirenPreview.SirenLoadSong(song);
        }

        private void Instance_NetworkLoadedSong(object sender, BmpSong song)
        {
            PlaybackFunctions.LoadSongFromPlaylist(song);
        }

        private void Instance_MidibardPlaylistEvent(Seer.Events.MidibardPlaylistEvent seerEvent)
        {
            this.Dispatcher.BeginInvoke(new Action(() => PlaylistCtl.SelectSongByIndex(seerEvent.Song)));
        }

        private void PlaybackTimeChanged(Maestro.Events.CurrentPlayPositionEvent e)
        {
            ElapsedTime.Content = HelperFunctions.TimeSpanToString(e.timeSpan);

            if (!_Playbar_dragStarted)
                Playbar_Slider.Value = e.tick;

            _obsCurrentElapsed = e.timeSpan;

            long elapsedSecond =
                Math.Max(
                    0,
                    (long)e.timeSpan.TotalSeconds);

            if (elapsedSecond != _obsLastElapsedSecond)
            {
                _obsLastElapsedSecond = elapsedSecond;
                UpdateObsNowPlaying();
            }
        }

        private void PlaybackMaxTime(Maestro.Events.MaxPlayTimeEvent e)
        {
            TotalTime.Content = HelperFunctions.TimeSpanToString(e.timeSpan);
            Playbar_Slider.Maximum = e.tick;
        }

        private void OnSongLoaded(Maestro.Events.SongLoadedEvent e)
        {
            //Songtitle update
            this.SongName.Text = PlaybackFunctions.GetSongName();
            //Statistics update
            UpdateStats(e);
            //update heatmap
            KeyHeat.initUI(PlaybackFunctions.CurrentSong, NumValue, OctaveNumValue);
            _playCountRecordedForCurrentSong = false;
            _obsLastElapsedSecond = -1;
            _obsCurrentElapsed = TimeSpan.Zero;
            UpdateSongMetadataUi();
            ApplyVisualizerSettings();
            UpdateObsNowPlaying();

            SpeedNumValue = 1.0f;
            if (PlaybackFunctions.PlaybackState != PlaybackFunctions.PlaybackState_Enum.PLAYBACK_STATE_PLAYING)
                Play_Button_State(false);

            MaxTracks = e.MaxTracks;
            if (NumValue <= MaxTracks)
                return;
            NumValue = MaxTracks;

            BmpMaestro.Instance.SetTracknumberOnHost(MaxTracks);
        }

        public void PlaybackStarted()
        {
            PlaybackFunctions.PlaybackState = PlaybackFunctions.PlaybackState_Enum.PLAYBACK_STATE_PLAYING;
            Play_Button_State(true);

            if (!_playCountRecordedForCurrentSong &&
                PlaybackFunctions.CurrentSong != null)
            {
                SongMetadataService.RecordPlay(
                    PlaybackFunctions.CurrentSong);

                ObsOutputService.RecordPlayedSong(
                    PlaybackFunctions.CurrentSong,
                    PlaybackFunctions.GetInstrumentNameForHostPlayer(),
                    NumValue);

                _playCountRecordedForCurrentSong = true;
                UpdateSongMetadataUi();
                UpdateObsNowPlaying();
            }
        }

        public void PlaybackStopped()
        {
            PlaybackFunctions.StopSong();
            Play_Button_State(false);

            if (BmpPigeonhole.Instance.ObsClearNowPlayingOnStop)
                ObsOutputService.ClearNowPlaying();
            else
                UpdateObsNowPlaying();

            //if this wasn't a song from the playlist, do nothing
            if (_directLoaded)
                return;

            if (BmpPigeonhole.Instance.PlaylistAutoPlay)
            {
                PlaylistCtl.PlayNextSong();
                Random rnd = new Random();
                PlaybackFunctions.PlaySong(rnd.Next(15, 35)*100);
                Play_Button_State(true);
            }
        }

        public void TracknumberChanged(Maestro.Events.TrackNumberChangedEvent e)
        {
            if (e.IsHost)
            {
                NumValue = e.TrackNumber;
                UpdateNoteCountForTrack();
                UpdateObsNowPlaying();
            }
        }

        public void OctaveShiftChanged(Maestro.Events.OctaveShiftChangedEvent e)
        {
            if (e.IsHost)
                OctaveNumValue = e.OctaveShift;
        }

        public void SpeedShiftChange(Maestro.Events.SpeedShiftEvent e)
        {
            if (e.IsHost)
                SpeedNumValue = e.SpeedShift;
        }

        public void AppendChatLog(Seer.Events.ChatLog ev)
        {
            if (BmpMaestro.Instance.GetHostPid() == ev.ChatLogGame.Pid)
            {
                BmpChatParser.AppendText(ChatBox, ev);
                this.ChatBox.ScrollToEnd();
            }
        }
        #endregion

        private void UpdateObsNowPlaying()
        {
            TimeSpan duration =
                PlaybackFunctions.CurrentSong == null
                    ? TimeSpan.Zero
                    : PlaybackFunctions.CurrentSong.Duration;

            ObsOutputService.UpdateNowPlaying(
                PlaybackFunctions.CurrentSong,
                PlaybackFunctions.GetInstrumentNameForHostPlayer(),
                NumValue,
                _obsCurrentElapsed,
                duration);
        }

        private ContentControl[] GetDashboardSlots()
        {
            return new[]
            {
                DashboardSlot1,
                DashboardSlot2,
                DashboardSlot3,
                DashboardSlot4
            };
        }

        private Dictionary<string, FrameworkElement> GetDashboardSections()
        {
            return new Dictionary<string, FrameworkElement>(
                StringComparer.OrdinalIgnoreCase)
            {
                { "Tabs", TabsDashboardSection },
                { "Player", PlayerDashboardSection },
                { "PianoRoll", PianoRollDashboardSection },
                { "Keyboard", KeyboardDashboardSection }
            };
        }

        private IList<string> NormalizeDashboardOrder(string value)
        {
            var result = new List<string>();

            if (!string.IsNullOrWhiteSpace(value))
            {
                foreach (string item in value.Split('|'))
                {
                    string clean = item.Trim();

                    if (DefaultDashboardOrder.Any(
                            allowed =>
                                string.Equals(
                                    allowed,
                                    clean,
                                    StringComparison.OrdinalIgnoreCase))
                        &&
                        !result.Any(
                            existing =>
                                string.Equals(
                                    existing,
                                    clean,
                                    StringComparison.OrdinalIgnoreCase)))
                    {
                        result.Add(
                            DefaultDashboardOrder.First(
                                allowed =>
                                    string.Equals(
                                        allowed,
                                        clean,
                                        StringComparison.OrdinalIgnoreCase)));
                    }
                }
            }

            // Migrate layouts saved by the earlier three-section
            // dashboard. The newly movable Tabs section starts at the top.
            if (!result.Any(
                    existing =>
                        string.Equals(
                            existing,
                            "Tabs",
                            StringComparison.OrdinalIgnoreCase)))
            {
                result.Insert(0, "Tabs");
            }

            foreach (string defaultSection in DefaultDashboardOrder)
            {
                if (!result.Any(
                        existing =>
                            string.Equals(
                                existing,
                                defaultSection,
                                StringComparison.OrdinalIgnoreCase)))
                {
                    result.Add(defaultSection);
                }
            }

            return result.Take(4).ToList();
        }

        private void ApplyDashboardLayoutFromSettings()
        {
            if (DashboardSlot1 == null)
                return;

            bool previousLoadingState =
                _loadingDashboardLayout;

            _loadingDashboardLayout = true;

            try
            {
                IList<string> order =
                    NormalizeDashboardOrder(
                        BmpPigeonhole.Instance.DashboardSectionOrder);

                Dictionary<string, FrameworkElement> sections =
                    GetDashboardSections();

                ContentControl[] slots =
                    GetDashboardSlots();

                foreach (ContentControl slot in slots)
                    slot.Content = null;

                for (int index = 0;
                     index < slots.Length;
                     index++)
                {
                    slots[index].Content =
                        sections[order[index]];
                }

                BmpPigeonhole.Instance.DashboardSectionOrder =
                    string.Join("|", order);

                ApplyDashboardSectionSizes();

                DashboardLockButton.IsChecked =
                    BmpPigeonhole.Instance.DashboardLayoutLocked;

                UpdateDashboardLockVisual();
                UpdateDashboardToolbarVisibility();
                UpdateDashboardSlotVisibility();

                Dispatcher.BeginInvoke(
                    new Action(
                        delegate
                        {
                            if (LowerContentScrollViewer != null)
                                LowerContentScrollViewer.ScrollToTop();
                        }));
            }
            finally
            {
                _loadingDashboardLayout =
                    previousLoadingState;
            }
        }

        private void PersistDashboardOrder()
        {
            var order = new List<string>();

            foreach (ContentControl slot in GetDashboardSlots())
            {
                FrameworkElement section =
                    slot.Content as FrameworkElement;

                string key =
                    section == null
                        ? string.Empty
                        : Convert.ToString(section.Tag);

                if (!string.IsNullOrWhiteSpace(key))
                    order.Add(key);
            }

            BmpPigeonhole.Instance.DashboardSectionOrder =
                string.Join("|", order);
        }

        private ContentControl FindDashboardSlot(
            string sectionKey)
        {
            foreach (ContentControl slot in GetDashboardSlots())
            {
                FrameworkElement section =
                    slot.Content as FrameworkElement;

                if (section != null &&
                    string.Equals(
                        Convert.ToString(section.Tag),
                        sectionKey,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return slot;
                }
            }

            return null;
        }

        private void UpdateDashboardSlotVisibility()
        {
            foreach (ContentControl slot in GetDashboardSlots())
            {
                FrameworkElement section =
                    slot.Content as FrameworkElement;

                slot.Visibility =
                    section == null ||
                    section.Visibility != Visibility.Visible
                        ? Visibility.Collapsed
                        : Visibility.Visible;
            }
        }

        private void DashboardHandle_PreviewMouseLeftButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            if (BmpPigeonhole.Instance.DashboardLayoutLocked)
                return;

            FrameworkElement handle =
                sender as FrameworkElement;

            if (handle == null)
                return;

            _dashboardDragStart =
                e.GetPosition(this);

            _dashboardDragSectionKey =
                Convert.ToString(handle.Tag);
        }

        private void DashboardHandle_PreviewMouseMove(
            object sender,
            MouseEventArgs e)
        {
            if (BmpPigeonhole.Instance.DashboardLayoutLocked ||
                e.LeftButton != MouseButtonState.Pressed ||
                string.IsNullOrWhiteSpace(_dashboardDragSectionKey))
            {
                return;
            }

            Point current =
                e.GetPosition(this);

            if (Math.Abs(
                    current.X - _dashboardDragStart.X)
                    < SystemParameters.MinimumHorizontalDragDistance
                &&
                Math.Abs(
                    current.Y - _dashboardDragStart.Y)
                    < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }

            var data =
                new DataObject(
                    DashboardDragDataFormat,
                    _dashboardDragSectionKey);

            DragDrop.DoDragDrop(
                sender as DependencyObject,
                data,
                DragDropEffects.Move);

            _dashboardDragSectionKey =
                string.Empty;

            ResetDashboardDropHighlights();
        }

        private void DashboardSlot_DragOver(
            object sender,
            DragEventArgs e)
        {
            if (BmpPigeonhole.Instance.DashboardLayoutLocked ||
                !e.Data.GetDataPresent(
                    DashboardDragDataFormat))
            {
                e.Effects =
                    DragDropEffects.None;

                e.Handled = true;
                return;
            }

            e.Effects =
                DragDropEffects.Move;

            ContentControl target =
                sender as ContentControl;

            ResetDashboardDropHighlights();

            if (target != null)
                target.Opacity = 0.62;

            e.Handled = true;
        }

        private void DashboardSlot_DragLeave(
            object sender,
            DragEventArgs e)
        {
            ResetDashboardDropHighlights();
        }

        private void DashboardSlot_Drop(
            object sender,
            DragEventArgs e)
        {
            ResetDashboardDropHighlights();

            if (BmpPigeonhole.Instance.DashboardLayoutLocked ||
                !e.Data.GetDataPresent(
                    DashboardDragDataFormat))
            {
                return;
            }

            string sectionKey =
                e.Data.GetData(
                    DashboardDragDataFormat)
                as string;

            ContentControl source =
                FindDashboardSlot(sectionKey);

            ContentControl target =
                sender as ContentControl;

            if (source == null ||
                target == null ||
                ReferenceEquals(source, target))
            {
                return;
            }

            object sourceContent =
                source.Content;

            object targetContent =
                target.Content;

            source.Content = null;
            target.Content = null;

            source.Content =
                targetContent;

            target.Content =
                sourceContent;

            PersistDashboardOrder();
            UpdateDashboardSlotVisibility();

            e.Handled = true;
        }

        private void ResetDashboardDropHighlights()
        {
            foreach (ContentControl slot in GetDashboardSlots())
                slot.Opacity = 1.0;
        }

        private void DashboardResizeThumb_DragStarted(
            object sender,
            DragStartedEventArgs e)
        {
            if (BmpPigeonhole.Instance.DashboardLayoutLocked)
                return;

            Thumb thumb =
                sender as Thumb;

            FrameworkElement section =
                thumb == null
                    ? null
                    : thumb.Parent as FrameworkElement;

            if (section == null)
                return;

            if (double.IsNaN(section.Width))
            {
                section.Width =
                    Math.Max(
                        section.MinWidth,
                        section.ActualWidth);
            }

            if (double.IsNaN(section.Height))
            {
                section.Height =
                    Math.Max(
                        section.MinHeight,
                        section.ActualHeight);
            }

            section.HorizontalAlignment =
                HorizontalAlignment.Left;

            e.Handled = true;
        }

        private void DashboardResizeThumb_DragDelta(
            object sender,
            DragDeltaEventArgs e)
        {
            if (BmpPigeonhole.Instance.DashboardLayoutLocked)
                return;

            Thumb thumb =
                sender as Thumb;

            FrameworkElement section =
                thumb == null
                    ? null
                    : thumb.Parent as FrameworkElement;

            if (section == null)
                return;

            string direction =
                Convert.ToString(thumb.Tag);

            double currentWidth =
                double.IsNaN(section.Width)
                    ? section.ActualWidth
                    : section.Width;

            double currentHeight =
                double.IsNaN(section.Height)
                    ? section.ActualHeight
                    : section.Height;

            double minimumWidth =
                Math.Max(
                    320,
                    section.MinWidth);

            double minimumHeight =
                Math.Max(
                    100,
                    section.MinHeight);

            double maximumWidth =
                GetDashboardMaximumWidth(
                    section,
                    minimumWidth);

            double newWidth =
                currentWidth;

            double newHeight =
                currentHeight;

            double newLeft =
                Math.Max(
                    0,
                    section.Margin.Left);

            if (direction.IndexOf(
                    "Left",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                newWidth =
                    ClampDashboardDimension(
                        currentWidth - e.HorizontalChange,
                        minimumWidth,
                        maximumWidth);

                double appliedChange =
                    currentWidth - newWidth;

                newLeft =
                    Math.Max(
                        0,
                        newLeft + appliedChange);
            }

            if (direction.IndexOf(
                    "Right",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                newWidth =
                    ClampDashboardDimension(
                        currentWidth + e.HorizontalChange,
                        minimumWidth,
                        maximumWidth);
            }

            if (direction.IndexOf(
                    "Top",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                newHeight =
                    ClampDashboardDimension(
                        currentHeight - e.VerticalChange,
                        minimumHeight,
                        1400);
            }

            if (direction.IndexOf(
                    "Bottom",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                newHeight =
                    ClampDashboardDimension(
                        currentHeight + e.VerticalChange,
                        minimumHeight,
                        1400);
            }

            newLeft =
                Math.Min(
                    newLeft,
                    Math.Max(
                        0,
                        maximumWidth - newWidth));

            section.Width =
                newWidth;

            section.Height =
                newHeight;

            section.Margin =
                new Thickness(
                    newLeft,
                    section.Margin.Top,
                    section.Margin.Right,
                    section.Margin.Bottom);

            section.HorizontalAlignment =
                HorizontalAlignment.Left;

            e.Handled = true;
        }

        private void DashboardResizeThumb_DragCompleted(
            object sender,
            DragCompletedEventArgs e)
        {
            Thumb thumb =
                sender as Thumb;

            FrameworkElement section =
                thumb == null
                    ? null
                    : thumb.Parent as FrameworkElement;

            if (section == null)
                return;

            PersistDashboardSectionSize(section);
            e.Handled = true;
        }

        private double GetDashboardMaximumWidth(
            FrameworkElement section,
            double minimumWidth)
        {
            string key =
                Convert.ToString(section.Tag);

            ContentControl slot =
                FindDashboardSlot(key);

            double availableWidth =
                slot == null
                    ? 0
                    : slot.ActualWidth;

            if (availableWidth <= 1 &&
                LowerContentScrollViewer != null)
            {
                availableWidth =
                    LowerContentScrollViewer.ViewportWidth;
            }

            if (availableWidth <= 1)
                availableWidth = 1600;

            return Math.Max(
                minimumWidth,
                availableWidth - 4);
        }

        private static double ClampDashboardDimension(
            double value,
            double minimum,
            double maximum)
        {
            return Math.Max(
                minimum,
                Math.Min(
                    maximum,
                    value));
        }

        private void ApplyDashboardSectionSizes()
        {
            ApplyDashboardSectionSize(
                TabsDashboardSection,
                BmpPigeonhole.Instance.DashboardTabsWidth,
                BmpPigeonhole.Instance.DashboardTabsHeight,
                BmpPigeonhole.Instance.DashboardTabsOffsetX);

            ApplyDashboardSectionSize(
                PlayerDashboardSection,
                BmpPigeonhole.Instance.DashboardPlayerWidth,
                BmpPigeonhole.Instance.DashboardPlayerHeight,
                BmpPigeonhole.Instance.DashboardPlayerOffsetX);

            ApplyDashboardSectionSize(
                PianoRollDashboardSection,
                BmpPigeonhole.Instance.DashboardPianoRollWidth,
                BmpPigeonhole.Instance.DashboardPianoRollHeight,
                BmpPigeonhole.Instance.DashboardPianoRollOffsetX);

            ApplyDashboardSectionSize(
                KeyboardDashboardSection,
                BmpPigeonhole.Instance.DashboardKeyboardWidth,
                BmpPigeonhole.Instance.DashboardKeyboardHeight,
                BmpPigeonhole.Instance.DashboardKeyboardOffsetX);
        }

        private void ApplyDashboardSectionSize(
            FrameworkElement section,
            double width,
            double height,
            double offsetX)
        {
            if (section == null)
                return;

            if (width <= 0)
            {
                section.Width =
                    double.NaN;

                section.HorizontalAlignment =
                    HorizontalAlignment.Stretch;

                offsetX = 0;
            }
            else
            {
                double minimumWidth =
                    Math.Max(
                        320,
                        section.MinWidth);

                double maximumWidth =
                    GetDashboardMaximumWidth(
                        section,
                        minimumWidth);

                section.Width =
                    ClampDashboardDimension(
                        width,
                        minimumWidth,
                        maximumWidth);

                offsetX =
                    Math.Min(
                        Math.Max(0, offsetX),
                        Math.Max(
                            0,
                            maximumWidth - section.Width));

                section.HorizontalAlignment =
                    HorizontalAlignment.Left;
            }

            section.Height =
                ClampDashboardDimension(
                    height,
                    Math.Max(100, section.MinHeight),
                    1400);

            section.Margin =
                new Thickness(
                    Math.Max(0, offsetX),
                    section.Margin.Top,
                    section.Margin.Right,
                    section.Margin.Bottom);
        }

        private void PersistDashboardSectionSize(
            FrameworkElement section)
        {
            string key =
                Convert.ToString(section.Tag);

            double width =
                double.IsNaN(section.Width)
                    ? 0
                    : section.Width;

            double height =
                double.IsNaN(section.Height)
                    ? section.ActualHeight
                    : section.Height;

            double offsetX =
                Math.Max(
                    0,
                    section.Margin.Left);

            if (string.Equals(
                    key,
                    "Tabs",
                    StringComparison.OrdinalIgnoreCase))
            {
                BmpPigeonhole.Instance.DashboardTabsWidth = width;
                BmpPigeonhole.Instance.DashboardTabsHeight = height;
                BmpPigeonhole.Instance.DashboardTabsOffsetX = offsetX;
            }
            else if (string.Equals(
                         key,
                         "Player",
                         StringComparison.OrdinalIgnoreCase))
            {
                BmpPigeonhole.Instance.DashboardPlayerWidth = width;
                BmpPigeonhole.Instance.DashboardPlayerHeight = height;
                BmpPigeonhole.Instance.DashboardPlayerOffsetX = offsetX;
            }
            else if (string.Equals(
                         key,
                         "PianoRoll",
                         StringComparison.OrdinalIgnoreCase))
            {
                BmpPigeonhole.Instance.DashboardPianoRollWidth = width;
                BmpPigeonhole.Instance.DashboardPianoRollHeight = height;
                BmpPigeonhole.Instance.DashboardPianoRollOffsetX = offsetX;
            }
            else if (string.Equals(
                         key,
                         "Keyboard",
                         StringComparison.OrdinalIgnoreCase))
            {
                BmpPigeonhole.Instance.DashboardKeyboardWidth = width;
                BmpPigeonhole.Instance.DashboardKeyboardHeight = height;
                BmpPigeonhole.Instance.DashboardKeyboardOffsetX = offsetX;
            }
        }

        private void ClassicMainView_PreviewKeyDown(
            object sender,
            KeyEventArgs e)
        {
            Key pressedKey =
                e.Key == Key.System
                    ? e.SystemKey
                    : e.Key;

            ModifierKeys requiredModifiers =
                ModifierKeys.Control
                |
                ModifierKeys.Shift;

            if (pressedKey != Key.L
                ||
                Keyboard.Modifiers != requiredModifiers)
            {
                return;
            }

            ToggleDashboardLayoutLockFromHotkey();
            e.Handled = true;
        }

        private void ToggleDashboardLayoutLockFromHotkey()
        {
            bool locked =
                !BmpPigeonhole.Instance.DashboardLayoutLocked;

            BmpPigeonhole.Instance.DashboardLayoutLocked =
                locked;

            bool previousLoadingState =
                _loadingDashboardLayout;

            _loadingDashboardLayout = true;

            try
            {
                DashboardLockButton.IsChecked =
                    locked;
            }
            finally
            {
                _loadingDashboardLayout =
                    previousLoadingState;
            }

            UpdateDashboardLockVisual();

            ShowLayoutHotkeyToast(
                locked
                    ? "Layout locked"
                    : "Layout unlocked");
        }

        private void ShowLayoutHotkeyToast(
            string message)
        {
            if (LayoutHotkeyToast == null
                ||
                LayoutHotkeyToastText == null)
            {
                return;
            }

            LayoutHotkeyToastText.Text =
                message;

            LayoutHotkeyToast.Visibility =
                Visibility.Visible;

            _layoutHotkeyToastTimer.Stop();
            _layoutHotkeyToastTimer.Start();
        }

        private void LayoutHotkeyToastTimer_Tick(
            object sender,
            EventArgs e)
        {
            _layoutHotkeyToastTimer.Stop();

            if (LayoutHotkeyToast != null)
            {
                LayoutHotkeyToast.Visibility =
                    Visibility.Collapsed;
            }
        }

        private void DashboardLockButton_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadingDashboardLayout)
                return;

            BmpPigeonhole.Instance.DashboardLayoutLocked =
                DashboardLockButton.IsChecked == true;

            UpdateDashboardLockVisual();
        }

        private void UpdateDashboardLockVisual()
        {
            bool locked =
                BmpPigeonhole.Instance.DashboardLayoutLocked;

            DashboardLockButton.Content =
                locked
                    ? "Layout locked"
                    : "Layout unlocked";

            DashboardInstructionText.Text =
                locked
                    ? "  Ctrl+Shift+L to unlock the layout"
                    : "  Drag the ☰ in the top-left to move · drag cyan edges/corners to resize";

            DashboardLockButton.ToolTip =
                locked
                    ? "Layout locked — Ctrl+Shift+L to unlock"
                    : "Layout unlocked — Ctrl+Shift+L to lock";

            Cursor handleCursor =
                locked
                    ? Cursors.Arrow
                    : Cursors.SizeAll;

            TabsDashboardHandle.Cursor =
                handleCursor;

            PlayerDashboardHandle.Cursor =
                handleCursor;

            PianoRollDashboardHandle.Cursor =
                handleCursor;

            KeyboardDashboardHandle.Cursor =
                handleCursor;

            Visibility handleVisibility =
                locked
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            TabsDashboardHandle.Visibility =
                handleVisibility;

            PlayerDashboardHandle.Visibility =
                handleVisibility;

            PianoRollDashboardHandle.Visibility =
                handleVisibility;

            KeyboardDashboardHandle.Visibility =
                handleVisibility;

            SetDashboardResizeThumbsVisible(
                TabsDashboardSection,
                !locked);

            SetDashboardResizeThumbsVisible(
                PlayerDashboardSection,
                !locked);

            SetDashboardResizeThumbsVisible(
                PianoRollDashboardSection,
                !locked);

            SetDashboardResizeThumbsVisible(
                KeyboardDashboardSection,
                !locked);

            UpdateSidebarLockVisual();
        }

        private static void SetDashboardResizeThumbsVisible(
            FrameworkElement section,
            bool visible)
        {
            Panel panel =
                section as Panel;

            if (panel == null)
                return;

            foreach (UIElement child in panel.Children)
            {
                Thumb thumb =
                    child as Thumb;

                if (thumb == null)
                    continue;

                string direction =
                    Convert.ToString(thumb.Tag);

                if (string.Equals(direction, "Left", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(direction, "Right", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(direction, "Top", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(direction, "Bottom", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(direction, "TopLeft", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(direction, "TopRight", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(direction, "BottomLeft", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(direction, "BottomRight", StringComparison.OrdinalIgnoreCase))
                {
                    thumb.Visibility =
                        visible
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }
            }
        }

        private void DashboardHideToolbarButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.DashboardToolbarHidden = true;
            UpdateDashboardToolbarVisibility();
        }

        private void DashboardShowToolbarButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.DashboardToolbarHidden = false;
            UpdateDashboardToolbarVisibility();
        }

        private void UpdateDashboardToolbarVisibility()
        {
            if (DashboardToolbar == null ||
                DashboardShowToolbarButton == null)
            {
                return;
            }

            bool hidden =
                BmpPigeonhole.Instance.DashboardToolbarHidden;

            DashboardToolbar.Visibility =
                hidden
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            DashboardShowToolbarButton.Visibility =
                hidden
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void DashboardResetButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.DashboardSectionOrder =
                string.Join(
                    "|",
                    DefaultDashboardOrder);

            ApplyDashboardLayoutFromSettings();
        }

        private void DashboardResetSizesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.DashboardTabsWidth = 0;
            BmpPigeonhole.Instance.DashboardTabsHeight = 380;
            BmpPigeonhole.Instance.DashboardTabsOffsetX = 0;

            BmpPigeonhole.Instance.DashboardPlayerWidth = 0;
            BmpPigeonhole.Instance.DashboardPlayerHeight = 210;
            BmpPigeonhole.Instance.DashboardPlayerOffsetX = 0;

            BmpPigeonhole.Instance.DashboardPianoRollWidth = 0;
            BmpPigeonhole.Instance.DashboardPianoRollHeight = 250;
            BmpPigeonhole.Instance.DashboardPianoRollOffsetX = 0;

            BmpPigeonhole.Instance.DashboardKeyboardWidth = 0;
            BmpPigeonhole.Instance.DashboardKeyboardHeight = 190;
            BmpPigeonhole.Instance.DashboardKeyboardOffsetX = 0;

            ApplyDashboardSectionSizes();
        }

        private void ApplyFeatureSettings()
        {
            UiCustomizationManager.Apply(this);
            ApplyVisualizerSettings();
        }

        private void ApplyVisualizerSettings()
        {
            BmpPigeonhole settings =
                BmpPigeonhole.Instance;

            bool showPiano =
                settings.VisualizerShowPianoRoll;

            bool showKeyboard =
                settings.VisualizerShowKeyboard;

            PianoRollDashboardSection.Visibility =
                showPiano
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            PianoRoll.Visibility =
                showPiano
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            double pianoHeight =
                Math.Max(
                    70,
                    Math.Min(
                        320,
                        settings.VisualizerPianoRollHeight));

            PianoRollRow.Height =
                showPiano
                    ? new GridLength(pianoHeight)
                    : new GridLength(0);

            PianoRollDashboardSection.MinHeight =
                showPiano
                    ? pianoHeight + 80
                    : 0;

            KeyboardDashboardSection.Visibility =
                showKeyboard
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            KeyHeat.Visibility =
                showKeyboard
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            KeyboardRow.Height =
                showKeyboard
                    ? new GridLength(104)
                    : new GridLength(0);

            KeyboardDashboardSection.MinHeight =
                showKeyboard
                    ? 175
                    : 0;

            double thickness =
                Math.Max(
                    2,
                    Math.Min(
                        12,
                        settings.VisualizerNoteThickness));

            PianoRoll.MinimumNoteHeight = thickness;
            PianoRoll.NoteHeightScale =
                Math.Max(
                    0.75,
                    thickness / 5.0 * 1.45);

            PianoRoll.ShowNoteNames =
                settings.VisualizerShowNoteNames;

            PianoRoll.ShowBarLines =
                settings.VisualizerShowBarLines;

            PianoRoll.ShowBeatLines =
                settings.VisualizerShowBeatLines;

            PianoRoll.AnimationsEnabled =
                settings.ThemeAnimationsEnabled;

            PianoRoll.GlowStrength =
                settings.ThemeGlowStrength;

            KeyHeat.ShowNoteNames =
                settings.VisualizerShowNoteNames;

            KeyHeat.AnimationsEnabled =
                settings.ThemeAnimationsEnabled;

            KeyHeat.GlowStrength =
                settings.ThemeGlowStrength;

            VisualizerModeText.Text =
                settings.VisualizerAllTracks
                    ? "All tracks · live playback tick"
                    : "Selected track · live playback tick";

            ApplyVisualizerTrackFilter();
            UpdateDashboardSlotVisibility();
        }

        private void ApplyVisualizerTrackFilter()
        {
            if (PianoRoll == null)
                return;

            PianoRoll.TrackFilter =
                BmpPigeonhole.Instance.VisualizerAllTracks
                    ? 0
                    : NumValue;
        }

        private void ApplySidebarLayoutFromSettings()
        {
            if (PlaylistDockPanel == null ||
                HistoryDockPanel == null ||
                LeftDockHost == null ||
                RightDockHost == null)
            {
                return;
            }

            bool previousLoadingState =
                _loadingSidebarLayout;

            _loadingSidebarLayout = true;

            try
            {
                MigrateCombinedSidebarSettings();

                BmpPigeonhole settings =
                    BmpPigeonhole.Instance;

                string playlistSide =
                    NormalizeSidebarDockSide(
                        settings.PlaylistDockSide);

                string historySide =
                    NormalizeSidebarDockSide(
                        settings.HistoryDockSide);

                settings.PlaylistDockSide =
                    playlistSide;

                settings.HistoryDockSide =
                    historySide;

                settings.PlaylistPanelWidth =
                    ClampSidePanelWidth(
                        settings.PlaylistPanelWidth);

                settings.HistoryPanelWidth =
                    ClampSidePanelWidth(
                        settings.HistoryPanelWidth);

                settings.PlaylistPanelHeight =
                    ClampSidePanelHeight(
                        settings.PlaylistPanelHeight,
                        220);

                settings.HistoryPanelHeight =
                    ClampSidePanelHeight(
                        settings.HistoryPanelHeight,
                        150);

                // Rebuild the hosts in a stable order. A collapsed panel remains
                // assigned to its chosen side so its restore button appears there.
                LeftDockHost.Children.Clear();
                RightDockHost.Children.Clear();

                AddPanelToDockHost(
                    PlaylistDockPanel,
                    playlistSide);

                AddPanelToDockHost(
                    HistoryDockPanel,
                    historySide);

                ApplySidePanelGeometry(
                    PlaylistDockPanel,
                    playlistSide,
                    settings.PlaylistPanelWidth,
                    settings.PlaylistPanelHeight,
                    settings.PlaylistPanelVisible);

                ApplySidePanelGeometry(
                    HistoryDockPanel,
                    historySide,
                    settings.HistoryPanelWidth,
                    settings.HistoryPanelHeight,
                    settings.HistoryPanelVisible);

                SetDockButtonIcon(
                    PlaylistDockButton,
                    string.Equals(
                        playlistSide,
                        "Right",
                        StringComparison.OrdinalIgnoreCase));

                PlaylistDockButton.ToolTip =
                    string.Equals(
                        playlistSide,
                        "Right",
                        StringComparison.OrdinalIgnoreCase)
                        ? "Move Playlist to the left"
                        : "Move Playlist to the right";

                SetDockButtonIcon(
                    HistoryDockButton,
                    string.Equals(
                        historySide,
                        "Right",
                        StringComparison.OrdinalIgnoreCase));

                HistoryDockButton.ToolTip =
                    string.Equals(
                        historySide,
                        "Right",
                        StringComparison.OrdinalIgnoreCase)
                        ? "Move History to the left"
                        : "Move History to the right";

                Grid.SetColumn(
                    PlaylistShowButton,
                    string.Equals(
                        playlistSide,
                        "Right",
                        StringComparison.OrdinalIgnoreCase)
                        ? 3
                        : 1);

                Grid.SetColumn(
                    HistoryShowButton,
                    string.Equals(
                        historySide,
                        "Right",
                        StringComparison.OrdinalIgnoreCase)
                        ? 3
                        : 1);

                PlaylistShowButton.Visibility =
                    settings.PlaylistPanelVisible
                        ? Visibility.Collapsed
                        : Visibility.Visible;

                HistoryShowButton.Visibility =
                    settings.HistoryPanelVisible
                        ? Visibility.Collapsed
                        : Visibility.Visible;

                UpdateIndependentDockColumnWidths();
                UpdateSidebarLockVisual();
            }
            finally
            {
                _loadingSidebarLayout =
                    previousLoadingState;
            }
        }

        private void MigrateCombinedSidebarSettings()
        {
            BmpPigeonhole settings =
                BmpPigeonhole.Instance;

            if (settings.IndependentSidePanelsInitialized)
                return;

            string oldSide =
                NormalizeSidebarDockSide(
                    settings.SidebarDockSide);

            double oldWidth =
                ClampSidePanelWidth(
                    settings.SidebarWidth);

            settings.PlaylistDockSide =
                oldSide;

            settings.HistoryDockSide =
                oldSide;

            settings.PlaylistPanelVisible =
                settings.SidebarVisible;

            settings.HistoryPanelVisible =
                settings.SidebarVisible;

            settings.PlaylistPanelWidth =
                oldWidth;

            settings.HistoryPanelWidth =
                oldWidth;

            settings.PlaylistPanelHeight =
                ClampSidePanelHeight(
                    settings.SidebarPlaylistHeight + 145,
                    220);

            settings.HistoryPanelHeight =
                ClampSidePanelHeight(
                    settings.SidebarHistoryHeight + 70,
                    150);

            settings.IndependentSidePanelsInitialized =
                true;
        }

        private static string NormalizeSidebarDockSide(
            string value)
        {
            return string.Equals(
                       value,
                       "Right",
                       StringComparison.OrdinalIgnoreCase)
                ? "Right"
                : "Left";
        }

        private static double ClampSidePanelWidth(
            double value)
        {
            return Math.Max(
                190,
                Math.Min(
                    620,
                    value));
        }

        private static double ClampSidePanelHeight(
            double value,
            double minimum)
        {
            return Math.Max(
                minimum,
                Math.Min(
                    1100,
                    value));
        }

        private void SetDockButtonIcon(
            Button button,
            bool showDockLeftIcon)
        {
            if (button == null)
                return;

            Brush stroke =
                TryFindResource("TextMainBrush") as Brush
                ??
                Brushes.White;

            Viewbox viewbox =
                new Viewbox
                {
                    Width = 22,
                    Height = 16,
                    Stretch = Stretch.Uniform,
                    Margin = new Thickness(0)
                };

            Canvas canvas =
                new Canvas
                {
                    Width = 22,
                    Height = 16
                };

            Rectangle bar =
                new Rectangle
                {
                    Width = 3,
                    Height = 12,
                    RadiusX = 0.6,
                    RadiusY = 0.6,
                    Stroke = stroke,
                    Fill = Brushes.Transparent,
                    StrokeThickness = 1.8
                };

            Polyline arrow =
                new Polyline
                {
                    Stroke = stroke,
                    StrokeThickness = 1.8,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };

            Polyline connectorTop =
                new Polyline
                {
                    Stroke = stroke,
                    StrokeThickness = 1.8,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };

            Polyline connectorBottom =
                new Polyline
                {
                    Stroke = stroke,
                    StrokeThickness = 1.8,
                    StrokeLineJoin = PenLineJoin.Round,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round
                };

            if (showDockLeftIcon)
            {
                Canvas.SetLeft(bar, 15);
                Canvas.SetTop(bar, 2);

                arrow.Points =
                    new PointCollection
                    {
                        new Point(10.8, 2.8),
                        new Point(4.8, 8),
                        new Point(10.8, 13.2)
                    };

                connectorTop.Points =
                    new PointCollection
                    {
                        new Point(10.8, 5.2),
                        new Point(13.1, 5.2),
                        new Point(13.1, 4.1),
                        new Point(15, 4.1)
                    };

                connectorBottom.Points =
                    new PointCollection
                    {
                        new Point(10.8, 10.8),
                        new Point(13.1, 10.8),
                        new Point(13.1, 11.9),
                        new Point(15, 11.9)
                    };
            }
            else
            {
                Canvas.SetLeft(bar, 4);
                Canvas.SetTop(bar, 2);

                arrow.Points =
                    new PointCollection
                    {
                        new Point(11.2, 2.8),
                        new Point(17.2, 8),
                        new Point(11.2, 13.2)
                    };

                connectorTop.Points =
                    new PointCollection
                    {
                        new Point(11.2, 5.2),
                        new Point(8.9, 5.2),
                        new Point(8.9, 4.1),
                        new Point(7, 4.1)
                    };

                connectorBottom.Points =
                    new PointCollection
                    {
                        new Point(11.2, 10.8),
                        new Point(8.9, 10.8),
                        new Point(8.9, 11.9),
                        new Point(7, 11.9)
                    };
            }

            canvas.Children.Add(bar);
            canvas.Children.Add(arrow);
            canvas.Children.Add(connectorTop);
            canvas.Children.Add(connectorBottom);

            viewbox.Child = canvas;
            button.Content = viewbox;
        }

        private void AddPanelToDockHost(
            FrameworkElement panel,
            string dockSide)
        {
            Panel currentParent =
                panel.Parent as Panel;

            if (currentParent != null)
                currentParent.Children.Remove(panel);

            Panel targetHost =
                string.Equals(
                    dockSide,
                    "Right",
                    StringComparison.OrdinalIgnoreCase)
                    ? (Panel)RightDockHost
                    : LeftDockHost;

            targetHost.Children.Add(panel);
        }

        private static void ApplySidePanelGeometry(
            FrameworkElement panel,
            string dockSide,
            double width,
            double height,
            bool visible)
        {
            panel.Width =
                ClampSidePanelWidth(width);

            panel.Height =
                ClampSidePanelHeight(
                    height,
                    panel.MinHeight);

            panel.HorizontalAlignment =
                string.Equals(
                    dockSide,
                    "Right",
                    StringComparison.OrdinalIgnoreCase)
                    ? HorizontalAlignment.Left
                    : HorizontalAlignment.Right;

            panel.Visibility =
                visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            panel.Opacity =
                visible ? 1.0 : 0.0;
        }

        private void UpdateIndependentDockColumnWidths()
        {
            BmpPigeonhole settings =
                BmpPigeonhole.Instance;

            double leftWidth = 0;
            double rightWidth = 0;

            if (settings.PlaylistPanelVisible)
            {
                if (string.Equals(
                        NormalizeSidebarDockSide(
                            settings.PlaylistDockSide),
                        "Right",
                        StringComparison.OrdinalIgnoreCase))
                {
                    rightWidth =
                        Math.Max(
                            rightWidth,
                            settings.PlaylistPanelWidth);
                }
                else
                {
                    leftWidth =
                        Math.Max(
                            leftWidth,
                            settings.PlaylistPanelWidth);
                }
            }

            if (settings.HistoryPanelVisible)
            {
                if (string.Equals(
                        NormalizeSidebarDockSide(
                            settings.HistoryDockSide),
                        "Right",
                        StringComparison.OrdinalIgnoreCase))
                {
                    rightWidth =
                        Math.Max(
                            rightWidth,
                            settings.HistoryPanelWidth);
                }
                else
                {
                    leftWidth =
                        Math.Max(
                            leftWidth,
                            settings.HistoryPanelWidth);
                }
            }

            bool playlistAssignedLeft =
                string.Equals(
                    NormalizeSidebarDockSide(
                        settings.PlaylistDockSide),
                    "Left",
                    StringComparison.OrdinalIgnoreCase);

            bool historyAssignedLeft =
                string.Equals(
                    NormalizeSidebarDockSide(
                        settings.HistoryDockSide),
                    "Left",
                    StringComparison.OrdinalIgnoreCase);

            bool playlistAssignedRight =
                !playlistAssignedLeft;

            bool historyAssignedRight =
                !historyAssignedLeft;

            bool hiddenPanelOnLeft =
                (playlistAssignedLeft &&
                 !settings.PlaylistPanelVisible)
                ||
                (historyAssignedLeft &&
                 !settings.HistoryPanelVisible);

            bool hiddenPanelOnRight =
                (playlistAssignedRight &&
                 !settings.PlaylistPanelVisible)
                ||
                (historyAssignedRight &&
                 !settings.HistoryPanelVisible);

            bool panelAssignedLeft =
                playlistAssignedLeft ||
                historyAssignedLeft;

            bool panelAssignedRight =
                playlistAssignedRight ||
                historyAssignedRight;

            LeftSidebarColumn.Width =
                leftWidth > 0
                    ? new GridLength(
                        ClampSidePanelWidth(leftWidth))
                    : new GridLength(0);

            RightSidebarColumn.Width =
                rightWidth > 0
                    ? new GridLength(
                        ClampSidePanelWidth(rightWidth))
                    : new GridLength(0);

            LeftSidebarHandleColumn.Width =
                panelAssignedLeft
                    ? new GridLength(
                        hiddenPanelOnLeft ? 32 : 9)
                    : new GridLength(0);

            RightSidebarHandleColumn.Width =
                panelAssignedRight
                    ? new GridLength(
                        hiddenPanelOnRight ? 32 : 9)
                    : new GridLength(0);
        }

        private void UpdateSidebarLockVisual()
        {
            if (PlaylistDockButton == null ||
                HistoryDockButton == null)
            {
                return;
            }

            bool locked =
                BmpPigeonhole.Instance.DashboardLayoutLocked;

            PlaylistDockButton.IsEnabled =
                !locked;

            HistoryDockButton.IsEnabled =
                !locked;

            PlaylistResetSizeButton.IsEnabled =
                !locked;

            HistoryResetSizeButton.IsEnabled =
                !locked;

            // Hiding or restoring a panel remains available when locked.
            PlaylistHideButton.IsEnabled = true;
            HistoryHideButton.IsEnabled = true;
            PlaylistShowButton.IsEnabled = true;
            HistoryShowButton.IsEnabled = true;

            UpdateSidePanelResizeThumbVisibility(
                "Playlist");

            UpdateSidePanelResizeThumbVisibility(
                "History");
        }

        private void UpdateSidePanelResizeThumbVisibility(
            string panelKey)
        {
            BmpPigeonhole settings =
                BmpPigeonhole.Instance;

            bool locked =
                settings.DashboardLayoutLocked;

            bool isPlaylist =
                string.Equals(
                    panelKey,
                    "Playlist",
                    StringComparison.OrdinalIgnoreCase);

            bool visible =
                isPlaylist
                    ? settings.PlaylistPanelVisible
                    : settings.HistoryPanelVisible;

            string dockSide =
                NormalizeSidebarDockSide(
                    isPlaylist
                        ? settings.PlaylistDockSide
                        : settings.HistoryDockSide);

            bool dockRight =
                string.Equals(
                    dockSide,
                    "Right",
                    StringComparison.OrdinalIgnoreCase);

            Visibility active =
                !locked && visible
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (isPlaylist)
            {
                PlaylistLeftResizeThumb.Visibility =
                    dockRight
                        ? active
                        : Visibility.Collapsed;

                PlaylistRightResizeThumb.Visibility =
                    dockRight
                        ? Visibility.Collapsed
                        : active;

                PlaylistBottomResizeThumb.Visibility =
                    active;

                PlaylistBottomLeftResizeThumb.Visibility =
                    dockRight
                        ? active
                        : Visibility.Collapsed;

                PlaylistBottomRightResizeThumb.Visibility =
                    dockRight
                        ? Visibility.Collapsed
                        : active;
            }
            else
            {
                HistoryLeftResizeThumb.Visibility =
                    dockRight
                        ? active
                        : Visibility.Collapsed;

                HistoryRightResizeThumb.Visibility =
                    dockRight
                        ? Visibility.Collapsed
                        : active;

                HistoryBottomResizeThumb.Visibility =
                    active;

                HistoryBottomLeftResizeThumb.Visibility =
                    dockRight
                        ? active
                        : Visibility.Collapsed;

                HistoryBottomRightResizeThumb.Visibility =
                    dockRight
                        ? Visibility.Collapsed
                        : active;
            }
        }

        private void PlaylistDockButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (BmpPigeonhole.Instance.DashboardLayoutLocked)
                return;

            BmpPigeonhole settings =
                BmpPigeonhole.Instance;

            settings.PlaylistDockSide =
                string.Equals(
                    NormalizeSidebarDockSide(
                        settings.PlaylistDockSide),
                    "Right",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Left"
                    : "Right";

            ApplySidebarLayoutFromSettings();
        }

        private void HistoryDockButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (BmpPigeonhole.Instance.DashboardLayoutLocked)
                return;

            BmpPigeonhole settings =
                BmpPigeonhole.Instance;

            settings.HistoryDockSide =
                string.Equals(
                    NormalizeSidebarDockSide(
                        settings.HistoryDockSide),
                    "Right",
                    StringComparison.OrdinalIgnoreCase)
                    ? "Left"
                    : "Right";

            ApplySidebarLayoutFromSettings();
        }

        private void PlaylistHideButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.PlaylistPanelVisible =
                false;

            ApplySidebarLayoutFromSettings();
        }

        private void HistoryHideButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.HistoryPanelVisible =
                false;

            ApplySidebarLayoutFromSettings();
        }

        private void PlaylistShowButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.PlaylistPanelVisible =
                true;

            ApplySidebarLayoutFromSettings();
        }

        private void HistoryShowButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.HistoryPanelVisible =
                true;

            ApplySidebarLayoutFromSettings();
        }

        private void PlaylistResetSizeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (BmpPigeonhole.Instance.DashboardLayoutLocked)
                return;

            BmpPigeonhole.Instance.PlaylistPanelWidth = 290;
            BmpPigeonhole.Instance.PlaylistPanelHeight = 410;

            ApplySidebarLayoutFromSettings();
        }

        private void HistoryResetSizeButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (BmpPigeonhole.Instance.DashboardLayoutLocked)
                return;

            BmpPigeonhole.Instance.HistoryPanelWidth = 290;
            BmpPigeonhole.Instance.HistoryPanelHeight = 250;

            ApplySidebarLayoutFromSettings();
        }

        private void SidePanelResizeThumb_DragStarted(
            object sender,
            DragStartedEventArgs e)
        {
            if (BmpPigeonhole.Instance.DashboardLayoutLocked)
                return;

            e.Handled = true;
        }

        private void SidePanelResizeThumb_DragDelta(
            object sender,
            DragDeltaEventArgs e)
        {
            if (BmpPigeonhole.Instance.DashboardLayoutLocked)
                return;

            Thumb thumb =
                sender as Thumb;

            if (thumb == null)
                return;

            string[] tagParts =
                Convert.ToString(thumb.Tag)
                    .Split(':');

            if (tagParts.Length != 2)
                return;

            string panelKey =
                tagParts[0];

            string direction =
                tagParts[1];

            FrameworkElement panel =
                string.Equals(
                    panelKey,
                    "Playlist",
                    StringComparison.OrdinalIgnoreCase)
                    ? (FrameworkElement)PlaylistDockPanel
                    : HistoryDockPanel;

            double currentWidth =
                double.IsNaN(panel.Width)
                    ? panel.ActualWidth
                    : panel.Width;

            double currentHeight =
                double.IsNaN(panel.Height)
                    ? panel.ActualHeight
                    : panel.Height;

            double newWidth =
                currentWidth;

            double newHeight =
                currentHeight;

            if (direction.IndexOf(
                    "Left",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                newWidth =
                    currentWidth - e.HorizontalChange;
            }

            if (direction.IndexOf(
                    "Right",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                newWidth =
                    currentWidth + e.HorizontalChange;
            }

            if (direction.IndexOf(
                    "Bottom",
                    StringComparison.OrdinalIgnoreCase) >= 0)
            {
                newHeight =
                    currentHeight + e.VerticalChange;
            }

            panel.Width =
                ClampSidePanelWidth(
                    newWidth);

            panel.Height =
                ClampSidePanelHeight(
                    newHeight,
                    panel.MinHeight);

            PersistSidePanelSize(
                panelKey);

            UpdateIndependentDockColumnWidths();

            e.Handled = true;
        }

        private void SidePanelResizeThumb_DragCompleted(
            object sender,
            DragCompletedEventArgs e)
        {
            Thumb thumb =
                sender as Thumb;

            if (thumb == null)
                return;

            string[] tagParts =
                Convert.ToString(thumb.Tag)
                    .Split(':');

            if (tagParts.Length > 0)
                PersistSidePanelSize(tagParts[0]);

            e.Handled = true;
        }

        private void PersistSidePanelSize(
            string panelKey)
        {
            bool isPlaylist =
                string.Equals(
                    panelKey,
                    "Playlist",
                    StringComparison.OrdinalIgnoreCase);

            FrameworkElement panel =
                isPlaylist
                    ? (FrameworkElement)PlaylistDockPanel
                    : HistoryDockPanel;

            double width =
                ClampSidePanelWidth(
                    double.IsNaN(panel.Width)
                        ? panel.ActualWidth
                        : panel.Width);

            double height =
                ClampSidePanelHeight(
                    double.IsNaN(panel.Height)
                        ? panel.ActualHeight
                        : panel.Height,
                    panel.MinHeight);

            if (isPlaylist)
            {
                BmpPigeonhole.Instance.PlaylistPanelWidth =
                    width;

                BmpPigeonhole.Instance.PlaylistPanelHeight =
                    height;
            }
            else
            {
                BmpPigeonhole.Instance.HistoryPanelWidth =
                    width;

                BmpPigeonhole.Instance.HistoryPanelHeight =
                    height;
            }
        }

        private void UpdateSongMetadataUi()
        {
            _loadingSongMetadata = true;

            try
            {
                SongMetadataSnapshot metadata =
                    SongMetadataService.Get(
                        PlaybackFunctions.CurrentSong);

                SongFavoriteButton.IsChecked =
                    metadata.Favorite;

                SongFavoriteButton.Content =
                    metadata.Favorite ? "★" : "☆";

                SongRatingComboBox.SelectedIndex =
                    Math.Max(
                        0,
                        Math.Min(
                            5,
                            metadata.Rating));

                SongPlayCountText.Text =
                    metadata.PlayCount == 1
                        ? "Played 1 time"
                        : "Played "
                          + metadata.PlayCount
                          + " times";

                DateTime lastPlayed;
                if (DateTime.TryParse(
                        metadata.LastPlayedUtc,
                        null,
                        System.Globalization.DateTimeStyles.RoundtripKind,
                        out lastPlayed))
                {
                    SongLastPlayedText.Text =
                        "Last "
                        + lastPlayed
                            .ToLocalTime()
                            .ToString("g");
                }
                else
                {
                    SongLastPlayedText.Text =
                        "Never played";
                }

                IList<string> availableTags =
                    SongMetadataService.GetAvailableTags();

                SongTagPickerComboBox.ItemsSource =
                    availableTags
                        .Where(
                            available =>
                                !metadata.Tags.Any(
                                    assigned =>
                                        string.Equals(
                                            assigned,
                                            available,
                                            StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                SongTagPickerComboBox.SelectedIndex =
                    SongTagPickerComboBox.Items.Count > 0
                        ? 0
                        : -1;

                SongAssignedTagsItemsControl.ItemsSource =
                    metadata.Tags.ToList();

                bool hasSong =
                    PlaybackFunctions.CurrentSong != null;

                if (hasSong)
                {
                    PlaylistCtl.SyncFavoritePlaylist(
                        PlaybackFunctions.CurrentSong,
                        metadata.Favorite);
                }

                SongTagPickerComboBox.IsEnabled = hasSong;
                AssignSongTagButton.IsEnabled = hasSong;
                CreateSongTagButton.IsEnabled = hasSong;
            }
            finally
            {
                _loadingSongMetadata = false;
            }
        }

        private void SongFavoriteButton_Changed(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadingSongMetadata)
                return;

            bool favorite =
                SongFavoriteButton.IsChecked == true;

            SongMetadataService.SetFavorite(
                PlaybackFunctions.CurrentSong,
                favorite);

            PlaylistCtl.SyncFavoritePlaylist(
                PlaybackFunctions.CurrentSong,
                favorite);

            SongFavoriteButton.Content =
                favorite ? "★" : "☆";
        }

        private void SongRatingComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_loadingSongMetadata)
                return;

            SongMetadataService.SetRating(
                PlaybackFunctions.CurrentSong,
                Math.Max(
                    0,
                    SongRatingComboBox.SelectedIndex));
        }

        private void AssignSongTagButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            string selectedTag =
                SongTagPickerComboBox.SelectedItem as string;

            if (string.IsNullOrWhiteSpace(selectedTag) ||
                PlaybackFunctions.CurrentSong == null)
            {
                return;
            }

            SongMetadataService.AddTag(
                PlaybackFunctions.CurrentSong,
                selectedTag);

            RefreshTagUiAndBrowser();
        }

        private void CreateSongTagButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (PlaybackFunctions.CurrentSong == null)
                return;

            var dialog = new TagPromptDialog
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true)
                return;

            string newTag =
                SongMetadataService.AddCatalogTag(
                    dialog.TagName);

            if (string.IsNullOrWhiteSpace(newTag))
                return;

            SongMetadataService.AddTag(
                PlaybackFunctions.CurrentSong,
                newTag);

            RefreshTagUiAndBrowser();
        }

        private void AssignedSongTagChip_Click(
            object sender,
            RoutedEventArgs e)
        {
            Button button = sender as Button;
            string tag =
                button == null
                    ? string.Empty
                    : Convert.ToString(button.Tag);

            if (string.IsNullOrWhiteSpace(tag) ||
                PlaybackFunctions.CurrentSong == null)
            {
                return;
            }

            SongMetadataService.RemoveTag(
                PlaybackFunctions.CurrentSong,
                tag);

            RefreshTagUiAndBrowser();
        }

        private void RefreshTagUiAndBrowser()
        {
            UpdateSongMetadataUi();

            if (SongBrowser != null)
                SongBrowser.RefreshTagFilterChoices();
        }

        #region Track UP/Down
        private int _numValue = 1;
        public int NumValue
        {
            get { return _numValue; }
            set
            {
                _numValue = value;
                track_txtNum.Text = "t" + value.ToString();

                //update heatmap
                KeyHeat.initUI(PlaybackFunctions.CurrentSong, NumValue, OctaveNumValue);
                ApplyVisualizerTrackFilter();
                this.InstrumentInfo.Content = PlaybackFunctions.GetInstrumentNameForHostPlayer();
            }
        }
        private void track_cmdUp_Click(object sender, RoutedEventArgs e)
        {
            if (NumValue == MaxTracks)
                return;
            NumValue++;
            BmpMaestro.Instance.SetTracknumberOnHost(NumValue);
        }

        private void track_cmdDown_Click(object sender, RoutedEventArgs e)
        {
            if (NumValue == 1)
                return;
            NumValue--;
            BmpMaestro.Instance.SetTracknumberOnHost(NumValue);
        }

        private void track_txtNum_KeyDownHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                if (track_txtNum == null)
                    return;

                if (int.TryParse(track_txtNum.Text.ToLower().Replace("t", ""), out var num))
                {
                    if (num < 0 || num > MaxTracks)
                    {
                        track_txtNum.Text = "t" + NumValue.ToString();
                        return;
                    }
                    _numValue = num;
                    track_txtNum.Text = "t" + _numValue.ToString();
                    BmpMaestro.Instance.SetTracknumberOnHost(_numValue);
                }
            }
        }
        #endregion

        #region Octave UP/Down
        private int _octavenumValue = 1;
        public int OctaveNumValue
        {
            get { return _octavenumValue; }
            set
            {
                _octavenumValue = value;
                octave_txtNum.Text = @"ø" + value.ToString();
                KeyHeat.initUI(PlaybackFunctions.CurrentSong, NumValue, OctaveNumValue);
            }
        }
        private void octave_cmdUp_Click(object sender, RoutedEventArgs e)
        {
            OctaveNumValue++;
            BmpMaestro.Instance.SetOctaveshiftOnHost(OctaveNumValue);
        }

        private void octave_cmdDown_Click(object sender, RoutedEventArgs e)
        {
            OctaveNumValue--;
            BmpMaestro.Instance.SetOctaveshiftOnHost(OctaveNumValue);
        }

        private void octave_txtNum_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (octave_txtNum == null)
                return;

            if (int.TryParse(octave_txtNum.Text.Replace(@"ø", ""), out _octavenumValue))
            {
                octave_txtNum.Text = @"ø" + _octavenumValue.ToString();
                BmpMaestro.Instance.SetOctaveshiftOnHost(_octavenumValue);
            }
        }
        #endregion

        #region Speed shift
        private float _speedNumValue = 1.0f;
        public float SpeedNumValue
        {
            get { return _speedNumValue; }
            set
            {
                _speedNumValue = value;
                speed_txtNum.Text = (value*100).ToString()+"%";
            }
        }

        private void speed_txtNum_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (speed_txtNum == null)
                return;

            int t = 0;
            if (int.TryParse(speed_txtNum.Text.Replace(@"%", ""), out t))
            {
                var speedShift = (Convert.ToDouble(t) / 100).Clamp(0.1f, 2.0f);
                BmpMaestro.Instance.SetSpeedShiftAll((float)speedShift);
            }
        }

        private void speed_cmdUp_Click(object sender, RoutedEventArgs e)
        {
            var speedShift = (SpeedNumValue +0.01);
            BmpMaestro.Instance.SetSpeedShiftAll((float)speedShift);
        }

        private void speed_cmdDown_Click(object sender, RoutedEventArgs e)
        {
            var speedShift = (SpeedNumValue - 0.01);
            BmpMaestro.Instance.SetSpeedShiftAll((float)speedShift);
        }
        #endregion


        private void ChatInputText_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return)
            {
                var perf = BmpMaestro.Instance.GetAllPerformers().FirstOrDefault(n => n.HostProcess == true);
                if (perf == null)
                    return;

                if (!perf.UsesDalamud)
                    return;

                string text = new string(ChatInputText.Text.ToCharArray());
                GameExtensions.SendText(perf.game, ChatMessageChannelType.Say, text);
                ChatInputText.Text = "";
            }
        }

        private void Playlist_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Playlist and History are now independent panels. Keep both
            // content controls visible instead of swapping one for the other.
            PlaylistGrid.Visibility =
                Visibility.Visible;

            HistoryGrid.Visibility =
                Visibility.Visible;
        }

        private void SongBrowser_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Tab)
                return;

            if (!SongBrowserGrid.IsMouseOver)
                return;


            e.Handled = true;

            if (SongBrowser.Visibility == Visibility.Hidden)
            {
                Songbrowser_Source_box.SelectedIndex = 0;
                XIVBrowser.Visibility = Visibility.Hidden;
                SongBrowser.Visibility = Visibility.Visible;
                SongBrowser.SongPath.Focus();
            }
            else
            {
                Songbrowser_Source_box.SelectedIndex = 1;
                XIVBrowser.Visibility = Visibility.Visible;
                XIVBrowser.RefreshButton.Focus();
                SongBrowser.Visibility = Visibility.Hidden;
            }
            
        }

        private void Songbrowser_Source_box_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!Songbrowser_Source_box.IsMouseOver)
            {
                e.Handled = true;
                return;
            }
            if (Songbrowser_Source_box.SelectedIndex == 0)
            {
                Songbrowser_Source_box.SelectedIndex = 0;
                XIVBrowser.Visibility = Visibility.Hidden;
                NetworkCtl.Visibility = Visibility.Hidden;
                SongBrowser.Visibility = Visibility.Visible;
                SongBrowser.SongPath.Focus();
            }
            else if (Songbrowser_Source_box.SelectedIndex == 1)
            {
                Songbrowser_Source_box.SelectedIndex = 1;
                XIVBrowser.Visibility = Visibility.Visible;
                XIVBrowser.RefreshButton.Focus();
                SongBrowser.Visibility = Visibility.Hidden;
                NetworkCtl.Visibility = Visibility.Hidden;
            }
            else if (Songbrowser_Source_box.SelectedIndex == 2)
            {
                Songbrowser_Source_box.SelectedIndex = 2;
                NetworkCtl.Visibility = Visibility.Visible;
                SongBrowser.Visibility = Visibility.Hidden;
                XIVBrowser.Visibility = Visibility.Hidden;
            }
        }
    }
}