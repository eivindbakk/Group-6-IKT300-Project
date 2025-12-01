namespace Contracts;

public enum EventType
{
    UserLoggedIn,
    Heartbeat
}

public class EventMessage
{
    public EventType Type { get; set; }
    public string Payload { get; set; } = string.Empty;
}
