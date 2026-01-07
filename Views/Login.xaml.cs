using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using UserModule.Components;
using UserModule.Storage;
using UserModule.Models;
using UserModule.Services;
using UserModule.Data;

namespace UserModule.Views
{
    public partial class Login : UserControl
    {
        public event Action<string> LoginSuccess = delegate { };

        // Ensure luggage types exist locally; if missing, fetch from API and cache
        private async Task EnsureLuggageTypesCachedAsync(string adminId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(adminId)) return;

                var cached = BookingDatabase.GetCachedLuggageTypes(adminId);
                if (cached != null && cached.Count > 0) return;

                var bookingService = new BookingService();
                var luggageTypes = await bookingService.GetLuggageTypesAsync(adminId);
                if (luggageTypes != null && luggageTypes.Count > 0)
                {
                    BookingDatabase.SaveLuggageTypes(luggageTypes, adminId);
                    System.Diagnostics.Debug.WriteLine($"✓ Cached {luggageTypes.Count} luggage types after auto-login");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
            }
        }

        public Login()
        {
            InitializeComponent();

            this.KeyDown += Login_KeyDown;
            this.Focusable = true;
            this.Focus();
            // Username events
            txtUsername.GotFocus += TxtUsername_GotFocus;
            txtUsername.LostFocus += TxtUsername_LostFocus;
            txtUsername.TextChanged += TxtUsername_TextChanged;

            // Password events
            txtPassword.GotFocus += TxtPassword_GotFocus;
            txtPassword.LostFocus += TxtPassword_LostFocus;
            txtPassword.PasswordChanged += TxtPassword_PasswordChanged;

            txtPasswordVisible.GotFocus += TxtPasswordVisible_GotFocus;
            txtPasswordVisible.LostFocus += TxtPasswordVisible_LostFocus;
            txtPasswordVisible.TextChanged += TxtPasswordVisible_TextChanged;
            Loaded += Login_Loaded;
            SizeChanged += Parent_SizeChanged;
        }

        private void TxtUsername_GotFocus(object sender, RoutedEventArgs e) => usernamePlaceholder.Visibility = Visibility.Collapsed;
        private void TxtUsername_LostFocus(object sender, RoutedEventArgs e) => usernamePlaceholder.Visibility = string.IsNullOrEmpty(txtUsername.Text) ? Visibility.Visible : Visibility.Collapsed;
        private void TxtUsername_TextChanged(object sender, TextChangedEventArgs e) => usernamePlaceholder.Visibility = string.IsNullOrEmpty(txtUsername.Text) ? Visibility.Visible : Visibility.Collapsed;

        // ===== Password Placeholder =====
        private void TxtPassword_GotFocus(object sender, RoutedEventArgs e) => pwdPlaceholder.Visibility = Visibility.Collapsed;
        private void TxtPassword_LostFocus(object sender, RoutedEventArgs e)
        {
            if (txtPasswordVisible.Visibility == Visibility.Visible)
                pwdPlaceholder.Visibility = string.IsNullOrEmpty(txtPasswordVisible.Text) ? Visibility.Visible : Visibility.Collapsed;
            else
                pwdPlaceholder.Visibility = string.IsNullOrEmpty(txtPassword.Password) ? Visibility.Visible : Visibility.Collapsed;
        }
        private void TxtPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (txtPassword.Visibility == Visibility.Visible)
                pwdPlaceholder.Visibility = string.IsNullOrEmpty(txtPassword.Password) ? Visibility.Visible : Visibility.Collapsed;
        }
        private void TxtPasswordVisible_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (txtPasswordVisible.Visibility == Visibility.Visible)
                pwdPlaceholder.Visibility = string.IsNullOrEmpty(txtPasswordVisible.Text) ? Visibility.Visible : Visibility.Collapsed;
        }
        private void TxtPasswordVisible_GotFocus(object sender, RoutedEventArgs e) => pwdPlaceholder.Visibility = Visibility.Collapsed;
        private void TxtPasswordVisible_LostFocus(object sender, RoutedEventArgs e) => pwdPlaceholder.Visibility = string.IsNullOrEmpty(txtPasswordVisible.Text) ? Visibility.Visible : Visibility.Collapsed;

        // ===== Show/Hide Password =====
        private void ShowPassword_Click(object sender, MouseButtonEventArgs e)
        {
            txtPasswordVisible.Text = txtPassword.Password;
            txtPassword.Visibility = Visibility.Collapsed;
            txtPasswordVisible.Visibility = Visibility.Visible;
            pwdPlaceholder.Visibility = string.IsNullOrEmpty(txtPasswordVisible.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void HidePassword_Click(object sender, MouseButtonEventArgs e)
        {
            txtPassword.Password = txtPasswordVisible.Text;
            txtPassword.Visibility = Visibility.Visible;
            txtPasswordVisible.Visibility = Visibility.Collapsed;
            pwdPlaceholder.Visibility = string.IsNullOrEmpty(txtPassword.Password) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Login_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                string username = txtUsername.Text.Trim();
                string password = txtPasswordVisible.Visibility == Visibility.Visible
                                    ? txtPasswordVisible.Text
                                    : txtPassword.Password;

                // Case 1: Username empty → focus username box
                if (string.IsNullOrEmpty(username))
                {
                    txtUsername.Focus();
                    e.Handled = true;
                    return;
                }

                // Case 2: Password empty → focus password box
                if (string.IsNullOrEmpty(password))
                {
                    if (txtPasswordVisible.Visibility == Visibility.Visible)
                        txtPasswordVisible.Focus();
                    else
                        txtPassword.Focus();

                    e.Handled = true;
                    return;
                }

                // Case 3: Both filled → trigger login
                Login_Click(btnLogin, new RoutedEventArgs());
                e.Handled = true;
            }
        }

        private void TxtUsername_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                if (!string.IsNullOrWhiteSpace(txtUsername.Text))
                {
                    if (txtPasswordVisible.Visibility == Visibility.Visible)
                        txtPasswordVisible.Focus();
                    else
                        txtPassword.Focus();
                }
            }
        }


        // Login Button
        private async void Login_Click(object sender, RoutedEventArgs e)
        {
            string username = txtUsername.Text.Trim();
            string password = txtPasswordVisible.Visibility == Visibility.Visible
                                ? txtPasswordVisible.Text
                                : txtPassword.Password;

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                MessageBox.Show("Please enter both username and password.",
                                "Missing Fields", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Check network connection
            if (!NetworkInterface.GetIsNetworkAvailable())
            {
                MessageBox.Show("No internet connection. Login requires network access.",
                                "Network Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoaderOverlay.Visibility = Visibility.Visible;

            try
            {
                // Create BookingService instance
                var bookingService = new BookingService();
                
                // Call the new BookingService.LoginWorkerAsync
                var loginResponse = await bookingService.LoginWorkerAsync(username, password);

                if (loginResponse == null)
                {
                    MessageBox.Show("Invalid username or password.",
                                    "Login Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Save worker session using WorkerSession singleton
                WorkerSession.SetSession(
                    loginResponse.WorkerCode,
                    loginResponse.AdminCode,
                    loginResponse.WorkerName
                );

                // Save session to LocalStorage for persistence
                LocalStorage.SaveWorkerSession(
                    loginResponse.WorkerCode,
                    loginResponse.AdminCode,
                    loginResponse.WorkerName
                );

                System.Diagnostics.Debug.WriteLine($"✓ Login successful - Worker: {loginResponse.WorkerName}, WorkerCode: {loginResponse.WorkerCode}, AdminCode: {loginResponse.AdminCode}");

                // Fetch and cache luggage types and lockers from server (combined call)
                try
                {
                    var data = await bookingService.GetLuggageTypesWithLockersAsync(loginResponse.AdminCode);
                    if (data != null)
                    {
                        // Save luggage types to local database
                        if (data.LuggageTypes != null && data.LuggageTypes.Count > 0)
                        {
                            BookingDatabase.SaveLuggageTypes(data.LuggageTypes, loginResponse.AdminCode);
                            System.Diagnostics.Debug.WriteLine($"✓ Cached {data.LuggageTypes.Count} luggage types to local database");
                        }
                        
                        // Save lockers to local database
                        if (data.Lockers != null)
                        {
                            BookingDatabase.SaveLockers(data.Lockers, loginResponse.AdminCode);
                            System.Diagnostics.Debug.WriteLine($"✓ Cached lockers ({data.Lockers.StartLockerNo}-{data.Lockers.EndLockerNo}) to local database");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"⚠ Failed to fetch luggage types and lockers: {ex.Message}");
                    // Don't block login if fetch fails
                }

                LoginSuccess?.Invoke(loginResponse.WorkerName);
            }
            catch (TaskCanceledException ex)
            {
                Logger.LogError(ex);
                MessageBox.Show("The connection seems slow. Please check your internet and try again.",
                                "Slow Network", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (HttpRequestException ex)
            {
                Logger.LogError(ex);
                MessageBox.Show("Unable to reach the server. Please check your network connection and try again.",
                                "Connection Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex);
                MessageBox.Show($"An unexpected issue occurred: {ex.Message}",
                                "Login Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                LoaderOverlay.Visibility = Visibility.Collapsed;
            }
        }



        private void Login_Loaded(object sender, RoutedEventArgs e)
        {
            // Stretch to fill parent container
            if (this.Parent is FrameworkElement parent)
            {
                this.HorizontalAlignment = HorizontalAlignment.Stretch;
                this.VerticalAlignment = VerticalAlignment.Stretch;

                this.Width = parent.ActualWidth;
                this.Height = parent.ActualHeight;
                parent.SizeChanged += Parent_SizeChanged;
            }
        }

        private void Parent_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.Width = e.NewSize.Width;
            this.Height = e.NewSize.Height;
        }

        private void UpdateCardSize()
        {
            double rightColumnWidth = RootGrid.ColumnDefinitions.Count > 1 ? RootGrid.ColumnDefinitions[1].ActualWidth : RootGrid.ActualWidth;
            double containerHeight = RootGrid.ActualHeight;

            if (double.IsNaN(rightColumnWidth) || rightColumnWidth <= 0) rightColumnWidth = this.ActualWidth;
            if (double.IsNaN(containerHeight) || containerHeight <= 0) containerHeight = this.ActualHeight;

            double desiredWidth = rightColumnWidth * 0.6;
            double newWidth = Math.Max(CardBorder.MinWidth, Math.Min(CardBorder.MaxWidth, desiredWidth));
            CardBorder.Width = newWidth;
        }

        // Loding the login page and checking for saved session
        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (this.Parent is FrameworkElement parent)
            {
                this.HorizontalAlignment = HorizontalAlignment.Stretch;
                this.VerticalAlignment = VerticalAlignment.Stretch;

                this.Width = parent.ActualWidth;
                this.Height = parent.ActualHeight;

                parent.SizeChanged += Parent_SizeChanged;
            }

            string? savedWorkerId = LocalStorage.GetItem("workerId");
            string? savedAdminId = LocalStorage.GetItem("adminId");
            string? savedUsername = LocalStorage.GetItem("username");

            if (!string.IsNullOrEmpty(savedWorkerId) && !string.IsNullOrEmpty(savedAdminId) && !string.IsNullOrEmpty(savedUsername))
            {
                // Check if settings exist and are still valid (not expired)
                var settings = OfflineBookingStorage.GetSettings();
                
                // If settings are expired or missing, refetch them
                if (settings == null)
                {
                    LoaderOverlay.Visibility = Visibility.Visible;
                    await OfflineBookingStorage.FetchAndSaveWorkerSettingsAsync(savedAdminId);
                    LoaderOverlay.Visibility = Visibility.Collapsed;
                }

                // Ensure luggage types are cached locally (in case user cleared the DB)
                await EnsureLuggageTypesCachedAsync(savedAdminId);

                LoginSuccess?.Invoke(savedUsername);
            }
        }
    }
}
