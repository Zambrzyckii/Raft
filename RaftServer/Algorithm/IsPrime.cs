namespace Raft.Algorithm;

public static class IsPrime
{
    public static bool CheckPrime(int number)
    {
        if (number <= 1) return false;
        if (number == 2) return true;
        for (int i = 2; i < (int)(Math.Sqrt(number) + 1); i++)
        {
            if (number % i == 0) return false;
        }

        return true;
    }
}