using DiscordRPC;
using DiscordRPC.Logging;
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net; // For WebUtility
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks; // For Task
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using TagLib;
using static Dummy_Music_Player.PlaylistItem;
//using GongSolutions.Wpf.DragDrop;

namespace Dummy_Music_Player
{
    public enum RepeatMode { Off, All, One }

    public class PlaylistItem : INotifyPropertyChanged
    {
        public string FilePath { get; set; }
        public string FileName { get; set; }

        public int TrackNumber { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }

        private bool _isNowPlaying;
        public bool IsNowPlaying
        {
            get => _isNowPlaying;
            set
            {
                _isNowPlaying = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(NowPlayingText));
            }
        }
        public string NowPlayingText => IsNowPlaying ? "NOW PLAYING" : "";

        private int _priorityNumber;
        public int PriorityNumber
        {
            get => _priorityNumber;
            set
            {
                _priorityNumber = value;
                OnPropertyChanged(nameof(PriorityNumber));
            }
        }

        private bool _isInPriorityQueue; // Defaults to false
        public bool IsInPriorityQueue
        {
            get => _isInPriorityQueue;
            set
            {
                if (_isInPriorityQueue != value)
                {
                    _isInPriorityQueue = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(AddButtonVisibility)); // Notify the UI to update
                }
            }
        }

        public class NaturalStringComparer : IComparer<string>
        {
            [DllImport("shlwapi.dll", CharSet = CharSet.Unicode)]
            private static extern int StrCmpLogicalW(string psz1, string psz2);

            public int Compare(string x, string y)
            {
                return StrCmpLogicalW(x, y);
            }
        }

        // This property will be bound to the button's Visibility
        public Visibility AddButtonVisibility => IsInPriorityQueue ? Visibility.Collapsed : Visibility.Visible;

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // ==================================================================
    // === VVVV ALL YOUR LIBRARY & API CLASSES ARE HERE VVVV ===
    // ==================================================================

    public class ArtistModel : INotifyPropertyChanged
    {
        public string ArtistName { get; set; }

        private BitmapImage _artistArt;
        public BitmapImage ArtistArt
        {
            get => _artistArt;
            set
            {
                _artistArt = value;
                OnPropertyChanged(nameof(ArtistArt)); // Notify the UI to update!
            }
        }

        public List<AlbumModel> Albums { get; set; } = new List<AlbumModel>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class AlbumModel : INotifyPropertyChanged
    {
        public string AlbumTitle { get; set; }
        public string ArtistName { get; set; }

        // Used to load art from local files first
        public byte[] AlbumArtBytes { get; set; }

        private BitmapImage _albumArt;
        public BitmapImage AlbumArt
        {
            get => _albumArt;
            set
            {
                _albumArt = value;
                OnPropertyChanged(nameof(AlbumArt));
            }
        }

        public List<PlaylistItem> Songs { get; set; } = new List<PlaylistItem>();

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // --- Classes for Last.fm JSON Deserialization (NEW VERSION) ---

    public class LastFmArtistResponse
    {
        [JsonPropertyName("artist")]
        public ArtistInfo Artist { get; set; }
    }

    public class TheAudioDbResponse
    {
        [JsonPropertyName("artists")]
        public List<TheAudioDbArtist> Artists { get; set; }
    }

    public class TheAudioDbTrackResponse
    {
        // Note: The API sends "track" not "tracks"
        [JsonPropertyName("track")]
        public List<TheAudioDbTrack> Track { get; set; }
    }

    public class TheAudioDbTrack
    {
        [JsonPropertyName("idArtist")]
        public string idArtist { get; set; }

        [JsonPropertyName("strArtist")]
        public string strArtist { get; set; }

        [JsonPropertyName("strArtistThumb")]
        public string ArtistThumbUrl { get; set; }
    }

    public class TheAudioDbArtist
    {
        [JsonPropertyName("strArtist")]
        public string ArtistName { get; set; }

        [JsonPropertyName("strArtistThumb")]
        public string ArtistThumbUrl { get; set; } // This is the image we want!

        [JsonPropertyName("strBiographyEN")]
        public string BiographyEN { get; set; } // This is the bio
    }

    public class TheAudioDbAlbumResponse
    {
        [JsonPropertyName("album")]
        public List<TheAudioDbAlbum> Album { get; set; }
    }

    public class TheAudioDbAlbum
    {
        [JsonPropertyName("idArtist")]
        public string idArtist { get; set; }

        [JsonPropertyName("strArtist")]
        public string strArtist { get; set; }
    }

    public class ArtistInfo
    {
        [JsonPropertyName("image")]
        public List<ImageInfo> Image { get; set; }

        [JsonPropertyName("tags")]
        public TagsInfo Tags { get; set; }

        [JsonPropertyName("bio")]
        public BioInfo Bio { get; set; }
    }

    public class ImageInfo
    {
        [JsonPropertyName("#text")]
        public string Url { get; set; }

        [JsonPropertyName("size")]
        public string Size { get; set; }
    }

    public class TagsInfo
    {
        [JsonPropertyName("tag")]
        public List<TagInfo> Tag { get; set; }
    }

    public class TagInfo
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }
    }

    public class BioInfo
    {
        [JsonPropertyName("summary")]
        public string Summary { get; set; }
    }

    public class LyricsLine : INotifyPropertyChanged
    {
        public TimeSpan Time { get; set; }
        public string Text { get; set; }

        private bool _isCurrentLine;
        public bool IsCurrentLine
        {
            get => _isCurrentLine;
            set
            {
                if (_isCurrentLine != value)
                {
                    _isCurrentLine = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    // ==================================================================
    // === ^^^^ ALL YOUR LIBRARY & API CLASSES ARE HERE ^^^^ ===
    // ==================================================================


    public partial class MainWindow : Window
    {

        private static AppSecrets _secrets;

        // PASTE YOUR LAST.FM API KEY HERE

        private static readonly HttpClient httpClient = new HttpClient();

        private static readonly SemaphoreSlim apiLimiter = new SemaphoreSlim(5, 5);

        private MediaPlayer mediaPlayer = new MediaPlayer();

        private DiscordRpcClient client;
        private RichPresence presence;

        private List<PlaylistItem> playlistFiles = new List<PlaylistItem>();
        private List<PlaylistItem> playbackQueue;
        private ICollectionView queueView;

        private ObservableCollection<PlaylistItem> playNextQueue;

        private int currentQueueIndex = -1;
        private bool isSeeking = false;
        private DispatcherTimer volumePopupTimer;
        private double lastVolume;
        private bool isMuted = false;
        private bool isShuffled = false;
        private RepeatMode repeatMode = RepeatMode.Off;
        private Random rng = new Random();

        private string currentArtTempPath = null;

        private bool notificationsEnabled;

        private bool isPinned = false;

        private string searchPlaceholder = "Search in queue";
        private bool isSearchPlaceholder = true;

        private bool playNextTrackInOrder = true;

        private MiniPlayerWindow miniPlayer = null;

        private HwndSource _hwndSource;

        private ObservableCollection<ArtistModel> libraryArtists = new ObservableCollection<ArtistModel>();
        private List<AlbumModel> libraryAlbums = new List<AlbumModel>();

        // This tracks our navigation state in the library
        private enum LibraryViewMode { Artists, Albums, Songs }
        private LibraryViewMode currentLibraryView = LibraryViewMode.Artists;
        private ArtistModel selectedArtist = null; // Remembers which artist we clicked on

        private bool _songIsAdvancing = false;

        private DispatcherTimer lyricsTimer;
        private ObservableCollection<LyricsLine> currentLyrics = new ObservableCollection<LyricsLine>();
        private int currentLyricIndex = -1;

        private bool isOnlineMode = false;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // 1. Get the current assembly version
            var currentVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;

            // 2. Get the last version the user saw (from the settings we made)
            string lastSeenVersionString = Properties.Settings.Default.LastSeenVersion;

            // 3. Try to parse the saved version string
            Version.TryParse(lastSeenVersionString, out Version lastSeenVersion);

            // 4. Compare!
            //    Show popup if the saved version is null (first run)
            //    OR if the current app version is newer.
            if (lastSeenVersion == null || currentVersion > lastSeenVersion)
            {
                // This is a new version! Show the popup.
                WhatsNewWindow popup = new WhatsNewWindow();
                popup.Owner = this;

                // --- CUSTOMIZE YOUR NEW FEATURES HERE ---
                // This is the only part you need to edit for future updates

                popup.VersionText.Text = $"Version {currentVersion.ToString(3)}"; // e.g., "Version 1.3.0"

                popup.FeaturesText.Text = "• Added Global Hotkeys for Play/Pause, Next, and Previous!\n  So you can use your keyboard shortcut.\n\n" +
                                          "• Save/Load Playlists From Queue.\n\n" +
                                          "• Mini-Player to Control Your Music.\n\n" +
                                          "• Library to Group an Artist/Album.\n\n" +
                                          "• Added Online Stream to Play Through Jamendo Music.\n\n"+
                                          "• Synced Lyrics Supported.\n\n";
                
                // --- END OF CUSTOMIZATION ---

                popup.ShowDialog(); // This stops the main window until "OK" is clicked

                // 5. After the user clicks "OK", save the *current* version to the settings.
                //    This stops the popup from appearing again.
                Properties.Settings.Default.LastSeenVersion = currentVersion.ToString();
                Properties.Settings.Default.Save();
            }

            LoadLibraryDataAsync();
        }

        public MainWindow()
        {
            InitializeComponent();

            try
            {
                string json = System.IO.File.ReadAllText("secrets.json");
                _secrets = JsonSerializer.Deserialize<AppSecrets>(json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"FATAL ERROR: Could not load 'secrets.json'.\n\nAPI features will be disabled.\n\nError: {ex.Message}", "Secrets File Missing", MessageBoxButton.OK, MessageBoxImage.Error);
                // Close the app if secrets are essential
                // Application.Current.Shutdown(); 
                // Or just continue with online features disabled
            }

            this.SourceInitialized += new EventHandler(OnSourceInitialized);

            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;

            client = new DiscordRpcClient(_secrets?.DiscordAppId ?? "1436651511974203393");

            // (Optional: Set up logging)
            client.Logger = new ConsoleLogger() { Level = LogLevel.Warning };

            client.Initialize();

            // Set the initial "Idle" presence
            presence = new RichPresence()
            {
                Type = ActivityType.Listening,
                Details = "Idle",
                State = "No music playing",
                Assets = new Assets()
                {
                    LargeImageKey = "logo", // <-- Your image key from Developer Portal
                    LargeImageText = "Simple Music Player"
                }
            };
            client.SetPresence(presence);

            DispatcherTimer timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;
            timer.Start();

            volumePopupTimer = new DispatcherTimer();
            volumePopupTimer.Interval = TimeSpan.FromSeconds(2);
            volumePopupTimer.Tick += VolumePopupTimer_Tick;

            double savedVolume = Properties.Settings.Default.LastVolume;
            mediaPlayer.Volume = savedVolume;
            VolumeSlider.Value = savedVolume;
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;

            repeatMode = (RepeatMode)Properties.Settings.Default.RepeatMode;
            UpdateRepeatButtonUI();

            notificationsEnabled = Properties.Settings.Default.NotificationsEnabled;
            NotificationToggleMenuItem.IsChecked = notificationsEnabled;

            playbackQueue = new List<PlaylistItem>();
            queueView = CollectionViewSource.GetDefaultView(playbackQueue);
            queueView.Filter = SearchFilter;
            PlaylistListBox.ItemsSource = queueView;

            playNextQueue = new ObservableCollection<PlaylistItem>();
            PriorityQueueListBox.ItemsSource = playNextQueue;

            ArtistGridView.ItemsSource = libraryArtists;

            LyricsListBox.ItemsSource = currentLyrics;

            lyricsTimer = new DispatcherTimer();
            lyricsTimer.Interval = TimeSpan.FromMilliseconds(100); // Check 10 times per second
            lyricsTimer.Tick += LyricsTimer_Tick;

            LoadLastFolder();
            LoadLastSong();

            lastVolume = (savedVolume > 0) ? savedVolume : 0.3;
            if (savedVolume == 0)
            {
                isMuted = true;
                SpeakerIcon.Text = " 🔇";
            }

            SearchBox.Text = searchPlaceholder;
            SearchBox.Foreground = Brushes.Gray;
            isSearchPlaceholder = true;

            PlayButton.ToolTip = "Play (Space)";
        }

        #region Load and Playback Logic

        private bool SearchFilter(object item)
        {
            if (isSearchPlaceholder || string.IsNullOrEmpty(SearchBox.Text))
                return true;

            PlaylistItem song = item as PlaylistItem;
            if (song == null) return false;

            string searchText = SearchBox.Text.ToLower();

            return song.FileName.ToLower().Contains(searchText) ||
                   song.Title.ToLower().Contains(searchText) ||
                   song.Artist.ToLower().Contains(searchText);
        }

        private void LoadLastFolder()
        {
            string folderPath = Properties.Settings.Default.LastFolderPath;
            if (!string.IsNullOrEmpty(folderPath) && Directory.Exists(folderPath))
            {
                LoadFilesFromFolder(folderPath);
            }
        }

        private void LoadLastSong()
        {
            string lastSongPath = Properties.Settings.Default.LastSongPath;
            if (string.IsNullOrEmpty(lastSongPath)) return;

            int index = playbackQueue.FindIndex(item => item.FilePath == lastSongPath);

            if (index != -1)
            {
                LoadTrack(index, false); // Cue up (don't play)
            }
        }

        private void LoadFilesFromFolder(string folderPath)
        {
            playlistFiles.Clear();
            playbackQueue.Clear();
            playNextQueue.Clear();

            PriorityQueueTitle.Visibility = Visibility.Collapsed;
            PriorityQueueListBox.Visibility = Visibility.Collapsed;

            var supportedExtensions = new[] { ".mp3", ".m4a" };

            try
            {
                var files = Directory.EnumerateFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                                     .Where(file => supportedExtensions.Any(file.ToLower().EndsWith))
                                     .OrderBy(file => file, new NaturalStringComparer());

                foreach (var file in files)
                {
                    string title = "";
                    string artist = "";
                    string album = "";
                    string fileName = Path.GetFileName(file);
                    uint trackNumber = 0;

                    try
                    {
                        var tagFile = TagLib.File.Create(file);
                        title = string.IsNullOrEmpty(tagFile.Tag.Title) ? fileName : tagFile.Tag.Title;
                        artist = string.IsNullOrEmpty(tagFile.Tag.FirstPerformer) ? "Unknown Artist" : tagFile.Tag.FirstPerformer;
                        album = string.IsNullOrEmpty(tagFile.Tag.Album) ? "Unknown Album" : tagFile.Tag.Album;
                        trackNumber = tagFile.Tag.Track;
                    }
                    catch (Exception)
                    {
                        title = fileName;
                        artist = "Unknown Artist";
                        album = "Unknown Album";
                        trackNumber = 0;
                    }

                    playlistFiles.Add(new PlaylistItem
                    {
                        FilePath = file,
                        FileName = fileName,
                        Title = title,
                        Artist = artist,
                        Album = album,
                        TrackNumber = (int)trackNumber,
                        IsNowPlaying = false
                    });
                }

                BuildPlaybackQueue();
                queueView.Refresh();

            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not read files: {ex.Message}");
            }
        }

        private void LoadFolderButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog();
            dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            dialog.Title = "Select Music Folder";
            dialog.FileName = "Folder Selection";
            dialog.ValidateNames = false;
            dialog.CheckFileExists = false;
            dialog.CheckPathExists = true;

            if (dialog.ShowDialog() == true)
            {
                string folderPath = Path.GetDirectoryName(dialog.FileName);
                LoadFilesFromFolder(folderPath);

                Properties.Settings.Default.LastFolderPath = folderPath;
                Properties.Settings.Default.Save();

                if (playbackQueue.Count > 0)
                {
                    PlayTrack(0);
                }

                // After loading a new folder, rebuild and reload the library
                LoadLibraryDataAsync();
            }
        }

        private void PlayTrack(int queueIndex)
        {
            LoadTrack(queueIndex, true);
        }

        private void LoadTrack(int queueIndex, bool play)
        {
            if (queueIndex < 0 || queueIndex >= playbackQueue.Count) return;

            if (currentQueueIndex >= 0 && currentQueueIndex < playbackQueue.Count)
            {
                playbackQueue[currentQueueIndex].IsNowPlaying = false;
            }
            currentQueueIndex = queueIndex;
            var currentItem = playbackQueue[currentQueueIndex];
            currentItem.IsNowPlaying = true;

            string filePath = currentItem.FilePath;
            mediaPlayer.Open(new Uri(filePath));

            PlaylistListBox.SelectedItem = currentItem;
            PlaylistListBox.ScrollIntoView(PlaylistListBox.SelectedItem);
            SeekBar.IsEnabled = true;
            CurrentTimeText.Text = "00:00";
            RemainingTimeText.Text = "-00:00";

            if (QueueViewGrid.Visibility == Visibility.Visible || currentLibraryView == LibraryViewMode.Artists)
            {
                SongTitleText.Text = currentItem.Title;
                ArtistAlbumText.Text = $"{currentItem.Artist} - {currentItem.Album}";
                SongTitleText.ToolTip = currentItem.Title;
                ArtistAlbumText.ToolTip = $"{currentItem.Artist} - {currentItem.Album}";

                LoadAlbumArt(filePath);
            }
            UpdateNextSongText(currentQueueIndex);

            Properties.Settings.Default.LastSongPath = filePath;
            Properties.Settings.Default.Save();

            if (play)
            {
                mediaPlayer.Play();
                PlayButton.Content = "❚❚";
                PlayButton.ToolTip = "Pause (Space)";
            }

            if (miniPlayer != null)
            {
                BitmapImage art = AlbumArtImage.Source as BitmapImage;
                miniPlayer.UpdateSongInfo(currentItem.Title, $"{currentItem.Artist} - {currentItem.Album}", art);

                if (play)
                    miniPlayer.UpdatePlayButtonState("❚❚");
            }
        }

        private void LoadAlbumArt(string filePath)
        {
            try
            {
                var tagFile = TagLib.File.Create(filePath);
                if (tagFile.Tag.Pictures.Length > 0 && tagFile.Tag.Pictures[0].Data.Data != null)
                {
                    var picture = tagFile.Tag.Pictures[0];
                    var artBytes = picture.Data.Data;

                    try
                    {
                        string tempPath = Path.Combine(Path.GetTempPath(), "dummy_player_art.jpg");
                        System.IO.File.WriteAllBytes(tempPath, artBytes);
                        currentArtTempPath = tempPath;
                    }
                    catch { currentArtTempPath = null; }

                    using (var stream = new MemoryStream(artBytes))
                    {
                        stream.Position = 0;
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.StreamSource = stream;
                        bitmap.EndInit();
                        bitmap.Freeze();

                        AlbumArtImage.Source = bitmap;
                        BlurredBackgroundImage.Source = bitmap;
                    }
                }
                else
                {
                    AlbumArtImage.Source = null;
                    BlurredBackgroundImage.Source = null;
                    currentArtTempPath = null;
                }
            }
            catch (Exception)
            {
                AlbumArtImage.Source = null;
                BlurredBackgroundImage.Source = null;
                currentArtTempPath = null;
            }
        }

        private void UpdateNextSongText(int queueIndex)
        {
            if (playNextQueue.Count > 0)
            {
                var nextSong = playNextQueue[0];
                NextSongText.Text = $"Next (Queue): {nextSong.Artist} - {nextSong.Title}";
            }
            else if (!playNextTrackInOrder && playbackQueue.Count > 0)
            {
                var nextSong = playbackQueue[0];
                NextSongText.Text = $"Next: {nextSong.Artist} - {nextSong.Title}";
            }
            else if (queueIndex + 1 < playbackQueue.Count)
            {
                var nextSong = playbackQueue[queueIndex + 1];
                NextSongText.Text = $"Next: {nextSong.Artist} - {nextSong.Title}";
            }
            else if (repeatMode == RepeatMode.All && playbackQueue.Count > 0)
            {
                var nextSong = playbackQueue[0];
                NextSongText.Text = $"Next: {nextSong.Artist} - {nextSong.Title}";
            }
            else
            {
                NextSongText.Text = "Next: End of queue";
            }
        }

        #endregion

        #region Playback Controls & Search

        private void PlaylistListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PlaylistItem selectedItem = PlaylistListBox.SelectedItem as PlaylistItem;
            if (selectedItem == null) return;

            int newQueueIndex = playbackQueue.IndexOf(selectedItem);

            if (newQueueIndex != -1 && newQueueIndex != currentQueueIndex)
            {
                playNextTrackInOrder = false;
                PlayTrack(newQueueIndex);
            }
        }

        private void PriorityQueueListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            PlaylistItem selectedItem = PriorityQueueListBox.SelectedItem as PlaylistItem;
            if (selectedItem == null) return;

            playNextTrackInOrder = false;

            PlaylistItem justSkippedSong = (currentQueueIndex >= 0 && currentQueueIndex < playbackQueue.Count)
                                             ? playbackQueue[currentQueueIndex]
                                             : null;


            int clickedIndex = playNextQueue.IndexOf(selectedItem);
            if (clickedIndex == -1) return;

            List<PlaylistItem> skippedSongs = new List<PlaylistItem>();
            for (int i = 0; i < clickedIndex; i++)
            {
                skippedSongs.Add(playNextQueue[i]);
            }

            foreach (var song in skippedSongs)
            {
                song.IsInPriorityQueue = false;
            }

            var songsToKeep = playNextQueue.Skip(clickedIndex + 1).ToList();
            playNextQueue.Clear();
            foreach (var song in songsToKeep)
            {
                playNextQueue.Add(song);
            }

            selectedItem.IsInPriorityQueue = false;

            RenumberPriorityQueue();

            if (playNextQueue.Count == 0)
            {
                PriorityQueueTitle.Visibility = Visibility.Collapsed;
                PriorityQueueListBox.Visibility = Visibility.Collapsed;
            }

            int newQueueIndex = playbackQueue.IndexOf(selectedItem);

            if (newQueueIndex != -1)
            {
                PlayTrack(newQueueIndex);
            }
            else
            {
                int insertIndex = (currentQueueIndex >= 0) ? currentQueueIndex + 1 : 0;
                playbackQueue.Insert(insertIndex, selectedItem);
                RenumberPlaybackQueue();
                queueView.Refresh();
                PlayTrack(insertIndex);
            }

            HandleSongCompletion(justSkippedSong, selectedItem);
        }

        private void PlayNext_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button button = sender as System.Windows.Controls.Button;
            if (button == null) return;

            PlaylistItem song = button.DataContext as PlaylistItem;
            if (song == null) return;

            if (song.IsNowPlaying)
            {
                return;
            }

            if (!playNextQueue.Contains(song))
            {
                playNextQueue.Add(song);
                RenumberPriorityQueue();
                song.IsInPriorityQueue = true;
            }

            PriorityQueueTitle.Visibility = Visibility.Visible;
            PriorityQueueListBox.Visibility = Visibility.Visible;

            UpdateNextSongText(currentQueueIndex);
        }

        private void RemoveFromPriority_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button button = sender as System.Windows.Controls.Button;
            if (button == null) return;

            PlaylistItem song = button.DataContext as PlaylistItem;
            if (song == null) return;

            song.IsInPriorityQueue = false;

            playNextQueue.Remove(song);
            RenumberPriorityQueue();

            if (playNextQueue.Count == 0)
            {
                PriorityQueueTitle.Visibility = Visibility.Collapsed;
                PriorityQueueListBox.Visibility = Visibility.Collapsed;
            }

            UpdateNextSongText(currentQueueIndex);
        }

        private void SearchBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (isSearchPlaceholder)
            {
                SearchBox.Text = "";
                SearchBox.Foreground = SystemColors.WindowTextBrush;
                isSearchPlaceholder = false;
            }
        }

        private void SearchBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(SearchBox.Text))
            {
                SearchBox.Text = searchPlaceholder;
                SearchBox.Foreground = Brushes.Gray;
                isSearchPlaceholder = true;

                queueView.Refresh();
                PlaylistListBox.SelectedItem = (currentQueueIndex >= 0) ? playbackQueue[currentQueueIndex] : null;
                if (PlaylistListBox.SelectedItem != null)
                {
                    PlaylistListBox.ScrollIntoView(PlaylistListBox.SelectedItem);
                }
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (queueView != null)
            {
                queueView.Refresh();
            }
        }

        public void TogglePlayPause()
        {
            if (isOnlineMode)
            {
                OnlineContentGrid.TogglePlayPause();
                // Update mini player's button
                if (miniPlayer != null)
                    miniPlayer.UpdatePlayButtonState(OnlineContentGrid.PlayButton.Content.ToString());
            }
            else
            {
                PlayButton_Click(this, null); // Fire the original offline method
                                              // Update mini player's button
                if (miniPlayer != null)
                    miniPlayer.UpdatePlayButtonState(PlayButton.Content.ToString());
            }
        }

        public void PlayNext()
        {
            if (isOnlineMode)
            {
                OnlineContentGrid.PlayNext();
            }
            else
            {
                NextButton_Click(this, null);
            }
        }

        public void PlayPrevious()
        {
            if (isOnlineMode)
            {
                OnlineContentGrid.PlayPrevious();
            }
            else
            {
                PrevButton_Click(this, null);
            }
        }


        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (mediaPlayer.Source == null && playbackQueue.Count > 0)
            {
                PlayTrack(0);
                return;
            }

            PlaylistItem currentItem = null;
            if (currentQueueIndex >= 0 && currentQueueIndex < playbackQueue.Count)
            {
                currentItem = playbackQueue[currentQueueIndex];
            }

            if (PlayButton.Content.ToString() == "▶")
            {
                mediaPlayer.Play();
                lyricsTimer.Start();
                PlayButton.Content = "❚❚";
                PlayButton.ToolTip = "Pause (Space)";

                TimeSpan currentPosition = mediaPlayer.Position;
                DateTime fakeStartTime = DateTime.UtcNow - currentPosition;
                presence.Timestamps = new Timestamps() { Start = fakeStartTime };

                if (currentItem != null)
                {
                    presence.State = $"by {currentItem.Artist}";
                }

                presence.Assets.LargeImageText = "";
                presence.Assets.SmallImageKey = "play_icon";
                presence.Assets.SmallImageText = "Playing";

                client.SetPresence(presence);

                if (miniPlayer != null)
                    miniPlayer.UpdatePlayButtonState("❚❚");
            }
            else
            {
                mediaPlayer.Pause();
                lyricsTimer.Stop();
                PlayButton.Content = "▶";
                PlayButton.ToolTip = "Play (Space)";

                presence.Timestamps = null;

                if (currentItem != null)
                {
                    presence.State = $"by {currentItem.Artist}";
                }

                presence.Assets.LargeImageText = "Paused";
                presence.Assets.SmallImageKey = "pause_icon";
                presence.Assets.SmallImageText = "Paused";

                client.SetPresence(presence);

                if (miniPlayer != null)
                    miniPlayer.UpdatePlayButtonState("▶");
            }
        }
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_songIsAdvancing) return;
            _songIsAdvancing = true;

            PlaylistItem justSkippedSong = (currentQueueIndex >= 0 && currentQueueIndex < playbackQueue.Count)
                                           ? playbackQueue[currentQueueIndex]
                                           : null;

            PlayNextSongInQueue();

            PlaylistItem newSong = (currentQueueIndex >= 0 && currentQueueIndex < playbackQueue.Count)
                                         ? playbackQueue[currentQueueIndex]
                                         : null;

            HandleSongCompletion(justSkippedSong, newSong);
        }

        private void MediaPlayer_MediaEnded(object sender, EventArgs e)
        {
            if (_songIsAdvancing) return; // We are already handling a "next" command
            _songIsAdvancing = true; // Set the lock

            PlaylistItem justFinishedSong = (currentQueueIndex >= 0 && currentQueueIndex < playbackQueue.Count)
                                                  ? playbackQueue[currentQueueIndex]
                                                  : null;

            if (repeatMode == RepeatMode.One)
            {
                // Rewind to the start instead of stopping
                mediaPlayer.Position = TimeSpan.Zero;
                mediaPlayer.Play();

                // Manually reset the Discord timestamp...
                if (presence != null)
                {
                    presence.Timestamps = Timestamps.Now;
                    client.SetPresence(presence);
                }
                _songIsAdvancing = false; // Release the lock
            }
            else
            {
                lyricsTimer.Stop();
                PlayNextSongInQueue();

                PlaylistItem newSong = (currentQueueIndex >= 0 && currentQueueIndex < playbackQueue.Count)
                                             ? playbackQueue[currentQueueIndex]
                                             : null;

                HandleSongCompletion(justFinishedSong, newSong);
            }
        }

        private void HandleSongCompletion(PlaylistItem songToProcess, PlaylistItem newlyPlayingSong)
        {
            if (songToProcess == null || songToProcess == newlyPlayingSong)
            {
                return;
            }

            if (repeatMode == RepeatMode.All)
            {
                playbackQueue.Remove(songToProcess);
                playbackQueue.Add(songToProcess);
            }
            else if (repeatMode == RepeatMode.Off)
            {
                playbackQueue.Remove(songToProcess);
            }

            RenumberPlaybackQueue();

            queueView.Refresh();

            currentQueueIndex = (newlyPlayingSong != null) ? playbackQueue.IndexOf(newlyPlayingSong) : -1;
        }

        private void PlayNextSongInQueue()
        {
            if (playbackQueue.Count == 0 && playNextQueue.Count == 0) return;

            if (playNextQueue.Count > 0)
            {
                PlaylistItem nextSong = playNextQueue[0];
                nextSong.IsInPriorityQueue = false;
                playNextQueue.RemoveAt(0);
                RenumberPriorityQueue();

                if (playNextQueue.Count == 0)
                {
                    PriorityQueueTitle.Visibility = Visibility.Collapsed;
                    PriorityQueueListBox.Visibility = Visibility.Collapsed;
                }

                playNextTrackInOrder = true;

                int oldIndex = playbackQueue.IndexOf(nextSong);

                if (oldIndex != -1)
                {
                    playbackQueue.RemoveAt(oldIndex);
                }

                playbackQueue.Insert(0, nextSong);

                RenumberPlaybackQueue();
                queueView.Refresh();

                PlayTrack(0);
                return;
            }

            int nextIndex = -1;

            if (playNextTrackInOrder)
            {
                if (currentQueueIndex < playbackQueue.Count - 1)
                {
                    nextIndex = currentQueueIndex + 1;
                }
            }
            else
            {
                if (playbackQueue.Count > 0)
                {
                    nextIndex = 0;
                }

                playNextTrackInOrder = true;
            }

            if (nextIndex != -1)
            {
                PlayTrack(nextIndex);
                return;
            }

            if (repeatMode == RepeatMode.All)
            {
                if (playbackQueue.Count > 0)
                {
                    PlayTrack(0);
                    return;
                }
            }
            _songIsAdvancing = false;

            mediaPlayer.Stop();
            lyricsTimer.Stop();
            PlayButton.Content = "▶";
            PlayButton.ToolTip = "Play (Space)";
            SeekBar.Value = 0;
            CurrentTimeText.Text = "00:00";
            UpdateNextSongText(currentQueueIndex);

            presence.Type = ActivityType.Listening;
            presence.Details = "Idle";
            presence.State = "No music playing";
            presence.Timestamps = null;
            presence.Assets = new Assets()
            {
                LargeImageKey = "logo",
                SmallImageKey = "pause_icon",
                SmallImageText = "Idle"
            };
            client.SetPresence(presence);

            if (currentQueueIndex >= 0 && currentQueueIndex < playbackQueue.Count)
                playbackQueue[currentQueueIndex].IsNowPlaying = false;
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            playNextTrackInOrder = true;

            if (currentQueueIndex > 0)
            {
                PlayTrack(currentQueueIndex - 1);
            }
            else if (currentQueueIndex == 0 && repeatMode == RepeatMode.All && playbackQueue.Count > 1)
            {
                PlaylistItem oldSong = playbackQueue[currentQueueIndex];
                PlaylistItem songToPlay = playbackQueue[playbackQueue.Count - 1];
                playbackQueue.RemoveAt(playbackQueue.Count - 1);
                playbackQueue.Insert(0, songToPlay);
                RenumberPlaybackQueue();
                queueView.Refresh();
                PlayTrack(0);
                oldSong.IsNowPlaying = false;
            }
            else if (mediaPlayer.Source != null)
            {
                mediaPlayer.Stop();
                mediaPlayer.Play();
            }
        }

        #endregion

        #region Shuffle & Repeat Buttons

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            isShuffled = !isShuffled;

            if (isShuffled)
            {
                ShuffleButton.Background = SystemColors.HighlightBrush;
                ShuffleButton.ToolTip = "Shuffle On";
            }
            else
            {
                ShuffleButton.Background = SystemColors.ControlBrush;
                ShuffleButton.ToolTip = "Shuffle Off";
            }

            PlaylistItem currentSong = (currentQueueIndex >= 0 && currentQueueIndex < playbackQueue.Count)
                                           ? playbackQueue[currentQueueIndex]
                                           : null;

            BuildPlaybackQueue(currentSong);

            if (currentSong != null)
            {
                currentQueueIndex = playbackQueue.IndexOf(currentSong);
            }
            else if (playbackQueue.Count > 0)
            {
                currentQueueIndex = 0;
            }
            else
            {
                currentQueueIndex = -1;
            }

            queueView.Refresh();

            PlaylistListBox.SelectedItem = (currentQueueIndex >= 0) ? playbackQueue[currentQueueIndex] : null;

            if (PlaylistListBox.SelectedItem != null)
            {
                Application.Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.ContextIdle,
                    new Action(() =>
                    {
                        if (PlaylistListBox.SelectedItem != null)
                        {
                            PlaylistListBox.ScrollIntoView(PlaylistListBox.SelectedItem);
                        }
                    }));
            }

            UpdateNextSongText(currentQueueIndex);
        }
        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            int nextMode = ((int)repeatMode + 1) % 3;
            repeatMode = (RepeatMode)nextMode;

            Properties.Settings.Default.RepeatMode = (int)repeatMode;
            Properties.Settings.Default.Save();

            UpdateRepeatButtonUI();
        }

        private void UpdateRepeatButtonUI()
        {
            switch (repeatMode)
            {
                case RepeatMode.Off:
                    RepeatButton.Content = "🔁";
                    RepeatButton.Background = SystemColors.ControlBrush;
                    RepeatButton.ToolTip = "Repeat Off";
                    break;
                case RepeatMode.All:
                    RepeatButton.Content = "🔁";
                    RepeatButton.Background = SystemColors.HighlightBrush;
                    RepeatButton.ToolTip = "Repeat All";
                    break;
                case RepeatMode.One:
                    RepeatButton.Content = "🔁¹";
                    RepeatButton.Background = SystemColors.HighlightBrush;
                    RepeatButton.ToolTip = "Repeat One";
                    break;
            }
        }

        private void BuildPlaybackQueue(PlaylistItem currentSong = null)
        {
            playbackQueue.Clear();

            if (isShuffled)
            {
                var shuffledList = new List<PlaylistItem>(playlistFiles);

                if (currentSong != null)
                {
                    shuffledList.Remove(currentSong);
                }

                int n = shuffledList.Count;
                while (n > 1)
                {
                    n--;
                    int k = rng.Next(n + 1);
                    (shuffledList[k], shuffledList[n]) = (shuffledList[n], shuffledList[k]);
                }

                if (currentSong != null)
                {
                    playbackQueue.Add(currentSong);
                }

                shuffledList.ForEach(item => playbackQueue.Add(item));
            }
            else
            {
                playlistFiles.ForEach(item => playbackQueue.Add(item));
            }

            RenumberPlaybackQueue();
        }

        private void RenumberPlaybackQueue()
        {
            for (int i = 0; i < playbackQueue.Count; i++)
            {
                playbackQueue[i].TrackNumber = i + 1;
            }
        }

        #endregion

        #region Volume, Seek, & Background Click

        private void MainGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is System.Windows.Controls.Primitives.Thumb ||
              e.OriginalSource is System.Windows.Controls.Primitives.RepeatButton ||
              e.OriginalSource is Slider)
            {
                return;
            }

            MainGrid.Focus();
        }

        private void VolumePopupTimer_Tick(object sender, EventArgs e)
        {
            VolumePopupText.Visibility = Visibility.Collapsed;
            volumePopupTimer.Stop();
        }



        private void SpeakerIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isMuted)
            {
                VolumeSlider.Value = (lastVolume > 0) ? lastVolume : 0.3;
            }
            else
            {
                VolumeSlider.Value = 0;
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VolumePopupText == null) return;
            if (mediaPlayer == null) return;

            double volume = VolumeSlider.Value;
            mediaPlayer.Volume = volume;

            Properties.Settings.Default.LastVolume = volume;
            Properties.Settings.Default.Save();

            int percentage = (int)(volume * 100);
            VolumePopupText.Text = $"{percentage}%";
            VolumePopupText.Visibility = Visibility.Visible;

            volumePopupTimer.Stop();
            volumePopupTimer.Start();

            if (volume == 0)
            {
                SpeakerIcon.Text = " 🔇";
                isMuted = true;
            }
            else
            {
                SpeakerIcon.Text = " 🔊";
                isMuted = false;
                lastVolume = volume;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (mediaPlayer.Source != null && mediaPlayer.NaturalDuration.HasTimeSpan && !isSeeking)
            {
                TimeSpan total = mediaPlayer.NaturalDuration.TimeSpan;
                TimeSpan current = mediaPlayer.Position;
                TimeSpan remaining = total - current;
                SeekBar.Maximum = total.TotalSeconds;
                SeekBar.ValueChanged -= SeekBar_ValueChanged;
                SeekBar.Value = current.TotalSeconds;  
                SeekBar.ValueChanged += SeekBar_ValueChanged;
                CurrentTimeText.Text = current.ToString(@"mm\:ss");
                RemainingTimeText.Text = "-" + remaining.ToString(@"mm\:ss");
            }
        }

        private void SeekBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // This check is CRITICAL. 
            // It only allows a 'drag' to update the position here.
            if (mediaPlayer.Source != null && isSeeking)
            {
                mediaPlayer.Position = TimeSpan.FromSeconds(SeekBar.Value);

                // Update Discord during the drag
                bool isPlaying = (PlayButton.Content.ToString() == "❚❚");
                if (isPlaying && presence != null)
                {
                    TimeSpan newPosition = mediaPlayer.Position;
                    DateTime fakeStartTime = DateTime.UtcNow - newPosition;
                    presence.Timestamps = new Timestamps() { Start = fakeStartTime };
                    client.SetPresence(presence);
                }
            }
        }
        private void SeekBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (mediaPlayer.Source != null)
            {
                isSeeking = true;
                mediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
            }
        }

        private void SeekBar_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            bool wasDragging = isSeeking; // Store the state

            if (wasDragging)
            {
                // --- This was the END of a DRAG ---
                isSeeking = false;
                mediaPlayer.MediaEnded += MediaPlayer_MediaEnded; // Re-subscribe
            }
            else
            {
                // --- This was a SINGLE-CLICK (MoveToPoint) ---
                // We must manually set the position, since ValueChanged was skipped
                if (mediaPlayer.Source != null)
                {
                    mediaPlayer.Position = TimeSpan.FromSeconds(SeekBar.Value);

                    // And update discord
                    bool isPlaying = (PlayButton.Content.ToString() == "❚❚");
                    if (isPlaying && presence != null)
                    {
                        TimeSpan newPosition = mediaPlayer.Position;
                        DateTime fakeStartTime = DateTime.UtcNow - newPosition;
                        presence.Timestamps = new Timestamps() { Start = fakeStartTime };
                        client.SetPresence(presence);
                    }
                }
            }

            // --- This logic now runs for BOTH a DRAG-to-end and a CLICK-to-end ---
            if (mediaPlayer.Source != null &&
                mediaPlayer.NaturalDuration.HasTimeSpan &&
                mediaPlayer.Position >= mediaPlayer.NaturalDuration.TimeSpan)
            {
                // We are at the end, so manually call the "MediaEnded" logic
                MediaPlayer_MediaEnded(this, null);
            }
        }

        private void RenumberPriorityQueue()
        {
            for (int i = 0; i < playNextQueue.Count; i++)
            {
                playNextQueue[i].PriorityNumber = i + 1;
            }
        }

        #endregion

        #region Window Events & Menu Items

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (miniPlayer != null)
            {
                miniPlayer.Close();
            }

            client.Deinitialize();
            client.Dispose();

            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            NativeMethods.UnregisterHotKey(windowHandle, NativeMethods.HOTKEY_ID_PLAY_PAUSE);
            NativeMethods.UnregisterHotKey(windowHandle, NativeMethods.HOTKEY_ID_NEXT);
            NativeMethods.UnregisterHotKey(windowHandle, NativeMethods.HOTKEY_ID_PREV);

            _hwndSource.RemoveHook(WndProc);
            _hwndSource.Dispose();
        }
        private void MediaPlayer_MediaOpened(object sender, EventArgs e)
        {
            _songIsAdvancing = false;

            if (currentQueueIndex < 0 || currentQueueIndex >= playbackQueue.Count)
                return;

            var currentItem = playbackQueue[currentQueueIndex];

            try
            {
                var tagFile = TagLib.File.Create(currentItem.FilePath);
                if (!string.IsNullOrEmpty(tagFile.Tag.Lyrics))
                {
                    ParseLyrics(tagFile.Tag.Lyrics);
                }
                else
                {
                    ParseLyrics("[00:00.00]No lyrics found for this song.");
                }
            }
            catch (Exception)
            {
                ParseLyrics("[00:00.00]Error loading lyrics.");
            }

            lyricsTimer.Start();

            if (mediaPlayer.NaturalDuration.HasTimeSpan)
            {
                presence.Type = ActivityType.Listening;
                presence.Details = currentItem.Title;
                presence.State = $"by {currentItem.Artist}";

                presence.Timestamps = Timestamps.Now;

                presence.Assets = new Assets()
                {
                    LargeImageKey = "logo",
                    LargeImageText = "",
                    SmallImageKey = "play_icon",
                    SmallImageText = "Playing"
                };

                client.SetPresence(presence);

                if (notificationsEnabled)
                {
                    var toastBuilder = new ToastContentBuilder()
                    .AddText(currentItem.Title)
                    .AddText($"by {currentItem.Artist}");

                    if (!string.IsNullOrEmpty(currentArtTempPath) && System.IO.File.Exists(currentArtTempPath))
                    {
                        toastBuilder.AddAppLogoOverride(new Uri(currentArtTempPath), ToastGenericAppLogoCrop.Circle);
                    }
                    toastBuilder.Show();
                }
            }
        }

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            isPinned = !isPinned;
            this.Topmost = isPinned;

            if (isPinned)
            {
                PinButton.Background = SystemColors.HighlightBrush;
                PinButton.ToolTip = "Pin to Top (On)";
            }
            else
            {
                PinButton.Background = SystemColors.ControlBrush;
                PinButton.ToolTip = "Pin to Top (Off)";
            }
        }

        private void NotificationToggle_Click(object sender, RoutedEventArgs e)
        {
            notificationsEnabled = NotificationToggleMenuItem.IsChecked;
            Properties.Settings.Default.NotificationsEnabled = notificationsEnabled;
            Properties.Settings.Default.Save();
        }



        private void MainGrid_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void MainGrid_Drop(object sender, DragEventArgs e)
        {
            e.Handled = true;

            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] paths = (string[])e.Data.GetData(DataFormats.FileDrop);

                if (paths != null && paths.Length > 0)
                {
                    string path = paths[0];
                    string folderPath;

                    if (System.IO.File.Exists(path))
                    {
                        folderPath = System.IO.Path.GetDirectoryName(path);
                    }
                    else if (System.IO.Directory.Exists(path))
                    {
                        folderPath = path;
                    }
                    else
                    {
                        return;
                    }

                    LoadFilesFromFolder(folderPath);

                    Properties.Settings.Default.LastFolderPath = folderPath;
                    Properties.Settings.Default.Save();

                    if (playbackQueue.Count > 0)
                    {
                        PlayTrack(0);
                    }

                    // After loading a new folder, rebuild and reload the library
                    LoadLibraryDataAsync();
                }
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
            {
                // Check if focus is on a textbox
                if (e.OriginalSource is System.Windows.Controls.TextBox searchBox)
                {
                    // Let the spacebar work normally in textboxes
                    return;
                }

                e.Handled = true;

                // NEW LOGIC: Control the active player
                if (isOnlineMode)
                {
                    OnlineContentGrid.TogglePlayPause();
                }
                else
                {
                    PlayButton_Click(this, null);
                }
            }
        }

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.Owner = this;
            aboutWindow.ShowDialog();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            IntPtr windowHandle = new WindowInteropHelper(this).Handle;
            _hwndSource = HwndSource.FromHwnd(windowHandle);
            _hwndSource.AddHook(WndProc);

            NativeMethods.RegisterHotKey(windowHandle, NativeMethods.HOTKEY_ID_PLAY_PAUSE, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_MEDIA_PLAY_PAUSE);
            NativeMethods.RegisterHotKey(windowHandle, NativeMethods.HOTKEY_ID_NEXT, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_MEDIA_NEXT_TRACK);
            NativeMethods.RegisterHotKey(windowHandle, NativeMethods.HOTKEY_ID_PREV, NativeMethods.MOD_NOREPEAT, NativeMethods.VK_MEDIA_PREV_TRACK);
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            // --- 1. Global Hotkeys (like from your keyboard media row) ---
            if (msg == NativeMethods.WM_HOTKEY)
            {
                int hotkeyId = (int)wParam;
                switch (hotkeyId)
                {
                    case NativeMethods.HOTKEY_ID_PLAY_PAUSE:
                        if (isOnlineMode) OnlineContentGrid.TogglePlayPause();
                        else PlayButton_Click(this, null);
                        handled = true;
                        break;

                    case NativeMethods.HOTKEY_ID_NEXT:
                        if (isOnlineMode) OnlineContentGrid.PlayNext();
                        else NextButton_Click(this, null);
                        handled = true;
                        break;

                    case NativeMethods.HOTKEY_ID_PREV:
                        if (isOnlineMode) OnlineContentGrid.PlayPrevious();
                        else PrevButton_Click(this, null);
                        handled = true;
                        break;
                }
            }

            // --- 2. System Media Commands (like from the Windows 10/11 volume flyout) ---
            if (msg == NativeMethods.WM_APPCOMMAND)
            {
                int command = (((int)lParam >> 16) & 0xFFFF);
                switch (command)
                {
                    case NativeMethods.APPCOMMAND_MEDIA_PLAY_PAUSE:
                        if (isOnlineMode) OnlineContentGrid.TogglePlayPause();
                        else PlayButton_Click(this, null);
                        handled = true;
                        break;

                    case NativeMethods.APPCOMMAND_MEDIA_PLAY:
                        if (isOnlineMode) OnlineContentGrid.EnsurePlaying();
                        else if (PlayButton.Content.ToString() == "▶") PlayButton_Click(this, null);
                        handled = true;
                        break;

                    case NativeMethods.APPCOMMAND_MEDIA_PAUSE:
                        if (isOnlineMode) OnlineContentGrid.EnsurePaused();
                        else if (PlayButton.Content.ToString() == "❚❚") PlayButton_Click(this, null);
                        handled = true;
                        break;

                    case NativeMethods.APPCOMMAND_MEDIA_NEXTTRACK:
                        if (isOnlineMode) OnlineContentGrid.PlayNext();
                        else NextButton_Click(this, null);
                        handled = true;
                        break;

                    case NativeMethods.APPCOMMAND_MEDIA_PREVIOUSTRACK:
                        if (isOnlineMode) OnlineContentGrid.PlayPrevious();
                        else PrevButton_Click(this, null);
                        handled = true;
                        break;
                }
            }

            return IntPtr.Zero;
        }


        //THIS METHOD FOR DEBUGGING AND DEVELOPMENT PURPOSE
        private void ResetWhatsNew_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.LastSeenVersion = null;
            Properties.Settings.Default.Save();
            MessageBox.Show(
                "The 'What's New' popup has been reset and will appear the next time you open the application.",
                "Reset Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void SaveQueue_Click(object sender, RoutedEventArgs e)
        {
            if (playbackQueue.Count == 0)
            {
                MessageBox.Show("There is nothing in the queue to save.", "Queue Empty", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string playlistsFolder = System.IO.Path.Combine(baseDirectory, "Playlists");

            if (!System.IO.Directory.Exists(playlistsFolder))
            {
                System.IO.Directory.CreateDirectory(playlistsFolder);
            }

            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.Filter = "M3U Playlist (*.m3u)|*.m3u|Text File (*.txt)|*.txt";
            saveDialog.DefaultExt = ".m3u";
            saveDialog.Title = "Save Queue as Playlist";
            saveDialog.InitialDirectory = playlistsFolder;

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    var filePaths = playbackQueue.Select(item => item.FilePath);
                    System.IO.File.WriteAllLines(saveDialog.FileName, filePaths);
                    MessageBox.Show($"Playlist saved successfully to:\n{saveDialog.FileName}", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not save playlist: {ex.Message}", "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        private void LoadQueue_Click(object sender, RoutedEventArgs e)
        {
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string playlistsFolder = System.IO.Path.Combine(baseDirectory, "Playlists");

            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.Filter = "Playlist Files (*.m3u;*.txt)|*.m3u;*.txt|All Files (*.*)|*.*";
            openDialog.Title = "Load Playlist";

            if (System.IO.Directory.Exists(playlistsFolder))
            {
                openDialog.InitialDirectory = playlistsFolder;
            }

            if (openDialog.ShowDialog() == true)
            {
                try
                {
                    string[] filePaths = System.IO.File.ReadAllLines(openDialog.FileName);

                    playlistFiles.Clear();
                    playbackQueue.Clear();
                    playNextQueue.Clear();
                    PriorityQueueTitle.Visibility = Visibility.Collapsed;
                    PriorityQueueListBox.Visibility = Visibility.Collapsed;
                    currentQueueIndex = -1;

                    foreach (var file in filePaths)
                    {
                        if (System.IO.File.Exists(file))
                        {
                            string title = "";
                            string artist = "";
                            string album = "";
                            string fileName = System.IO.Path.GetFileName(file);
                            uint trackNumber = 0;

                            try
                            {
                                var tagFile = TagLib.File.Create(file);
                                title = string.IsNullOrEmpty(tagFile.Tag.Title) ? fileName : tagFile.Tag.Title;
                                artist = string.IsNullOrEmpty(tagFile.Tag.FirstPerformer) ? "Unknown Artist" : tagFile.Tag.FirstPerformer;
                                album = string.IsNullOrEmpty(tagFile.Tag.Album) ? "Unknown Album" : tagFile.Tag.Album;
                                trackNumber = tagFile.Tag.Track;
                            }
                            catch { /* Use defaults if TagLib fails */ }

                            var playlistItem = new PlaylistItem
                            {
                                FilePath = file,
                                FileName = fileName,
                                Title = title,
                                Artist = artist,
                                Album = album,
                                TrackNumber = (int)trackNumber,
                                IsNowPlaying = false
                            };

                            playlistFiles.Add(playlistItem);
                            playbackQueue.Add(playlistItem);
                        }
                    }

                    RenumberPlaybackQueue();
                    queueView.Refresh();

                    if (playbackQueue.Count > 0)
                    {
                        isShuffled = false;
                        ShuffleButton.Background = SystemColors.ControlBrush;
                        ShuffleButton.ToolTip = "Shuffle Off";

                        PlayTrack(0);
                    }

                    // After loading a new playlist, rebuild and reload the library
                    LoadLibraryDataAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not load playlist: {ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        #endregion

        #region Mini-Player Methods

        // REPLACE your old MiniPlayerButton_Click with this one
        private void MiniPlayerButton_Click(object sender, RoutedEventArgs e)
        {
            if (miniPlayer != null)
            {
                miniPlayer.Activate();
                return;
            }

            miniPlayer = new MiniPlayerWindow(this);
            miniPlayer.Left = this.Left + (this.Width - miniPlayer.Width) / 2;
            miniPlayer.Top = this.Top + (this.Height - miniPlayer.Height) / 2;
            miniPlayer.Show();

            if (isOnlineMode)
            {
                // Get data from the ONLINE player
                var currentItem = OnlineContentGrid.CurrentItem;
                if (currentItem != null)
                {
                    miniPlayer.UpdateSongInfo(currentItem.Name, $"{currentItem.Artist} - {currentItem.Album}", currentItem.AlbumArt);
                    miniPlayer.UpdatePlayButtonState(OnlineContentGrid.PlayButton.Content.ToString());
                }
                else
                {
                    miniPlayer.UpdateSongInfo("Online Mode", "No song selected", null);
                    miniPlayer.UpdatePlayButtonState("▶");
                }
            }
            else
            {
                // Get data from the OFFLINE player (your old logic)
                BitmapImage currentArt = AlbumArtImage.Source as BitmapImage;
                string currentTitle = SongTitleText.Text;
                string currentArtist = ArtistAlbumText.Text;

                miniPlayer.UpdateSongInfo(currentTitle, currentArtist, currentArt);
                miniPlayer.UpdatePlayButtonState(PlayButton.Content.ToString());
            }

            this.Hide();
        }

        public void ShowFullPlayer()
        {
            this.Show();
            this.Activate();

            if (isOnlineMode)
            {
                // Restore to the Online View
                OnlineModeButton_Click(this, null);
            }
            else
            {
                // Restore to the Offline View
                OfflineModeButton_Click(this, null);
            }

            if (miniPlayer != null)
            {
                var tempPlayer = miniPlayer;
                miniPlayer = null;
                tempPlayer.Close();
            }
        }

        #endregion

        // ==================================================================
        // === VVVV YOUR NEW LIBRARY & API LOGIC (ON-DEMAND) VVVV ===
        // ==================================================================

        #region Library View Logic

        /// <summary>
        /// A helper method to reset the top player bar to show the currently playing song.
        /// </summary>
        private void UpdatePlayerWithCurrentSong()
        {
            // --- MAKE CONTROLS VISIBLE ---
            PlayerControlsGrid.Visibility = Visibility.Visible;
            PlaybackControlsPanel.Visibility = Visibility.Visible;
            NextSongText.Visibility = Visibility.Visible;

            // --- MANAGE UI STATE ---
            ArtistAlbumText.Visibility = Visibility.Visible; // Show "Artist - Album" text
            ArtistBioScrollViewer.Visibility = Visibility.Collapsed; // Hide Bio text //

            if (currentQueueIndex >= 0 && currentQueueIndex < playbackQueue.Count)
            {
                // We have a song playing (or paused)
                var currentItem = playbackQueue[currentQueueIndex];
                SongTitleText.Text = currentItem.Title;
                ArtistAlbumText.Text = $"{currentItem.Artist} - {currentItem.Album}";
                ArtistAlbumText.ToolTip = $"{currentItem.Artist} - {currentItem.Album}"; // Reset tooltip

                // Reload the album art for the song
                LoadAlbumArt(currentItem.FilePath);
            }
            else
            {
                // No song is loaded
                SongTitleText.Text = "No Song Selected";
                ArtistAlbumText.Text = "Artist - Album";
                ArtistAlbumText.ToolTip = "Artist - Album"; // Reset tooltip
                AlbumArtImage.Source = null;
                BlurredBackgroundImage.Source = null;
            }
        }

        private List<ArtistModel> BuildLibraryViews()
        {
            // Create a temporary list instead of clearing the main one.
            var newArtistList = new List<ArtistModel>();
            var newAlbumList = new List<AlbumModel>();
            // ---

            // Use LINQ to group all songs by artist
            var artistGroups = playlistFiles.GroupBy(song => song.Artist)
                                            .OrderBy(g => g.Key); // Sort artists alphabetically

            foreach (var artistGroup in artistGroups)
            {
                var artist = new ArtistModel { ArtistName = artistGroup.Key };

                // Now, for each artist, group their songs by album
                var albumGroups = artistGroup.GroupBy(song => song.Album)
                                             .OrderBy(g => g.Key); // Sort albums alphabetically

                foreach (var albumGroup in albumGroups)
                {
                    var album = new AlbumModel
                    {
                        AlbumTitle = albumGroup.Key,
                        ArtistName = artist.ArtistName,
                        Songs = albumGroup.OrderBy(song => song.TrackNumber).ToList() // Sort songs by track #
                    };
                    // Find the first song's art to use for this album
                    var songWithArt = albumGroup.FirstOrDefault();
                    if (songWithArt != null)
                    {
                        try
                        {
                            var tagFile = TagLib.File.Create(songWithArt.FilePath);
                            if (tagFile.Tag.Pictures.Length > 0)
                            {
                                // We just save the raw bytes. We DON'T create a BitmapImage here.
                                album.AlbumArtBytes = tagFile.Tag.Pictures[0].Data.Data;
                            }
                        }
                        catch { /* couldn't load art, leave it null */ }
                    }

                    artist.Albums.Add(album);
                }
                newArtistList.Add(artist);
            }
            // Return the temporary list. This is all happening on the background thread.
            return newArtistList;
        }

        private void QueueViewButton_Click(object sender, RoutedEventArgs e)
        {
            // Show Queue, hide Library
            LibraryViewGrid.Visibility = Visibility.Collapsed;
            QueueViewGrid.Visibility = Visibility.Visible;

            // Update button state
            QueueViewButton.IsEnabled = false;
            LibraryViewButton.IsEnabled = true;
            LibraryBackButton.Visibility = Visibility.Collapsed;

            // Reset the player bar to show the current song
            UpdatePlayerWithCurrentSong();
        }

        private void LibraryViewButton_Click(object sender, RoutedEventArgs e)
        {
            // Show Library, hide Queue
            QueueViewGrid.Visibility = Visibility.Collapsed;
            LibraryViewGrid.Visibility = Visibility.Visible;

            // Update button state
            QueueViewButton.IsEnabled = true;
            LibraryViewButton.IsEnabled = false;

            // Update which library grid is visible (start at artists)
            UpdateLibraryView(LibraryViewMode.Artists);
        }

        private void LibraryBackButton_Click(object sender, RoutedEventArgs e)
        {
            // This is our "up" navigation
            if (currentLibraryView == LibraryViewMode.Songs)
            {
                UpdateLibraryView(LibraryViewMode.Albums);

                // We are still in the artist view, so show the artist info
                LoadArtistInfoForPlayerAsync(selectedArtist);
            }
            else if (currentLibraryView == LibraryViewMode.Albums)
            {
                UpdateLibraryView(LibraryViewMode.Artists);

                // We are back at the main artist list, so show the current song
                UpdatePlayerWithCurrentSong();
            }
        }

        private void ArtistGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ArtistGridView.SelectedItem == null) return;

            // Get the artist that was clicked
            selectedArtist = ArtistGridView.SelectedItem as ArtistModel;
            if (selectedArtist == null) return;

            // Set the source for the *next* grid (the albums)
            AlbumGridView.ItemsSource = selectedArtist.Albums;

            // Navigate to the Album view
            UpdateLibraryView(LibraryViewMode.Albums);

            // Call the API for *only this artist* and update the player bar
            LoadArtistInfoForPlayerAsync(selectedArtist);

            // Deselect item to allow re-selection
            ArtistGridView.SelectedItem = null;
        }

        private void AlbumGridView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (AlbumGridView.SelectedItem == null) return;

            // Get the album that was clicked
            var selectedAlbum = AlbumGridView.SelectedItem as AlbumModel;
            if (selectedAlbum == null) return;

            // Set the source for the *next* grid (the songs)
            LibrarySongList.ItemsSource = selectedAlbum.Songs;

            // Navigate to the Song view
            UpdateLibraryView(LibraryViewMode.Songs);

            // Deselect item to allow re-selection
            AlbumGridView.SelectedItem = null;
        }

        private void LibrarySongList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (LibrarySongList.SelectedItem == null) return;

            PlaylistItem selectedSong = LibrarySongList.SelectedItem as PlaylistItem;
            if (selectedSong == null) return;

            // Find this song in the main playback queue
            int index = playbackQueue.IndexOf(selectedSong);
            if (index != -1)
            {
                // If it's in the queue, just play it
                PlayTrack(index);

                // Switch back to the queue view to see the song playing
                QueueViewButton_Click(this, null);
            }
        }

        // This is a helper method to manage which grid is visible
        private void UpdateLibraryView(LibraryViewMode newView)
        {
            currentLibraryView = newView;

            // Show/hide grids
            ArtistGridView.Visibility = (newView == LibraryViewMode.Artists) ? Visibility.Visible : Visibility.Collapsed;
            AlbumGridView.Visibility = (newView == LibraryViewMode.Albums) ? Visibility.Visible : Visibility.Collapsed;
            LibrarySongList.Visibility = (newView == LibraryViewMode.Songs) ? Visibility.Visible : Visibility.Collapsed;

            // Show/hide back button
            LibraryBackButton.Visibility = (newView != LibraryViewMode.Artists) ? Visibility.Visible : Visibility.Collapsed;
        }
        /// <summary>
        /// Fetches info for a *single* artist and updates the top player bar.
        /// This now uses a 3-step search (Album, then Song, then Artist) for maximum accuracy.
        /// </summary>
        private async void LoadArtistInfoForPlayerAsync(ArtistModel artist)
        {
            // --- HIDE CONTROLS ---
            PlayerControlsGrid.Visibility = Visibility.Collapsed;
            PlaybackControlsPanel.Visibility = Visibility.Collapsed;
            NextSongText.Visibility = Visibility.Collapsed;

            // --- MANAGE UI STATE ---
            ArtistAlbumText.Visibility = Visibility.Collapsed; // Hide "Artist - Album" text
            ArtistBioScrollViewer.Visibility = Visibility.Visible; // Show Bio text
            // ---

            // --- STEP 1: Immediately update the UI with local info ---
            SongTitleText.Text = artist.ArtistName;
            ArtistBioTextBlock.Text = "Loading artist info..."; // Use the new TextBlock
            AlbumArtImage.Source = artist.ArtistArt;
            BlurredBackgroundImage.Source = artist.ArtistArt;

            try
            {
                // --- STEP 2: Get all local data we can use (WITH .Trim()) ---
                string artistName = CleanArtistNameForApi(artist.ArtistName.Trim());
                string firstSongTitle = artist.Albums?.FirstOrDefault()?.Songs?.FirstOrDefault()?.Title?.Trim();
                string firstAlbumTitle = artist.Albums?.FirstOrDefault()?.AlbumTitle?.Trim();

                string foundArtistId = null; // This is our goal!
                string apiKey = _secrets?.TheAudioDbApiKey ?? "2"; // Use "2" as a fallback if load failed
                // --- STEP 3: Start the 3-step search ---
                
                // 1. Try searching by Artist + Album (Most Accurate)
                if (!string.IsNullOrEmpty(firstAlbumTitle) && firstAlbumTitle != "Unknown Album")
                {
                    string artistEncoded = WebUtility.UrlEncode(artistName);
                    string albumEncoded = WebUtility.UrlEncode(firstAlbumTitle);
                    
                    string url_albumSearch = $"https://www.theaudiodb.com/api/v1/json/{apiKey}/searchalbum.php?s={artistEncoded}&a={albumEncoded}";

                    HttpResponseMessage albumResponse = await httpClient.GetAsync(url_albumSearch);
                    if (albumResponse.IsSuccessStatusCode)
                    {
                        string albumJson = await albumResponse.Content.ReadAsStringAsync();
                        var albumSearchResponse = JsonSerializer.Deserialize<TheAudioDbAlbumResponse>(albumJson);

                        // Check if the result is not null and has an artist
                        foundArtistId = albumSearchResponse?.Album?.FirstOrDefault()?.idArtist;
                    }
                }

                // 2. If that failed, try searching by Artist + Song (Very Accurate)
                if (string.IsNullOrEmpty(foundArtistId) && !string.IsNullOrEmpty(firstSongTitle))
                {
                    string artistEncoded = WebUtility.UrlEncode(artistName);
                    string songEncoded = WebUtility.UrlEncode(firstSongTitle);
                    string url_trackSearch = $"https://www.theaudiodb.com/api/v1/json/{apiKey}/searcht.php?s={artistEncoded}&t={songEncoded}";

                    HttpResponseMessage trackResponse = await httpClient.GetAsync(url_trackSearch);
                    if (trackResponse.IsSuccessStatusCode)
                    {
                        string trackJson = await trackResponse.Content.ReadAsStringAsync();
                        var trackSearchResponse = JsonSerializer.Deserialize<TheAudioDbTrackResponse>(trackJson);

                        foundArtistId = trackSearchResponse?.Track?.FirstOrDefault()?.idArtist;
                    }
                }

                // --- STEP 4: Get The Artist's Info ---

                TheAudioDbArtist finalArtist = null;
                string imageUrl = null;
                string bio = "No biography available.";

                if (!string.IsNullOrEmpty(foundArtistId))
                {
                    // PATH A (SUCCESS): We found a reliable Artist ID.
                    // Now we can get the correct artist bio and image.
                    string url_artistLookup = $"https://www.theaudiodb.com/api/v1/json/{apiKey}/artist.php?i={foundArtistId}";
                    HttpResponseMessage artistResponse = await httpClient.GetAsync(url_artistLookup);
                    if (artistResponse.IsSuccessStatusCode)
                    {
                        string artistJson = await artistResponse.Content.ReadAsStringAsync();
                        var artistLookupResponse = JsonSerializer.Deserialize<TheAudioDbResponse>(artistJson);
                        finalArtist = artistLookupResponse?.Artists?.FirstOrDefault();
                    }
                }
                else
                {
                    // PATH B (FALLBACK): Both searches failed.
                    // We'll just search by artist name and hope for the best.
                    string artistEncoded = WebUtility.UrlEncode(artistName);
                    string url_artistSearch = $"https://www.theaudiodb.com/api/v1/json/{apiKey}/search.php?s={artistEncoded}";

                    HttpResponseMessage artistResponse = await httpClient.GetAsync(url_artistSearch);
                    if (artistResponse.IsSuccessStatusCode)
                    {
                        string artistJson = await artistResponse.Content.ReadAsStringAsync();
                        var artistSearchResponse = JsonSerializer.Deserialize<TheAudioDbResponse>(artistJson);
                        finalArtist = artistSearchResponse?.Artists?.FirstOrDefault();
                    }
                }

                // --- STEP 5: Update the UI with whatever we found ---

                if (finalArtist != null)
                {
                    imageUrl = finalArtist.ArtistThumbUrl;
                    bio = finalArtist.BiographyEN ?? bio;
                }

                ArtistBioTextBlock.Text = bio; // Set the text of the new TextBlock

                if (!string.IsNullOrEmpty(imageUrl))
                {
                    byte[] imageBytes = await httpClient.GetByteArrayAsync(imageUrl);
                    using (var stream = new MemoryStream(imageBytes))
                    {
                        BitmapImage bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.StreamSource = stream;
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.DecodePixelWidth = 100; // Size for the player bar
                        bitmap.EndInit();
                        bitmap.Freeze();

                        artist.ArtistArt = bitmap;
                        AlbumArtImage.Source = bitmap;
                        BlurredBackgroundImage.Source = bitmap;
                    }
                }
                else
                {
                    // If all searches fail to find an image, just use the local one.
                    AlbumArtImage.Source = artist.ArtistArt;
                    BlurredBackgroundImage.Source = artist.ArtistArt;
                }
            }
            catch (Exception)
            {
                ArtistBioTextBlock.Text = "Artist (Error loading info)";
            }
        }

        private async void LoadLibraryDataAsync()
        {
            // Step 1: Run the SLOW file-reading code on a background thread.
            // It will return a temporary list.
            List<ArtistModel> tempArtistList = await Task.Run(() =>
            {
                return BuildLibraryViews();
            });

            // --- This part runs *after* the await, back on the MAIN UI THREAD ---

            // Step 2: Clear the old artists from the ObservableCollection.
            // This is safe because we are on the UI thread.
            libraryArtists.Clear();

            // Step 3: Convert all the byte arrays to BitmapImages (fast, on UI thread)
            foreach (var artist in tempArtistList)
            {
                foreach (var album in artist.Albums)
                {
                    if (album.AlbumArtBytes != null)
                    {
                        try
                        {
                            using (var stream = new MemoryStream(album.AlbumArtBytes))
                            {
                                var bitmap = new BitmapImage();
                                bitmap.BeginInit();
                                bitmap.StreamSource = stream;
                                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                                bitmap.DecodePixelWidth = 110; // Decode at a size for the album grid
                                bitmap.EndInit();
                                bitmap.Freeze();
                                album.AlbumArt = bitmap; // Now we set the BitmapImage
                            }
                            album.AlbumArtBytes = null; // Clear the memory
                        }
                        catch { /* Failed to create image */ }
                    }
                }
                // Set default artist art from the first album
                artist.ArtistArt = artist.Albums.FirstOrDefault(a => a.AlbumArt != null)?.AlbumArt;

                // Step 4: Add the fully-processed artist to the ObservableCollection.
                // This is safe and will update the UI.
                libraryArtists.Add(artist);
            }
        }

        private string CleanArtistNameForApi(string name)
        {
            // This is a safer cleaner. It finds common suffixes and removes them.
            string cleaned = name;

            // List of suffixes to remove
            string[] suffixes = { "(G)I-DLE", "(빌리)", "(CLC)" }; // Add more as needed

            // Check for "Artist (Group)" format, e.g., "ELKIE (CLC)"
            var match = System.Text.RegularExpressions.Regex.Match(name, @"^(.*?) \((.*?)\)$");
            if (match.Success)
            {
                // Check if the part in () is a known group, if so, use the main name
                if (suffixes.Contains(match.Groups[2].Value))
                {
                    cleaned = match.Groups[1].Value; // e.g., "ELKIE (CLC)" -> "ELKIE"
                }
            }

            // Special case for (G)I-DLE
            if (name.Contains("(G)I-DLE"))
            {
                cleaned = "(G)I-DLE";
            }

            return cleaned.Trim();
        }

        //private void PlaylistListBox_GongDropped(object sender, GongSolutions.Wpf.DragDrop.DropEventArgs e)
        //{
        //    // The library has already moved the item in the playbackQueue for us.
        //    // We just need to update our track numbers.
        //    RenumberPlaybackQueue();

        //    // After the drop, we also need to find the new index of the song that is playing.
        //    if (currentQueueIndex >= 0)
        //    {
        //        var currentSong = playbackQueue.FirstOrDefault(s => s.IsNowPlaying);
        //        if (currentSong != null)
        //        {
        //            // Update the index to its new position
        //            currentQueueIndex = playbackQueue.IndexOf(currentSong);
        //        }
        //    }
        ////}
        ///

        private void OfflineModeButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Set mode flag
            isOnlineMode = false;

            // 2. Show ALL offline UI
            MainPlayerGrid.Visibility = Visibility.Visible;
            PlayerControlsGrid.Visibility = Visibility.Visible;
            OfflineContentGrid.Visibility = Visibility.Visible;
            OfflineViewSwitcherPanel.Visibility = Visibility.Visible;

            // 3. Hide Online UI
            OnlineContentGrid.Visibility = Visibility.Collapsed;

            // 4. Update button "active" state
            OfflineModeButton.IsEnabled = false;
            OnlineModeButton.IsEnabled = true;

            // 5. Pause the *other* player
            OnlineContentGrid.PausePlayer();
        }

        private void OnlineModeButton_Click(object sender, RoutedEventArgs e)
        {
            // 1. Set mode flag
            isOnlineMode = true;

            // 2. Hide ALL offline UI
            MainPlayerGrid.Visibility = Visibility.Collapsed;
            PlayerControlsGrid.Visibility = Visibility.Collapsed;
            OfflineContentGrid.Visibility = Visibility.Collapsed;
            OfflineViewSwitcherPanel.Visibility = Visibility.Collapsed;

            // 3. Initialize the online control with our loaded API keys
            if (_secrets != null)
            {
                OnlineContentGrid.InitializeApiKeys(_secrets.JamendoClientId, _secrets.TheAudioDbApiKey);
            }

            // 4. Show Online UI
            OnlineContentGrid.Visibility = Visibility.Visible;

            // 5. Update button "active" state
            OfflineModeButton.IsEnabled = true;
            OnlineModeButton.IsEnabled = false;

            // 6. Pause the *other* player (your main, offline one)
            mediaPlayer.Pause();
            PlayButton.Content = "▶";
            PlayButton.ToolTip = "Play (Space)";
            if (miniPlayer != null)
                miniPlayer.UpdatePlayButtonState("▶");
        }

        private void OnlineMode_PinClicked()
        {
            // This just "presses" your main window's original Pin button
            PinButton_Click(this, null);
            // We must be explicit about which "Button" we mean
            var onlinePinButton = OnlineContentGrid.FindName("PinButton") as System.Windows.Controls.Button;

            if (onlinePinButton != null)
            {
                if (isPinned)
                {
                    onlinePinButton.Background = SystemColors.HighlightBrush;
                    onlinePinButton.ToolTip = "Pin to Top (On)";
                }
                else
                {
                    onlinePinButton.Background = SystemColors.ControlBrush;
                    onlinePinButton.ToolTip = "Pin to Top (Off)";
                }
            }
        }

        private void OnlineMode_MiniPlayerClicked()
        {
            // This just "presses" your main window's original Mini-Player button
            MiniPlayerButton_Click(this, null);
        }

        #endregion

        /// <summary>
/// Parses a standard .LRC format string into the lyrics collection.
/// </summary>
private void ParseLyrics(string lrcText)
{
    currentLyrics.Clear();
    currentLyricIndex = -1;
    if (string.IsNullOrEmpty(lrcText)) return;

    // Regex to match [mm:ss.xx] or [mm:ss.xxx]
    var regex = new Regex(@"\[(\d{2}):(\d{2})\.(\d{2,3})\](.*)", RegexOptions.Compiled);

    foreach (var line in lrcText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
    {
        var match = regex.Match(line);
        if (match.Success)
        {
            int minutes = int.Parse(match.Groups[1].Value);
            int seconds = int.Parse(match.Groups[2].Value);
            int milliseconds = int.Parse(match.Groups[3].Value.PadRight(3, '0')); // Ensure 3 digits
            string text = match.Groups[4].Value.Trim();

            currentLyrics.Add(new LyricsLine
            {
                Time = new TimeSpan(0, 0, minutes, seconds, milliseconds),
                Text = text
            });
        }
    }
}

/// <summary>
/// This timer tick runs 10x per second to highlight the correct lyric line.
/// </summary>
private void LyricsTimer_Tick(object sender, EventArgs e)
{
    if (currentLyrics.Count == 0 || !mediaPlayer.NaturalDuration.HasTimeSpan) return;

    TimeSpan currentPosition = mediaPlayer.Position;

    // Find the index of the line that should be active
    int newLyricIndex = -1;
    for (int i = 0; i < currentLyrics.Count; i++)
    {
        if (currentPosition >= currentLyrics[i].Time)
        {
            newLyricIndex = i;
        }
        else
        {
            // Stop as soon as we find a line that is in the future
            break; 
        }
    }

    // If the line has changed, update the UI
    if (newLyricIndex != currentLyricIndex)
    {
        // Un-highlight the old line
        if (currentLyricIndex >= 0 && currentLyricIndex < currentLyrics.Count)
        {
            currentLyrics[currentLyricIndex].IsCurrentLine = false;
        }

        // Highlight the new line
        if (newLyricIndex >= 0 && newLyricIndex < currentLyrics.Count)
        {
            currentLyrics[newLyricIndex].IsCurrentLine = true;

            // Select and scroll to the new line
            LyricsListBox.SelectedItem = currentLyrics[newLyricIndex];
            LyricsListBox.ScrollIntoView(LyricsListBox.SelectedItem);
        }

        currentLyricIndex = newLyricIndex;
    }
}

/// <summary>
/// Click handlers for the new lyrics buttons.
/// </summary>
private void LyricsButton_Click(object sender, RoutedEventArgs e)
{
    // Toggle the visibility
    LyricsViewGrid.Visibility = (LyricsViewGrid.Visibility == Visibility.Visible) 
        ? Visibility.Collapsed 
        : Visibility.Visible;
}

private void CloseLyricsButton_Click(object sender, RoutedEventArgs e)
{
    LyricsViewGrid.Visibility = Visibility.Collapsed;
}

    }

    public class AppSecrets
    {
        public string LastFmApiKey { get; set; }
        public string DiscordAppId { get; set; }
        public string JamendoClientId { get; set; }
        public string TheAudioDbApiKey { get; set; }
    }

}