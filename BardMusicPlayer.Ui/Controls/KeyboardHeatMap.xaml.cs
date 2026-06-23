/*
 * Copyright(c) 2026 GiR-Zippo
 * Licensed under the GPL v3 license.
 * See https://github.com/GiR-Zippo/LightAmp/blob/main/LICENSE
 * for full license information.
 */

using BardMusicPlayer.Maestro;
using BardMusicPlayer.Transmogrify.Song;
using BardMusicPlayer.Ui.Functions;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Shapes;

namespace BardMusicPlayer.Ui.Controls
{
    public struct NoteRectInfo
    {
        public NoteRectInfo(string n, bool blk, int freq)
        {
            name = n;
            black_key = blk;
            frequency = freq;
        }

        public string name;
        public bool black_key;
        public int frequency;
    };

    public sealed partial class KeyboardHeatMap : UserControl
    {
        private sealed class LiveNote
        {
            public long StartTick;
            public long EndTick;
            public int MidiNote;
        }

        int current_Track { get; set; } = -1;
        private int mOctave = 4;

        private readonly List<LiveNote> _liveNotes =
            new List<LiveNote>();

        private readonly HashSet<string> _liveKeyNames =
            new HashSet<string>(StringComparer.Ordinal);

        private long _longestLiveNote;
        private bool _isPlaying;
        private bool _isSubscribed;
        private const long GlowTailTicks = 12;

        private readonly Dictionary<int, NoteRectInfo> noteInfo =
            new Dictionary<int, NoteRectInfo>
        {
            { 0, new NoteRectInfo("C", false, 60) },
            { 1, new NoteRectInfo("CSharp", true, 61) },
            { 2, new NoteRectInfo("D", false, 62) },
            { 3, new NoteRectInfo("DSharp", true, 63) },
            { 4, new NoteRectInfo("E", false, 64) },
            { 5, new NoteRectInfo("F", false, 65) },
            { 6, new NoteRectInfo("FSharp", true, 66) },
            { 7, new NoteRectInfo("G", false, 67) },
            { 8, new NoteRectInfo("GSharp", true, 68) },
            { 9, new NoteRectInfo("A", false, 69) },
            { 10, new NoteRectInfo("ASharp", true, 70) },
            { 11, new NoteRectInfo("H", false, 71) },
            { 12, new NoteRectInfo("COne", false, 60) },
            { 13, new NoteRectInfo("CSharpOne", true, 61) },
            { 14, new NoteRectInfo("DOne", false, 62) },
            { 15, new NoteRectInfo("DSharpOne", true, 63) },
            { 16, new NoteRectInfo("EOne", false, 64) },
            { 17, new NoteRectInfo("FOne", false, 65) },
            { 18, new NoteRectInfo("FSharpOne", true, 66) },
            { 19, new NoteRectInfo("GOne", false, 67) },
            { 20, new NoteRectInfo("GSharpOne", true, 68) },
            { 21, new NoteRectInfo("AOne", false, 69) },
            { 22, new NoteRectInfo("ASharpOne", true, 70) },
            { 23, new NoteRectInfo("HOne", false, 71) },
            { 24, new NoteRectInfo("CTwo", false, 60) },
            { 25, new NoteRectInfo("CSharpTwo", true, 61) },
            { 26, new NoteRectInfo("DTwo", false, 62) },
            { 27, new NoteRectInfo("DSharpTwo", true, 63) },
            { 28, new NoteRectInfo("ETwo", false, 64) },
            { 29, new NoteRectInfo("FTwo", false, 65) },
            { 30, new NoteRectInfo("FSharpTwo", true, 66) },
            { 31, new NoteRectInfo("GTwo", false, 67) },
            { 32, new NoteRectInfo("GSharpTwo", true, 68) },
            { 33, new NoteRectInfo("ATwo", false, 69) },
            { 34, new NoteRectInfo("ASharpTwo", true, 70) },
            { 35, new NoteRectInfo("HTwo", false, 71) },
            { 36, new NoteRectInfo("CThree", false, 72) }
        };

        private readonly HashSet<string> _pressedKeyNames =
            new HashSet<string>(StringComparer.Ordinal);

        public KeyboardHeatMap()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty ShowNoteNamesProperty =
            DependencyProperty.Register(
                nameof(ShowNoteNames),
                typeof(bool),
                typeof(KeyboardHeatMap),
                new FrameworkPropertyMetadata(
                    false,
                    OnVisualOptionChanged));

        public bool ShowNoteNames
        {
            get { return (bool)GetValue(ShowNoteNamesProperty); }
            set { SetValue(ShowNoteNamesProperty, value); }
        }

        public static readonly DependencyProperty AnimationsEnabledProperty =
            DependencyProperty.Register(
                nameof(AnimationsEnabled),
                typeof(bool),
                typeof(KeyboardHeatMap),
                new FrameworkPropertyMetadata(true));

        public bool AnimationsEnabled
        {
            get { return (bool)GetValue(AnimationsEnabledProperty); }
            set { SetValue(AnimationsEnabledProperty, value); }
        }

        public static readonly DependencyProperty GlowStrengthProperty =
            DependencyProperty.Register(
                nameof(GlowStrength),
                typeof(double),
                typeof(KeyboardHeatMap),
                new FrameworkPropertyMetadata(1.0));

        public double GlowStrength
        {
            get { return (double)GetValue(GlowStrengthProperty); }
            set { SetValue(GlowStrengthProperty, value); }
        }

        private static void OnVisualOptionChanged(
            DependencyObject sender,
            DependencyPropertyChangedEventArgs e)
        {
            KeyboardHeatMap control =
                sender as KeyboardHeatMap;

            if (control != null)
                control.UpdateNoteNameVisibility();
        }

        public void InitUi()
        {
            initUI();
        }

        public int getOctave()
        {
            return mOctave;
        }

        private Dictionary<int, int> getNoteCountForKey(BmpSong song, int tracknumber, int octaveshift)
        {
            var trackChunks = song.GetProcessedMidiFile().Result.GetTrackChunks().ToList();
            var notedict = new Dictionary<int, int>();
            int notecount = 0;

            if (tracknumber < 0 || tracknumber >= trackChunks.Count)
                tracknumber = 0;

            foreach (var note in trackChunks[tracknumber].GetNotes())
            {
                int noteNum = note.NoteNumber;
                noteNum -= 48 - (12 * octaveshift);

                int count = 1;
                if (notedict.ContainsKey(noteNum))
                {
                    notedict.TryGetValue(noteNum, out count);
                    count++;
                    notedict.Remove(noteNum);
                }

                if (noteNum >= 0)
                    notedict.Add(noteNum, count);
            }

            notecount = trackChunks[tracknumber].GetNotes().Count;
            var result = new Dictionary<int, int>();

            foreach (var note in notedict)
            {
                double f = notecount <= 0 ? 0 : ((double)note.Value / (double)notecount) * 100;
                result.Add(note.Key, (int)f);
            }

            return result;
        }

        public void initUI(BmpSong song = null, int tracknumber = -1, int octaveshift = 0)
        {
            current_Track = tracknumber;
            mOctave = octaveshift;

            ClearLiveGlow();
            ResetFill();
            BuildLiveNoteCache(song);

            if (song == null)
                return;

            if ((tracknumber - 1) >= song.TrackContainers.Count())
                return;

            Dictionary<int, int> noteCountDict = getNoteCountForKey(song, tracknumber, octaveshift);

            foreach (var n in noteCountDict)
            {
                if (n.Key >= noteInfo.Count)
                    continue;

                object wantedNode = this.FindName(noteInfo[n.Key].name);
                Rectangle r = wantedNode as Rectangle;

                if (r == null)
                    continue;

                r.Fill = NoteFill(noteInfo[n.Key].black_key, n.Value);

                if (_pressedKeyNames.Contains(r.Name))
                    ApplyPressedVisual(r, noteInfo[n.Key].black_key, true);
            }

            noteCountDict.Clear();
        }

        private void ResetFill()
        {
            foreach (var n in noteInfo)
            {
                object wantedNode = this.FindName(n.Value.name);
                Rectangle r = wantedNode as Rectangle;

                if (r == null)
                    continue;

                r.Fill = NoteFill(n.Value.black_key, 0);
                ApplyPressedVisual(r, n.Value.black_key, false);
            }
        }

        private LinearGradientBrush NoteFill(bool blk, double count)
        {
            double normalized = Math.Max(0.0, Math.Min(100.0, count)) / 100.0;
            if (normalized < 0.02)
                normalized = 0.02;

            Color heatColor = GetColorFromBrush(
                FindThemeBrush(blk ? "KeyboardHeatBlackStartBrush" : "KeyboardHeatWhiteStartBrush"),
                blk ? Colors.Yellow : Colors.Red);

            byte alpha = (byte)(45 + (normalized * 170));
            Color activeColor = Color.FromArgb(alpha, heatColor.R, heatColor.G, heatColor.B);

            LinearGradientBrush brush = new LinearGradientBrush
            {
                StartPoint = new Point(0, 0),
                EndPoint = new Point(0, 1)
            };

            if (blk)
            {
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)(alpha / 2), heatColor.R, heatColor.G, heatColor.B), 0.40));
                brush.GradientStops.Add(new GradientStop(activeColor, 0.95));
            }
            else
            {
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.0));
                brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 0, 0, 0), 0.50));
                brush.GradientStops.Add(new GradientStop(activeColor, 0.82));
                brush.GradientStops.Add(new GradientStop(activeColor, 1.0));
            }

            return brush;
        }

        public void highlightKey(Rectangle rect, bool isBlack)
        {
            if (rect == null)
                return;

            _pressedKeyNames.Add(rect.Name);
            ApplyPressedVisual(rect, isBlack, true);
        }

        public void unHighlightKey(Rectangle rect, bool isBlack)
        {
            if (rect == null)
                return;

            _pressedKeyNames.Remove(rect.Name);
            ApplyPressedVisual(rect, isBlack, false);
        }

        public void HighlightMidiNote(int midiNoteNumber, int octaveshift = 0)
        {
            if (!TryResolveMidiNote(midiNoteNumber, octaveshift, out Rectangle rect, out bool isBlack))
                return;

            highlightKey(rect, isBlack);
        }

        public void UnhighlightMidiNote(int midiNoteNumber, int octaveshift = 0)
        {
            if (!TryResolveMidiNote(midiNoteNumber, octaveshift, out Rectangle rect, out bool isBlack))
                return;

            unHighlightKey(rect, isBlack);
        }

        public Rectangle GetKeyRectangleForMidi(int midiNoteNumber, int octaveshift = 0)
        {
            return TryResolveMidiNote(midiNoteNumber, octaveshift, out Rectangle rect, out _)
                ? rect
                : null;
        }

        private bool TryResolveMidiNote(int midiNoteNumber, int octaveshift, out Rectangle rect, out bool isBlack)
        {
            rect = null;
            isBlack = false;

            int noteNum = midiNoteNumber;
            noteNum -= 48 - (12 * octaveshift);

            if (!noteInfo.ContainsKey(noteNum))
                return false;

            NoteRectInfo info = noteInfo[noteNum];
            isBlack = info.black_key;
            rect = FindName(info.name) as Rectangle;
            return rect != null;
        }

        private void ApplyPressedVisual(Rectangle rect, bool isBlack, bool pressed)
        {
            if (rect == null)
                return;

            if (!pressed)
            {
                rect.Stroke = null;
                rect.StrokeThickness = 0;
                rect.Effect = null;
                rect.Opacity = isBlack ? 0.82 : 0.90;
                return;
            }

            Brush glowBrush =
                FindThemeBrush(
                    isBlack
                        ? "KeyboardGlowBlackKeyBrush"
                        : "KeyboardGlowWhiteKeyBrush")
                ??
                FindThemeBrush(
                    isBlack
                        ? "AccentOrangeHoverBrush"
                        : "AccentOrangeBrush")
                ??
                Brushes.Cyan;

            Color glowColor = GetColorFromBrush(
                glowBrush,
                isBlack ? Colors.MediumPurple : Colors.Cyan);

            try
            {
                rect.Stroke = glowBrush.CloneCurrentValue();
            }
            catch
            {
                rect.Stroke = glowBrush;
            }

            double strength =
                Math.Max(0.0, Math.Min(2.5, GlowStrength));

            rect.StrokeThickness =
                (isBlack ? 2.2 : 3.0)
                * Math.Max(0.35, strength);

            rect.Opacity = 1.0;

            if (!AnimationsEnabled || strength <= 0.01)
            {
                rect.Effect = null;
                return;
            }

            rect.Effect = new DropShadowEffect
            {
                Color = glowColor,
                BlurRadius =
                    (isBlack ? 18 : 24)
                    * (0.35 + strength * 0.65),
                ShadowDepth = 0,
                Opacity = Math.Min(1.0, strength)
            };
        }

        private Brush FindThemeBrush(string key)
        {
            // Start at the parent so the external Classic_MainView theme wins
            // over this control's local startup defaults.
            DependencyObject current = VisualTreeHelper.GetParent(this);

            while (current != null)
            {
                if (current is FrameworkElement element)
                {
                    object found = element.TryFindResource(key);
                    if (found is Brush brush)
                        return brush;
                }

                current = VisualTreeHelper.GetParent(current);
            }

            object applicationResource =
                Application.Current?.TryFindResource(key);

            if (applicationResource is Brush applicationBrush)
                return applicationBrush;

            return Resources[key] as Brush;
        }

        private static Color GetColorFromBrush(Brush brush, Color fallback)
        {
            if (brush is SolidColorBrush solid)
                return solid.Color;

            if (brush is GradientBrush gradient && gradient.GradientStops.Count > 0)
            {
                int middle = gradient.GradientStops.Count / 2;
                return gradient.GradientStops[middle].Color;
            }

            return fallback;
        }

        private void SubscribeToPlaybackEvents()
        {
            if (_isSubscribed)
                return;

            BmpMaestro.Instance.OnPlaybackTimeChanged +=
                Maestro_OnPlaybackTimeChanged;

            BmpMaestro.Instance.OnSongLoaded +=
                Maestro_OnSongLoaded;

            BmpMaestro.Instance.OnPlaybackStarted +=
                Maestro_OnPlaybackStarted;

            BmpMaestro.Instance.OnPlaybackStopped +=
                Maestro_OnPlaybackStopped;

            _isSubscribed = true;
        }

        private void UnsubscribeFromPlaybackEvents()
        {
            if (!_isSubscribed)
                return;

            BmpMaestro.Instance.OnPlaybackTimeChanged -=
                Maestro_OnPlaybackTimeChanged;

            BmpMaestro.Instance.OnSongLoaded -=
                Maestro_OnSongLoaded;

            BmpMaestro.Instance.OnPlaybackStarted -=
                Maestro_OnPlaybackStarted;

            BmpMaestro.Instance.OnPlaybackStopped -=
                Maestro_OnPlaybackStopped;

            _isSubscribed = false;
        }

        private void Maestro_OnPlaybackTimeChanged(
            object sender,
            BardMusicPlayer.Maestro.Events.CurrentPlayPositionEvent e)
        {
            if (!_isPlaying)
                return;

            Dispatcher.BeginInvoke(
                new Action(() => UpdateLiveGlow(e.tick)));
        }

        private void Maestro_OnSongLoaded(
            object sender,
            BardMusicPlayer.Maestro.Events.SongLoadedEvent e)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    ClearLiveGlow();
                    BuildLiveNoteCache(PlaybackFunctions.CurrentSong);
                }));
        }

        private void Maestro_OnPlaybackStarted(
            object sender,
            bool e)
        {
            Dispatcher.BeginInvoke(
                new Action(() => _isPlaying = true));
        }

        private void Maestro_OnPlaybackStopped(
            object sender,
            bool e)
        {
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    _isPlaying = false;
                    ClearLiveGlow();
                }));
        }

        private void BuildLiveNoteCache(BmpSong song)
        {
            _liveNotes.Clear();
            _longestLiveNote = 0;

            var midiFile = song?.cachedSequencerMidi;
            if (midiFile == null)
                return;

            var trackChunks = midiFile.GetTrackChunks().ToList();
            if (trackChunks.Count == 0)
                return;

            // LightAmp's NumValue is one-based and the original heatmap uses
            // that value directly as the MIDI TrackChunk index, which skips
            // the conductor/meta chunk in many files. Match that behavior.
            int trackIndex = current_Track;
            if (trackIndex < 0 || trackIndex >= trackChunks.Count)
                trackIndex = 0;

            foreach (var note in trackChunks[trackIndex].GetNotes())
            {
                long length = Math.Max(1L, note.Length);

                _liveNotes.Add(
                    new LiveNote
                    {
                        StartTick = note.Time,
                        EndTick = note.Time + length,
                        MidiNote = note.NoteNumber
                    });

                if (length > _longestLiveNote)
                    _longestLiveNote = length;
            }

            _liveNotes.Sort(
                (left, right) =>
                    left.StartTick.CompareTo(right.StartTick));
        }

        private void UpdateLiveGlow(long currentTick)
        {
            if (_liveNotes.Count == 0)
            {
                ClearLiveGlow();
                return;
            }

            var nextKeys =
                new HashSet<string>(StringComparer.Ordinal);

            long safeStart = Math.Max(
                0L,
                currentTick - _longestLiveNote - GlowTailTicks);

            int firstIndex = LowerBoundStartTick(safeStart);

            for (int index = firstIndex;
                 index < _liveNotes.Count;
                 index++)
            {
                LiveNote note = _liveNotes[index];

                if (note.StartTick > currentTick)
                    break;

                if (note.EndTick + GlowTailTicks < currentTick)
                    continue;

                if (TryResolveMidiNote(
                        note.MidiNote,
                        mOctave,
                        out Rectangle rect,
                        out bool isBlack))
                {
                    nextKeys.Add(rect.Name);

                    if (!_liveKeyNames.Contains(rect.Name))
                        highlightKey(rect, isBlack);
                }
            }

            foreach (string oldKey in _liveKeyNames.ToArray())
            {
                if (nextKeys.Contains(oldKey))
                    continue;

                if (TryGetRectangleByName(
                        oldKey,
                        out Rectangle rect,
                        out bool isBlack))
                {
                    unHighlightKey(rect, isBlack);
                }
            }

            _liveKeyNames.Clear();
            foreach (string key in nextKeys)
                _liveKeyNames.Add(key);
        }

        private int LowerBoundStartTick(long tick)
        {
            int low = 0;
            int high = _liveNotes.Count;

            while (low < high)
            {
                int middle = low + ((high - low) / 2);

                if (_liveNotes[middle].StartTick < tick)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        private bool TryGetRectangleByName(
            string name,
            out Rectangle rect,
            out bool isBlack)
        {
            rect = FindName(name) as Rectangle;
            isBlack = false;

            if (rect == null)
                return false;

            foreach (NoteRectInfo info in noteInfo.Values)
            {
                if (!string.Equals(
                        info.name,
                        name,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                isBlack = info.black_key;
                return true;
            }

            return false;
        }

        private void ClearLiveGlow()
        {
            foreach (string key in _liveKeyNames.ToArray())
            {
                if (TryGetRectangleByName(
                        key,
                        out Rectangle rect,
                        out bool isBlack))
                {
                    unHighlightKey(rect, isBlack);
                }
            }

            _liveKeyNames.Clear();
        }

        private void UpdateNoteNameVisibility()
        {
            UpdateNoteNameVisibility(this);
        }

        private void UpdateNoteNameVisibility(
            DependencyObject parent)
        {
            if (parent == null)
                return;

            int count =
                VisualTreeHelper.GetChildrenCount(parent);

            for (int index = 0; index < count; index++)
            {
                DependencyObject child =
                    VisualTreeHelper.GetChild(parent, index);

                TextBlock text =
                    child as TextBlock;

                if (text != null &&
                    string.Equals(
                        Convert.ToString(text.Tag),
                        "KeyboardNoteLabel",
                        StringComparison.Ordinal))
                {
                    text.Visibility =
                        ShowNoteNames
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                }

                UpdateNoteNameVisibility(child);
            }
        }

        private void evtLeftButtonDown(Rectangle r)
        {
            Console.WriteLine("left button down");
        }

        private void evtLeftButtonUp(Rectangle r)
        {
            Console.WriteLine("left button up");
        }

        private void evtMouseLeave(Rectangle r, MouseEventArgs e)
        {
            Console.WriteLine("mouse leave");
        }

        private void evtMouseEnter(Rectangle r, MouseEventArgs e)
        {
            Console.WriteLine("mouse enter");
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            SubscribeToPlaybackEvents();
            BuildLiveNoteCache(PlaybackFunctions.CurrentSong);
            UpdateNoteNameVisibility();
        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeFromPlaybackEvents();
            ClearLiveGlow();
        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
        }
    }
}
