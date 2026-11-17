// Код має глобальний змінюваний стан (DispatchStore.nextRideId, ZoneSurge — дефект D-13),
// тому тести виконуються послідовно, а Id поїздок беруться з результату CreateRide.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace TaxiDispatch.Tests;

internal static class Rides
{
    public const string Phone = "0671234567";

    public static int Create(
        Dispatcher sut,
        double px,
        double py,
        string carClass = "ECO",
        bool childSeat = false,
        string? zone = "center")
    {
        return sut.CreateRide("Test Passenger", Phone, px, py, px + 3, py + 4,
            carClass, childSeat, zone);
    }
}
