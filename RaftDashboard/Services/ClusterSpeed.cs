namespace RaftDashboard.Services;

public class ClusterSpeed
{
    public ClusterSpeed(int delayMs)
    {
        DelayMs = delayMs;
    }

    public int DelayMs { get; set; }
}
