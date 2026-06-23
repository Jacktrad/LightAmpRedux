using BardMusicPlayer.Pigeonhole;
using BardMusicPlayer.Transmogrify.Song;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace BardMusicPlayer.Ui.Functions
{
    public sealed class SongMetadataSnapshot
    {
        public bool Favorite { get; set; }
        public int Rating { get; set; }
        public int PlayCount { get; set; }
        public string LastPlayedUtc { get; set; }
        public IList<string> Tags { get; set; }
    }

    /// <summary>
    /// Persistent favorites, ratings, history and tag metadata.
    /// </summary>
    public static class SongMetadataService
    {
        private static readonly string[] DefaultTags =
        {
            "Solo",
            "Octet",
            "Metal",
            "Jazz",
            "Needs Editing"
        };

        public static event EventHandler TagsChanged;
        public static event EventHandler FavoritesChanged;

        public static string GetKey(BmpSong song)
        {
            if (song == null)
                return string.Empty;

            string title = string.IsNullOrWhiteSpace(song.Title)
                ? song.DisplayedTitle
                : song.Title;

            return (title ?? string.Empty).Trim()
                   + "|"
                   + song.Duration.Ticks;
        }

        public static SongMetadataSnapshot Get(BmpSong song)
        {
            EnsureStorage();

            string key = GetKey(song);
            BmpPigeonhole settings = BmpPigeonhole.Instance;

            bool favorite = false;
            int rating = 0;
            int playCount = 0;
            string lastPlayed = string.Empty;
            string tagValue = string.Empty;

            settings.SongFavorites.TryGetValue(key, out favorite);
            settings.SongRatings.TryGetValue(key, out rating);
            settings.SongPlayCounts.TryGetValue(key, out playCount);
            settings.SongLastPlayedUtc.TryGetValue(key, out lastPlayed);
            settings.SongTags.TryGetValue(key, out tagValue);

            return new SongMetadataSnapshot
            {
                Favorite = favorite,
                Rating = Math.Max(0, Math.Min(5, rating)),
                PlayCount = Math.Max(0, playCount),
                LastPlayedUtc = lastPlayed ?? string.Empty,
                Tags = ParseTags(tagValue)
            };
        }

        public static void SetFavorite(BmpSong song, bool value)
        {
            string key = GetKey(song);
            if (string.IsNullOrWhiteSpace(key))
                return;

            EnsureStorage();

            var copy = new Dictionary<string, bool>(
                BmpPigeonhole.Instance.SongFavorites,
                StringComparer.OrdinalIgnoreCase);

            copy[key] = value;
            BmpPigeonhole.Instance.SongFavorites = copy;

            RaiseFavoritesChanged();
        }

        public static void SetRating(BmpSong song, int value)
        {
            string key = GetKey(song);
            if (string.IsNullOrWhiteSpace(key))
                return;

            EnsureStorage();

            var copy = new Dictionary<string, int>(
                BmpPigeonhole.Instance.SongRatings,
                StringComparer.OrdinalIgnoreCase);

            copy[key] = Math.Max(0, Math.Min(5, value));
            BmpPigeonhole.Instance.SongRatings = copy;
        }

        public static void SetTags(
            BmpSong song,
            IEnumerable<string> values)
        {
            string key = GetKey(song);
            if (string.IsNullOrWhiteSpace(key))
                return;

            SetTagsByKey(key, values);
        }

        public static void AddTag(BmpSong song, string tag)
        {
            string cleanTag = CleanTag(tag);
            if (song == null || string.IsNullOrWhiteSpace(cleanTag))
                return;

            IList<string> current = Get(song).Tags;
            SetTags(
                song,
                current.Concat(new[] { cleanTag }));
        }

        public static void RemoveTag(BmpSong song, string tag)
        {
            string cleanTag = CleanTag(tag);
            if (song == null || string.IsNullOrWhiteSpace(cleanTag))
                return;

            SetTags(
                song,
                Get(song).Tags.Where(
                    current =>
                        !string.Equals(
                            current,
                            cleanTag,
                            StringComparison.OrdinalIgnoreCase)));
        }

        public static IList<string> GetAvailableTags()
        {
            EnsureStorage();

            var tags = new HashSet<string>(
                DefaultTags,
                StringComparer.OrdinalIgnoreCase);

            foreach (string tag in BmpPigeonhole.Instance.SongTagCatalog)
            {
                string clean = CleanTag(tag);
                if (!string.IsNullOrWhiteSpace(clean))
                    tags.Add(clean);
            }

            foreach (string storedTags in BmpPigeonhole.Instance.SongTags.Values)
            {
                foreach (string tag in ParseTags(storedTags))
                    tags.Add(tag);
            }

            return tags
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static string AddCatalogTag(string tag)
        {
            EnsureStorage();

            string cleanTag = CleanTag(tag);
            if (string.IsNullOrWhiteSpace(cleanTag))
                return string.Empty;

            var catalog = new HashSet<string>(
                BmpPigeonhole.Instance.SongTagCatalog,
                StringComparer.OrdinalIgnoreCase);

            bool added = catalog.Add(cleanTag);

            BmpPigeonhole.Instance.SongTagCatalog =
                catalog.OrderBy(
                    value => value,
                    StringComparer.OrdinalIgnoreCase)
                       .ToList();

            if (added)
                RaiseTagsChanged();

            return cleanTag;
        }

        public static void AssociatePath(BmpSong song, string path)
        {
            string key = GetKey(song);
            string normalizedPath = NormalizePath(path);

            if (string.IsNullOrWhiteSpace(key) ||
                string.IsNullOrWhiteSpace(normalizedPath))
            {
                return;
            }

            EnsureStorage();

            var aliases = new Dictionary<string, string>(
                BmpPigeonhole.Instance.SongPathAliases,
                StringComparer.OrdinalIgnoreCase);

            aliases[normalizedPath] = key;
            BmpPigeonhole.Instance.SongPathAliases = aliases;
        }

        public static int GetRatingForPath(string path)
        {
            EnsureStorage();

            string normalizedPath = NormalizePath(path);
            string metadataKey;

            if (!string.IsNullOrWhiteSpace(normalizedPath) &&
                BmpPigeonhole.Instance.SongPathAliases.TryGetValue(
                    normalizedPath,
                    out metadataKey))
            {
                return GetRatingByKey(metadataKey);
            }

            // Fallback for metadata created before path aliases existed.
            string title = string.Empty;

            try
            {
                title =
                    Path.GetFileNameWithoutExtension(path)
                    ?? string.Empty;
            }
            catch
            {
            }

            if (string.IsNullOrWhiteSpace(title))
                return 0;

            string prefix = title.Trim() + "|";

            // If multiple metadata records share the same title, show the
            // highest assigned rating rather than hiding a known rating.
            return BmpPigeonhole.Instance.SongRatings
                .Where(
                    entry =>
                        entry.Key.StartsWith(
                            prefix,
                            StringComparison.OrdinalIgnoreCase))
                .Select(entry => Math.Max(0, Math.Min(5, entry.Value)))
                .DefaultIfEmpty(0)
                .Max();
        }

        public static IList<string> GetTagsForPath(string path)
        {
            EnsureStorage();

            string normalizedPath = NormalizePath(path);
            string metadataKey;

            if (!string.IsNullOrWhiteSpace(normalizedPath) &&
                BmpPigeonhole.Instance.SongPathAliases.TryGetValue(
                    normalizedPath,
                    out metadataKey))
            {
                return GetTagsByKey(metadataKey);
            }

            // Fallback for songs loaded through playlists or older settings
            // before path aliases existed. BmpSong.Title is normally the file
            // name without extension.
            string title = string.Empty;

            try
            {
                title = Path.GetFileNameWithoutExtension(path) ?? string.Empty;
            }
            catch
            {
            }

            if (string.IsNullOrWhiteSpace(title))
                return new List<string>();

            string prefix = title.Trim() + "|";

            return BmpPigeonhole.Instance.SongTags
                .Where(
                    entry =>
                        entry.Key.StartsWith(
                            prefix,
                            StringComparison.OrdinalIgnoreCase))
                .SelectMany(entry => ParseTags(entry.Value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public static bool PathHasTag(string path, string tag)
        {
            string cleanTag = CleanTag(tag);

            if (string.IsNullOrWhiteSpace(cleanTag))
                return true;

            return GetTagsForPath(path).Any(
                current =>
                    string.Equals(
                        current,
                        cleanTag,
                        StringComparison.OrdinalIgnoreCase));
        }

        public static SongMetadataSnapshot RecordPlay(BmpSong song)
        {
            string key = GetKey(song);
            if (string.IsNullOrWhiteSpace(key))
                return Get(song);

            EnsureStorage();

            int current = 0;
            BmpPigeonhole.Instance.SongPlayCounts.TryGetValue(
                key,
                out current);

            var playCounts = new Dictionary<string, int>(
                BmpPigeonhole.Instance.SongPlayCounts,
                StringComparer.OrdinalIgnoreCase);

            playCounts[key] = current + 1;
            BmpPigeonhole.Instance.SongPlayCounts = playCounts;

            var lastPlayed = new Dictionary<string, string>(
                BmpPigeonhole.Instance.SongLastPlayedUtc,
                StringComparer.OrdinalIgnoreCase);

            lastPlayed[key] = DateTime.UtcNow.ToString("o");
            BmpPigeonhole.Instance.SongLastPlayedUtc = lastPlayed;

            return Get(song);
        }

        private static void SetTagsByKey(
            string key,
            IEnumerable<string> values)
        {
            EnsureStorage();

            IList<string> normalizedTags = values
                .Select(CleanTag)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var copy = new Dictionary<string, string>(
                BmpPigeonhole.Instance.SongTags,
                StringComparer.OrdinalIgnoreCase);

            copy[key] = string.Join(";", normalizedTags);
            BmpPigeonhole.Instance.SongTags = copy;

            var catalog = new HashSet<string>(
                BmpPigeonhole.Instance.SongTagCatalog,
                StringComparer.OrdinalIgnoreCase);

            foreach (string tag in normalizedTags)
                catalog.Add(tag);

            BmpPigeonhole.Instance.SongTagCatalog =
                catalog.OrderBy(
                    value => value,
                    StringComparer.OrdinalIgnoreCase)
                       .ToList();

            RaiseTagsChanged();
        }

        private static int GetRatingByKey(string key)
        {
            int rating;

            if (string.IsNullOrWhiteSpace(key) ||
                !BmpPigeonhole.Instance.SongRatings.TryGetValue(
                    key,
                    out rating))
            {
                return 0;
            }

            return Math.Max(0, Math.Min(5, rating));
        }

        private static IList<string> GetTagsByKey(string key)
        {
            string value;

            if (string.IsNullOrWhiteSpace(key) ||
                !BmpPigeonhole.Instance.SongTags.TryGetValue(
                    key,
                    out value))
            {
                return new List<string>();
            }

            return ParseTags(value);
        }

        private static IList<string> ParseTags(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();

            return value
                .Split(
                    new[] { ';' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(CleanTag)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string CleanTag(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            // Semicolons are the persisted delimiter.
            return value
                .Replace(";", string.Empty)
                .Trim();
        }

        private static string NormalizePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            try
            {
                return Path.GetFullPath(value)
                    .TrimEnd(
                        Path.DirectorySeparatorChar,
                        Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return value.Trim();
            }
        }

        private static void RaiseTagsChanged()
        {
            EventHandler handler = TagsChanged;
            if (handler != null)
                handler(null, EventArgs.Empty);
        }

        private static void RaiseFavoritesChanged()
        {
            EventHandler handler = FavoritesChanged;
            if (handler != null)
                handler(null, EventArgs.Empty);
        }

        private static void EnsureStorage()
        {
            BmpPigeonhole settings = BmpPigeonhole.Instance;

            if (settings.SongFavorites == null)
                settings.SongFavorites =
                    new Dictionary<string, bool>();

            if (settings.SongRatings == null)
                settings.SongRatings =
                    new Dictionary<string, int>();

            if (settings.SongPlayCounts == null)
                settings.SongPlayCounts =
                    new Dictionary<string, int>();

            if (settings.SongLastPlayedUtc == null)
                settings.SongLastPlayedUtc =
                    new Dictionary<string, string>();

            if (settings.SongTags == null)
                settings.SongTags =
                    new Dictionary<string, string>();

            if (settings.SongTagCatalog == null)
                settings.SongTagCatalog =
                    new List<string>();

            if (settings.SongPathAliases == null)
                settings.SongPathAliases =
                    new Dictionary<string, string>();
        }
    }
}
