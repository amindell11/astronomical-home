namespace Game.Player
{
    public interface IFlightInput
    {
        bool BoostDown { get; }
        bool PrimaryFire { get; }
        bool SecondaryFire { get; }
    }
}
