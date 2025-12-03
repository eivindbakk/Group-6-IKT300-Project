using System;

namespace Contracts.Events
{
    /// <summary>
    /// Event raised when a user logs in.
    /// </summary>
    public class UserLoggedInEvent
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public DateTime LoginTime { get; set; }
        public string IpAddress { get; set; }
        
        // Unique session identifier for tracking
        public string SessionId { get; set; }

        public UserLoggedInEvent()
        {
            // Auto-set login time and generate session ID
            LoginTime = DateTime. UtcNow;
            SessionId = Guid.NewGuid().ToString();
        }
    }
}