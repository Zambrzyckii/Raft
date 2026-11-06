namespace Raft.Timer;
//Currently unused, time stamps are hardcoded
public static class RandomTime
{
    public static async Task WaitRandomTime()
    {
        Random rnd = new Random();
        await Task.Delay(rnd.Next(3000, 6000));
    }
}