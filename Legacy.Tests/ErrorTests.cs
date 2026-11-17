namespace TaxiDispatch.Tests;

// Сценарії помилок: фіксують, що код робить зараз — повертає коди -1/-2 і пише
// причину в LastNote (D-09) або кидає NullReferenceException.
public class ErrorTests
{
    private readonly DispatchStore _store = new();
    private readonly Dispatcher _sut;

    public ErrorTests()
    {
        _sut = new Dispatcher(_store);
    }

    [Fact]
    public void Assign_UnknownRide_ReturnsMinus1AndNote()
    {
        int result = _sut.Assign(999_999, 3, false);

        Assert.Equal(-1, result);
        Assert.Equal("ride #999999 not found", _sut.LastNote);
    }

    [Fact]
    public void Assign_CancelledRide_ReturnsMinus2AndNote()
    {
        int id = Rides.Create(_sut, 2, 3);
        _sut.ChangeStatus(id, RideStatus.Cancelled);

        int result = _sut.Assign(id, 3, false);

        Assert.Equal(-2, result);
        Assert.Equal($"ride #{id} is Cancelled", _sut.LastNote);
    }

    [Fact]
    public void CreateRide_BlankName_ReturnsMinus1AndNote()
    {
        int result = _sut.CreateRide("  ", Rides.Phone, 1, 1, 2, 2, "ECO", false, null);

        Assert.Equal(-1, result);
        Assert.Equal("name is empty", _sut.LastNote);
    }

    [Fact]
    public void CreateRide_ShortPhone_ReturnsMinus1AndNote()
    {
        int result = _sut.CreateRide("Petro Bondar", "12-34", 1, 1, 2, 2, "ECO", false, null);

        Assert.Equal(-1, result);
        Assert.Equal("invalid phone 12-34", _sut.LastNote);
    }

    // Характеризаційний тест: null у телефоні не перевіряється, метод падає.
    [Fact]
    public void CreateRide_NullPhone_ThrowsNullReference()
    {
        Assert.Throws<NullReferenceException>(
            () => _sut.CreateRide("Olena", null!, 1, 1, 2, 2, "ECO", false, null));
    }

    [Fact]
    public void CreateRide_RawInput_IsNormalized()
    {
        int id = _sut.CreateRide("  Olena Kovalenko ", "067 321-45-67", 1.5, 1.5, 6, 4.5,
            "standard", false, null);

        Ride ride = _store.FindRide(id);
        Assert.Equal("Olena Kovalenko", ride.Passenger);
        Assert.Equal("+380673214567", ride.Phone);
        Assert.Equal("ECO", ride.CarClass);
        Assert.Equal("CENTER", ride.Zone);
    }

    [Fact]
    public void ChangeStatus_UnknownRide_ReturnsFalse()
    {
        Assert.False(_sut.ChangeStatus(999_999, RideStatus.Cancelled));
    }
}
