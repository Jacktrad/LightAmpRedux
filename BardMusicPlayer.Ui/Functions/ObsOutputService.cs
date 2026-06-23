using BardMusicPlayer.Pigeonhole;
using BardMusicPlayer.Transmogrify.Song;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace BardMusicPlayer.Ui.Functions
{
    /// <summary>
    /// Writes small UTF-8 text files that OBS Text sources can read.
    /// </summary>
    public static class ObsOutputService
    {
        public const string NowPlayingFileName = "NowPlaying.txt";
        public const string HistoryFileName = "SongHistory.txt";

        private static readonly object FileLock = new object();

        private static string _lastNowPlayingText = string.Empty;
        private static string _lastHistoryText = string.Empty;

        public static string GetOutputDirectory()
        {
            string configured =
                BmpPigeonhole.Instance.ObsOutputDirectory;

            if (string.IsNullOrWhiteSpace(configured))
                configured = "OBS";

            try
            {
                string resolved = Path.IsPathRooted(configured)
                    ? configured
                    : Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        configured);

                return Path.GetFullPath(resolved);
            }
            catch
            {
                return Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "OBS");
            }
        }

        public static string GetNowPlayingPath()
        {
            return Path.Combine(
                GetOutputDirectory(),
                NowPlayingFileName);
        }

        public static string GetHistoryPath()
        {
            return Path.Combine(
                GetOutputDirectory(),
                HistoryFileName);
        }

        public static void ApplySettings()
        {
            if (!BmpPigeonhole.Instance.ObsOutputEnabled)
                return;

            EnsureOutputDirectory();
            RewriteHistoryFile();
        }

        public static void UpdateNowPlaying(
            BmpSong song,
            string instrument,
            int track,
            TimeSpan elapsed,
            TimeSpan duration)
        {
            if (!BmpPigeonhole.Instance.ObsOutputEnabled)
                return;

            string output = FormatTemplate(
                BmpPigeonhole.Instance.ObsNowPlayingTemplate,
                song,
                instrument,
                track,
                elapsed,
                duration);

            if (string.Equals(
                    output,
                    _lastNowPlayingText,
                    StringComparison.Ordinal))
            {
                return;
            }

            _lastNowPlayingText = output;
            WriteTextFile(GetNowPlayingPath(), output);
        }

        public static void RecordPlayedSong(
            BmpSong song,
            string instrument,
            int track)
        {
            if (!BmpPigeonhole.Instance.ObsOutputEnabled ||
                song == null)
            {
                return;
            }

            string entry = FormatTemplate(
                BmpPigeonhole.Instance.ObsHistoryTemplate,
                song,
                instrument,
                track,
                TimeSpan.Zero,
                song.Duration);

            if (BmpPigeonhole.Instance.ObsHistoryShowTimestamp)
            {
                entry =
                    DateTime.Now.ToString(
                        "HH:mm",
                        CultureInfo.CurrentCulture)
                    + "  "
                    + entry;
            }

            List<string> history =
                BmpPigeonhole.Instance.ObsSongHistory == null
                    ? new List<string>()
                    : new List<string>(
                        BmpPigeonhole.Instance.ObsSongHistory);

            // Avoid a duplicated first line if the play event fires twice.
            if (history.Count == 0 ||
                !string.Equals(
                    history[0],
                    entry,
                    StringComparison.Ordinal))
            {
                history.Insert(0, entry);
            }

            int keep =
                Math.Max(
                    1,
                    Math.Min(
                        50,
                        BmpPigeonhole.Instance.ObsHistoryLength));

            if (history.Count > keep)
                history.RemoveRange(keep, history.Count - keep);

            BmpPigeonhole.Instance.ObsSongHistory = history;
            RewriteHistoryFile();
        }

        public static void ClearNowPlaying()
        {
            _lastNowPlayingText = string.Empty;
            WriteTextFile(GetNowPlayingPath(), string.Empty);
        }

        public static void ClearHistory()
        {
            BmpPigeonhole.Instance.ObsSongHistory =
                new List<string>();

            _lastHistoryText = string.Empty;
            WriteTextFile(GetHistoryPath(), string.Empty);
        }

        public static void WritePreviewFiles()
        {
            if (!BmpPigeonhole.Instance.ObsOutputEnabled)
                return;

            EnsureOutputDirectory();

            string preview = ApplyTokens(
                BmpPigeonhole.Instance.ObsNowPlayingTemplate,
                "Example Song - LightAmp",
                "Harp",
                1,
                TimeSpan.FromSeconds(42),
                TimeSpan.FromMinutes(3));

            WriteTextFile(GetNowPlayingPath(), preview);

            string[] previewHistory =
            {
                "Example Song - LightAmp",
                "Previous Song",
                "Earlier Song"
            };

            WriteTextFile(
                GetHistoryPath(),
                string.Join(
                    Environment.NewLine,
                    previewHistory));
        }

        public static void RewriteHistoryFile()
        {
            if (!BmpPigeonhole.Instance.ObsOutputEnabled)
                return;

            IList<string> stored =
                BmpPigeonhole.Instance.ObsSongHistory
                ?? new List<string>();

            int keep =
                Math.Max(
                    1,
                    Math.Min(
                        50,
                        BmpPigeonhole.Instance.ObsHistoryLength));

            string output = string.Join(
                Environment.NewLine,
                stored.Take(keep));

            if (string.Equals(
                    output,
                    _lastHistoryText,
                    StringComparison.Ordinal))
            {
                return;
            }

            _lastHistoryText = output;
            WriteTextFile(GetHistoryPath(), output);
        }

        private static string FormatTemplate(
            string template,
            BmpSong song,
            string instrument,
            int track,
            TimeSpan elapsed,
            TimeSpan duration)
        {
            string songName = GetSongName(song);

            SongMetadataSnapshot metadata =
                SongMetadataService.Get(song);

            string rating =
                metadata.Rating > 0
                    ? new string('★', metadata.Rating)
                    : string.Empty;

            string tags =
                metadata.Tags == null
                    ? string.Empty
                    : string.Join(", ", metadata.Tags);

            return ApplyTokens(
                template,
                songName,
                instrument,
                track,
                elapsed,
                duration,
                rating,
                tags);
        }

        private static string ApplyTokens(
            string template,
            string song,
            string instrument,
            int track,
            TimeSpan elapsed,
            TimeSpan duration,
            string rating = "",
            string tags = "")
        {
            string value =
                string.IsNullOrWhiteSpace(template)
                    ? "{song}"
                    : template;

            return value
                .Replace("{song}", song ?? string.Empty)
                .Replace("{instrument}", instrument ?? string.Empty)
                .Replace(
                    "{track}",
                    track.ToString(
                        CultureInfo.InvariantCulture))
                .Replace(
                    "{elapsed}",
                    FormatTime(elapsed))
                .Replace(
                    "{duration}",
                    FormatTime(duration))
                .Replace("{rating}", rating ?? string.Empty)
                .Replace("{tags}", tags ?? string.Empty)
                .Trim();
        }

        private static string GetSongName(BmpSong song)
        {
            if (song == null)
                return string.Empty;

            if (!string.IsNullOrWhiteSpace(song.DisplayedTitle))
                return song.DisplayedTitle.Trim();

            if (!string.IsNullOrWhiteSpace(song.Title))
                return song.Title.Trim();

            return string.Empty;
        }

        private static string FormatTime(TimeSpan value)
        {
            if (value < TimeSpan.Zero)
                value = TimeSpan.Zero;

            if (value.TotalHours >= 1)
            {
                return value.ToString(
                    @"h\:mm\:ss",
                    CultureInfo.InvariantCulture);
            }

            return value.ToString(
                @"m\:ss",
                CultureInfo.InvariantCulture);
        }

        private static void EnsureOutputDirectory()
        {
            string directory = GetOutputDirectory();

            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            if (!File.Exists(GetNowPlayingPath()))
                WriteTextFile(GetNowPlayingPath(), string.Empty);

            if (!File.Exists(GetHistoryPath()))
                WriteTextFile(GetHistoryPath(), string.Empty);
        }

        private static void WriteTextFile(
            string path,
            string content)
        {
            try
            {
                lock (FileLock)
                {
                    string directory =
                        Path.GetDirectoryName(path);

                    if (!string.IsNullOrWhiteSpace(directory) &&
                        !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(
                        path,
                        content ?? string.Empty,
                        new UTF8Encoding(false));
                }
            }
            catch
            {
                // OBS output should never interrupt playback or UI startup.
            }
        }
    }
}
