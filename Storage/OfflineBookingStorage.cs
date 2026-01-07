using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UserModule.Models;
using UserModule.Helpers;
using UserModule.Services;
using UserModule.Data;

namespace UserModule.Storage
{
    public static class OfflineBookingStorage
    {
        // Store database in AppData\Local instead of Program Files to avoid permission issues
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "Luggage", "Data");
        private static readonly string DbPath = Path.Combine(AppDataFolder, "luggage.db");
        private static readonly string connectionString = $"Data Source={DbPath};Version=3;";
        private static readonly BookingService _bookingService = new BookingService();

        static OfflineBookingStorage()
        {
            // Ensure the directory exists
            Directory.CreateDirectory(AppDataFolder);
        }

        // Save booking offline when API is unavailable (Synced = 0)
        // Uses OfflineBookings table from BookingDatabase.cs
        public static void SaveOfflineBooking(string bookingId, string workerCode, string adminCode, 
            string lockerNumber, string userName, string phoneNumber, List<BookingItem> items, string status = "pending")
        {
            using var connection = new SQLiteConnection($"Data Source={DbPath}");
            connection.Open();

            string insert = @"
                INSERT INTO OfflineBookings 
                (BookingId, WorkerCode, AdminCode, LockerNumber, UserName, PhoneNumber, Items, Status, CreatedAt, Synced)
                VALUES (@BookingId, @WorkerCode, @AdminCode, @LockerNumber, @UserName, @PhoneNumber, @Items, @Status, @CreatedAt, 0)";

            using var cmd = new SQLiteCommand(insert, connection);
            cmd.Parameters.AddWithValue("@BookingId", bookingId);
            cmd.Parameters.AddWithValue("@WorkerCode", workerCode);
            cmd.Parameters.AddWithValue("@AdminCode", adminCode);
            cmd.Parameters.AddWithValue("@LockerNumber", lockerNumber);
            cmd.Parameters.AddWithValue("@UserName", userName);
            cmd.Parameters.AddWithValue("@PhoneNumber", phoneNumber);
            cmd.Parameters.AddWithValue("@Items", JsonConvert.SerializeObject(items));
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("O"));
            cmd.ExecuteNonQuery();

            // Also save items to BookingItems table for easier querying
            BookingDatabase.SaveBookingItems(bookingId, items);
        }

        // Save booking as already synced (Synced = 1) - called after successful online save
        // Uses INSERT OR REPLACE to prevent duplicates
        private static void SaveBookingAsSynced(string bookingId, string workerCode, string adminCode, 
            string lockerNumber, string userName, string phoneNumber, List<BookingItem> items, string status = "pending")
        {
            using var connection = new SQLiteConnection($"Data Source={DbPath}");
            connection.Open();

            string insert = @"
                INSERT OR REPLACE INTO OfflineBookings 
                (BookingId, WorkerCode, AdminCode, LockerNumber, UserName, PhoneNumber, Items, Status, CreatedAt, Synced)
                VALUES (@BookingId, @WorkerCode, @AdminCode, @LockerNumber, @UserName, @PhoneNumber, @Items, @Status, @CreatedAt, 1)";

            using var cmd = new SQLiteCommand(insert, connection);
            cmd.Parameters.AddWithValue("@BookingId", bookingId);
            cmd.Parameters.AddWithValue("@WorkerCode", workerCode ?? LocalStorage.GetItem(LocalStorage.KEY_WORKER_CODE) ?? "");
            cmd.Parameters.AddWithValue("@AdminCode", adminCode ?? LocalStorage.GetItem(LocalStorage.KEY_ADMIN_CODE) ?? "");
            cmd.Parameters.AddWithValue("@LockerNumber", lockerNumber);
            cmd.Parameters.AddWithValue("@UserName", userName ?? "");
            cmd.Parameters.AddWithValue("@PhoneNumber", phoneNumber ?? "");
            cmd.Parameters.AddWithValue("@Items", JsonConvert.SerializeObject(items));
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@CreatedAt", DateTime.Now.ToString("O"));
            cmd.ExecuteNonQuery();

            // Also save items to BookingItems table for easier querying
            BookingDatabase.SaveBookingItems(bookingId, items);
        }

        // Save booking with online-first approach
        // POST - http://localhost:PORT/api/v1/common/bookings
        public static async Task<(bool success, string? bookingId)> SaveBookingAsync(
            string workerCode, string adminCode, string lockerNumber, 
            string userName, string phoneNumber, List<BookingItem> items, bool showMessages = true)
        {
            // Get codes from LocalStorage if not provided
            if (string.IsNullOrEmpty(workerCode))
                workerCode = LocalStorage.GetItem(LocalStorage.KEY_WORKER_CODE) ?? string.Empty;
            
            if (string.IsNullOrEmpty(adminCode))
                adminCode = LocalStorage.GetItem(LocalStorage.KEY_ADMIN_CODE) ?? string.Empty;

            // Check if network is available
            bool isOnline = NetworkInterface.GetIsNetworkAvailable();

            if (isOnline)
            {
                try
                {
                    // Try to save directly to API with timeout
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    
                    // Transform to API format
                    var apiData = new
                    {
                        workerCode = workerCode,
                        adminCode = adminCode,
                        lockerNumber = lockerNumber,
                        itemsList = items.Select(i => new
                        {
                            typeName = i.TypeName,
                            days = i.Days,
                            rate = i.Rate,
                            quantity = i.Quantity
                        }).ToList(),
                        userName = userName,
                        phoneNumber = phoneNumber
                    };
                    
                    var json = JsonConvert.SerializeObject(apiData);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync(ApiConfig.CreateBooking, content);
                    
                    if (response.IsSuccessStatusCode)
                    {
                        string responseBody = await response.Content.ReadAsStringAsync();
                        System.Diagnostics.Debug.WriteLine($"API Response: {responseBody}");
                        
                        // Extract online booking ID from wrapped response
                        string onlineBookingId = string.Empty;
                        try
                        {
                            var apiResponse = JsonConvert.DeserializeObject<ApiResponse<dynamic>>(responseBody);
                            if (apiResponse?.Success == true && apiResponse.Data != null)
                            {
#pragma warning disable CS8602 // Dereference of a possibly null reference - Data null checked above
                                var bookingIdValue = apiResponse.Data.bookingId;
#pragma warning restore CS8602
                                onlineBookingId = bookingIdValue?.ToString() ?? string.Empty;
                                System.Diagnostics.Debug.WriteLine($"✓ Online booking ID: {onlineBookingId}");
                            }
                        }
                        catch (Exception parseEx)
                        {
                            System.Diagnostics.Debug.WriteLine($"Error parsing response: {parseEx.Message}");
                        }
                        
                        // Save to local database even when synced online
                        SaveBookingAsSynced(
                            string.IsNullOrEmpty(onlineBookingId) ? GenerateOfflineBookingId(workerCode, adminCode) : onlineBookingId,
                            workerCode, adminCode, lockerNumber, userName, phoneNumber, items);
                        
                        if (showMessages)
                        {
                            System.Windows.MessageBox.Show($"✅ Booking created successfully!\n\nBooking ID: {onlineBookingId}", 
                                "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                        }
                        
                        return (true, onlineBookingId);
                    }
                    else
                    {
                        // API rejected, save offline
                        string responseBody = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"API rejected booking: {response.StatusCode} - {responseBody}");
                        
                        string offlineId = GenerateOfflineBookingId(workerCode, adminCode);
                        SaveOfflineBooking(offlineId, workerCode, adminCode, lockerNumber, userName, phoneNumber, items);
                        
                        if (showMessages)
                        {
                            System.Windows.MessageBox.Show($"Booking saved locally.\nWill sync when connection is restored.", 
                                "Saved Offline", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                        }
                        
                        return (false, offlineId);
                    }
                }
                catch (Exception ex)
                {
                    // Network error, save offline
                    Console.WriteLine($"Error saving booking online: {ex.Message}");
                    
                    string offlineId = GenerateOfflineBookingId(workerCode, adminCode);
                    SaveOfflineBooking(offlineId, workerCode, adminCode, lockerNumber, userName, phoneNumber, items);
                    
                    if (showMessages)
                    {
                        System.Windows.MessageBox.Show($"Booking saved locally.\nWill sync when connection is restored.", 
                            "Saved Offline", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                    }
                    
                    return (false, offlineId);
                }
            }
            else
            {
                // No network, save offline
                string offlineId = GenerateOfflineBookingId(workerCode, adminCode);
                SaveOfflineBooking(offlineId, workerCode, adminCode, lockerNumber, userName, phoneNumber, items);
                
                if (showMessages)
                {
                    System.Windows.MessageBox.Show("📴 No internet connection. Booking saved locally.\nWill sync when connection is restored.", 
                        "Saved Offline", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                
                return (false, offlineId);
            }
        }

        // Generate offline booking ID (temporary until synced with server)
        private static string GenerateOfflineBookingId(string workerCode, string adminCode)
        {
            // Use same format as online bookings - tracked via Synced column instead
            var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            var random = new Random().Next(1000, 9999);
            return $"B{timestamp}{random}";
        }

        // Update booking status in local database
        public static async Task UpdateBookingStatusLocalAsync(string bookingId, string status)
        {
            await Task.Run(() =>
            {
                try
                {
                    using var connection = new SQLiteConnection($"Data Source={DbPath}");
                    connection.Open();

                    string updateQuery = @"
                        UPDATE OfflineBookings 
                        SET Status = @Status, 
                            UpdatedAt = @UpdatedAt,
                            Synced = CASE WHEN Synced = 1 THEN 1 ELSE 2 END
                        WHERE BookingId = @BookingId";

                    using var cmd = new SQLiteCommand(updateQuery, connection);
                    cmd.Parameters.AddWithValue("@Status", status);
                    cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now.ToString("O"));
                    cmd.Parameters.AddWithValue("@BookingId", bookingId);
                    
                    int rowsAffected = cmd.ExecuteNonQuery();
                    
                    if (rowsAffected > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"✓ Updated booking {bookingId} status to {status} in local DB");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"✗ Error updating booking status locally: {ex.Message}");
                    throw;
                }
            });
        }

        // Get count of unsynced offline bookings (async version)
        public static async Task<int> GetPendingSyncCountAsync()
        {
            return await Task.Run(() =>
            {
                using var connection = new SQLiteConnection($"Data Source={DbPath}");
                connection.Open();

                // Count both Synced = 0 (new/offline bookings)
                string countQuery = "SELECT COUNT(*) FROM OfflineBookings WHERE Synced = 0";
                using var cmd = new SQLiteCommand(countQuery, connection);
                var result = cmd.ExecuteScalar();
                return result != null ? Convert.ToInt32(result) : 0;
            });
        }

        // Get count of unsynced offline bookings (sync version)
        public static int GetPendingSyncCount()
        {
            using var connection = new SQLiteConnection($"Data Source={DbPath}");
            connection.Open();

            string countQuery = "SELECT COUNT(*) FROM OfflineBookings WHERE Synced = 0";
            using var cmd = new SQLiteCommand(countQuery, connection);
            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }

        // Get all unsynced offline bookings
        public static List<(int id, string bookingId, string workerCode, string adminCode, 
            string lockerNumber, string userName, string phoneNumber, List<BookingItem> items)> GetUnsyncedBookings()
        {
            var bookings = new List<(int, string, string, string, string, string, string, List<BookingItem>)>();
            
            using var connection = new SQLiteConnection($"Data Source={DbPath}");
            connection.Open();

            string select = "SELECT * FROM OfflineBookings WHERE Synced = 0";
            
            using var cmd = new SQLiteCommand(select, connection);
            using var reader = cmd.ExecuteReader();
            
            while (reader.Read())
            {
                var items = JsonConvert.DeserializeObject<List<BookingItem>>(reader["Items"].ToString() ?? "[]") 
                    ?? new List<BookingItem>();

                bookings.Add((
                    Convert.ToInt32(reader["Id"]),
                    reader["BookingId"].ToString() ?? string.Empty,
                    reader["WorkerCode"].ToString() ?? string.Empty,
                    reader["AdminCode"].ToString() ?? string.Empty,
                    reader["LockerNumber"].ToString() ?? string.Empty,
                    reader["UserName"].ToString() ?? string.Empty,
                    reader["PhoneNumber"].ToString() ?? string.Empty,
                    items
                ));
            }

            return bookings;
        }

        // Sync all offline bookings to server
        public static async Task<(int synced, int failed)> SyncAllOfflineBookingsAsync(bool showMessages = true)
        {
            var unsyncedBookings = GetUnsyncedBookings();
            int syncedCount = 0;
            int failedCount = 0;

            foreach (var booking in unsyncedBookings)
            {
                try
                {
                    var result = await _bookingService.CreateBookingAsync(
                        booking.workerCode, booking.adminCode, booking.lockerNumber, 
                        booking.items, booking.userName, booking.phoneNumber);

                    if (result != null)
                    {
                        // Mark as synced
                        MarkBookingAsSynced(booking.id);
                        syncedCount++;
                    }
                    else
                    {
                        failedCount++;
                    }
                }
                catch
                {
                    failedCount++;
                }
            }

            if (showMessages && (syncedCount > 0 || failedCount > 0))
            {
                System.Windows.MessageBox.Show($"Synced {syncedCount} booking(s).{Environment.NewLine}Failed: {failedCount}", 
                    "Sync Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }

            return (syncedCount, failedCount);
        }

        // Mark offline booking as synced
        private static void MarkBookingAsSynced(int id)
        {
            using var connection = new SQLiteConnection($"Data Source={DbPath}");
            connection.Open();

            string update = "UPDATE OfflineBookings SET Synced = 1 WHERE Id = @Id";
            using var cmd = new SQLiteCommand(update, connection);
            cmd.Parameters.AddWithValue("@Id", id);
            cmd.ExecuteNonQuery();
        }

        // Get all bookings from local database (both synced and unsynced)
        public static List<(int id, string bookingId, string workerCode, string adminCode, 
            string lockerNumber, string userName, string phoneNumber, List<BookingItem> items, int synced)> GetAllLocalBookings()
        {
            var bookings = new List<(int, string, string, string, string, string, string, List<BookingItem>, int)>();
            
            using var connection = new SQLiteConnection($"Data Source={DbPath}");
            connection.Open();

            string select = "SELECT * FROM OfflineBookings ORDER BY CreatedAt DESC";
            
            using var cmd = new SQLiteCommand(select, connection);
            using var reader = cmd.ExecuteReader();
            
            while (reader.Read())
            {
                var items = JsonConvert.DeserializeObject<List<BookingItem>>(reader["Items"].ToString() ?? "[]") 
                    ?? new List<BookingItem>();

                bookings.Add((
                    Convert.ToInt32(reader["Id"]),
                    reader["BookingId"].ToString() ?? string.Empty,
                    reader["WorkerCode"].ToString() ?? string.Empty,
                    reader["AdminCode"].ToString() ?? string.Empty,
                    reader["LockerNumber"].ToString() ?? string.Empty,
                    reader["UserName"].ToString() ?? string.Empty,
                    reader["PhoneNumber"].ToString() ?? string.Empty,
                    items,
                    Convert.ToInt32(reader["Synced"])
                ));
            }

            return bookings;
        }

        // Update booking status in local database
        public static void UpdateLocalBookingStatus(string bookingId, string status)
        {
            using var connection = new SQLiteConnection($"Data Source={DbPath}");
            connection.Open();

            string update = @"UPDATE OfflineBookings 
                             SET Status = @Status, UpdatedAt = @UpdatedAt, Synced = 2 
                             WHERE BookingId = @BookingId";
            
            using var cmd = new SQLiteCommand(update, connection);
            cmd.Parameters.AddWithValue("@BookingId", bookingId);
            cmd.Parameters.AddWithValue("@Status", status);
            cmd.Parameters.AddWithValue("@UpdatedAt", DateTime.Now.ToString("O"));
            cmd.ExecuteNonQuery();
            
            Console.WriteLine($"Status updated locally for booking {bookingId}: {status} (marked for sync)");
        }

        // Sync status updates to server (Synced = 2 means status changed but not synced)
        // Renamed to match old code expectations
        public static async Task<(int synced, int failed)> SyncUpdatedBookingsAsync(bool showMessages = true)
        {
            return await SyncStatusUpdatesAsync(showMessages);
        }

        // Internal method for syncing status updates
        private static async Task<(int synced, int failed)> SyncStatusUpdatesAsync(bool showMessages = true)
        {
            var bookingsToUpdate = new List<(string bookingId, string status)>();
            
            using (var connection = new SQLiteConnection($"Data Source={DbPath}"))
            {
                connection.Open();
                string select = "SELECT BookingId, Status FROM OfflineBookings WHERE Synced = 2";
                
                using var cmd = new SQLiteCommand(select, connection);
                using var reader = cmd.ExecuteReader();
                
                while (reader.Read())
                {
                    bookingsToUpdate.Add((
                        reader["BookingId"].ToString() ?? string.Empty,
                        reader["Status"].ToString() ?? "pending"
                    ));
                }
            }

            if (bookingsToUpdate.Count == 0)
                return (0, 0);

            int syncedCount = 0;
            int failedCount = 0;

            foreach (var (bookingId, status) in bookingsToUpdate)
            {
                try
                {
                    var result = await _bookingService.UpdateBookingStatusWithTimeoutAsync(bookingId, status);

                    if (result.success)
                    {
                        // Mark as synced (Synced = 1)
                        using var connection = new SQLiteConnection($"Data Source={DbPath}");
                        connection.Open();
                        
                        string update = "UPDATE OfflineBookings SET Synced = 1 WHERE BookingId = @BookingId";
                        using var cmd = new SQLiteCommand(update, connection);
                        cmd.Parameters.AddWithValue("@BookingId", bookingId);
                        cmd.ExecuteNonQuery();
                        
                        syncedCount++;
                        Console.WriteLine($"Successfully synced status update for {bookingId}");
                    }
                    else
                    {
                        failedCount++;
                        Console.WriteLine($"Failed to sync status for {bookingId}: {result.errorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    failedCount++;
                    Console.WriteLine($"Error syncing status for {bookingId}: {ex.Message}");
                }

                // Small delay between requests
                await Task.Delay(500);
            }

            if (showMessages && syncedCount > 0)
            {
                System.Windows.MessageBox.Show($"Synced {syncedCount} status update(s).\nFailed: {failedCount}", 
                    "Sync Complete", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }

            return (syncedCount, failedCount);
        }

        // Get count of pending status updates (Synced = 2)
        public static int GetPendingStatusUpdateCount()
        {
            using var connection = new SQLiteConnection($"Data Source={DbPath}");
            connection.Open();

            string countQuery = "SELECT COUNT(*) FROM OfflineBookings WHERE Synced = 2";
            using var cmd = new SQLiteCommand(countQuery, connection);
            var result = cmd.ExecuteScalar();
            return result != null ? Convert.ToInt32(result) : 0;
        }
        // Legacy methods for backward compatibility with old views
        // TODO: Update views to use new API-based methods

        public static Booking1? GetBookingById(string bookingId)
        {
            // Placeholder - old views use this
            // Returns null as this should fetch from API in new system
            return null;
        }

        public static List<Booking1> GetBasicBookings()
        {
            var bookings = new List<Booking1>();
            
            try
            {
                using (var connection = new SQLiteConnection(connectionString))
                {
                    connection.Open();
                    
                    string query = @"SELECT 
                        BookingId, WorkerCode, AdminCode, LockerNumber, 
                        UserName, PhoneNumber, Items, Status, 
                        CreatedAt, UpdatedAt, Synced
                    FROM OfflineBookings 
                    ORDER BY CreatedAt DESC";
                    
                    using (var command = new SQLiteCommand(query, connection))
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var booking = new Booking1
                            {
                                booking_id = reader["BookingId"].ToString(),
                                worker_id = reader["WorkerCode"].ToString(),
                                admin_id = reader["AdminCode"].ToString(),
                                guest_name = reader["UserName"].ToString(),
                                phone_number = reader["PhoneNumber"].ToString(),
                                status = reader["Status"].ToString(),
                                created_at = DateTime.TryParse(reader["CreatedAt"].ToString(), out var createdAt) ? createdAt : DateTime.Now,
                                updated_at = DateTime.TryParse(reader["UpdatedAt"].ToString(), out var updatedAt) ? (DateTime?)updatedAt : null,
                                // Store locker numbers in booking_type field for dashboard display
                                booking_type = reader["LockerNumber"].ToString(),
                                number_of_persons = 0, // Not used for luggage system
                                // Calculate total amount from BookingItems table
                                total_amount = BookingDatabase.GetBookingTotalAmount(reader["BookingId"].ToString() ?? ""),
                                in_time = createdAt.TimeOfDay,
                                booking_date = createdAt.Date
                            };
                            
                            bookings.Add(booking);
                        }
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"✓ Loaded {bookings.Count} bookings from local database");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error loading bookings from database: {ex.Message}");
                Logger.LogError(ex);
            }
            
            return bookings;
        }

        public static dynamic? GetSettings()
        {
            // Placeholder - old views use this  
            // Returns null as settings should come from API in new system
            return null;
        }

        public static async Task<bool> FetchAndSaveWorkerSettingsAsync(string adminId)
        {
            // Placeholder - old views use this
            // In new system, luggage types are fetched via BookingService.GetLuggageTypesAsync
            return true;
        }

        public static async Task MarkBookingAsCompletedAsync(string bookingId)
        {
            // Placeholder - use UpdateLocalBookingStatus instead
            UpdateLocalBookingStatus(bookingId, "completed");
            await Task.CompletedTask;
        }

        public static async Task SyncSingleBookingAsync(string bookingId)
        {
            // Placeholder - sync single booking
            await Task.CompletedTask;
        }

        public static List<dynamic> GetBookingTypes()
        {
            // Placeholder - return empty list
            // Use BookingService.GetLuggageTypesAsync instead
            return new List<dynamic>();
        }

        public static void ShowAllBookingsData()
        {
            // Placeholder - debug method
            Console.WriteLine("ShowAllBookingsData called");
        }

        public static async Task<bool> SyncSingleBookingToServer(Booking1 booking)
        {
            // Placeholder - return false
            await Task.CompletedTask;
            return false;
        }

        public static async Task<string> CompleteBookingWithPaymentAsync(
            string bookingId, decimal totalAmount, decimal paidAmount, string paymentMethod)
        {
            // Placeholder - update status and return message
            UpdateLocalBookingStatus(bookingId, "completed");
            await Task.CompletedTask;
            return "Booking completed offline. Will sync when online.";
        }

        public static async Task<string> CompleteBookingWithOvertimeAsync(
            string bookingId, int overtimeHours, decimal overtimeCost, decimal totalAmount, decimal paidAmount, string paymentMethod)
        {
            // Placeholder - update status and return message
            UpdateLocalBookingStatus(bookingId, "completed");
            await Task.CompletedTask;
            return "Booking completed offline. Will sync when online.";
        }    }
}
