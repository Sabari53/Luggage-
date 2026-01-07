using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace UserModule
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer? sessionTimer;
        
        public MainWindow()
        {
            InitializeComponent();

            // Set icon in code to avoid pack URI issues in single-file deployment
            try
            {
                var iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "assests", "app.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    this.Icon = new BitmapImage(new Uri(iconPath, UriKind.Absolute));
                }
            }
            catch { /* Icon is optional */ }

            // Load Login first
            var loginControl = new Views.Login();
            loginControl.LoginSuccess += OnLoginSuccess;
            MainContent.Content = loginControl;

        }

        // ✅ Use this to load UserControls inside MainContent
        public void LoadContent(UserControl content)
        {
            MainContent.Content = content;
        }

        // Called when login succeeds
        private void OnLoginSuccess(string username)
        {
            // Replace MainContent with Header after login
            var header = new Header();
            MainContent.Content = header;

            // Set the username in Header and load dashboard
            header.SetLoggedInUser(username);
            header.MainContentHost.Content = new Dashboard();
            
            // Start session timeout timer (1 hour)
            StartSessionTimer();
        }
        
        private void StartSessionTimer()
        {
            // Stop existing timer if any
            sessionTimer?.Stop();
            
            // Create timer that checks every 30 seconds
            sessionTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30)
            };
            
            sessionTimer.Tick += (s, e) =>
            {
                if (!LocalStorage.IsSessionValid())
                {
                    sessionTimer?.Stop();
                    LogoutUser("Session expired. Please login again.");
                }
                else
                {
                    var remaining = LocalStorage.GetRemainingSessionTime();
                    if (remaining.HasValue && remaining.Value.TotalMinutes <= 5)
                    {
                        System.Diagnostics.Debug.WriteLine($"⚠ Session expires in {remaining.Value.TotalMinutes:F1} minutes");
                    }
                }
            };
            
            sessionTimer.Start();
            System.Diagnostics.Debug.WriteLine("✓ Session timer started - 1 hour timeout");
        }
        
        private void LogoutUser(string message)
        {
            try
            {
                // Clear session
                LocalStorage.ClearWorkerSession();
                
                // Show message
                MessageBox.Show(message, "Session Timeout", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Return to login screen
                var loginControl = new Views.Login();
                loginControl.LoginSuccess += OnLoginSuccess;
                MainContent.Content = loginControl;
                
                System.Diagnostics.Debug.WriteLine("✓ User logged out due to session timeout");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error during logout: {ex.Message}");
            }
        }
    }
}
