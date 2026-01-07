using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace UserModule.Models
{
    // Matches database schema from serverdatabase.txt
    public class Booking
    {
        // Booking table fields
        public int Id { get; set; }
        public string BookingId { get; set; } = string.Empty;
        public int UserId { get; set; }
        public int WorkerId { get; set; }
        public int AdminId { get; set; }
        public string LockerNumber { get; set; } = string.Empty; // Changed to string to support multiple lockers like "1,2,3,4"
        public string Status { get; set; } = "pending";
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // User information (from users table join)
        public string UserName { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;

        // Worker information (from worker table join)
        public string WorkerName { get; set; } = string.Empty;

        // Booking items (from booking_items table)
        public List<BookingItem> Items { get; set; } = new();

        // Internal ID for pagination
        public int InternalId { get; set; }

        // UI helper
        public bool IsMatch { get; set; } = false;
    }

    // Matches booking_items table schema
    public class BookingItem
    {
        public int Id { get; set; }
        public int BookingId { get; set; }
        public string TypeName { get; set; } = string.Empty;
        public int Days { get; set; }
        public decimal Rate { get; set; }
        public int Quantity { get; set; } = 1;
    }

    // Matches types table schema
    public class LuggageType
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        
        [JsonProperty("type_name")]
        public string TypeName { get; set; } = string.Empty;
    }

    // Matches lockers table schema
    public class Locker
    {
        [JsonProperty("id")]
        public int Id { get; set; }
        
        [JsonProperty("admin_id")]
        public int AdminId { get; set; }
        
        [JsonProperty("start_locker_no")]
        public int StartLockerNo { get; set; }
        
        [JsonProperty("end_locker_no")]
        public int EndLockerNo { get; set; }
        
        [JsonProperty("total_lockers")]
        public int TotalLockers { get; set; }
        
        // Helper method to get all locker numbers as a list
        public List<int> GetLockerNumbers()
        {
            var lockers = new List<int>();
            for (int i = StartLockerNo; i <= EndLockerNo; i++)
            {
                lockers.Add(i);
            }
            return lockers;
        }
    }

    // API response wrapper
    public class ApiResponse<T>
    {
        [JsonProperty("success")]
        public bool Success { get; set; }
        
        [JsonProperty("source")]
        public string? Source { get; set; }
        
        [JsonProperty("data")]
        public T? Data { get; set; }
    }

    // Combined response for luggage types and lockers
    public class LuggageTypesWithLockers
    {
        [JsonProperty("luggageTypes")]
        public List<LuggageType> LuggageTypes { get; set; } = new();
        
        [JsonProperty("lockers")]
        public Locker? Lockers { get; set; }
        
        [JsonProperty("totalLockers")]
        public int TotalLockers { get; set; }
    }

    // Cache model for storing luggage types and total lockers locally
    public class LuggageTypesWithLockersCache
    {
        public List<LuggageType> LuggageTypes { get; set; } = new();
        public int TotalLockers { get; set; }
        public DateTime CachedAt { get; set; } = DateTime.Now;
        public string AdminCode { get; set; } = string.Empty;
    }

    // Worker login response model (from API)
    public class WorkerLoginResponse
    {
        public string WorkerCode { get; set; } = string.Empty;
        public string AdminCode { get; set; } = string.Empty;
        public string WorkerName { get; set; } = string.Empty;
    }

    // Backward compatibility class for old views (Dashboard, SimpleScanControl, etc.)
    // TODO: Update views to use new Booking model
    public class Booking1
    {
        public string? booking_id { get; set; }
        public string? user_id { get; set; }
        public string? admin_id { get; set; }
        public string? worker_id { get; set; }
        public string? guest_name { get; set; }
        public string? phone_number { get; set; }
        public int number_of_persons { get; set; }
        public string? booking_type { get; set; }
        public int total_hours { get; set; }
        public DateTime booking_date { get; set; } = DateTime.Now;
        public TimeSpan in_time { get; set; }
        public TimeSpan? out_time { get; set; }
        public string? proof_type { get; set; }
        public string? proof_id { get; set; }
        public decimal price_per_person { get; set; }
        public decimal total_amount { get; set; }
        public decimal paid_amount { get; set; }
        public decimal balance_amount { get; set; }
        public string? payment_method { get; set; }
        public DateTime? created_at { get; set; }
        public DateTime? updated_at { get; set; }
        public string? status { get; set; }
        public string? online_booking_id { get; set; }
        
        // Additional property for compatibility
        public string? Status 
        { 
            get => status; 
            set => status = value; 
        }
    }
}
