using System.Windows;
using System.Reflection;

namespace Dummy_Music_Player
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
            VersionText.Text = $"Version {Assembly.GetExecutingAssembly().GetName().Version.ToString(3)}";
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // Closes this "About" window
            this.Close();
        }
    }
}