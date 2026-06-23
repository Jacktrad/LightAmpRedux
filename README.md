# LightAmpRedux

LightAmpRedux is a modernized fork of LightAmp for playing MIDI music with the Bard performance system in **Final Fantasy XIV**.

It adds a refreshed interface, improved song-library tools, customizable layouts, visualizers, favorites, ratings, tags, playlists, and OBS-friendly now-playing output.

> LightAmpRedux is an independent community project and is not affiliated with or endorsed by Square Enix.

## Features

* BlackGlassNeon default theme
* Responsive, resizable dashboard layout
* Collapsible and dockable interface panels
* Flat and folder-tree song browser views
* Fast browsing for large MIDI libraries
* Search by filename or folder
* Multi-tag filtering
* Rating filters
* Favorites-only filtering
* Automatic **Favorites** playlist
* Song ratings and metadata
* Piano-roll visualizer
* Live keyboard heat map
* Dockable playlist and song-history panels
* OBS now-playing and song-history text output
* External XAML theme support
* Theme-aware custom title bar
* Persistent layout and interface settings

## Download

Download the latest Windows release from the repository's **Releases** page:

[Download LightAmpRedux](https://github.com/Jacktrad/LightAmpRedux/releases)

1. Download the latest Windows x64 ZIP.
2. Extract the complete ZIP to a folder.
3. Run `LightAmpRedux.exe`.

Do not run the program directly from inside the ZIP.

## Song Browser

The Local Song Browser supports:

* Flat recursive song lists
* Lazy-loading folder trees
* Filename and path search
* Multiple selected tags
* Exact rating filters
* Unrated songs
* Favorites-only results

Multiple tag selections use **AND matching**. For example, selecting `Final Fantasy`, `Metal`, and `Octet` displays only songs containing all three tags.

## Favorites

Selecting the Favorite star on a loaded song:

* Marks the song as a favorite
* Creates a playlist named **Favorites** when needed
* Adds the song to that playlist
* Prevents duplicate Favorites entries

Unselecting the star removes the song from the Favorites playlist without deleting it from the music library.

## Themes

LightAmpRedux uses:

```text
Themes\BlackGlassNeon.xaml
```

as its default release theme.

Additional `.xaml` themes can be placed in the `Themes` folder and selected from the application settings.

## OBS Output

LightAmpRedux can write now-playing and song-history information to text files for use with OBS text sources.

Typical uses include:

* Current song title
* Recently played songs
* Stream overlays
* Venue performance displays


## Credits

LightAmpRedux is based on the original [LightAmp](https://github.com/GiR-Zippo/LightAmp) project by GiR-Zippo and its contributors.

Thanks to the FFXIV bard-music community for testing, feedback, MIDI arrangements, and feature ideas.

## License

LightAmpRedux follows the license terms included with the source repository.

Please review the repository's license before redistributing modified builds.

## Disclaimer

Use third-party tools responsibly and review the current FINAL FANTASY XIV User Agreement and policies before use.

FINAL FANTASY XIV and related names are trademarks or registered trademarks of Square Enix Holdings Co., Ltd.
::: 
