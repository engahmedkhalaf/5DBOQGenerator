using System;
using System.Windows;
using System.Threading.Tasks;

namespace QicBoqMapper
{
    public partial class LicenseWindow : Window
    {
        public LicenseWindow()
        {
            InitializeComponent();
            
            EmailTextBox.Text = LicenseManager.GetSavedEmail();

            if (LicenseManager.IsActivated())
            {
                StatusLabel.Text = "Status: Activated successfully.";
                StatusLabel.Foreground = System.Windows.Media.Brushes.LightGreen;
                StatusLabel.Visibility = Visibility.Visible;
            }
            else if (LicenseManager.IsExpired())
            {
                BuyNowPanel.Visibility = Visibility.Visible;
            }
        }

        private async void StartTrial_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text;
            if (string.IsNullOrWhiteSpace(email))
            {
                MessageBox.Show("Please enter your email above to start a free trial.", "Free Trial", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                this.IsEnabled = false;
                StatusLabel.Text = "Requesting trial license... Please wait.";
                StatusLabel.Foreground = System.Windows.Media.Brushes.Orange;
                StatusLabel.Visibility = Visibility.Visible;

                var trial = await Task.Run(() => LicenseManager.StartTrialAsync(email));
                string trialEmail = trial.Item1;
                string trialCode = trial.Item2;
                string? expiresAtStr = trial.Item3;

                // Sign in (will sign-up + sign-in under the hood since the auth
                // user doesn't exist yet). This writes the encrypted session
                // tokens to the registry instead of the plaintext code.
                await Task.Run(() => LicenseManager.SignInAsync(trialEmail, trialCode));

                CodeTextBox.Text = string.Empty; // don't leave the code visible in the UI
                BuyNowPanel.Visibility = Visibility.Collapsed;
                StatusLabel.Text = $"Trial activated. Expires {expiresAtStr}.";
                StatusLabel.Foreground = System.Windows.Media.Brushes.LightGreen;
                this.IsEnabled = true;
                MessageBox.Show($"Your 30-day free trial is now active.\nExpires: {expiresAtStr}", "Free Trial", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                this.IsEnabled = true;
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.Foreground = System.Windows.Media.Brushes.LightPink;
                StatusLabel.Visibility = Visibility.Visible;
                MessageBox.Show(ex.Message, "Free Trial", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BuyNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string email = string.IsNullOrWhiteSpace(EmailTextBox.Text) ? "(Not specified)" : EmailTextBox.Text.Trim();
                string subject = "QicBoqMapper Add-in License Purchase";
                string body = $"Hello Ahmed,\n\nMy 30-day trial has ended and I would like to purchase a full license.\n\nEmail: {email}\nMachine: {Environment.MachineName} ({Environment.UserName})\n";
                string mailto = $"mailto:engkhalaf7@gmail.com?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(mailto) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open email client: {ex.Message}\n\nYou can manually email engkhalaf7@gmail.com.", "License Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private async void ActivateButton_Click(object sender, RoutedEventArgs e)
        {
            string email = EmailTextBox.Text;
            string code = CodeTextBox.Text;

            if (!LicenseManager.ValidateInput(email, code))
            {
                StatusLabel.Text = "Error: Invalid email address or activation code.";
                StatusLabel.Foreground = System.Windows.Media.Brushes.LightPink;
                StatusLabel.Visibility = Visibility.Visible;
                MessageBox.Show("Validation failed. Please verify your email and code.", "License Manager", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                this.IsEnabled = false;
                StatusLabel.Text = "Signing in to Supabase... Please wait.";
                StatusLabel.Foreground = System.Windows.Media.Brushes.Orange;
                StatusLabel.Visibility = Visibility.Visible;

                var result = await Task.Run(() => LicenseManager.SignInAsync(email, code));
                this.IsEnabled = true;

                StatusLabel.Text = result.LicenseExpiresAtStr == null
                    ? "Status: Activated successfully (lifetime)."
                    : $"Status: Activated. Expires {result.LicenseExpiresAtStr}.";
                StatusLabel.Foreground = System.Windows.Media.Brushes.LightGreen;
                StatusLabel.Visibility = Visibility.Visible;
                MessageBox.Show("Product license activated successfully!", "License Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                this.IsEnabled = true;
                StatusLabel.Text = $"Error: {ex.Message}";
                StatusLabel.Foreground = System.Windows.Media.Brushes.LightPink;
                StatusLabel.Visibility = Visibility.Visible;
                MessageBox.Show(ex.Message, "License Manager", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                string subject = "QicBoqMapper Add-in Activation Code Request";
                
                string body = $"Hello Ahmed,\n\n" +
                              $"I would like to request an activation code for the QicBoqMapper Add-in.\n\n" +
                              $"--- Device Details ---\n" +
                              $"Machine ID: {Environment.MachineName} ({Environment.UserName})\n" +
                              $"Email Address: {email}\n";

                string mailtoUrl = $"mailto:engkhalaf7@gmail.com?subject={Uri.EscapeDataString(subject)}&body={Uri.EscapeDataString(body)}";
                
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(mailtoUrl) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open email client: {ex.Message}\n\nYou can manually email engkhalaf7@gmail.com with your details.", "License Manager", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }
}
