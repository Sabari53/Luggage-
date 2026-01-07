using System;

namespace UserModule.Models
{
    // Worker session data after login
    public class WorkerSession
    {
        public string WorkerCode { get; set; } = string.Empty;
        public string AdminCode { get; set; } = string.Empty;
        public string WorkerName { get; set; } = string.Empty;
        public DateTime LoginTime { get; set; } = DateTime.Now;

        // Singleton instance for current session
        private static WorkerSession? _currentSession;

        public static WorkerSession? Current
        {
            get => _currentSession;
            set => _currentSession = value;
        }

        public static void SetSession(string workerCode, string adminCode, string workerName = "")
        {
            _currentSession = new WorkerSession
            {
                WorkerCode = workerCode,
                AdminCode = adminCode,
                WorkerName = workerName,
                LoginTime = DateTime.Now
            };
        }

        public static void ClearSession()
        {
            _currentSession = null;
        }

        public static bool IsLoggedIn => _currentSession != null;
    }
}
