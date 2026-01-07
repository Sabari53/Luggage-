using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using UserModule.Models;

namespace UserModule.Data
{
    public static class BookingDatabase
    {
        // Store database in AppData\Local instead of Program Files to avoid permission issues
        private static string appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "Luggage", "Data");
        private static string dbPath = Path.Combine(appDataFolder, "luggage.db");
        private static string connectionString = $"Data Source={dbPath};Version=3;";

        static BookingDatabase()
        {
            // Ensure the directory exists
            Directory.CreateDirectory(appDataFolder);
            InitializeDatabase();
        }

        public static void InitializeDatabase()
        {
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }

            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                
                // Create LuggageTypes table to cache types from API
                string createLuggageTypesTable = @"
                    CREATE TABLE IF NOT EXISTS LuggageTypes (
                        Id INTEGER PRIMARY KEY,
                        TypeName TEXT NOT NULL,
                        AdminCode TEXT NOT NULL,
                        SyncedAt TEXT NOT NULL
                    );";

                // Create Lockers table to cache locker numbers from API
                string createLockersTable = @"
                    CREATE TABLE IF NOT EXISTS Lockers (
                        Id INTEGER PRIMARY KEY,
                        AdminCode TEXT NOT NULL UNIQUE,
                        StartLockerNo INTEGER NOT NULL,
                        EndLockerNo INTEGER NOT NULL,
                        TotalLockers INTEGER NOT NULL,
                        SyncedAt TEXT NOT NULL
                    );";

                // Create offline bookings table for when API is unavailable
                string createOfflineBookingsTable = @"
                    CREATE TABLE IF NOT EXISTS OfflineBookings (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        BookingId TEXT UNIQUE NOT NULL,
                        WorkerCode TEXT NOT NULL,
                        AdminCode TEXT NOT NULL,
                        LockerNumber TEXT NOT NULL,
                        UserName TEXT NOT NULL,
                        PhoneNumber TEXT NOT NULL,
                        Items TEXT NOT NULL,
                        Status TEXT DEFAULT 'pending',
                        CreatedAt TEXT NOT NULL,
                        UpdatedAt TEXT,
                        Synced INTEGER DEFAULT 0
                    );";

                // Create booking items table to store individual items with prices
                string createBookingItemsTable = @"
                    CREATE TABLE IF NOT EXISTS BookingItems (
                        Id INTEGER PRIMARY KEY AUTOINCREMENT,
                        BookingId TEXT NOT NULL,
                        TypeName TEXT NOT NULL,
                        Days INTEGER NOT NULL,
                        Rate REAL NOT NULL,
                        Quantity INTEGER DEFAULT 1,
                        FOREIGN KEY (BookingId) REFERENCES OfflineBookings(BookingId) ON DELETE CASCADE
                    );";

                using (var command = new SQLiteCommand(createLuggageTypesTable, connection))
                {
                    command.ExecuteNonQuery();
                }

                using (var command = new SQLiteCommand(createLockersTable, connection))
                {
                    command.ExecuteNonQuery();
                }

                using (var command = new SQLiteCommand(createOfflineBookingsTable, connection))
                {
                    command.ExecuteNonQuery();
                }

                using (var command = new SQLiteCommand(createBookingItemsTable, connection))
                {
                    command.ExecuteNonQuery();
                }

                // Migration: Convert LockerNumber from INTEGER to TEXT if needed
                try
                {
                    // Check if LockerNumber is INTEGER type
                    string checkType = "PRAGMA table_info(OfflineBookings)";
                    using (var cmd = new SQLiteCommand(checkType, connection))
                    using (var reader = cmd.ExecuteReader())
                    {
                        bool needsMigration = false;
                        while (reader.Read())
                        {
                            if (reader["name"].ToString() == "LockerNumber" && reader["type"].ToString() == "INTEGER")
                            {
                                needsMigration = true;
                                break;
                            }
                        }

                        if (needsMigration)
                        {
                            reader.Close();
                            // SQLite doesn't support ALTER COLUMN, so we need to recreate the table
                            string migrateSql = @"
                                -- Create temporary table with new schema
                                CREATE TABLE OfflineBookings_temp (
                                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                    BookingId TEXT UNIQUE NOT NULL,
                                    WorkerCode TEXT NOT NULL,
                                    AdminCode TEXT NOT NULL,
                                    LockerNumber TEXT NOT NULL,
                                    UserName TEXT NOT NULL,
                                    PhoneNumber TEXT NOT NULL,
                                    Items TEXT NOT NULL,
                                    Status TEXT DEFAULT 'pending',
                                    CreatedAt TEXT NOT NULL,
                                    UpdatedAt TEXT,
                                    Synced INTEGER DEFAULT 0
                                );
                                
                                -- Copy data, converting INTEGER to TEXT
                                INSERT INTO OfflineBookings_temp 
                                SELECT Id, BookingId, WorkerCode, AdminCode, 
                                       CAST(LockerNumber AS TEXT), 
                                       UserName, PhoneNumber, Items, Status, CreatedAt, UpdatedAt, Synced
                                FROM OfflineBookings;
                                
                                -- Drop old table
                                DROP TABLE OfflineBookings;
                                
                                -- Rename temp table
                                ALTER TABLE OfflineBookings_temp RENAME TO OfflineBookings;
                            ";
                            
                            using (var migrateCmd = new SQLiteCommand(migrateSql, connection))
                            {
                                migrateCmd.ExecuteNonQuery();
                            }
                            
                            System.Diagnostics.Debug.WriteLine("✓ Migrated LockerNumber column from INTEGER to TEXT");
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Migration check: {ex.Message}");
                }

                // Add Status and UpdatedAt columns if they don't exist (migration)
                try
                {
                    string addStatusColumn = "ALTER TABLE OfflineBookings ADD COLUMN Status TEXT DEFAULT 'pending'";
                    using (var cmd = new SQLiteCommand(addStatusColumn, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch { /* Column already exists */ }

                try
                {
                    string addUpdatedAtColumn = "ALTER TABLE OfflineBookings ADD COLUMN UpdatedAt TEXT";
                    using (var cmd = new SQLiteCommand(addUpdatedAtColumn, connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }
                catch { /* Column already exists */ }
            }
        }

        public static SQLiteConnection GetConnection()
        {
            return new SQLiteConnection(connectionString);
        }

        // Save luggage types to local database after login
        public static void SaveLuggageTypes(List<LuggageType> types, string adminCode)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();

                    // Clear existing types for this admin
                    string deleteQuery = "DELETE FROM LuggageTypes WHERE AdminCode = @AdminCode";
                    using (var deleteCmd = new SQLiteCommand(deleteQuery, connection))
                    {
                        deleteCmd.Parameters.AddWithValue("@AdminCode", adminCode);
                        deleteCmd.ExecuteNonQuery();
                    }

                    // Insert new types
                    string insertQuery = @"INSERT INTO LuggageTypes (Id, TypeName, AdminCode, SyncedAt) 
                                          VALUES (@Id, @TypeName, @AdminCode, @SyncedAt)";

                    foreach (var type in types)
                    {
                        using (var insertCmd = new SQLiteCommand(insertQuery, connection))
                        {
                            insertCmd.Parameters.AddWithValue("@Id", type.Id);
                            insertCmd.Parameters.AddWithValue("@TypeName", type.TypeName);
                            insertCmd.Parameters.AddWithValue("@AdminCode", adminCode);
                            insertCmd.Parameters.AddWithValue("@SyncedAt", DateTime.Now.ToString("O"));
                            insertCmd.ExecuteNonQuery();
                        }
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"✓ Saved {types.Count} luggage types to database for admin {adminCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error saving luggage types: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
                throw;
            }
        }

        // Get cached luggage types from local database
        public static List<LuggageType> GetCachedLuggageTypes(string adminCode)
        {
            var types = new List<LuggageType>();
            using (var connection = GetConnection())
            {
                connection.Open();
                string query = "SELECT Id, TypeName FROM LuggageTypes WHERE AdminCode = @AdminCode";
                
                using (var command = new SQLiteCommand(query, connection))
                {
                    command.Parameters.AddWithValue("@AdminCode", adminCode);
                    
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            types.Add(new LuggageType
                            {
                                Id = Convert.ToInt32(reader["Id"]),
                                TypeName = reader["TypeName"].ToString() ?? string.Empty
                            });
                        }
                    }
                }
            }
            return types;
        }

        // Save locker configuration to local database after login
        public static void SaveLockers(Locker locker, string adminCode)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();

                    // Delete existing lockers for this admin
                    string deleteQuery = "DELETE FROM Lockers WHERE AdminCode = @AdminCode";
                    using (var deleteCmd = new SQLiteCommand(deleteQuery, connection))
                    {
                        deleteCmd.Parameters.AddWithValue("@AdminCode", adminCode);
                        deleteCmd.ExecuteNonQuery();
                    }

                    // Insert new locker configuration
                    string insertQuery = @"INSERT INTO Lockers (Id, AdminCode, StartLockerNo, EndLockerNo, TotalLockers, SyncedAt) 
                                          VALUES (@Id, @AdminCode, @StartLockerNo, @EndLockerNo, @TotalLockers, @SyncedAt)";

                    using (var insertCmd = new SQLiteCommand(insertQuery, connection))
                    {
                        insertCmd.Parameters.AddWithValue("@Id", locker.Id);
                        insertCmd.Parameters.AddWithValue("@AdminCode", adminCode);
                        insertCmd.Parameters.AddWithValue("@StartLockerNo", locker.StartLockerNo);
                        insertCmd.Parameters.AddWithValue("@EndLockerNo", locker.EndLockerNo);
                        insertCmd.Parameters.AddWithValue("@TotalLockers", locker.TotalLockers);
                        insertCmd.Parameters.AddWithValue("@SyncedAt", DateTime.Now.ToString("O"));
                        insertCmd.ExecuteNonQuery();
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"✓ Saved lockers ({locker.StartLockerNo}-{locker.EndLockerNo}) to database for admin {adminCode}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error saving lockers: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack: {ex.StackTrace}");
                throw;
            }
        }

        // Get cached locker configuration from local database
        public static Locker? GetCachedLockers(string adminCode)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    string query = "SELECT Id, StartLockerNo, EndLockerNo, TotalLockers FROM Lockers WHERE AdminCode = @AdminCode";
                    
                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@AdminCode", adminCode);
                        
                        using (var reader = command.ExecuteReader())
                        {
                            if (reader.Read())
                            {
                                return new Locker
                                {
                                    Id = Convert.ToInt32(reader["Id"]),
                                    StartLockerNo = Convert.ToInt32(reader["StartLockerNo"]),
                                    EndLockerNo = Convert.ToInt32(reader["EndLockerNo"]),
                                    TotalLockers = Convert.ToInt32(reader["TotalLockers"])
                                };
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error getting cached lockers: {ex.Message}");
            }
            return null;
        }

        // Get all locker numbers as a list
        public static List<int> GetAvailableLockerNumbers(string adminCode)
        {
            var locker = GetCachedLockers(adminCode);
            return locker?.GetLockerNumbers() ?? new List<int>();
        }

        // Save booking items separately for easier querying and total calculation
        public static void SaveBookingItems(string bookingId, List<BookingItem> items)
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();

                    // Clear existing items for this booking
                    string deleteQuery = "DELETE FROM BookingItems WHERE BookingId = @BookingId";
                    using (var deleteCmd = new SQLiteCommand(deleteQuery, connection))
                    {
                        deleteCmd.Parameters.AddWithValue("@BookingId", bookingId);
                        deleteCmd.ExecuteNonQuery();
                    }

                    // Insert new items
                    string insertQuery = @"INSERT INTO BookingItems (BookingId, TypeName, Days, Rate, Quantity) 
                                          VALUES (@BookingId, @TypeName, @Days, @Rate, @Quantity)";

                    foreach (var item in items)
                    {
                        using (var insertCmd = new SQLiteCommand(insertQuery, connection))
                        {
                            insertCmd.Parameters.AddWithValue("@BookingId", bookingId);
                            insertCmd.Parameters.AddWithValue("@TypeName", item.TypeName);
                            insertCmd.Parameters.AddWithValue("@Days", item.Days);
                            insertCmd.Parameters.AddWithValue("@Rate", item.Rate);
                            insertCmd.Parameters.AddWithValue("@Quantity", item.Quantity);
                            insertCmd.ExecuteNonQuery();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error saving booking items: {ex.Message}");
            }
        }

        // Calculate total amount for a booking from its items
        public static decimal GetBookingTotalAmount(string bookingId)
        {
            decimal total = 0;
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Open();
                    string query = "SELECT Days, Rate, Quantity FROM BookingItems WHERE BookingId = @BookingId";

                    using (var command = new SQLiteCommand(query, connection))
                    {
                        command.Parameters.AddWithValue("@BookingId", bookingId);

                        using (var reader = command.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                int days = Convert.ToInt32(reader["Days"]);
                                decimal rate = Convert.ToDecimal(reader["Rate"]);
                                int quantity = Convert.ToInt32(reader["Quantity"]);
                                total += days * rate * quantity;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"✗ Error calculating total: {ex.Message}");
            }
            return total;
        }
    }
}
