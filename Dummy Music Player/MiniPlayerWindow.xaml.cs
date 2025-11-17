using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace Dummy_Music_Player
{
    public partial class MiniPlayerWindow : Window
    {
        // This will hold a reference to our main application
        private MainWindow mainApp;

        public MiniPlayerWindow(MainWindow owner)
        {
            InitializeComponent();
            this.Owner = owner;
            this.mainApp = owner;
        }

        // --- Public Methods (called by MainWindow) ---

        public void UpdateSongInfo(string title, string artist, BitmapImage albumArt)
        {
            SongTitleText.Text = title;
            ArtistAlbumText.Text = artist;
            AlbumArtImage.Source = albumArt;
        }

        public void UpdatePlayButtonState(string content)
        {
            // content will be "▶" or "❚❚"
            PlayButton.Content = content;
        }


        // --- Control Clicks (calls MainWindow) ---

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            // Call the public method we will create on MainWindow
            mainApp.TogglePlayPause();
        }

        private void PrevButton_Click(object sender, RoutedEventArgs e)
        {
            mainApp.PlayPrevious();
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            mainApp.PlayNext();
        }

        private void MaximizeButton_Click(object sender, RoutedEventArgs e)
        {
            // Tell the main window to show itself, then close this mini-player
            mainApp.ShowFullPlayer();
            this.Close();
        }

        // --- Window Dragging ---
        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }
    }
}