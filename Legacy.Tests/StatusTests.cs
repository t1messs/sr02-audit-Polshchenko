namespace TaxiDispatch.Tests;

// Зміна стану поїздки: дозволені й заборонені переходи.
public class StatusTests
{
    private readonly DispatchStore _store = new();
    private readonly Dispatcher _sut;

    public StatusTests()
    {
        _sut = new Dispatcher(_store);
    }

    [Fact]
    public void ChangeStatus_CreatedToCancelled_IsAllowedWithoutFee()
    {
        int id = Rides.Create(_sut, 2, 3);

        bool ok = _sut.ChangeStatus(id, RideStatus.Cancelled);

        Ride ride = _store.FindRide(id);
        Assert.True(ok);
        Assert.Equal(RideStatus.Cancelled, ride.Status);
        Assert.Equal(0, ride.Fare);
    }

    [Fact]
    public void ChangeStatus_CreatedToInProgress_IsRejected()
    {
        int id = Rides.Create(_sut, 2, 3);

        bool ok = _sut.ChangeStatus(id, RideStatus.InProgress);

        Assert.False(ok);
        Assert.Equal(RideStatus.Created, _store.FindRide(id).Status);
    }

    [Fact]
    public void ChangeStatus_InProgressToCancelled_IsRejected()
    {
        int id = Rides.Create(_sut, 2, 3);
        _sut.Assign(id, 3, false);
        _sut.ChangeStatus(id, RideStatus.InProgress);

        bool ok = _sut.ChangeStatus(id, RideStatus.Cancelled);

        Assert.False(ok);
        Assert.Equal(RideStatus.InProgress, _store.FindRide(id).Status);
    }

    [Fact]
    public void ChangeStatus_FullRide_CompletesWithFareAndFreesDriver()
    {
        int id = _sut.CreateRide("Olena Kovalenko", Rides.Phone, 1.5, 1.5, 6, 4.5,
            "ECO", false, "center");
        _sut.Assign(id, 3, false);

        bool started = _sut.ChangeStatus(id, RideStatus.InProgress);
        bool completed = _sut.ChangeStatus(id, RideStatus.Completed);

        Ride ride = _store.FindRide(id);
        Driver driver = _store.FindDriver(1);
        Assert.True(started);
        Assert.True(completed);
        Assert.Equal(103, ride.Fare);
        Assert.Equal("free", driver.Status);
        Assert.Equal(6, driver.X);
        Assert.Equal(4.5, driver.Y);
        Assert.Equal(1, driver.TripsToday);
        Assert.Equal(82.4, driver.EarnedToday);
    }

    // Характеризаційний тест. Коментар «водія звільняє додаток», але після скасування
    // водій лишається зайнятим (onride x1). Зафіксовано як наявну поведінку; див. TD.
    [Fact]
    public void ChangeStatus_AssignedBusinessCancelled_TakesFeeAndKeepsDriverBusy()
    {
        int id = Rides.Create(_sut, 6.5, 6.5, "BUSINESS");
        int driverId = _sut.Assign(id, 3, false);

        bool ok = _sut.ChangeStatus(id, RideStatus.Cancelled);

        Driver driver = _store.FindDriver(driverId);
        Assert.True(ok);
        Assert.Equal(7, driverId);
        Assert.Equal(80, _store.FindRide(id).Fare);
        Assert.Equal("onride", driver.Status);
        Assert.Equal(1, driver.ActiveRides);
    }
}
