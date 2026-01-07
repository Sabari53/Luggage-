using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UserModule.Models;
using UserModule.Helpers;

namespace UserModule.Services
{
    public class BookingService
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        // Worker Login
        // POST - http://localhost:PORT/api/v1/worker/login
        public async Task<WorkerLoginResponse?> LoginWorkerAsync(string username, string password)
        {
            try
            {
                var requestBody = new
                {
                    username = username,
                    password = password
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(ApiConfig.WorkerLogin, content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<WorkerLoginResponse>(responseJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Login error: {ex.Message}");
                return null;
            }
        }

        // Get Luggage Types
        // GET - http://localhost:PORT/api/v1/common/luggage-types/:adminCode
        public async Task<List<LuggageType>> GetLuggageTypesAsync(string adminCode)
        {
            try
            {
                var url = ApiConfig.GetLuggageTypes(adminCode);
                System.Diagnostics.Debug.WriteLine($"Fetching luggage types from: {url}");
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"API Response: {json}");
                
                // Deserialize the wrapped response
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<List<LuggageType>>>(json);
                
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Deserialized {apiResponse.Data.Count} luggage types");
                    return apiResponse.Data;
                }
                
                System.Diagnostics.Debug.WriteLine("API response was not successful or data was null");
                return new List<LuggageType>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get luggage types error: {ex.Message}");
                Console.WriteLine($"Get luggage types error: {ex.Message}");
                return new List<LuggageType>();
            }
        }

        // Get Lockers
        // GET - http://localhost:PORT/api/v1/common/lockers/:adminCode
        public async Task<Locker?> GetLockersAsync(string adminCode)
        {
            try
            {
                var url = ApiConfig.GetLockers(adminCode);
                System.Diagnostics.Debug.WriteLine($"Fetching lockers from: {url}");
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"API Response: {json}");
                
                // Deserialize the wrapped response
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<Locker>>(json);
                
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Lockers: {apiResponse.Data.StartLockerNo} to {apiResponse.Data.EndLockerNo}");
                    return apiResponse.Data;
                }
                
                System.Diagnostics.Debug.WriteLine("API response was not successful or data was null");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get lockers error: {ex.Message}");
                Console.WriteLine($"Get lockers error: {ex.Message}");
                return null;
            }
        }

        // Get Luggage Types with Lockers (Combined)
        // GET - http://localhost:PORT/api/v1/common/luggage-types-lockers/:adminCode
        public async Task<LuggageTypesWithLockers?> GetLuggageTypesWithLockersAsync(string adminCode)
        {
            try
            {
                var url = ApiConfig.GetLuggageTypesWithLockers(adminCode);
                System.Diagnostics.Debug.WriteLine($"Fetching luggage types with lockers from: {url}");
                
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                System.Diagnostics.Debug.WriteLine($"API Response: {json}");
                
                // Deserialize the wrapped response
                var apiResponse = JsonConvert.DeserializeObject<ApiResponse<LuggageTypesWithLockers>>(json);
                
                if (apiResponse?.Success == true && apiResponse.Data != null)
                {
                    System.Diagnostics.Debug.WriteLine($"Fetched {apiResponse.Data.LuggageTypes.Count} luggage types and {apiResponse.Data.TotalLockers} lockers");
                    return apiResponse.Data;
                }
                
                System.Diagnostics.Debug.WriteLine("API response was not successful or data was null");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Get luggage types with lockers error: {ex.Message}");
                Console.WriteLine($"Get luggage types with lockers error: {ex.Message}");
                return null;
            }
        }

        // Get Worker Bookings
        // GET - http://localhost:PORT/api/v1/common/bookings/:adminCode/:workerCode
        public async Task<List<Booking>> GetWorkerBookingsAsync(string adminCode, string workerCode, int limit = 20, string? timeFilter = null, int? cursor = null)
        {
            try
            {
                var url = ApiConfig.GetWorkerBookings(adminCode, workerCode);
                
                // Add query parameters
                var queryParams = new List<string> { $"limit={limit}" };
                if (!string.IsNullOrEmpty(timeFilter))
                    queryParams.Add($"timeFilter={timeFilter}");
                if (cursor.HasValue)
                    queryParams.Add($"cursor={cursor.Value}");

                if (queryParams.Count > 0)
                    url += "?" + string.Join("&", queryParams);

                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<Booking>>(json) ?? new List<Booking>();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Get worker bookings error: {ex.Message}");
                return new List<Booking>();
            }
        }

        // Create Booking
        // POST - http://localhost:PORT/api/v1/common/bookings
        public async Task<dynamic?> CreateBookingAsync(string workerCode, string adminCode, string lockerNumber, 
            List<BookingItem> itemsList, string userName, string phoneNumber)
        {
            try
            {
                var requestBody = new
                {
                    workerCode = workerCode,
                    adminCode = adminCode,
                    lockerNumber = lockerNumber,
                    itemsList = itemsList.Select(i => new
                    {
                        typeName = i.TypeName,
                        days = i.Days,
                        rate = i.Rate,
                        quantity = i.Quantity
                    }).ToList(),
                    userName = userName,
                    phoneNumber = phoneNumber
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync(ApiConfig.CreateBooking, content);
                response.EnsureSuccessStatusCode();

                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<dynamic>(responseJson);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Create booking error: {ex.Message}");
                return null;
            }
        }

        // Update Booking Status
        // PUT - http://localhost:PORT/api/v1/common/:bookingCode/status
        public async Task<bool> UpdateBookingStatusAsync(string bookingCode, string status)
        {
            try
            {
                var requestBody = new
                {
                    status = status.ToLower()
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = ApiConfig.UpdateBookingStatus(bookingCode);
                var response = await _httpClient.PutAsync(url, content);
                response.EnsureSuccessStatusCode();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Update booking status error: {ex.Message}");
                return false;
            }
        }

        // Update Booking Status with timeout
        public async Task<(bool success, string? errorMessage)> UpdateBookingStatusWithTimeoutAsync(string bookingCode, string status, int timeoutSeconds = 10)
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSeconds) };
                
                var requestBody = new
                {
                    status = status.ToLower()
                };

                var json = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var url = ApiConfig.UpdateBookingStatus(bookingCode);
                var response = await client.PutAsync(url, content);
                
                if (response.IsSuccessStatusCode)
                {
                    return (true, null);
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync();
                    return (false, $"Status {response.StatusCode}: {errorBody}");
                }
            }
            catch (TaskCanceledException)
            {
                return (false, "Request timeout");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }
    }
}
