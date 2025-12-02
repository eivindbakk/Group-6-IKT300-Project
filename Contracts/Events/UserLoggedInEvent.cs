using System;

namespace Contracts.Events
{
    /// <summary>
    /// Event raised when a user logs in (required by assignment).
    /// </summary>
    public class UserLoggedInEvent
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public DateTime LoginTime { get; set; }
        public string IpAddress { get; set; }
        public string SessionId { get; set; }

        public UserLoggedInEvent()
        {
            LoginTime = DateTime.UtcNow;
            SessionId = Guid. NewGuid().ToString();
        }
    }
}