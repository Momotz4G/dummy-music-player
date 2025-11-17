using System;
using System.Collections.Generic;
using System.Linq; // <-- ADDED FOR SHUFFLE
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.Wave;

namespace Dummy_Music_Player
{
    // (NEW) Define the RepeatMode enum
    public enum OnlineRepeatMode { Off, All, One }

    public partial class OnlineMode : System.Windows.Controls.UserControl
    {

        private string _jamendoClientId;
        private string _theAudioDbApiKey;
        private bool _keysInitialized = false;


        private IWavePlayer waveOut;
        private MediaFoundationReader audioFileReader;

        private JamendoService jamendoService;

        // (MODIFIED) We now need two lists for shuffling
        private List<JamendoTrack> searchResults = new List<JamendoTrack>(); // This list will be shuffled
        private List<JamendoTrack> originalSearchResults = new List<JamendoTrack>(); // This is the master list

        private int currentQueueIndex = -1;
        public JamendoTrack CurrentItem { get; private set; }

        private bool isSeeking = false;
        private readonly DispatcherTimer timer;
        private bool isSearchPlaceholder = true;


        private bool isShuffled = false;
        private OnlineRepeatMode repeatMode = OnlineRepeatMode.Off;
        private Random rng = new Random();
        private bool isMuted = false;
        private double lastVolume = 0.3; // Default volume

        public event Action OnPinClicked;
        public event Action OnMiniPlayerClicked;

        public OnlineMode()
        {
            InitializeComponent();
            waveOut = new WaveOutEvent();
            waveOut.PlaybackStopped += OnPlaybackStopped;

            // ==============================================
            // === VVVV (NEW) SET INITIAL VOLUME VVVV ===
            // ==============================================
            // NAudio uses a float from 0.0 to 1.0
            waveOut.Volume = (float)lastVolume;
            VolumeSlider.Value = lastVolume;
            // ==============================================

            timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += Timer_Tick;
            timer.Start();

            SearchBox.Text = "Search for music...";
            SearchBox.Foreground = Brushes.Gray;
            isSearchPlaceholder = true;
        }

        /// Receives API keys from the MainWindow.
        public void InitializeApiKeys(string jamendoId, string audioDbKey)
        {
            if (_keysInitialized) return; // Only run once

            _jamendoClientId = jamendoId;
            _theAudioDbApiKey = audioDbKey;

            // Now that we have the key, initialize the service
            jamendoService = new JamendoService(_jamendoClientId);

            _keysInitialized = true;
        }

        // (MODIFIED) This event now handles Repeat logic
        private void OnPlaybackStopped(object sender, StoppedEventArgs e)
        {
            // Only run if the song finished naturally (not stopped)
            if (e.Exception == null && (audioFileReader != null && audioFileReader.Position >= audioFileReader.Length))
            {
                Dispatcher.Invoke(() =>
                {
                    if (repeatMode == OnlineRepeatMode.One)
                    {
                        // Replay the current track
                        audioFileReader.CurrentTime = TimeSpan.Zero;
                        waveOut.Play();
                    }
                    else
                    {
                        // Play the next song in the queue
                        PlayNextSongInQueue();
                    }
                });
            }
        }

        public void PausePlayer()
        {
            waveOut.Pause();
            PlayButton.Content = "▶";
        }

        private void StopAndCleanupAudio()
        {
            waveOut.Stop();
            if (audioFileReader != null)
            {
                audioFileReader.Dispose();
                audioFileReader = null;
            }
        }

        #region Search Logic

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
                SearchBox.Text = "Search for music...";
                SearchBox.Foreground = Brushes.Gray;
                isSearchPlaceholder = true;
            }
        }

        // (MODIFIED) This now resets shuffle and fills both lists
        private async void SearchButton_Click(object sender, RoutedEventArgs e)
        {
            if (isSearchPlaceholder || string.IsNullOrWhiteSpace(SearchBox.Text))
                return;

            string searchTerm = SearchBox.Text;
            SearchButton.IsEnabled = false;
            SearchButton.Content = "Searching...";

            // Clear old results
            searchResults.Clear();
            originalSearchResults.Clear();
            SearchResultsListBox.ItemsSource = null;

            // Reset shuffle
            isShuffled = false;
            ShuffleButton.Background = SystemColors.ControlBrush;
            ShuffleButton.ToolTip = "Shuffle Off";

            try
            {
                // Fill the original list
                originalSearchResults = await jamendoService.SearchMusic(searchTerm);

                // Copy to the playback list
                searchResults = new List<JamendoTrack>(originalSearchResults);

                SearchResultsListBox.ItemsSource = searchResults;

                if (searchResults.Count == 0)
                {
                    System.Windows.MessageBox.Show("No results found for that search.", "Search Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"API Error: {ex.Message}", "API Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            SearchButton.IsEnabled = true;
            SearchButton.Content = "Search";
        }
        #endregion

        #region Playback Logic

        private void SearchResultsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchResultsListBox.SelectedItem == null) return;
            int newIndex = SearchResultsListBox.SelectedIndex;
            if (newIndex != -1 && newIndex != currentQueueIndex)
            {
                PlayTrack(newIndex);
            }
        }

        private void PlayTrack(int queueIndex)
        {
            if (queueIndex < 0 || queueIndex >= searchResults.Count) return;

            currentQueueIndex = queueIndex;
            var currentItem = searchResults[currentQueueIndex];

            this.CurrentItem = currentItem;

            if (string.IsNullOrEmpty(currentItem.AudioUrl))
            {
                System.Windows.MessageBox.Show("This track is not available for streaming.", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                StopAndCleanupAudio();
                audioFileReader = new MediaFoundationReader(currentItem.AudioUrl);
                waveOut.Init(audioFileReader);
                waveOut.Play();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Could not play audio stream: {ex.Message}", "Playback Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            PlayButton.Content = "❚❚";
            SearchResultsListBox.SelectedItem = currentItem;
            SearchResultsListBox.ScrollIntoView(currentItem);

            SongTitleText.Text = currentItem.Name;
            ArtistAlbumText.Text = $"{currentItem.Artist} - {currentItem.Album}";
            AlbumArtImage.Source = currentItem.AlbumArt;

            SeekBar.IsEnabled = true;
            CurrentTimeText.Text = "00:00";
            RemainingTimeText.Text = "-00:00";
            UpdateNextSongText(currentQueueIndex);
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            // If there's no song loaded AND the list is empty, do nothing.
            if (audioFileReader == null && searchResults.Count == 0)
            {
                return; // Stop the method here to prevent a crash
            }

            // If there are songs in the list but none is loaded, play the first one.
            if (audioFileReader == null && searchResults.Count > 0)
            {
                PlayTrack(0);
                return;
            }

            // If a song is already loaded, toggle play/pause
            if (waveOut.PlaybackState == PlaybackState.Playing)
            {
                waveOut.Pause();
                PlayButton.Content = "▶";
            }
            else
            {
                waveOut.Play();
                PlayButton.Content = "❚❚";
            }
        }

        // (MODIFIED) This now calls the new PlayNextSongInQueue method
        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            PlayNextSongInQueue();
        }

        // (MODIFIED) This now plays the previous track in the *current* (shuffled or unshuffled) list
        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            if (audioFileReader != null && audioFileReader.CurrentTime.TotalSeconds > 3)
            {
                audioFileReader.CurrentTime = TimeSpan.Zero;
                return;
            }

            int prevIndex = currentQueueIndex - 1;
            if (prevIndex < 0)
            {
                // If at the start, wrap to the end of the list
                prevIndex = searchResults.Count - 1;
            }

            if (searchResults.Count > 0)
            {
                PlayTrack(prevIndex);
            }
        }

        // (NEW) This method contains the "what to play next" logic
        private void PlayNextSongInQueue()
        {
            if (searchResults.Count == 0) return;

            int nextIndex = currentQueueIndex + 1;
            if (nextIndex >= searchResults.Count)
            {
                // We're at the end of the list
                if (repeatMode == OnlineRepeatMode.All)
                {
                    nextIndex = 0; // Loop back to start
                }
                else
                {
                    // Repeat is Off, so stop playback
                    StopAndCleanupAudio();
                    PlayButton.Content = "▶";
                    SeekBar.Value = 0;
                    CurrentTimeText.Text = "00:00";
                    return;
                }
            }

            PlayTrack(nextIndex);
        }

        private void UpdateNextSongText(int queueIndex)
        {
            int nextIndex = queueIndex + 1;
            if (nextIndex >= searchResults.Count)
            {
                nextIndex = (repeatMode == OnlineRepeatMode.All) ? 0 : -1;
            }

            if (nextIndex != -1 && searchResults.Count > 0)
            {
                var nextSong = searchResults[nextIndex];
                NextSongText.Text = $"Next: {nextSong.Artist} - {nextSong.Name}";
            }
            else
            {
                NextSongText.Text = "Next: End of queue";
            }
        }
        #endregion

        #region Seekbar & Timer Logic (Unchanged)

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (audioFileReader != null && !isSeeking)
            {
                TimeSpan total = audioFileReader.TotalTime;
                TimeSpan current = audioFileReader.CurrentTime;
                TimeSpan remaining = total - current;

                SeekBar.Maximum = total.TotalSeconds;
                SeekBar.ValueChanged -= SeekBar_ValueChanged;
                SeekBar.Value = current.TotalSeconds;
                SeekBar.ValueChanged += SeekBar_ValueChanged;

                CurrentTimeText.Text = current.ToString(@"mm\:ss");
                RemainingTimeText.Text = "-" + remaining.ToString(@"mm\:ss");
            }
        }

        private void SeekBar_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (audioFileReader != null)
                isSeeking = true;
        }

        private void SeekBar_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (audioFileReader != null && isSeeking)
            {
                audioFileReader.CurrentTime = TimeSpan.FromSeconds(SeekBar.Value);
            }
            isSeeking = false;
        }

        private void SeekBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (audioFileReader != null && isSeeking)
            {
                audioFileReader.CurrentTime = TimeSpan.FromSeconds(SeekBar.Value);
            }
        }
        #endregion

        // ==============================================
        // === VVVV (NEW) METHODS FOR NEW CONTROLS VVVV ===
        // ==============================================

        #region New Control Logic

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (waveOut != null)
            {
                float newVolume = (float)VolumeSlider.Value;
                waveOut.Volume = newVolume;

                if (newVolume == 0)
                {
                    SpeakerIcon.Text = " 🔇";
                    isMuted = true;
                }
                else
                {
                    SpeakerIcon.Text = " 🔊";
                    isMuted = false;
                    lastVolume = newVolume;
                }
            }
        }

        private void SpeakerIcon_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (isMuted)
            {
                // Unmute to last known volume
                VolumeSlider.Value = lastVolume;
            }
            else
            {
                // Mute
                VolumeSlider.Value = 0;
            }
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            isShuffled = !isShuffled;

            JamendoTrack currentItem = null;
            if (currentQueueIndex >= 0 && currentQueueIndex < searchResults.Count)
            {
                currentItem = searchResults[currentQueueIndex];
            }

            if (isShuffled)
            {
                // --- SHUFFLE ---
                ShuffleButton.Background = SystemColors.HighlightBrush;
                ShuffleButton.ToolTip = "Shuffle On";

                // Create a new shuffled list from the original
                var shuffledList = originalSearchResults.OrderBy(x => rng.Next()).ToList();

                // Re-insert the currently playing song at the top
                if (currentItem != null)
                {
                    shuffledList.Remove(currentItem);
                    shuffledList.Insert(0, currentItem);
                }

                searchResults = shuffledList;
            }
            else
            {
                // --- UNSHUFFLE ---
                ShuffleButton.Background = SystemColors.ControlBrush;
                ShuffleButton.ToolTip = "Shuffle Off";

                // Restore the original, sorted list
                searchResults = new List<JamendoTrack>(originalSearchResults);
            }

            // Re-bind the ListBox
            SearchResultsListBox.ItemsSource = searchResults;

            // Find the new index of the current song
            if (currentItem != null)
            {
                currentQueueIndex = searchResults.IndexOf(currentItem);
                SearchResultsListBox.SelectedItem = currentItem;
                SearchResultsListBox.ScrollIntoView(currentItem);
            }
            else
            {
                currentQueueIndex = -1;
            }

            UpdateNextSongText(currentQueueIndex);
        }

        private void RepeatButton_Click(object sender, RoutedEventArgs e)
        {
            int nextMode = ((int)repeatMode + 1) % 3;
            repeatMode = (OnlineRepeatMode)nextMode;
            UpdateRepeatButtonUI();
            UpdateNextSongText(currentQueueIndex);
        }

        private void UpdateRepeatButtonUI()
        {
            switch (repeatMode)
            {
                case OnlineRepeatMode.Off:
                    RepeatButton.Content = "🔁";
                    RepeatButton.Background = SystemColors.ControlBrush;
                    RepeatButton.ToolTip = "Repeat Off";
                    break;
                case OnlineRepeatMode.All:
                    RepeatButton.Content = "🔁";
                    RepeatButton.Background = SystemColors.HighlightBrush;
                    RepeatButton.ToolTip = "Repeat All";
                    break;
                case OnlineRepeatMode.One:
                    RepeatButton.Content = "🔁¹";
                    RepeatButton.Background = SystemColors.HighlightBrush;
                    RepeatButton.ToolTip = "Repeat One";
                    break;
            }
        }

        #endregion

        #region Public Control Methods (For Hotkeys)

        public void TogglePlayPause()
        {
            PlayButton_Click(this, null);
        }

        public void PlayNext()
        {
            NextButton_Click(this, null);
        }

        public void PlayPrevious()
        {
            PrevButton_Click(this, null);
        }

        // These are for the WM_APPCOMMAND media keys
        public void EnsurePlaying()
        {
            if (waveOut.PlaybackState != PlaybackState.Playing)
            {
                PlayButton_Click(this, null);
            }
        }

        public void EnsurePaused()
        {
            if (waveOut.PlaybackState == PlaybackState.Playing)
            {
                PlayButton_Click(this, null);
            }
        }

        #endregion

        private void PinButton_Click(object sender, RoutedEventArgs e)
        {
            // "Raise the alarm" for the main window
            OnPinClicked?.Invoke();
        }

        private void MiniPlayerButton_Click(object sender, RoutedEventArgs e)
        {
            // "Raise the alarm" for the main window
            OnMiniPlayerClicked?.Invoke();
        }
    }
}