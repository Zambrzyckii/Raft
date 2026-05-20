namespace Raft.Services;

public class FileStorageService
{
    private static readonly Lock _fileLock = new ();
    public void SaveToFile(int number, string whoFoundThisNumber, string timeToFindThisNumberInMs)
    {
        Console.WriteLine($"Number : {number}, {whoFoundThisNumber}, {timeToFindThisNumberInMs}");
        lock (_fileLock)
        {
            string line = $"Number {number} | Who found {whoFoundThisNumber} | Time {timeToFindThisNumberInMs}\n";
            File.AppendAllText("primes.txt", line);
        }
    }
}