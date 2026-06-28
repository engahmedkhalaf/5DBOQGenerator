using System;
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

            // Set the starting tab
            if (!string.IsNullOrEmpty(startingTab))
            {
                SelectTab(startingTab!);
            }
            else
            {
                if (LicenseManager.IsActivated())
                {
                    SelectTab("Status");
                }
                else
                {
                    SelectTab("Activate");
                }
            }
        }

        private void UpdateStatusDetails()
        {
            bool fullyActivated = LicenseManager.IsFullyActivated();
            double trialRemaining = LicenseManager.GetTrialRemainingDays();

            if (fullyActivated)
            {
                StatusTextVal.Text = "Active / Licensed";
                StatusTextVal.Foreground = Brushes.LightGreen;
                StatusEmailVal.Text = LicenseManager.GetSavedEmail();

                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\RuknTools\RuknBoqMapper"))
                {
                    object? expiresAtVal = key?.GetValue("ExpiresAt");
                    StatusExpiryVal.Text = expiresAtVal != null ? expiresAtVal.ToString()! : "Lifetime License";
                }
                
                StatusLabel.Text = "Status: Activated successfully.";
                StatusLabel.Foreground = Brushes.LightGreen;
                StatusLabel.Visibility = Visibility.Visible;
                TrialExpiredWarning.Visibility = Visibility.Collapsed;
                BtnSignOut.Visibility = Visibility.Visible;
                BtnStartTrial.Visibility = Visibility.Collapsed;
            }
            else if (trialRemaining > 0)
            {
                StatusTextVal.Text = $"Trial ({trialRemaining:F1} Days Remaining)";
                StatusTextVal.Foreground = Brushes.LightBlue;
                StatusEmailVal.Text = "Trial User";
                
                DateTime trialEndDate = DateTime.UtcNow.AddDays(trialRemaining);
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\RuknTools\RuknBoqMapper\Trial"))
                {
                    object? startStr = key?.GetValue("TrialStartDate");
                    if (startStr != null && DateTime.TryParse(startStr.ToString(), out DateTime startDate))
                    {
                        trialEndDate = startDate.ToLocalTime().AddDays(7);
                    }
                }
                StatusExpiryVal.Text = trialEndDate.ToString("dd MMMM yyyy HH:mm:ss");
                
                StatusLabel.Text = "Status: Running on trial license.";
                StatusLabel.Foreground = Brushes.LightBlue;
                StatusLabel.Visibility = Visibility.Visible;
                TrialExpiredWarning.Visibility = Visibility.Collapsed;
                BtnSignOut.Visibility = Visibility.Visible;
                BtnStartTrial.Visibility = Visibility.Collapsed;
            }
            else
            {
                bool isExpired = LicenseManager.IsExpired() || LicenseManager.IsLocalTrialActive();
                StatusTextVal.Text = isExpired ? "Expired" : "Not Activated";
                StatusTextVal.Foreground = Brushes.LightPink;
                StatusEmailVal.Text = "N/A";
                StatusExpiryVal.Text = "N/A";

                StatusLabel.Text = isExpired ? "Error: License or Trial has expired." : "Status: License not activated.";
                StatusLabel.Foreground = Brushes.LightPink;
                StatusLabel.Visibility = Visibility.Visible;
                
                TrialExpiredWarning.Visibility = isExpired ? Visibility.Visible : Visibility.Collapsed;
                BtnSignOut.Visibility = Visibility.Collapsed;
                BtnStartTrial.Visibility = Visibility.Visible;
            }
        }

        public void SelectTab(string tabName)
        {
            var activeStyle = FindResource("ActiveSidebarButton") as Style;
            var inactiveStyle = FindResource("SidebarButton") as Style;

            // Reset button styles
            NavGetLicense.Style = inactiveStyle;
            NavActivateLicense.Style = inactiveStyle;
            NavLicenseStatus.Style = inactiveStyle;
            NavInformation.Style = inactiveStyle;

            // Reset panel visibilities
            GetLicensePanel.Visibility = Visibility.Collapsed;
            ActivateLicensePanel.Visibility = Visibility.Collapsed;
            LicenseStatusPanel.Visibility = Visibility.Collapsed;
            InformationPanel.Visibility = Visibility.Collapsed;

            switch (tabName.ToLowerInvariant())
            {
                case "getlicense":
                case "get license":
                    NavGetLicense.Style = activeStyle;
                    GetLicensePanel.Visibility = Visibility.Visible;
                    break;
                case "activate":
                case "activate license":
                    NavActivateLicense.Style = activeStyle;
                    ActivateLicensePanel.Visibility = Visibility.Visible;
                    break;
                case "status":
                case "license status":
                    NavLicenseStatus.Style = activeStyle;
                    LicenseStatusPanel.Visibility = Visibility.Visible;
                    break;
                case "information":
                case "info":
                    NavInformation.Style = activeStyle;
                    InformationPanel.Visibility = Visibility.Visible;
                    break;
            }
        }

        private void Tab_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                string tabName = clickedButton.Content?.ToString() ?? string.Empty;
                SelectTab(tabName);
            }
        }

        private void StartTrial_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                LicenseManager.StartLocalTrial();
                MessageBox.Show(
                    "Free 7-day trial started successfully!\n\nYou can now use all tool features.",
                    "Trial Started",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                UpdateStatusDetails();
                SelectTab("Status");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not start trial: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BuyNow_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string email = string.IsNullOrWhiteSpace(EmailTextBox.Text) ? "(Not specified)" : EmailTextBox.Text.Trim();
                string subject = "RuknBoqMapper Add-in License Purchase";
                string body = $"Hello Ahmed,\n\nI would like to purchase a full license for RUKNBIM API.\n\nEmail: {email}\nMachine: {Environment.MachineName} ({Environment.UserName})\n";
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

                UpdateStatusDetails();
                MessageBox.Show("Product license activated successfully!", "License Manager", MessageBoxButton.OK, MessageBoxImage.Information);
                SelectTab("Status");
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
                SelectTab("Activate");
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
                string subject = "RuknBoqMapper Add-in Activation Code Request";
                
                string body = $"Hello Ahmed,\n\n" +
                              $"I would like to request an activation code for the RuknBoqMapper Add-in.\n\n" +
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
