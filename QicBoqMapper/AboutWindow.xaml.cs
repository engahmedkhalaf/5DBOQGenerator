using System.Diagnostics;
using System.Windows;
using System.Windows.Navigation;

namespace QicBoqMapper
{
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch { /* ignore — opening URL is best-effort */ }
            e.Handled = true;
        }
    }
}
