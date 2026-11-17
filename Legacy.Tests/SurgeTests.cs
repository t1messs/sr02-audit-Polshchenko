namespace TaxiDispatch.Tests;

// Коефіцієнт за нестачею машин і годиною пік. Без шва Now() тест залежав би від
// години запуску (D-12): зелений о 12:00, червоний о 18:00.
public class SurgeTests
{
    private readonly DispatchStore _store = new();

    [Fact]
    public void Assign_ShortageOutsideRushHour_AddsSurgePerExtraWaitingRide()
    {
        // 10 замовлень чекають, вільних водіїв 8: 1,0 + 0,15 × 2 = 1,3.
        var sut = new FixedClockDispatcher(_store, At(12));
        int first = CreateWaiting(sut, 10);

        sut.Assign(first, 3, false);

        Assert.Equal(1.3, _store.FindRide(first).Surge, 3);
    }

    [Theory]
    [InlineData(6, 1.3)]
    [InlineData(7, 1.5)]
    [InlineData(9, 1.5)]
    [InlineData(10, 1.3)]
    [InlineData(16, 1.3)]
    [InlineData(17, 1.5)]
    [InlineData(19, 1.5)]
    [InlineData(20, 1.3)]
    public void Assign_ShortageAtHour_AddsRushHourSurge(int hour, double expected)
    {
        var sut = new FixedClockDispatcher(_store, At(hour));
        int first = CreateWaiting(sut, 10);

        sut.Assign(first, 3, false);

        Assert.Equal(expected, _store.FindRide(first).Surge, 3);
    }

    [Fact]
    public void Assign_ManyWaitingRidesInRushHour_SurgeIsCappedAt25()
    {
        var sut = new FixedClockDispatcher(_store, At(18));
        int first = CreateWaiting(sut, 40, "airport");

        sut.Assign(first, 3, false);

        Assert.Equal(2.5, _store.FindRide(first).Surge, 3);
    }

    [Fact]
    public void Assign_FixedClock_StoresAssignmentTime()
    {
        var sut = new FixedClockDispatcher(_store, At(12));
        int id = Rides.Create(sut, 2, 3);

        sut.Assign(id, 3, false);

        Assert.Equal(At(12), _store.FindRide(id).AssignedAt);
    }

    private static DateTime At(int hour) => new(2026, 11, 17, hour, 0, 0);

    private static int CreateWaiting(Dispatcher sut, int count, string zone = "center")
    {
        int first = Rides.Create(sut, 2, 3, zone: zone);
        for (int i = 1; i < count; i++)
        {
            Rides.Create(sut, 2, 3, zone: zone);
        }

        return first;
    }

    // Тестовий нащадок: перекриває шов Dispatcher.Now() фіксованим часом.
    private sealed class FixedClockDispatcher : Dispatcher
    {
        private readonly DateTime _now;

        public FixedClockDispatcher(DispatchStore store, DateTime now)
            : base(store)
        {
            _now = now;
        }

        protected override DateTime Now() => _now;
    }
}
