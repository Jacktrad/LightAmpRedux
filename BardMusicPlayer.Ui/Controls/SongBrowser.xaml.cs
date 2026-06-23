/*
 * Copyright(c) 2026 GiR-Zippo
 * Licensed under the GPL v3 license. See https://github.com/GiR-Zippo/LightAmp/blob/main/LICENSE for full license information.
 */

using BardMusicPlayer.Pigeonhole;
using BardMusicPlayer.Ui.Resources;
using BardMusicPlayer.Ui.Functions;
using BardMusicPlayer.Ui.Windows;
using BardMusicPlayer.XIVMIDI.Events;
using BardMusicPlayer.XIVMIDI.IO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BardMusicPlayer.Ui.Controls
{
    /// <summary>
    /// The songbrowser
    /// </summary>
    public sealed partial class SongBrowser : UserControl
    {
        public EventHandler<string> OnLoadSongFromBrowser;
        public EventHandler<string> OnLoadSongFromBrowserToPreview;
        public EventHandler<string> OnAddSongFromBrowser;

        /// Temporary sender object
        private object _Sender { get; set; } = null;

        private const string AllTagsFilter = "All tags";
        private const string UntaggedFilter = "Untagged";

        private bool _loadingTagFilterChoices;
        private bool _tagEventsSubscribed;

        private readonly HashSet<string> _selectedTagFilters =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        private ObservableCollection<TagFilterChoice> _tagFilterChoices =
            new ObservableCollection<TagFilterChoice>();

        private bool _loadingRatingFilter;
        private int _selectedRatingFilter = -1;

        private bool _loadingFavoritesFilter;
        private bool _favoritesOnly;

        private bool _buildingSongTree;

        private const int MaxTreeItemsPerFolder = 2000;
        private const int MaxRememberedExpandedFolders = 100;

        private readonly Dictionary<string, int> _treeRatingsByTitle =
            new Dictionary<string, int>(
                StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, string> _treeTagsByTitle =
            new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);

        private readonly HashSet<string> _expandedFolderPaths =
            new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);

        private IList<SongBrowserTreeNode> _currentTreeRoots =
            new List<SongBrowserTreeNode>();

        public SongBrowser()
        {
            InitializeComponent();

            SongbrowserContainer.AddHandler(
                ListViewItem.PreviewMouseDoubleClickEvent,
                new MouseButtonEventHandler(
                    SongbrowserContainer_PreviewMouseDoubleClick));

            SongbrowserContainer.AddHandler(
                ListViewItem.PreviewMouseRightButtonDownEvent,
                new MouseButtonEventHandler(
                    OnListViewItemPreviewMouseRightButtonDown));

            XIVMIDI.XIVMidiApi.Instance.OnXIVUploadResponse +=
                Instance_OnUploadResponse;

            SongPath.Text =
                BmpPigeonhole.Instance.SongDirectory;

            LoadExpandedFolderSettings();
            LoadSelectedTagFilters();
            LoadRatingFilter();
            LoadFavoritesFilter();
            ApplyBrowserViewMode();

            RefreshTagFilterChoices();
            RefreshContainer();
        }

        private void RefreshContainer()
        {
            // TextChanged can fire while InitializeComponent is still building
            // the remaining controls.
            if (SongbrowserContainer == null
                ||
                SongTreeContainer == null
                ||
                TagFilterStatusText == null)
            {
                return;
            }

            string directory =
                SongPath == null
                    ? string.Empty
                    : SongPath.Text;

            if (string.IsNullOrWhiteSpace(directory)
                ||
                !Directory.Exists(directory))
            {
                SongbrowserContainer.ItemsSource =
                    new Dictionary<string, string>();

                SongTreeContainer.ItemsSource =
                    new List<SongBrowserTreeNode>();

                _currentTreeRoots =
                    new List<SongBrowserTreeNode>();

                TagFilterStatusText.Text =
                    "Folder not found";

                ApplyBrowserViewMode();
                return;
            }

            bool useTree =
                BmpPigeonhole.Instance.SongBrowserUseTreeView;

            bool filterActive =
                IsTreeFilterActive();

            // The normal Tree view is lazy. It does not recursively enumerate
            // the library and it does not create song controls for collapsed
            // folders.
            if (useTree
                &&
                !filterActive)
            {
                BuildTreeMetadataCache();

                _buildingSongTree = true;

                try
                {
                    _currentTreeRoots =
                        BuildLazySongTree(
                            directory);

                    SongTreeContainer.ItemsSource =
                        _currentTreeRoots;
                }
                finally
                {
                    _buildingSongTree = false;
                }

                SongbrowserContainer.ItemsSource =
                    new Dictionary<string, string>();

                TagFilterStatusText.Text =
                    "Folders load when expanded";

                ApplyBrowserViewMode();
                return;
            }

            // Flat mode retains the original recursive behaviour. Tree mode
            // also uses this proven ListView while a search or tag filter is
            // active, avoiding a second full hierarchy in memory.
            IList<BrowserSongEntry> matchingSongs =
                BuildFilteredSongList(
                    directory);

            SongbrowserContainer.ItemsSource =
                BuildFlatSongList(
                    matchingSongs);

            SongTreeContainer.ItemsSource =
                new List<SongBrowserTreeNode>();

            _currentTreeRoots =
                new List<SongBrowserTreeNode>();

            int count =
                matchingSongs.Count;

            TagFilterStatusText.Text =
                (count == 1
                    ? "1 song"
                    : count + " songs")
                +
                (useTree && filterActive
                    ? " · filtered flat results"
                    : string.Empty);

            ApplyBrowserViewMode();
        }

        private IList<BrowserSongEntry> BuildFilteredSongList(
            string directory)
        {
            string search =
                SongSearch == null
                    ? string.Empty
                    : (SongSearch.Text ?? string.Empty).Trim();

            IList<string> selectedTags =
                GetAppliedTagFilters();

            bool filterForUntagged =
                selectedTags.Any(
                    tag =>
                        string.Equals(
                            tag,
                            UntaggedFilter,
                            StringComparison.OrdinalIgnoreCase));

            var matchingSongs =
                new List<BrowserSongEntry>();

            HashSet<string> favoriteMetadataKeys =
                null;

            HashSet<string> favoriteTitles =
                null;

            Dictionary<string, string> favoritePathAliases =
                null;

            if (_favoritesOnly)
            {
                BuildFavoriteLookup(
                    out favoriteMetadataKeys,
                    out favoriteTitles,
                    out favoritePathAliases);
            }

            IEnumerable<string> files;

            try
            {
                files =
                    Directory.EnumerateFiles(
                        directory,
                        "*.*",
                        SearchOption.AllDirectories)
                    .Where(IsSupportedSongFile)
                    .OrderBy(
                        path => path,
                        StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch
            {
                files =
                    new string[0];
            }

            foreach (string file in files)
            {
                if (!string.IsNullOrWhiteSpace(search)
                    &&
                    file.IndexOf(
                        search,
                        StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (_favoritesOnly
                    &&
                    !IsFavoritePath(
                        file,
                        favoriteMetadataKeys,
                        favoriteTitles,
                        favoritePathAliases))
                {
                    continue;
                }

                IList<string> tags =
                    SongMetadataService.GetTagsForPath(file);

                int rating =
                    Math.Max(
                        0,
                        Math.Min(
                            5,
                            SongMetadataService.GetRatingForPath(file)));

                if (_selectedRatingFilter >= 0
                    &&
                    rating != _selectedRatingFilter)
                {
                    continue;
                }

                if (filterForUntagged)
                {
                    if (tags.Count > 0)
                        continue;
                }
                else if (selectedTags.Count > 0
                         &&
                         !selectedTags.All(
                             selectedTag =>
                                 tags.Any(
                                     songTag =>
                                         string.Equals(
                                             songTag,
                                             selectedTag,
                                             StringComparison.OrdinalIgnoreCase))))
                {
                    continue;
                }

                matchingSongs.Add(
                    new BrowserSongEntry
                    {
                        FullPath =
                            file,
                        DisplayName =
                            BuildSongDisplayName(
                                file,
                                rating,
                                tags)
                    });
            }

            return matchingSongs;
        }

        private static void BuildFavoriteLookup(
            out HashSet<string> metadataKeys,
            out HashSet<string> titles,
            out Dictionary<string, string> pathAliases)
        {
            BmpPigeonhole settings =
                BmpPigeonhole.Instance;

            metadataKeys =
                new HashSet<string>(
                    (settings.SongFavorites
                     ??
                     new Dictionary<string, bool>())
                    .Where(
                        entry => entry.Value)
                    .Select(
                        entry => entry.Key),
                    StringComparer.OrdinalIgnoreCase);

            titles =
                new HashSet<string>(
                    metadataKeys
                        .Select(GetMetadataTitle)
                        .Where(
                            title => !string.IsNullOrWhiteSpace(title)),
                    StringComparer.OrdinalIgnoreCase);

            pathAliases =
                new Dictionary<string, string>(
                    settings.SongPathAliases
                    ??
                    new Dictionary<string, string>(),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsFavoritePath(
            string path,
            ISet<string> metadataKeys,
            ISet<string> titles,
            IDictionary<string, string> pathAliases)
        {
            if (metadataKeys == null
                ||
                metadataKeys.Count == 0)
            {
                return false;
            }

            string normalizedPath =
                NormalizeSongPath(
                    path);

            string metadataKey;

            if (!string.IsNullOrWhiteSpace(normalizedPath)
                &&
                pathAliases != null
                &&
                pathAliases.TryGetValue(
                    normalizedPath,
                    out metadataKey))
            {
                return metadataKeys.Contains(
                    metadataKey);
            }

            string title =
                string.Empty;

            try
            {
                title =
                    Path.GetFileNameWithoutExtension(path)
                    ??
                    string.Empty;
            }
            catch
            {
            }

            return !string.IsNullOrWhiteSpace(title)
                   &&
                   titles != null
                   &&
                   titles.Contains(
                       title.Trim());
        }

        private static bool IsSupportedSongFile(
            string path)
        {
            string extension =
                Path.GetExtension(path);

            return extension.Equals(
                       ".mid",
                       StringComparison.OrdinalIgnoreCase)
                   ||
                   extension.Equals(
                       ".mml",
                       StringComparison.OrdinalIgnoreCase)
                   ||
                   extension.Equals(
                       ".mmsong",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static IDictionary<string, string> BuildFlatSongList(
            IEnumerable<BrowserSongEntry> songs)
        {
            var list =
                new Dictionary<string, string>();

            string lastDirectory =
                string.Empty;

            foreach (BrowserSongEntry song in songs)
            {
                string fileDirectory =
                    Path.GetDirectoryName(song.FullPath)
                    ??
                    string.Empty;

                if (!string.Equals(
                        fileDirectory,
                        lastDirectory,
                        StringComparison.OrdinalIgnoreCase))
                {
                    lastDirectory =
                        fileDirectory;

                    list["+" + lastDirectory + "+"] =
                        " ";

                    list["-" + lastDirectory] =
                        "-" + lastDirectory;

                    list["+" + lastDirectory + "-"] =
                        "------------------------------------------------------------------";
                }

                list[song.FullPath] =
                    song.DisplayName;
            }

            return list;
        }

        private IList<SongBrowserTreeNode> BuildLazySongTree(
            string rootDirectory)
        {
            string rootPath =
                NormalizeFolderPath(
                    rootDirectory);

            if (!BmpPigeonhole.Instance.SongBrowserTreeExpansionInitialized)
            {
                _expandedFolderPaths.Add(
                    rootPath);

                BmpPigeonhole.Instance.SongBrowserTreeExpansionInitialized =
                    true;

                SaveExpandedFolderSettings();
            }

            var root =
                CreateLazyFolderNode(
                    rootPath,
                    GetRootFolderDisplayName(
                        rootPath));

            root.IsExpanded =
                _expandedFolderPaths.Contains(
                    rootPath);

            // Load only the immediate contents of the root. Subfolders remain
            // lightweight placeholder nodes until the user expands them.
            EnsureTreeNodeChildrenLoaded(
                root);

            return new List<SongBrowserTreeNode>
            {
                root
            };
        }

        private SongBrowserTreeNode CreateLazyFolderNode(
            string path,
            string displayName)
        {
            var node =
                new SongBrowserTreeNode
                {
                    DisplayName =
                        displayName,
                    FullPath =
                        path,
                    IsFolder =
                        true,
                    IsExpanded =
                        _expandedFolderPaths.Contains(
                            path),
                    ChildrenLoaded =
                        false
                };

            node.Children.Add(
                SongBrowserTreeNode.CreatePlaceholder());

            return node;
        }

        private void EnsureTreeNodeChildrenLoaded(
            SongBrowserTreeNode node)
        {
            if (node == null
                ||
                !node.IsFolder
                ||
                node.ChildrenLoaded)
            {
                return;
            }

            node.ChildrenLoaded =
                true;

            node.Children.Clear();

            try
            {
                int added =
                    0;

                IEnumerable<string> folders =
                    Directory.EnumerateDirectories(
                        node.FullPath,
                        "*",
                        SearchOption.TopDirectoryOnly)
                    .OrderBy(
                        path => Path.GetFileName(path),
                        StringComparer.OrdinalIgnoreCase);

                foreach (string folder in folders)
                {
                    if (added >= MaxTreeItemsPerFolder)
                        break;

                    node.Children.Add(
                        CreateLazyFolderNode(
                            NormalizeFolderPath(folder),
                            Path.GetFileName(folder)));

                    added++;
                }

                if (added < MaxTreeItemsPerFolder)
                {
                    IEnumerable<string> files =
                        Directory.EnumerateFiles(
                            node.FullPath,
                            "*.*",
                            SearchOption.TopDirectoryOnly)
                        .Where(IsSupportedSongFile)
                        .OrderBy(
                            path => Path.GetFileName(path),
                            StringComparer.OrdinalIgnoreCase);

                    foreach (string file in files)
                    {
                        if (added >= MaxTreeItemsPerFolder)
                            break;

                        node.Children.Add(
                            new SongBrowserTreeNode
                            {
                                DisplayName =
                                    BuildTreeSongDisplayName(
                                        file),
                                FullPath =
                                    file,
                                IsFolder =
                                    false,
                                ChildrenLoaded =
                                    true
                            });

                        added++;
                    }
                }

                if (added >= MaxTreeItemsPerFolder)
                {
                    node.Children.Add(
                        SongBrowserTreeNode.CreateMessage(
                            "More items exist — use Flat view or Search"));
                }

                if (node.Children.Count == 0)
                {
                    node.Children.Add(
                        SongBrowserTreeNode.CreateMessage(
                            "Empty folder"));
                }
            }
            catch (UnauthorizedAccessException)
            {
                node.Children.Add(
                    SongBrowserTreeNode.CreateMessage(
                        "Access denied"));
            }
            catch (IOException)
            {
                node.Children.Add(
                    SongBrowserTreeNode.CreateMessage(
                        "Folder could not be read"));
            }
            catch
            {
                node.Children.Add(
                    SongBrowserTreeNode.CreateMessage(
                        "Folder could not be loaded"));
            }
        }

        private void BuildTreeMetadataCache()
        {
            _treeRatingsByTitle.Clear();
            _treeTagsByTitle.Clear();

            BmpPigeonhole settings =
                BmpPigeonhole.Instance;

            if (settings.SongRatings != null)
            {
                foreach (KeyValuePair<string, int> item in settings.SongRatings)
                {
                    string title =
                        GetMetadataTitle(
                            item.Key);

                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    int current =
                        0;

                    _treeRatingsByTitle.TryGetValue(
                        title,
                        out current);

                    _treeRatingsByTitle[title] =
                        Math.Max(
                            current,
                            Math.Max(
                                0,
                                Math.Min(
                                    5,
                                    item.Value)));
                }
            }

            if (settings.SongTags != null)
            {
                foreach (KeyValuePair<string, string> item in settings.SongTags)
                {
                    string title =
                        GetMetadataTitle(
                            item.Key);

                    if (string.IsNullOrWhiteSpace(title))
                        continue;

                    string existing =
                        string.Empty;

                    _treeTagsByTitle.TryGetValue(
                        title,
                        out existing);

                    IList<string> merged =
                        ParseStoredTags(existing)
                        .Concat(
                            ParseStoredTags(
                                item.Value))
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .OrderBy(
                            tag => tag,
                            StringComparer.OrdinalIgnoreCase)
                        .ToList();

                    _treeTagsByTitle[title] =
                        string.Join(
                            ";",
                            merged);
                }
            }
        }

        private string BuildTreeSongDisplayName(
            string path)
        {
            BmpPigeonhole settings =
                BmpPigeonhole.Instance;

            string metadataKey =
                string.Empty;

            string normalizedPath =
                NormalizeSongPath(
                    path);

            if (settings.SongPathAliases != null)
            {
                settings.SongPathAliases.TryGetValue(
                    normalizedPath,
                    out metadataKey);
            }

            int rating =
                0;

            IList<string> tags =
                new List<string>();

            if (!string.IsNullOrWhiteSpace(metadataKey))
            {
                if (settings.SongRatings != null)
                {
                    settings.SongRatings.TryGetValue(
                        metadataKey,
                        out rating);
                }

                string storedTags =
                    string.Empty;

                if (settings.SongTags != null
                    &&
                    settings.SongTags.TryGetValue(
                        metadataKey,
                        out storedTags))
                {
                    tags =
                        ParseStoredTags(
                            storedTags);
                }
            }
            else
            {
                string title =
                    Path.GetFileNameWithoutExtension(path)
                    ??
                    string.Empty;

                _treeRatingsByTitle.TryGetValue(
                    title,
                    out rating);

                string storedTags =
                    string.Empty;

                if (_treeTagsByTitle.TryGetValue(
                        title,
                        out storedTags))
                {
                    tags =
                        ParseStoredTags(
                            storedTags);
                }
            }

            return BuildSongDisplayName(
                path,
                rating,
                tags);
        }

        private static string BuildSongDisplayName(
            string path,
            int rating,
            IList<string> tags)
        {
            string displayName =
                Path.GetFileNameWithoutExtension(path)
                ??
                Path.GetFileName(path)
                ??
                path;

            if (rating > 0)
            {
                displayName +=
                    "   "
                    + BuildRatingText(
                        Math.Max(
                            0,
                            Math.Min(
                                5,
                                rating)));
            }

            if (tags != null
                &&
                tags.Count > 0)
            {
                displayName +=
                    "   ["
                    + string.Join(
                        ", ",
                        tags)
                    + "]";
            }

            return displayName;
        }

        private static string GetMetadataTitle(
            string metadataKey)
        {
            if (string.IsNullOrWhiteSpace(metadataKey))
                return string.Empty;

            int separator =
                metadataKey.IndexOf('|');

            return separator < 0
                ? metadataKey.Trim()
                : metadataKey
                    .Substring(
                        0,
                        separator)
                    .Trim();
        }

        private static IList<string> ParseStoredTags(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return new List<string>();

            return value
                .Split(
                    new[]
                    {
                        ';'
                    },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(
                    item => item.Trim())
                .Where(
                    item => !string.IsNullOrWhiteSpace(item))
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .OrderBy(
                    item => item,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static string NormalizeSongPath(
            string value)
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

        private static string NormalizeFolderPath(
            string path)
        {
            string fullPath =
                Path.GetFullPath(path);

            string root =
                Path.GetPathRoot(fullPath)
                ??
                string.Empty;

            string trimmed =
                fullPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            string trimmedRoot =
                root.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            if (string.Equals(
                    trimmed,
                    trimmedRoot,
                    StringComparison.OrdinalIgnoreCase))
            {
                return root;
            }

            return trimmed;
        }

        private static string GetRootFolderDisplayName(
            string rootPath)
        {
            string trimmed =
                rootPath.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);

            string name =
                Path.GetFileName(trimmed);

            return string.IsNullOrWhiteSpace(name)
                ? rootPath
                : name;
        }

        private void ApplyBrowserViewMode()
        {
            if (FlatViewButton == null
                ||
                TreeViewButton == null
                ||
                SongbrowserContainer == null
                ||
                SongTreeContainer == null)
            {
                return;
            }

            bool useTree =
                BmpPigeonhole.Instance.SongBrowserUseTreeView;

            bool filteredTree =
                useTree
                &&
                IsTreeFilterActive();

            bool showTree =
                useTree
                &&
                !filteredTree;

            FlatViewButton.IsChecked =
                !useTree;

            TreeViewButton.IsChecked =
                useTree;

            SongbrowserContainer.Visibility =
                showTree
                    ? Visibility.Collapsed
                    : Visibility.Visible;

            SongTreeContainer.Visibility =
                showTree
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            ExpandAllFoldersButton.Visibility =
                showTree
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            CollapseAllFoldersButton.Visibility =
                showTree
                    ? Visibility.Visible
                    : Visibility.Collapsed;
        }

        private void FlatViewButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.SongBrowserUseTreeView =
                false;

            RefreshContainer();
        }

        private void TreeViewButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BmpPigeonhole.Instance.SongBrowserUseTreeView =
                true;

            RefreshContainer();
        }

        private void ExpandAllFoldersButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            foreach (SongBrowserTreeNode root in _currentTreeRoots)
            {
                SetLoadedFolderExpansionRecursive(
                    root,
                    true);
            }

            BmpPigeonhole.Instance.SongBrowserTreeExpansionInitialized =
                true;

            SaveExpandedFolderSettings();

            TagFilterStatusText.Text =
                "Expanded loaded folders";
        }

        private void CollapseAllFoldersButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            _expandedFolderPaths.Clear();

            foreach (SongBrowserTreeNode root in _currentTreeRoots)
            {
                SetLoadedFolderExpansionRecursive(
                    root,
                    false);
            }

            BmpPigeonhole.Instance.SongBrowserTreeExpansionInitialized =
                true;

            SaveExpandedFolderSettings();

            TagFilterStatusText.Text =
                "Collapsed loaded folders";
        }

        private void SetLoadedFolderExpansionRecursive(
            SongBrowserTreeNode node,
            bool expanded)
        {
            if (node == null
                ||
                !node.IsFolder
                ||
                !node.ChildrenLoaded)
            {
                return;
            }

            node.IsExpanded =
                expanded;

            if (expanded)
            {
                _expandedFolderPaths.Add(
                    node.FullPath);
            }
            else
            {
                _expandedFolderPaths.Remove(
                    node.FullPath);
            }

            foreach (SongBrowserTreeNode child in node.Children)
            {
                if (child.IsFolder
                    &&
                    child.ChildrenLoaded)
                {
                    SetLoadedFolderExpansionRecursive(
                        child,
                        expanded);
                }
            }
        }

        private void LoadExpandedFolderSettings()
        {
            _expandedFolderPaths.Clear();

            string saved =
                BmpPigeonhole.Instance.SongBrowserExpandedFolders
                ??
                string.Empty;

            foreach (string folder in saved.Split('|'))
            {
                string clean =
                    folder.Trim();

                if (!string.IsNullOrWhiteSpace(clean))
                {
                    _expandedFolderPaths.Add(
                        clean);
                }
            }

            // The earlier eager Tree implementation could persist every folder
            // after Expand All. Refusing to restore an unbounded expansion set
            // prevents that old state from recreating the original crash.
            if (_expandedFolderPaths.Count > MaxRememberedExpandedFolders)
            {
                _expandedFolderPaths.Clear();

                BmpPigeonhole.Instance.SongBrowserExpandedFolders =
                    string.Empty;

                BmpPigeonhole.Instance.SongBrowserTreeExpansionInitialized =
                    false;
            }
        }

        private void SaveExpandedFolderSettings()
        {
            IEnumerable<string> safeFolders =
                _expandedFolderPaths
                    .OrderBy(
                        folder => folder,
                        StringComparer.OrdinalIgnoreCase)
                    .Take(
                        MaxRememberedExpandedFolders);

            BmpPigeonhole.Instance.SongBrowserExpandedFolders =
                string.Join(
                    "|",
                    safeFolders);
        }

        private void SongTreeItem_Expanded(
            object sender,
            RoutedEventArgs e)
        {
            TreeViewItem item =
                sender as TreeViewItem;

            SongBrowserTreeNode node =
                item == null
                    ? null
                    : item.DataContext
                      as SongBrowserTreeNode;

            if (node != null
                &&
                node.IsFolder)
            {
                EnsureTreeNodeChildrenLoaded(
                    node);
            }

            UpdateExpandedFolderState(
                item,
                true);
        }

        private void SongTreeItem_Collapsed(
            object sender,
            RoutedEventArgs e)
        {
            UpdateExpandedFolderState(
                sender as TreeViewItem,
                false);
        }

        private void UpdateExpandedFolderState(
            TreeViewItem item,
            bool expanded)
        {
            if (_buildingSongTree
                ||
                item == null
                ||
                IsTreeFilterActive())
            {
                return;
            }

            SongBrowserTreeNode node =
                item.DataContext
                as SongBrowserTreeNode;

            if (node == null
                ||
                !node.IsFolder)
            {
                return;
            }

            if (expanded)
            {
                _expandedFolderPaths.Add(
                    node.FullPath);
            }
            else
            {
                _expandedFolderPaths.Remove(
                    node.FullPath);
            }

            SaveExpandedFolderSettings();
        }

        private bool IsTreeFilterActive()
        {
            string search =
                SongSearch == null
                    ? string.Empty
                    : (SongSearch.Text ?? string.Empty).Trim();

            return !string.IsNullOrWhiteSpace(search)
                   ||
                   _selectedTagFilters.Count > 0
                   ||
                   _selectedRatingFilter >= 0
                   ||
                   _favoritesOnly;
        }

        private sealed class BrowserSongEntry
        {
            public string FullPath { get; set; }
            public string DisplayName { get; set; }
        }

        public sealed class SongBrowserTreeNode :
            INotifyPropertyChanged
        {
            private bool _isExpanded;

            public string DisplayName { get; set; }
            public string FullPath { get; set; }
            public bool IsFolder { get; set; }
            public bool IsPlaceholder { get; set; }
            public bool IsMessage { get; set; }
            public bool ChildrenLoaded { get; set; }

            public string Icon
            {
                get
                {
                    if (IsPlaceholder)
                        return string.Empty;

                    if (IsMessage)
                        return "…";

                    return IsFolder
                        ? "📁"
                        : "♪";
                }
            }

            public ObservableCollection<SongBrowserTreeNode> Children
            {
                get;
                set;
            } =
                new ObservableCollection<SongBrowserTreeNode>();

            public bool IsExpanded
            {
                get
                {
                    return _isExpanded;
                }
                set
                {
                    if (_isExpanded == value)
                        return;

                    _isExpanded =
                        value;

                    PropertyChanged?.Invoke(
                        this,
                        new PropertyChangedEventArgs(
                            nameof(IsExpanded)));
                }
            }

            public static SongBrowserTreeNode CreatePlaceholder()
            {
                return new SongBrowserTreeNode
                {
                    DisplayName =
                        "Loading...",
                    IsPlaceholder =
                        true,
                    ChildrenLoaded =
                        true
                };
            }

            public static SongBrowserTreeNode CreateMessage(
                string message)
            {
                return new SongBrowserTreeNode
                {
                    DisplayName =
                        message,
                    IsMessage =
                        true,
                    ChildrenLoaded =
                        true
                };
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private static string BuildRatingText(int rating)
        {
            int normalized =
                Math.Max(0, Math.Min(5, rating));

            return new string('★', normalized)
                   + new string('☆', 5 - normalized);
        }

        public void RefreshTagFilterChoices()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(
                    new Action(RefreshTagFilterChoices));
                return;
            }

            if (TagFilterItemsControl == null
                ||
                TagFilterDropDownButton == null)
            {
                return;
            }

            _loadingTagFilterChoices = true;

            try
            {
                IList<string> availableTags =
                    SongMetadataService.GetAvailableTags()
                        .Where(
                            tag => !string.IsNullOrWhiteSpace(tag))
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .OrderBy(
                            tag => tag,
                            StringComparer.OrdinalIgnoreCase)
                        .ToList();

                var validTags =
                    new HashSet<string>(
                        availableTags,
                        StringComparer.OrdinalIgnoreCase);

                validTags.Add(
                    UntaggedFilter);

                bool removedUnavailable =
                    _selectedTagFilters.RemoveWhere(
                        tag => !validTags.Contains(tag)) > 0;

                _tagFilterChoices =
                    new ObservableCollection<TagFilterChoice>();

                _tagFilterChoices.Add(
                    new TagFilterChoice
                    {
                        Name =
                            UntaggedFilter,
                        IsChecked =
                            _selectedTagFilters.Contains(
                                UntaggedFilter)
                    });

                foreach (string tag in availableTags)
                {
                    _tagFilterChoices.Add(
                        new TagFilterChoice
                        {
                            Name =
                                tag,
                            IsChecked =
                                _selectedTagFilters.Contains(
                                    tag)
                        });
                }

                TagFilterItemsControl.ItemsSource =
                    _tagFilterChoices;

                UpdateTagFilterButtonText();

                if (removedUnavailable)
                    SaveSelectedTagFilters();
            }
            finally
            {
                _loadingTagFilterChoices = false;
            }

            RefreshContainer();
        }

        private void TagFilterPopup_Opened(
            object sender,
            EventArgs e)
        {
            foreach (TagFilterChoice choice in _tagFilterChoices)
            {
                choice.IsChecked =
                    _selectedTagFilters.Contains(
                        choice.Name);
            }
        }

        private void TagFilterPopup_Closed(
            object sender,
            EventArgs e)
        {
            if (_loadingTagFilterChoices)
                return;

            var nextSelection =
                new HashSet<string>(
                    _tagFilterChoices
                        .Where(
                            choice => choice.IsChecked)
                        .Select(
                            choice => choice.Name),
                    StringComparer.OrdinalIgnoreCase);

            if (nextSelection.SetEquals(
                    _selectedTagFilters))
            {
                UpdateTagFilterButtonText();
                return;
            }

            _selectedTagFilters.Clear();

            foreach (string tag in nextSelection)
            {
                _selectedTagFilters.Add(
                    tag);
            }

            SaveSelectedTagFilters();
            UpdateTagFilterButtonText();
            RefreshContainer();
        }

        private void TagFilterCheckBox_Click(
            object sender,
            RoutedEventArgs e)
        {
            CheckBox checkBox =
                sender as CheckBox;

            TagFilterChoice selectedChoice =
                checkBox == null
                    ? null
                    : checkBox.DataContext
                      as TagFilterChoice;

            if (selectedChoice == null
                ||
                !selectedChoice.IsChecked)
            {
                return;
            }

            bool selectedUntagged =
                string.Equals(
                    selectedChoice.Name,
                    UntaggedFilter,
                    StringComparison.OrdinalIgnoreCase);

            foreach (TagFilterChoice choice in _tagFilterChoices)
            {
                if (ReferenceEquals(
                        choice,
                        selectedChoice))
                {
                    continue;
                }

                bool choiceIsUntagged =
                    string.Equals(
                        choice.Name,
                        UntaggedFilter,
                        StringComparison.OrdinalIgnoreCase);

                if (selectedUntagged
                    ||
                    choiceIsUntagged)
                {
                    choice.IsChecked =
                        false;
                }
            }
        }

        private void TagFilterClearButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            foreach (TagFilterChoice choice in _tagFilterChoices)
            {
                choice.IsChecked =
                    false;
            }

            TagFilterDropDownButton.IsChecked =
                false;
        }

        private void LoadSelectedTagFilters()
        {
            _selectedTagFilters.Clear();

            string saved =
                BmpPigeonhole.Instance.SongBrowserSelectedTags
                ??
                string.Empty;

            foreach (string tag in saved.Split('|'))
            {
                string clean =
                    tag.Trim();

                if (string.IsNullOrWhiteSpace(clean)
                    ||
                    string.Equals(
                        clean,
                        AllTagsFilter,
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                _selectedTagFilters.Add(
                    clean);
            }
        }

        private void SaveSelectedTagFilters()
        {
            BmpPigeonhole.Instance.SongBrowserSelectedTags =
                string.Join(
                    "|",
                    _selectedTagFilters
                        .OrderBy(
                            tag => tag,
                            StringComparer.OrdinalIgnoreCase));
        }

        private IList<string> GetAppliedTagFilters()
        {
            return _selectedTagFilters
                .OrderBy(
                    tag => tag,
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void UpdateTagFilterButtonText()
        {
            if (TagFilterDropDownButton == null)
                return;

            IList<string> selected =
                GetAppliedTagFilters();

            if (selected.Count == 0)
            {
                TagFilterDropDownButton.Content =
                    AllTagsFilter;

                TagFilterDropDownButton.ToolTip =
                    "No tag filtering. Check several tags to require all of them.";

                return;
            }

            TagFilterDropDownButton.Content =
                selected.Count == 1
                    ? selected[0]
                    : selected.Count + " tags selected";

            TagFilterDropDownButton.ToolTip =
                "Matches songs containing all selected tags: "
                + string.Join(
                    ", ",
                    selected);
        }

        private sealed class TagFilterChoice :
            INotifyPropertyChanged
        {
            private bool _isChecked;

            public string Name { get; set; }

            public bool IsChecked
            {
                get
                {
                    return _isChecked;
                }
                set
                {
                    if (_isChecked == value)
                        return;

                    _isChecked =
                        value;

                    PropertyChanged?.Invoke(
                        this,
                        new PropertyChangedEventArgs(
                            nameof(IsChecked)));
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;
        }

        private void LoadFavoritesFilter()
        {
            _favoritesOnly =
                BmpPigeonhole.Instance.SongBrowserFavoritesOnly;

            if (FavoritesOnlyButton == null)
                return;

            _loadingFavoritesFilter =
                true;

            try
            {
                FavoritesOnlyButton.IsChecked =
                    _favoritesOnly;
            }
            finally
            {
                _loadingFavoritesFilter =
                    false;
            }
        }

        private void FavoritesOnlyButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (_loadingFavoritesFilter
                ||
                FavoritesOnlyButton == null)
            {
                return;
            }

            _favoritesOnly =
                FavoritesOnlyButton.IsChecked == true;

            BmpPigeonhole.Instance.SongBrowserFavoritesOnly =
                _favoritesOnly;

            RefreshContainer();
        }

        private void LoadRatingFilter()
        {
            int saved =
                BmpPigeonhole.Instance.SongBrowserRatingFilter;

            if (saved < -1
                ||
                saved > 5)
            {
                saved = -1;
            }

            _selectedRatingFilter =
                saved;

            if (RatingFilterComboBox == null)
                return;

            _loadingRatingFilter =
                true;

            try
            {
                foreach (ComboBoxItem item in RatingFilterComboBox.Items)
                {
                    int itemValue;

                    if (int.TryParse(
                            Convert.ToString(item.Tag),
                            out itemValue)
                        &&
                        itemValue == saved)
                    {
                        RatingFilterComboBox.SelectedItem =
                            item;

                        break;
                    }
                }

                if (RatingFilterComboBox.SelectedIndex < 0)
                {
                    RatingFilterComboBox.SelectedIndex =
                        0;

                    _selectedRatingFilter =
                        -1;
                }
            }
            finally
            {
                _loadingRatingFilter =
                    false;
            }
        }

        private void RatingFilterComboBox_SelectionChanged(
            object sender,
            SelectionChangedEventArgs e)
        {
            if (_loadingRatingFilter
                ||
                RatingFilterComboBox == null)
            {
                return;
            }

            ComboBoxItem selectedItem =
                RatingFilterComboBox.SelectedItem
                as ComboBoxItem;

            int selectedValue =
                -1;

            if (selectedItem != null)
            {
                int parsed;

                if (int.TryParse(
                        Convert.ToString(selectedItem.Tag),
                        out parsed))
                {
                    selectedValue =
                        Math.Max(
                            -1,
                            Math.Min(
                                5,
                                parsed));
                }
            }

            if (_selectedRatingFilter == selectedValue)
                return;

            _selectedRatingFilter =
                selectedValue;

            BmpPigeonhole.Instance.SongBrowserRatingFilter =
                selectedValue;

            RefreshContainer();
        }

        private void TagFilterRefreshButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            LoadRatingFilter();
            LoadFavoritesFilter();
            RefreshTagFilterChoices();
        }

        private void UserControl_Loaded(
            object sender,
            RoutedEventArgs e)
        {
            if (_tagEventsSubscribed)
                return;

            SongMetadataService.TagsChanged +=
                SongMetadataService_TagsChanged;

            SongMetadataService.FavoritesChanged +=
                SongMetadataService_FavoritesChanged;

            _tagEventsSubscribed = true;
            ApplyBrowserViewMode();
            RefreshTagFilterChoices();
        }

        private void UserControl_Unloaded(
            object sender,
            RoutedEventArgs e)
        {
            if (!_tagEventsSubscribed)
                return;

            SongMetadataService.TagsChanged -=
                SongMetadataService_TagsChanged;

            SongMetadataService.FavoritesChanged -=
                SongMetadataService_FavoritesChanged;

            _tagEventsSubscribed = false;
        }

        private void SongMetadataService_TagsChanged(
            object sender,
            EventArgs e)
        {
            RefreshTagFilterChoices();
        }

        private void SongMetadataService_FavoritesChanged(
            object sender,
            EventArgs e)
        {
            if (!_favoritesOnly)
                return;

            Dispatcher.BeginInvoke(
                new Action(
                    RefreshContainer));
        }

        /// <summary>
        /// Load the doubleclicked song into the sequencer
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SongbrowserContainer_PreviewMouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            string filename = GetFilenameFromSelection();
            if (filename == "")
                return;

            OnLoadSongFromBrowser?.Invoke(this, filename);
        }

        private void SongTreeContainer_PreviewMouseDoubleClick(
            object sender,
            MouseButtonEventArgs e)
        {
            TreeViewItem item =
                ItemsControl.ContainerFromElement(
                    SongTreeContainer,
                    e.OriginalSource as DependencyObject)
                as TreeViewItem;

            SongBrowserTreeNode node =
                item == null
                    ? null
                    : item.DataContext
                      as SongBrowserTreeNode;

            if (node == null)
                return;

            if (node.IsFolder)
            {
                item.IsExpanded =
                    !item.IsExpanded;

                e.Handled =
                    true;

                return;
            }

            if (!File.Exists(node.FullPath))
                return;

            OnLoadSongFromBrowser?.Invoke(
                this,
                node.FullPath);

            e.Handled =
                true;
        }

        private void SongTreeContainer_PreviewMouseRightButtonDown(
            object sender,
            MouseButtonEventArgs e)
        {
            _Sender =
                null;

            TreeViewItem item =
                ItemsControl.ContainerFromElement(
                    SongTreeContainer,
                    e.OriginalSource as DependencyObject)
                as TreeViewItem;

            if (item == null)
                return;

            item.IsSelected =
                true;

            item.Focus();
            _Sender = item;
        }

        private void SongTreeContainer_KeyDown(
            object sender,
            KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            SongBrowserTreeNode node =
                SongTreeContainer.SelectedItem
                as SongBrowserTreeNode;

            if (node == null)
                return;

            if (node.IsFolder)
            {
                node.IsExpanded =
                    !node.IsExpanded;

                e.Handled =
                    true;

                return;
            }

            if (!File.Exists(node.FullPath))
                return;

            OnLoadSongFromBrowser?.Invoke(
                this,
                node.FullPath);

            e.Handled =
                true;
        }

        /// <summary>
        /// Sets the search parameter
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SongSearch_PreviewTextInput(
            object sender,
            TextCompositionEventArgs e)
        {
            RefreshContainer();
        }

        private void SongSearch_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            RefreshContainer();
        }

        /// <summary>
        /// Sets the songs folder path by typing
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SongPath_PreviewTextInput(
            object sender,
            TextCompositionEventArgs e)
        {
            RefreshContainer();
        }

        private void SongPath_TextChanged(
            object sender,
            TextChangedEventArgs e)
        {
            RefreshContainer();
        }

        /// <summary>
        /// Sets the songs folder path by folderselection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new FolderPicker();

            if (Directory.Exists(BmpPigeonhole.Instance.SongDirectory))
                dlg.InputPath = Path.GetFullPath(BmpPigeonhole.Instance.SongDirectory);
            else
                dlg.InputPath = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);

            if (dlg.ShowDialog() == true)
            {
                string path = dlg.ResultPath;
                if (!Directory.Exists(path))
                    return;

                path = path + (path.EndsWith("\\") ? "" : "\\");
                SongPath.Text = path;
                BmpPigeonhole.Instance.SongDirectory = path;
                RefreshContainer();
            }
        }

        /// <summary>
        /// Handle the right click on an item from ListView
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void OnListViewItemPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            _Sender = sender; //set the sender to the item we hovered over
            e.Handled = true;
        }

        /// <summary>
        /// Handle add to playlist context menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddToPlaylist_Click(object sender, RoutedEventArgs e)
        {
            string filename = GetFilenameFromSender(_Sender);
            if (filename == "")
                return;
            OnAddSongFromBrowser?.Invoke(this, filename);
        }

        /// <summary>
        /// Handle the load to preview context menu
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadSongToPreview(object sender, RoutedEventArgs e)
        {
            string filename = GetFilenameFromSender(_Sender);
            if (filename == "")
                return;
            OnLoadSongFromBrowserToPreview?.Invoke(this, filename);
        }

        /// <summary>
        /// Get the filename from the sender
        /// </summary>
        /// <param name="sender"></param>
        /// <returns></returns>
        private string GetFilenameFromSender(
            object sender)
        {
            string filename =
                string.Empty;

            if (SongTreeContainer.Visibility == Visibility.Visible)
            {
                TreeViewItem treeItem =
                    sender as TreeViewItem;

                SongBrowserTreeNode treeNode =
                    treeItem == null
                        ? SongTreeContainer.SelectedItem
                          as SongBrowserTreeNode
                        : treeItem.DataContext
                          as SongBrowserTreeNode;

                if (treeNode != null
                    &&
                    !treeNode.IsFolder
                    &&
                    File.Exists(treeNode.FullPath))
                {
                    filename =
                        treeNode.FullPath;
                }

                _Sender = null;
                return filename;
            }

            ListViewItem listItem =
                sender as ListViewItem;

            if (listItem != null
                &&
                listItem.Content
                is KeyValuePair<string, string>)
            {
                KeyValuePair<string, string> item =
                    (KeyValuePair<string, string>)listItem.Content;

                if (File.Exists(item.Key))
                {
                    filename =
                        item.Key;
                }
            }

            _Sender = null;

            if (string.IsNullOrWhiteSpace(filename))
            {
                filename =
                    GetFilenameFromSelection();
            }

            return filename;
        }

        /// <summary>
        /// Get the selected filename
        /// </summary>
        /// <returns></returns>
        private string GetFilenameFromSelection()
        {
            if (SongTreeContainer.Visibility == Visibility.Visible)
            {
                SongBrowserTreeNode node =
                    SongTreeContainer.SelectedItem
                    as SongBrowserTreeNode;

                if (node == null
                    ||
                    node.IsFolder
                    ||
                    !File.Exists(node.FullPath))
                {
                    return string.Empty;
                }

                return node.FullPath;
            }

            try
            {
                IEnumerable<KeyValuePair<string, string>> selected =
                    SongbrowserContainer.SelectedItems
                        .OfType<KeyValuePair<string, string>>();

                KeyValuePair<string, string> first =
                    selected.First();

                if (string.IsNullOrWhiteSpace(first.Key)
                    ||
                    !File.Exists(first.Key))
                {
                    return string.Empty;
                }

                return first.Key;
            }
            catch
            {
                return string.Empty;
            }
        }

        private void Instance_OnUploadResponse(object sender, XIVMidiUploadResponseEvent e)
        {
            if (e.StatusCode == System.Net.HttpStatusCode.OK || e.StatusCode == System.Net.HttpStatusCode.Created)
                MessageBox.Show("Uploaded", "Wohoo \\o/");
            else if (e.StatusCode == System.Net.HttpStatusCode.Unauthorized || e.StatusCode == System.Net.HttpStatusCode.Forbidden)
                MessageBox.Show("Bad or missing Api Key", "Error");
            else
                MessageBox.Show("Something went wrong here...", "Error");
        }

        private async void UploadToBMP_Click(object sender, RoutedEventArgs e)
        {
            string filename = GetFilenameFromSelection();
            if (filename == "")
                return;

            UploadData data = new UploadData(filename);
            BMPUploadBuilder bmpUpload = data.ShowDialog();
            if (bmpUpload == null)
                return;
            if (bmpUpload.title == "" || bmpUpload.artist == "" || bmpUpload.source == "")
            {
                MessageBox.Show("Missing Title or Artist or Source!", "Error");
                return;
            }

            bmpUpload.ApiKey = BmpPigeonhole.Instance.BMPApiKey;
            bmpUpload.MidiFile = File.ReadAllBytes(filename);
            bmpUpload.FileName = Path.GetFileName(filename);
            XIVMIDI.XIVMidiApi.Instance.UploadMidi(bmpUpload);
        }
    }
}
