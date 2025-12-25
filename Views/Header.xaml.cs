using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Threading;


namespace UserModule
{
    /// <summary>
    /// Interaction logic for Header.xaml
    /// </summary>
    public partial class Header : UserControl
    {
        private Button? _selectedButton;
        
        // Internet status monitoring
        private DispatcherTimer? internetCheckTimer;
        private int consecutiveFailures = 0;

        public Header()
        {
            InitializeComponent();
            LoadContent(new Dashboard());

            // Initially select Dashboard button
            _selectedButton = DashboardButton;
            SetSelectedButton(_selectedButton);
            
            // Initialize internet status monitoring
            InitializeInternetStatusMonitor();
        }

        public void SetLoggedInUser(string username)
        {
            // Capitalize first letter of username
            string capitalizedUsername = !string.IsNullOrEmpty(username) 
                ? char.ToUpper(username[0]) + username.Substring(1).ToLower() 
                : username;

            // Get time-based greeting
            string greeting = GetTimeBasedGreeting();

            // Update username text
            UserNameTextBlock.Text = capitalizedUsername;

            // Create popup container
            var popupBorder = new Border
            {
                Background = Brushes.White,
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(14),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 10, 20, 0),
                Opacity = 0,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 15,
                    ShadowDepth = 3,
                    Opacity = 0.25
                }
            };

            // Text inside popup
            var popupText = new TextBlock
            {
                Text = $"{greeting}, {capitalizedUsername}! Welcome Back. Have a good day!!",
                Foreground = (Brush)new BrushConverter().ConvertFromString("#28C76F"),
                FontFamily = new FontFamily("Segoe UI Semibold"),
                FontWeight = FontWeights.SemiBold,
                FontSize = 14
            };

            popupBorder.Child = popupText;

            // Add to MainContentGrid
            MainContentGrid.Children.Add(popupBorder);

            // Animate slide-in from top-right
            var anim = new System.Windows.Media.Animation.DoubleAnimation(0, 1, new Duration(TimeSpan.FromMilliseconds(400)));
            popupBorder.BeginAnimation(OpacityProperty, anim);

            // Auto remove after 3 seconds
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (s, e) =>
            {
                var fadeOut = new System.Windows.Media.Animation.DoubleAnimation(1, 0, new Duration(TimeSpan.FromMilliseconds(300)));
                fadeOut.Completed += (s2, e2) => MainContentGrid.Children.Remove(popupBorder);
                popupBorder.BeginAnimation(OpacityProperty, fadeOut);
                timer.Stop();
            };
            timer.Start();
        }

        private void LogoutButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var result = MessageBox.Show(
                    "Are you sure you want to logout?", 
                    "Confirm Logout", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    LocalStorage.RemoveItem("username");
                    LocalStorage.RemoveItem("password");
                    LocalStorage.RemoveItem("workerId");
                    LocalStorage.RemoveItem("rememberMe");
                    
                    Logger.Log($"User {UserNameTextBlock.Text} logged out successfully");

                    var mainWindow = Application.Current.MainWindow as MainWindow;
                    if (mainWindow != null)
                    {
                        var loginControl = new Login();

                        loginControl.LoginSuccess += username =>
                        {
                            var header = new Header();
                            header.SetLoggedInUser(username);
                            header.MainContentHost.Content = new Dashboard();
                            mainWindow.MainContent.Content = header;
                        };

                        mainWindow.MainContent.Content = loginControl;
                        
                        MessageBox.Show(
                            "You have been logged out successfully!", 
                            "Logout Successful", 
                            MessageBoxButton.OK, 
                            MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        public void LoadContent(UserControl control)
        {
            MainContentHost.Content = control;
        }

        private void SetSelectedButton(Button button)
        {
            if (_selectedButton == button)
                return;

            if (_selectedButton != null && _selectedButton != SubmitBookingButton)
                _selectedButton.Style = (Style)FindResource("HeaderButtonStyle");

            _selectedButton = button;

            if (button == SubmitBookingButton)
            {
                SubmitBookingButton.Style = (Style)FindResource("HeaderButtonSelectedStyle");
            }
            else
            {
                _selectedButton.Style = (Style)FindResource("HeaderButtonSelectedStyle");
            }
        }

        private void Dashboard_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedButton(DashboardButton);
            SetSubmitButtonVisibility(false);
            LoadContent(new Dashboard());
        }

        private void Booking_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedButton(BookingButton);
            SetSubmitButtonVisibility(false);
            LoadContent(new UserModule.Views.Luggage());
        }

        public void OpenBooking()
        {
            SetSelectedButton(BookingButton);
            SetSubmitButtonVisibility(false);
            LoadContent(new UserModule.Views.Luggage());
        }

        private void Submit_Click(object sender, RoutedEventArgs e)
        {
            SubmitBookingButton.Style = (Style)FindResource("HeaderButtonSelectedStyle");
            SetSubmitButtonVisibility(true);
            SetSelectedButton(SubmitBookingButton);
            LoadContent(new Submit());
        }

        public void UpdateUsername(string username)
        {
            UserNameTextBlock.Text = username;
        }

        public void SetSubmitButtonVisibility(bool isVisible)
        {
            SubmitBookingButton.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private string GetTimeBasedGreeting()
        {
            int hour = DateTime.Now.Hour;

            if (hour >= 5 && hour < 12)
                return "Good Morning";
            else if (hour >= 12 && hour < 17)
                return "Good Afternoon";
            else if (hour >= 17 && hour < 21)
                return "Good Evening";
            else
                return "Good Night";
        }

        /// <summary>
        /// Initialize the internet status monitoring timer
        /// </summary>
        private void InitializeInternetStatusMonitor()
        {
            _ = CheckInternetStatusAsync();

            internetCheckTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(5)
            };
            internetCheckTimer.Tick += async (s, e) => await CheckInternetStatusAsync();
            internetCheckTimer.Start();
        }

        /// <summary>
        /// Check internet connection status and update the indicator
        /// </summary>
        private async Task CheckInternetStatusAsync()
        {
            try
            {
                bool isConnected = NetworkInterface.GetIsNetworkAvailable();
                
                if (!isConnected)
                {
                    consecutiveFailures = 3;
                    UpdateInternetStatus(InternetStatus.NoConnection);
                    return;
                }

                bool hasInternet = await PingServerAsync("8.8.8.8", 3000);
                
                if (hasInternet)
                {
                    consecutiveFailures = 0;
                    UpdateInternetStatus(InternetStatus.Good);
                }
                else
                {
                    consecutiveFailures++;
                    
                    if (consecutiveFailures >= 3)
                    {
                        UpdateInternetStatus(InternetStatus.NoConnection);
                    }
                    else if (consecutiveFailures >= 1)
                    {
                        UpdateInternetStatus(InternetStatus.Unstable);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                consecutiveFailures++;
                
                if (consecutiveFailures >= 2)
                {
                    UpdateInternetStatus(InternetStatus.NoConnection);
                }
                else
                {
                    UpdateInternetStatus(InternetStatus.Unstable);
                }
            }
        }

        /// <summary>
        /// Ping a server to check internet connectivity
        /// </summary>
        private async Task<bool> PingServerAsync(string host, int timeout)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(host, timeout);
                    return reply.Status == IPStatus.Success;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Update the UI with the current internet status
        /// </summary>
        private void UpdateInternetStatus(InternetStatus status)
        {
            if (InternetStatusDot == null || InternetStatusText == null)
                return;

            switch (status)
            {
                case InternetStatus.Good:
                    InternetStatusDot.Fill = new SolidColorBrush(Color.FromRgb(76, 175, 80));
                    InternetStatusText.Text = "Online";
                    InternetStatusDot.ToolTip = "Internet connection is stable";
                    break;

                case InternetStatus.Unstable:
                    InternetStatusDot.Fill = new SolidColorBrush(Color.FromRgb(255, 193, 7));
                    InternetStatusText.Text = "Unstable";
                    InternetStatusDot.ToolTip = "Internet connection is weak or unstable";
                    break;

                case InternetStatus.NoConnection:
                    InternetStatusDot.Fill = new SolidColorBrush(Color.FromRgb(244, 67, 54));
                    InternetStatusText.Text = "Offline";
                    InternetStatusDot.ToolTip = "No internet connection";
                    break;
            }
        }

        private enum InternetStatus
        {
            Good,
            Unstable,
            NoConnection
        }

        /// <summary>
        /// Handle Scan button click - opens scan control in Dashboard
        /// </summary>
        private void ScanButton_Click(object sender, RoutedEventArgs e)
        {
            OpenScanControl();
        }

        /// <summary>
        /// Handle Header Scan button click - opens scan control in Dashboard
        /// </summary>
        private void HeaderScanButton_Click(object sender, RoutedEventArgs e)
        {
            SetSelectedButton(ScanHeaderButton);
            OpenScanControl();
        }

        /// <summary>
        /// Opens the scan control in Dashboard
        /// </summary>
        private void OpenScanControl()
        {
            try
            {
                if (MainContentHost.Content is Dashboard dashboard)
                {
                    dashboard.OpenScanControl();
                }
                else
                {
                    var newDashboard = new Dashboard();
                    LoadContent(newDashboard);
                    SetSelectedButton(DashboardButton);
                    newDashboard.OpenScanControl();
                }
                
                Logger.Log("Scan button clicked from Header");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }
    }
}
