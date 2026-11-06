namespace Raft.Algorithm;

public static class IsPrime
{
    public static bool CheckPrime(int number)
    {
        if (number <= 1) return false;
        if (number <= 3) return true;
        if (number % 2 == 0 || number % 3 == 0) return false;
        int limit = (int)Math.Sqrt(number) + 1;
        for (int i = 6; i <= limit; i+=6)
        {
            if (number % (i - 1) == 0 || number % (i + 1) == 0) return false;
        }

        return true;
    }
}