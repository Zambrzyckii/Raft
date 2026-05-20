namespace Raft.Timer;
//Currently unused, time stamps are hardcoded
public static class RandomTime
{
    public static async Task WaitRandomTime()
    {
        Random rnd = new Random();
        await Task.Delay(rnd.Next(100, 300));
    }
}