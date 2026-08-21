using GameShared.Random;

namespace DragonBound.Foundation.Contracts
{
    public interface IRunRandomProvider
    {
        IRunRandom Random { get; }
    }
}
