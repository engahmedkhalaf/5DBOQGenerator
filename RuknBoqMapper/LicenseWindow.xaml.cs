using System;
using System.Diagnostics;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace RuknBoqMapper
{
    public partial class LicenseWindow : Window
    {
        public LicenseWindow(string? startingTab = null)
        {
            InitializeComponent();
            
            // Populate values
            EmailTextBox.Text = LicenseManager.GetSavedEmail();
            UpdateStatusDetails();
        }

        // Maintained for backward compatibility with external calls in RuknBoqApp
        public void SelectTab(string tabName)
        {
            // The tabs are merged into a single status screen
        }

        private void UpdateStatusDetails()
        {
            bool fullyActivated = LicenseManager.IsFullyActivated();

            if (fullyActivated)
            {
                StatusTextVal.Text = "Active / Licensed";
                StatusTextVal.Foreground = Brushes.LightGreen;
                
                string expiresAtStr = "Lifetime License";
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\RuknTools\RuknBoqMapper"))
                {
                    object? expiresAtVal = key?.GetValue("ExpiresAt");
                    if (expiresAtVal != null) expiresAtStr = expiresAtVal.ToString()!;
                }

                DetailsTextVal.Text = $"Email: {LicenseManager.GetSavedEmail()}\nExpiration: {expiresAtStr}";
                
                ActivationFormPanel.Visibility = Visibility.Collapsed;
                DeactivationPanel.Visibility = Visibility.Visible;
            }
            else
            {
                bool isExpired = LicenseManager.IsExpired();
                StatusTextVal.Text = isExpired ? "Expired" : "Not Activated";
                StatusTextVal.Foreground = Brushes.LightPink;

                DetailsTextVal.Text = isExpired 
                    ? "Your license has expired." 
                    : "No active license found.";

                ActivationFormPanel.Visibility = Visibility.Visible;
                DeactivationPanel.Visibility = Visibility.Collapsed;
            }
        }

        private async void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text;
            string code = CodeTextBox.Text;

            if (!LicenseManager.ValidateInput(email, code))
            {
                StatusLabel.Text = "Error: Invalid email address or activation code.";
                StatusLabel.Foreground = Brushes.LightPink;
                StatusLabel.Visibility = Visibility.Visible;
                MessageBox.Show("Validation failed. Please verify your email and code.", "License Manager", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                this.IsEnabled = false;
                StatusLabel.Text = "Signing in to Supabase... Please wait.";
                StatusLabel.Foreground = Brushes.Orange;
                StatusLabel.Visibility = Visibility.Visible;

                var result = await Task.Run(() => LicenseManager.ActivateAsync(email, code));
                this.IsEnabled = true;

                if (!result.Item1)
                {
                    StatusLabel.Text = "Error: License not found, inactive, or expired.";
                    StatusLabel.Foreground = Brushes.LightPink;
                    StatusLabel.Visibility = Visibility.Visible;
                    MessageBox.Show("Invalid, inactive, or expired license.", "License Manager", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                StatusLabel.Visibility = Visibility.Collapsed;
                UpdateStatusDetails();
                MessageBox.Show("Product license activated successfully!", "License Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                this.IsEnabled = true;
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.Foreground = Brushes.LightPink;
                StatusLabel.Visibility = Visibility.Visible;
                MessageBox.Show(ex.Message, "License Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SignOutButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Are you sure you want to deactivate and sign out of this license?", "Deactivate License", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                LicenseManager.SignOut();
                CodeTextBox.Text = string.Empty;
                UpdateStatusDetails();
                MessageBox.Show("You have been signed out successfully.", "License Manager", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LogoButton_Click(object sender, RoutedEventArgs e)
        {
            try { Process.Start(new ProcessStartInfo("https://www.ruknbim.com/") { UseShellExecute = true }); }
            catch { }
        }

        private async void PingButton_Click(object sender, RoutedEventArgs e)
        {
            PingButton.IsEnabled = false;
            PingResultText.Text = "Checking...";
            PingResultText.Foreground = Brushes.Orange;

            bool online = await LicenseManager.PingAsync();

            PingResultText.Text = online ? "✔ Connected" : "✘ Unreachable";
            PingResultText.Foreground = online ? Brushes.LightGreen : Brushes.LightPink;
            PingButton.IsEnabled = true;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void RequestCode_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string email = string.IsNullOrWhiteSpace(EmailTextBox.Text) ? "(Not specified by user)" : EmailTextBox.Text.Trim();
                string subject = "RuknBoqMapper Add-in Activation Code Request";
                
                string body = $"Hello Ahmed,\n\n" +
                              $"I would like to request an activation code for the RuknBoqMapper Add-in.\n\n" +
                              $"--- Device Details ---\n" +
                              $"Machine ID: {Environment.MachineName} ({Environment.UserName})\n" +
                              $"Email Address: {email}\n";

                string mailtoUrl = $"mailto:support@ruknbim.com?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
                
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(mailtoUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open email client: {ex.Message}\n\nYou can manually email support@ruknbim.com with your details.", "License Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
                e.Handled = true;
            }
            catch { }
        }
    }
}
