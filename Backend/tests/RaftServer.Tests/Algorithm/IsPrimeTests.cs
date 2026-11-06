using Raft.Algorithm;
namespace RaftServer.Tests.Algorithm;

public class IsPrimeTests
{
    [Theory]
    [InlineData(2, true)]
    [InlineData(4, false)]
    [InlineData(17, true)]
    [InlineData(19, true)]
    [InlineData(21, false)]
    [InlineData(67, true)]
    [InlineData(100, false)]
    public void CheckPrime_ForVariousNums_ReturnExceptedResult(int input, bool result)
    {
        bool acutalResult = IsPrime.CheckPrime(input);
        Assert.Equal(acutalResult,result);
    }
}