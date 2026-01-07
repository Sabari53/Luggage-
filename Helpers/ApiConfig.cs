using System;
using System.Configuration;

namespace UserModule.Helpers
{
    public static class ApiConfig
    {
        // Default port
        private static int _port = 3000;
        
        public static int Port 
        { 
            get => _port;
            set 
            { 
                _port = value;
                _baseUrl = $"http://localhost:{_port}";
            }
        }
        
        private static string _baseUrl = $"http://localhost:3000";
        
        public static string BaseUrl 
        { 
            get => _baseUrl;
            set => _baseUrl = value;
        }

        // API v1 base path
        public static string ApiV1 => $"{BaseUrl}/api/v1";

        // Worker Routes
        // POST - http://localhost:PORT/api/v1/worker/login
        public static string WorkerLogin => $"{ApiV1}/worker/login";
        
        // Common Routes
        // POST - http://localhost:PORT/api/v1/common/bookings
        public static string CreateBooking => $"{ApiV1}/common/bookings";
        
        // Dynamic methods for Common Routes
        // GET - http://localhost:PORT/api/v1/common/luggage-types/:adminCode
        public static string GetLuggageTypes(string adminCode) => $"{ApiV1}/common/luggage-types/{adminCode}";
        
        // GET - http://localhost:PORT/api/v1/common/lockers/:adminCode
        public static string GetLockers(string adminCode) => $"{ApiV1}/common/lockers/{adminCode}";
        
        // GET - http://localhost:PORT/api/v1/common/luggage-types-lockers/:adminCode
        public static string GetLuggageTypesWithLockers(string adminCode) => $"{ApiV1}/common/luggage-types-lockers/{adminCode}";
        
        // GET - http://localhost:PORT/api/v1/common/bookings/:adminCode/:workerCode
        public static string GetWorkerBookings(string adminCode, string workerCode) => $"{ApiV1}/common/bookings/{adminCode}/{workerCode}";
        
        // PUT - http://localhost:PORT/api/v1/common/:bookingCode/status
        public static string UpdateBookingStatus(string bookingCode) => $"{ApiV1}/common/{bookingCode}/status";

        // Set port dynamically
        public static void SetPort(int port)
        {
            Port = port;
        }
    }
}
