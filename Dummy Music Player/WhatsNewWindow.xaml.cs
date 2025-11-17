using System.Windows;

namespace Dummy_Music_Player
{
    public partial class WhatsNewWindow : Window
    {
        public WhatsNewWindow()
        {
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}