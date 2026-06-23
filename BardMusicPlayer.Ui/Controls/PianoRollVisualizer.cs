using BardMusicPlayer.Maestro;
using BardMusicPlayer.Transmogrify.Song;
using BardMusicPlayer.Ui.Functions;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Interaction;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace BardMusicPlayer.Ui.Controls
{
    /// <summary>
    /// Lightweight scrolling MIDI piano-roll visualizer.
    ///
    /// Time moves from right to left and the playhead remains near the left side.
    /// Notes currently sounding are drawn with a brighter neon outline and glow.
    /// </summary>
    public sealed class PianoRollVisualizer : FrameworkElement
    {
        private sealed class VisualNote
        {
            public long StartTick;
            public long EndTick;
            public int Pitch;
            public int Velocity;
            public int Track;
        }

        private readonly List<VisualNote> _notes = new List<VisualNote>();
        private readonly Brush[] _trackBrushes;

        private long _currentTick;
        private long _longestNote;
        private int _ticksPerQuarter = 96;
        private int _minimumPitch = 48;
        private int _maximumPitch = 84;
        private bool _isSubscribed;

        public PianoRollVisualizer()
        {
            ClipToBounds = true;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            Loaded += PianoRollVisualizer_Loaded;
            Unloaded += PianoRollVisualizer_Unloaded;

            _trackBrushes = new[]
            {
                CreateFrozenBrush(Color.FromRgb(0x00, 0xF5, 0xFF)), // cyan
                CreateFrozenBrush(Color.FromRgb(0x7A, 0x5C, 0xFF)), // violet
                CreateFrozenBrush(Color.FromRgb(0xFF, 0x4F, 0xC8)), // pink
                CreateFrozenBrush(Color.FromRgb(0x56, 0xF0, 0x9A)), // green
                CreateFrozenBrush(Color.FromRgb(0xFF, 0xB8, 0x4D)), // amber
                CreateFrozenBrush(Color.FromRgb(0x4D, 0x9D, 0xFF)), // blue
                CreateFrozenBrush(Color.FromRgb(0xFF, 0x67, 0x67)), // red
                CreateFrozenBrush(Color.FromRgb(0xC4, 0xFF, 0x55)), // lime
            };
        }

        private void PianoRollVisualizer_Loaded(object sender, RoutedEventArgs e)
        {
            SubscribeToPlaybackEvents();
            LoadSong(PlaybackFunctions.CurrentSong);
        }

        private void PianoRollVisualizer_Unloaded(object sender, RoutedEventArgs e)
        {
            UnsubscribeFromPlaybackEvents();
        }

        private void SubscribeToPlaybackEvents()
        {
            if (_isSubscribed)
                return;

            BmpMaestro.Instance.OnPlaybackTimeChanged += Maestro_OnPlaybackTimeChanged;
            BmpMaestro.Instance.OnSongLoaded += Maestro_OnSongLoaded;
            BmpMaestro.Instance.OnPlaybackStarted += Maestro_OnPlaybackStarted;
            BmpMaestro.Instance.OnPlaybackStopped += Maestro_OnPlaybackStopped;
            _isSubscribed = true;
        }

        private void UnsubscribeFromPlaybackEvents()
        {
            if (!_isSubscribed)
                return;

            BmpMaestro.Instance.OnPlaybackTimeChanged -= Maestro_OnPlaybackTimeChanged;
            BmpMaestro.Instance.OnSongLoaded -= Maestro_OnSongLoaded;
            BmpMaestro.Instance.OnPlaybackStarted -= Maestro_OnPlaybackStarted;
            BmpMaestro.Instance.OnPlaybackStopped -= Maestro_OnPlaybackStopped;
            _isSubscribed = false;
        }

        private void Maestro_OnPlaybackTimeChanged(
            object sender,
            BardMusicPlayer.Maestro.Events.CurrentPlayPositionEvent e)
        {
            Dispatcher.BeginInvoke(new Action(() => SetCurrentTick(e.tick)));
        }

        private void Maestro_OnSongLoaded(
            object sender,
            BardMusicPlayer.Maestro.Events.SongLoadedEvent e)
        {
            Dispatcher.BeginInvoke(new Action(() => LoadSong(PlaybackFunctions.CurrentSong)));
        }

        private void Maestro_OnPlaybackStarted(object sender, bool e)
        {
            Dispatcher.BeginInvoke(new Action(() => IsPlaying = true));
        }

        private void Maestro_OnPlaybackStopped(object sender, bool e)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                IsPlaying = false;
                SetCurrentTick(0);
            }));
        }

        #region Themeable dependency properties

        public static readonly DependencyProperty BackgroundProperty =
            DependencyProperty.Register(
                nameof(Background),
                typeof(Brush),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    CreateFrozenBrush(Color.FromRgb(0x05, 0x05, 0x08)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush Background
        {
            get => (Brush)GetValue(BackgroundProperty);
            set => SetValue(BackgroundProperty, value);
        }

        public static readonly DependencyProperty KeyboardBackgroundProperty =
            DependencyProperty.Register(
                nameof(KeyboardBackground),
                typeof(Brush),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    CreateFrozenBrush(Color.FromRgb(0x0B, 0x0B, 0x0F)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush KeyboardBackground
        {
            get => (Brush)GetValue(KeyboardBackgroundProperty);
            set => SetValue(KeyboardBackgroundProperty, value);
        }

        public static readonly DependencyProperty GridLineBrushProperty =
            DependencyProperty.Register(
                nameof(GridLineBrush),
                typeof(Brush),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    CreateFrozenBrush(Color.FromArgb(0x32, 0x00, 0xF5, 0xFF)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush GridLineBrush
        {
            get => (Brush)GetValue(GridLineBrushProperty);
            set => SetValue(GridLineBrushProperty, value);
        }

        public static readonly DependencyProperty BeatLineBrushProperty =
            DependencyProperty.Register(
                nameof(BeatLineBrush),
                typeof(Brush),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    CreateFrozenBrush(Color.FromArgb(0x66, 0x00, 0xF5, 0xFF)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush BeatLineBrush
        {
            get => (Brush)GetValue(BeatLineBrushProperty);
            set => SetValue(BeatLineBrushProperty, value);
        }

        public static readonly DependencyProperty BarLineBrushProperty =
            DependencyProperty.Register(
                nameof(BarLineBrush),
                typeof(Brush),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    CreateFrozenBrush(Color.FromArgb(0xA0, 0x7A, 0x5C, 0xFF)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush BarLineBrush
        {
            get => (Brush)GetValue(BarLineBrushProperty);
            set => SetValue(BarLineBrushProperty, value);
        }

        public static readonly DependencyProperty PlayheadBrushProperty =
            DependencyProperty.Register(
                nameof(PlayheadBrush),
                typeof(Brush),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    CreateFrozenBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush PlayheadBrush
        {
            get => (Brush)GetValue(PlayheadBrushProperty);
            set => SetValue(PlayheadBrushProperty, value);
        }

        public static readonly DependencyProperty TextBrushProperty =
            DependencyProperty.Register(
                nameof(TextBrush),
                typeof(Brush),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    CreateFrozenBrush(Color.FromRgb(0xD8, 0xF8, 0xFF)),
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public Brush TextBrush
        {
            get => (Brush)GetValue(TextBrushProperty);
            set => SetValue(TextBrushProperty, value);
        }

        public static readonly DependencyProperty MinimumNoteHeightProperty =
            DependencyProperty.Register(
                nameof(MinimumNoteHeight),
                typeof(double),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    5.0,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Minimum rendered thickness of a MIDI note in device-independent pixels.
        /// Increase this when notes look too thin.
        /// </summary>
        public double MinimumNoteHeight
        {
            get => (double)GetValue(MinimumNoteHeightProperty);
            set => SetValue(MinimumNoteHeightProperty, value);
        }

        public static readonly DependencyProperty NoteHeightScaleProperty =
            DependencyProperty.Register(
                nameof(NoteHeightScale),
                typeof(double),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    1.35,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Multiplies the natural pitch-row height. Values above 1 allow notes
        /// to overlap neighbouring pitch rows slightly for a bolder appearance.
        /// </summary>
        public double NoteHeightScale
        {
            get => (double)GetValue(NoteHeightScaleProperty);
            set => SetValue(NoteHeightScaleProperty, value);
        }

        public static readonly DependencyProperty TrackFilterProperty =
            DependencyProperty.Register(
                nameof(TrackFilter),
                typeof(int),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    0,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        /// <summary>
        /// Zero shows all tracks. Positive values show one one-based track number.
        /// </summary>
        public int TrackFilter
        {
            get => (int)GetValue(TrackFilterProperty);
            set => SetValue(TrackFilterProperty, value);
        }

        public static readonly DependencyProperty LookBehindBeatsProperty =
            DependencyProperty.Register(
                nameof(LookBehindBeats),
                typeof(double),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    1.5,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public double LookBehindBeats
        {
            get => (double)GetValue(LookBehindBeatsProperty);
            set => SetValue(LookBehindBeatsProperty, value);
        }

        public static readonly DependencyProperty LookAheadBeatsProperty =
            DependencyProperty.Register(
                nameof(LookAheadBeats),
                typeof(double),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    10.0,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public double LookAheadBeats
        {
            get => (double)GetValue(LookAheadBeatsProperty);
            set => SetValue(LookAheadBeatsProperty, value);
        }

        public static readonly DependencyProperty ShowKeyboardProperty =
            DependencyProperty.Register(
                nameof(ShowKeyboard),
                typeof(bool),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    true,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public bool ShowKeyboard
        {
            get => (bool)GetValue(ShowKeyboardProperty);
            set => SetValue(ShowKeyboardProperty, value);
        }

        public static readonly DependencyProperty ShowNoteNamesProperty =
            DependencyProperty.Register(
                nameof(ShowNoteNames),
                typeof(bool),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public bool ShowNoteNames
        {
            get => (bool)GetValue(ShowNoteNamesProperty);
            set => SetValue(ShowNoteNamesProperty, value);
        }

        public static readonly DependencyProperty ShowBeatLinesProperty =
            DependencyProperty.Register(
                nameof(ShowBeatLines),
                typeof(bool),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    true,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public bool ShowBeatLines
        {
            get => (bool)GetValue(ShowBeatLinesProperty);
            set => SetValue(ShowBeatLinesProperty, value);
        }

        public static readonly DependencyProperty ShowBarLinesProperty =
            DependencyProperty.Register(
                nameof(ShowBarLines),
                typeof(bool),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    true,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public bool ShowBarLines
        {
            get => (bool)GetValue(ShowBarLinesProperty);
            set => SetValue(ShowBarLinesProperty, value);
        }

        public static readonly DependencyProperty AnimationsEnabledProperty =
            DependencyProperty.Register(
                nameof(AnimationsEnabled),
                typeof(bool),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    true,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public bool AnimationsEnabled
        {
            get => (bool)GetValue(AnimationsEnabledProperty);
            set => SetValue(AnimationsEnabledProperty, value);
        }

        public static readonly DependencyProperty GlowStrengthProperty =
            DependencyProperty.Register(
                nameof(GlowStrength),
                typeof(double),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    1.0,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public double GlowStrength
        {
            get => (double)GetValue(GlowStrengthProperty);
            set => SetValue(GlowStrengthProperty, value);
        }

        public static readonly DependencyProperty IsPlayingProperty =
            DependencyProperty.Register(
                nameof(IsPlaying),
                typeof(bool),
                typeof(PianoRollVisualizer),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.AffectsRender));

        public bool IsPlaying
        {
            get => (bool)GetValue(IsPlayingProperty);
            set => SetValue(IsPlayingProperty, value);
        }

        #endregion

        /// <summary>
        /// Reads all note objects from the song's cached DryWetMIDI file.
        /// Call this whenever LightAmp loads a new song.
        /// </summary>
        public void LoadSong(BmpSong song)
        {
            _notes.Clear();
            _currentTick = 0;
            _longestNote = 0;
            _ticksPerQuarter = 96;
            _minimumPitch = 48;
            _maximumPitch = 84;

            var midiFile = song?.cachedSequencerMidi;
            if (midiFile == null)
            {
                InvalidateVisual();
                return;
            }

            var ticksDivision = midiFile.TimeDivision as TicksPerQuarterNoteTimeDivision;
            if (ticksDivision != null && ticksDivision.TicksPerQuarterNote > 0)
                _ticksPerQuarter = ticksDivision.TicksPerQuarterNote;

            int trackNumber = 0;
            foreach (var trackChunk in midiFile.GetTrackChunks())
            {
                trackNumber++;

                foreach (var note in trackChunk.GetNotes())
                {
                    int pitch = Convert.ToInt32(note.NoteNumber, CultureInfo.InvariantCulture);
                    int velocity = Convert.ToInt32(note.Velocity, CultureInfo.InvariantCulture);
                    long length = Math.Max(1L, note.Length);

                    _notes.Add(new VisualNote
                    {
                        StartTick = note.Time,
                        EndTick = note.Time + length,
                        Pitch = pitch,
                        Velocity = velocity,
                        Track = trackNumber
                    });

                    if (length > _longestNote)
                        _longestNote = length;
                }
            }

            _notes.Sort((left, right) => left.StartTick.CompareTo(right.StartTick));

            if (_notes.Count > 0)
            {
                int noteMinimum = _notes.Min(note => note.Pitch);
                int noteMaximum = _notes.Max(note => note.Pitch);

                // Add a small pitch margin and keep at least two visible octaves.
                _minimumPitch = Math.Max(0, noteMinimum - 2);
                _maximumPitch = Math.Min(127, noteMaximum + 2);

                if (_maximumPitch - _minimumPitch < 23)
                {
                    int middle = (_maximumPitch + _minimumPitch) / 2;
                    _minimumPitch = Math.Max(0, middle - 12);
                    _maximumPitch = Math.Min(127, _minimumPitch + 24);
                }
            }

            InvalidateVisual();
        }

        /// <summary>
        /// Moves the fixed playhead to LightAmp's current MIDI tick.
        /// </summary>
        public void SetCurrentTick(long tick)
        {
            _currentTick = Math.Max(0L, tick);
            InvalidateVisual();
        }

        public void Clear()
        {
            _notes.Clear();
            _currentTick = 0;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);

            double width = ActualWidth;
            double height = ActualHeight;
            if (width <= 1 || height <= 1)
                return;

            var fullRect = new Rect(0, 0, width, height);
            drawingContext.DrawRoundedRectangle(Background, null, fullRect, 10, 10);

            double keyboardWidth = ShowKeyboard ? Math.Min(54.0, width * 0.18) : 0.0;
            double rollWidth = Math.Max(1.0, width - keyboardWidth);
            double rollHeight = height;
            int pitchCount = Math.Max(1, _maximumPitch - _minimumPitch + 1);
            double rowHeight = rollHeight / pitchCount;

            double behindBeats = Math.Max(0.0, LookBehindBeats);
            double aheadBeats = Math.Max(1.0, LookAheadBeats);
            double visibleBeats = behindBeats + aheadBeats;
            double visibleTicks = Math.Max(1.0, visibleBeats * _ticksPerQuarter);
            long visibleStartTick = Math.Max(
                0L,
                _currentTick - (long)Math.Round(behindBeats * _ticksPerQuarter));
            long visibleEndTick = visibleStartTick + (long)Math.Ceiling(visibleTicks);

            var rollRect = new Rect(keyboardWidth, 0, rollWidth, rollHeight);
            drawingContext.PushClip(new RectangleGeometry(rollRect));

            DrawPitchRows(drawingContext, keyboardWidth, rollWidth, rowHeight);
            DrawBeatGrid(
                drawingContext,
                keyboardWidth,
                rollWidth,
                rollHeight,
                visibleStartTick,
                visibleEndTick);

            DrawVisibleNotes(
                drawingContext,
                keyboardWidth,
                rollWidth,
                rowHeight,
                visibleStartTick,
                visibleEndTick);

            double playheadX = TickToX(
                _currentTick,
                keyboardWidth,
                rollWidth,
                visibleStartTick,
                visibleEndTick);

            DrawPlayhead(drawingContext, playheadX, rollHeight);
            drawingContext.Pop();

            if (ShowKeyboard)
                DrawKeyboard(drawingContext, keyboardWidth, rowHeight);

            var borderPen = CreatePen(Color.FromArgb(0x80, 0x00, 0xF5, 0xFF), 1.0);
            drawingContext.DrawRoundedRectangle(null, borderPen, fullRect, 10, 10);
        }

        private void DrawPitchRows(
            DrawingContext drawingContext,
            double keyboardWidth,
            double rollWidth,
            double rowHeight)
        {
            for (int pitch = _minimumPitch; pitch <= _maximumPitch; pitch++)
            {
                int pitchClass = pitch % 12;
                bool isBlackKey = IsBlackKey(pitchClass);
                double y = PitchToY(pitch, rowHeight);

                if (isBlackKey)
                {
                    var shade = CreateFrozenBrush(Color.FromArgb(0x32, 0x00, 0x00, 0x00));
                    drawingContext.DrawRectangle(
                        shade,
                        null,
                        new Rect(keyboardWidth, y, rollWidth, rowHeight));
                }

                var rowPen = new Pen(
                    GridLineBrush,
                    pitchClass == 0 ? 1.0 : 0.5);

                drawingContext.DrawLine(
                    rowPen,
                    new Point(keyboardWidth, y),
                    new Point(keyboardWidth + rollWidth, y));
            }
        }

        private void DrawBeatGrid(
            DrawingContext drawingContext,
            double keyboardWidth,
            double rollWidth,
            double rollHeight,
            long visibleStartTick,
            long visibleEndTick)
        {
            long firstBeat = visibleStartTick / _ticksPerQuarter;
            if (visibleStartTick % _ticksPerQuarter != 0)
                firstBeat++;

            for (long beat = firstBeat; ; beat++)
            {
                long tick = beat * _ticksPerQuarter;
                if (tick > visibleEndTick)
                    break;

                double x = TickToX(
                    tick,
                    keyboardWidth,
                    rollWidth,
                    visibleStartTick,
                    visibleEndTick);

                bool isBar = beat % 4 == 0;

                if (isBar && !ShowBarLines)
                    continue;

                if (!isBar && !ShowBeatLines)
                    continue;

                var pen = new Pen(
                    isBar ? BarLineBrush : BeatLineBrush,
                    isBar ? 1.4 : 0.75);

                drawingContext.DrawLine(
                    pen,
                    new Point(x, 0),
                    new Point(x, rollHeight));
            }
        }

        private void DrawVisibleNotes(
            DrawingContext drawingContext,
            double keyboardWidth,
            double rollWidth,
            double rowHeight,
            long visibleStartTick,
            long visibleEndTick)
        {
            if (_notes.Count == 0)
                return;

            long safeSearchStart = Math.Max(0L, visibleStartTick - _longestNote);
            int firstIndex = LowerBoundStartTick(safeSearchStart);

            for (int index = firstIndex; index < _notes.Count; index++)
            {
                VisualNote note = _notes[index];

                if (note.StartTick > visibleEndTick)
                    break;

                if (note.EndTick < visibleStartTick)
                    continue;

                if (TrackFilter > 0 && note.Track != TrackFilter)
                    continue;

                double x1 = TickToX(
                    note.StartTick,
                    keyboardWidth,
                    rollWidth,
                    visibleStartTick,
                    visibleEndTick);

                double x2 = TickToX(
                    note.EndTick,
                    keyboardWidth,
                    rollWidth,
                    visibleStartTick,
                    visibleEndTick);

                double noteWidth = Math.Max(2.5, x2 - x1);

                // Keep notes readable when the MIDI spans many pitches.
                // A small amount of vertical overlap is intentional and creates
                // the thicker neon-bar appearance.
                double requestedHeight = Math.Max(
                    MinimumNoteHeight,
                    Math.Max(1.0, rowHeight - 1.0) * Math.Max(0.25, NoteHeightScale));

                double noteHeight = Math.Min(
                    requestedHeight,
                    Math.Max(MinimumNoteHeight, rowHeight * 1.85));

                double y = PitchToY(note.Pitch, rowHeight)
                           + ((rowHeight - noteHeight) / 2.0);

                var noteRect = new Rect(x1, y, noteWidth, noteHeight);

                bool isActive =
                    IsPlaying &&
                    note.StartTick <= _currentTick &&
                    note.EndTick >= _currentTick;

                Brush brush = _trackBrushes[(note.Track - 1) % _trackBrushes.Length];
                double velocityOpacity = 0.52 + (Math.Min(127, note.Velocity) / 127.0) * 0.48;

                double glowStrength =
                    Math.Max(0.0, Math.Min(2.5, GlowStrength));

                if (isActive &&
                    AnimationsEnabled &&
                    glowStrength > 0.01)
                {
                    byte alpha =
                        (byte)Math.Min(
                            220,
                            70 + glowStrength * 75);

                    var glowBrush =
                        CreateFrozenBrush(
                            Color.FromArgb(
                                alpha,
                                0x00,
                                0xF5,
                                0xFF));

                    double expansion =
                        2.0 + glowStrength * 2.2;

                    var largeGlowRect = new Rect(
                        noteRect.X - expansion,
                        noteRect.Y - expansion / 2,
                        noteRect.Width + expansion * 2,
                        noteRect.Height + expansion);

                    drawingContext.DrawRoundedRectangle(
                        glowBrush,
                        null,
                        largeGlowRect,
                        5,
                        5);
                }

                drawingContext.PushOpacity(
                    isActive ? 1.0 : velocityOpacity);

                drawingContext.DrawRoundedRectangle(
                    brush,
                    isActive
                        ? new Pen(
                            PlayheadBrush,
                            1.0 + glowStrength * 0.4)
                        : null,
                    noteRect,
                    3,
                    3);

                drawingContext.Pop();

                if (ShowNoteNames &&
                    noteRect.Width >= 26 &&
                    noteRect.Height >= 7)
                {
                    string noteName =
                        GetNoteName(note.Pitch);

                    var label = new FormattedText(
                        noteName,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        Math.Max(
                            6.0,
                            Math.Min(10.0, noteRect.Height - 1)),
                        TextBrush);

                    drawingContext.DrawText(
                        label,
                        new Point(
                            noteRect.X + 3,
                            noteRect.Y
                            + Math.Max(
                                0,
                                (noteRect.Height - label.Height) / 2)));
                }
            }
        }

        private void DrawPlayhead(
            DrawingContext drawingContext,
            double x,
            double height)
        {
            // Layered lines create a cheap neon-glow effect without WPF effects.
            drawingContext.DrawLine(
                new Pen(CreateFrozenBrush(Color.FromArgb(0x32, 0x00, 0xF5, 0xFF)), 8),
                new Point(x, 0),
                new Point(x, height));

            drawingContext.DrawLine(
                new Pen(CreateFrozenBrush(Color.FromArgb(0x70, 0x00, 0xF5, 0xFF)), 4),
                new Point(x, 0),
                new Point(x, height));

            drawingContext.DrawLine(
                new Pen(PlayheadBrush, 1.5),
                new Point(x, 0),
                new Point(x, height));
        }

        private void DrawKeyboard(
            DrawingContext drawingContext,
            double keyboardWidth,
            double rowHeight)
        {
            drawingContext.DrawRectangle(
                KeyboardBackground,
                null,
                new Rect(0, 0, keyboardWidth, ActualHeight));

            for (int pitch = _minimumPitch; pitch <= _maximumPitch; pitch++)
            {
                int pitchClass = pitch % 12;
                bool isBlackKey = IsBlackKey(pitchClass);
                double y = PitchToY(pitch, rowHeight);

                Brush keyBrush = isBlackKey
                    ? CreateFrozenBrush(Color.FromRgb(0x12, 0x12, 0x17))
                    : CreateFrozenBrush(Color.FromRgb(0xD8, 0xE0, 0xE8));

                double keyWidth = isBlackKey ? keyboardWidth * 0.64 : keyboardWidth;
                drawingContext.DrawRectangle(
                    keyBrush,
                    new Pen(CreateFrozenBrush(Color.FromArgb(0x65, 0x00, 0x00, 0x00)), 0.6),
                    new Rect(0, y, keyWidth, rowHeight));

                if (pitchClass == 0 && rowHeight >= 5.0)
                {
                    string label = "C" + ((pitch / 12) - 1).ToString(CultureInfo.InvariantCulture);
                    var formattedText = new FormattedText(
                        label,
                        CultureInfo.InvariantCulture,
                        FlowDirection.LeftToRight,
                        new Typeface("Segoe UI"),
                        Math.Max(7.0, Math.Min(10.0, rowHeight - 1.0)),
                        TextBrush);

                    drawingContext.DrawText(
                        formattedText,
                        new Point(
                            Math.Max(2.0, keyboardWidth - formattedText.Width - 3.0),
                            y + Math.Max(0.0, (rowHeight - formattedText.Height) / 2.0)));
                }
            }

            drawingContext.DrawLine(
                new Pen(GridLineBrush, 1.0),
                new Point(keyboardWidth, 0),
                new Point(keyboardWidth, ActualHeight));
        }

        private int LowerBoundStartTick(long tick)
        {
            int low = 0;
            int high = _notes.Count;

            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (_notes[middle].StartTick < tick)
                    low = middle + 1;
                else
                    high = middle;
            }

            return low;
        }

        private double PitchToY(int pitch, double rowHeight)
        {
            return (_maximumPitch - pitch) * rowHeight;
        }

        private static double TickToX(
            long tick,
            double keyboardWidth,
            double rollWidth,
            long visibleStartTick,
            long visibleEndTick)
        {
            double tickRange = Math.Max(1.0, visibleEndTick - visibleStartTick);
            double fraction = (tick - visibleStartTick) / tickRange;
            return keyboardWidth + (fraction * rollWidth);
        }

        private static string GetNoteName(int midiNote)
        {
            string[] names =
            {
                "C", "C#", "D", "D#", "E", "F",
                "F#", "G", "G#", "A", "A#", "B"
            };

            int pitchClass =
                ((midiNote % 12) + 12) % 12;

            int octave =
                (midiNote / 12) - 1;

            return names[pitchClass]
                   + octave.ToString(
                       CultureInfo.InvariantCulture);
        }

        private static bool IsBlackKey(int pitchClass)
        {
            return pitchClass == 1 ||
                   pitchClass == 3 ||
                   pitchClass == 6 ||
                   pitchClass == 8 ||
                   pitchClass == 10;
        }

        private static Brush CreateFrozenBrush(Color color)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            return brush;
        }

        private static Pen CreatePen(Color color, double thickness)
        {
            var pen = new Pen(CreateFrozenBrush(color), thickness);
            pen.Freeze();
            return pen;
        }
    }
}
