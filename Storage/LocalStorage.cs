using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

public class StoredItem
{
    public string? Value { get; set; }
    public DateTime? Expiry { get; set; }  // Null = never expires
}

public static class LocalStorage
{
    // Store localStorage file in AppData\Local instead of Program Files to avoid permission issues
    private static readonly string AppDataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
        "Luggage", "Data");
    private static readonly string FilePath = Path.Combine(AppDataFolder, "localstorage.json");

    private static Dictionary<string, StoredItem> data = new();

    // Storage keys for API configuration
    public const string KEY_API_PORT = "api_port";
    public const string KEY_API_BASE_URL = "api_base_url";
    
    // Storage keys for worker session
    public const string KEY_WORKER_CODE = "worker_code";
    public const string KEY_ADMIN_CODE = "admin_code";
    public const string KEY_WORKER_NAME = "worker_name";
    public const string KEY_LOGIN_TIME = "login_time";

    static LocalStorage()
    {
        // Ensure the directory exists
        Directory.CreateDirectory(AppDataFolder);
        
        if (File.Exists(FilePath))
        {
            try
            {
                string json = File.ReadAllText(FilePath);
                data = JsonConvert.DeserializeObject<Dictionary<string, StoredItem>>(json)
                       ?? new Dictionary<string, StoredItem>();
                CleanupExpired();
            }
            catch
            {
                data = new Dictionary<string, StoredItem>();
            }
        }
    }

    public static void SetItem(string key, string value, TimeSpan? expiryTime = null)
    {
        data[key] = new StoredItem
        {
            Value = value,
            Expiry = expiryTime.HasValue ? DateTime.Now.Add(expiryTime.Value) : null
        };
        SaveToFile();
    }

    public static string? GetItem(string key)
    {
        if (data.ContainsKey(key))
        {
            var item = data[key];
            if (item.Expiry == null || item.Expiry > DateTime.Now)
                return item.Value;

            // Expired → remove automatically
            data.Remove(key);
            SaveToFile();
        }
        return null;
    }

    public static void RemoveItem(string key)
    {
        if (data.ContainsKey(key))
        {
            data.Remove(key);
            SaveToFile();
        }
    }

    public static void Clear()
    {
        data.Clear();
        SaveToFile();
    }

    // Helper methods for worker session management
    public static void SaveWorkerSession(string workerCode, string adminCode, string workerName = "")
    {
        SetItem(KEY_WORKER_CODE, workerCode);
        SetItem(KEY_ADMIN_CODE, adminCode);
        SetItem(KEY_WORKER_NAME, workerName);
        SetItem(KEY_LOGIN_TIME, DateTime.Now.ToString("O"));
    }

    public static (string? workerCode, string? adminCode, string? workerName) GetWorkerSession()
    {
        return (
            GetItem(KEY_WORKER_CODE),
            GetItem(KEY_ADMIN_CODE),
            GetItem(KEY_WORKER_NAME)
        );
    }

    public static void ClearWorkerSession()
    {
        RemoveItem(KEY_WORKER_CODE);
        RemoveItem(KEY_ADMIN_CODE);
        RemoveItem(KEY_WORKER_NAME);
        RemoveItem(KEY_LOGIN_TIME);
    }

    public static bool IsWorkerLoggedIn()
    {
        if (string.IsNullOrEmpty(GetItem(KEY_WORKER_CODE)) || string.IsNullOrEmpty(GetItem(KEY_ADMIN_CODE)))
            return false;
        
        // Check if session is still valid (within 1 hour)
        return IsSessionValid();
    }
    
    public static bool IsSessionValid()
    {
        var loginTimeStr = GetItem(KEY_LOGIN_TIME);
        if (string.IsNullOrEmpty(loginTimeStr))
            return false;
        
        if (DateTime.TryParse(loginTimeStr, out DateTime loginTime))
        {
            var sessionDuration = DateTime.Now - loginTime;
            return sessionDuration.TotalHours < 1; // 1 hour session timeout
        }
        
        return false;
    }
    
    public static TimeSpan? GetRemainingSessionTime()
    {
        var loginTimeStr = GetItem(KEY_LOGIN_TIME);
        if (string.IsNullOrEmpty(loginTimeStr))
            return null;
        
        if (DateTime.TryParse(loginTimeStr, out DateTime loginTime))
        {
            var sessionDuration = DateTime.Now - loginTime;
            var remainingTime = TimeSpan.FromHours(1) - sessionDuration;
            return remainingTime.TotalSeconds > 0 ? remainingTime : TimeSpan.Zero;
        }
        
        return null;
    }

    // Helper methods for API configuration
    public static void SaveApiConfig(int port, string? baseUrl = null)
    {
        SetItem(KEY_API_PORT, port.ToString());
        if (!string.IsNullOrEmpty(baseUrl))
            SetItem(KEY_API_BASE_URL, baseUrl);
    }

    public static (int? port, string? baseUrl) GetApiConfig()
    {
        var portStr = GetItem(KEY_API_PORT);
        int? port = int.TryParse(portStr, out var p) ? p : null;
        var baseUrl = GetItem(KEY_API_BASE_URL);
        return (port, baseUrl);
    }

    private static void CleanupExpired()
    {
        bool changed = false;
        foreach (var key in new List<string>(data.Keys))
        {
            var item = data[key];
            if (item.Expiry != null && item.Expiry <= DateTime.Now)
            {
                data.Remove(key);
                changed = true;
            }
        }
        if (changed) SaveToFile();
    }

    private static void SaveToFile()
    {
        string json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(FilePath, json);
    }
}
