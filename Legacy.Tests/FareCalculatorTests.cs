namespace TaxiDispatch.Tests;

// Тариф, штраф і виплата водію (FareCalculator).
public class FareCalculatorTests
{
    private readonly FareCalculator _sut = new();

    private static Ride Ride(string carClass, double surge = 1.0, bool seat = false, int waitMin = 0)
    {
        var assigned = new DateTime(2026, 11, 17, 10, 0, 0);
        return new Ride
        {
            CarClass = carClass,
            PickupX = 0,
            PickupY = 0,
            DropX = 3,
            DropY = 4,
            Surge = surge,
            ChildSeat = seat,
            AssignedAt = assigned,
            StartedAt = assigned.AddMinutes(waitMin),
            Status = RideStatus.Assigned,
        };
    }

    [Fact]
    public void Calc_VanWithSeatSurgeAndWaiting_Returns199()
    {
        // 5 км × 1,3 = 6,5 км; 40 + 6,5 × 14 = 131; × 1,2 = 157,2; + 30 крісло; + 4 хв × 3.
        Assert.Equal(199, _sut.Calc(Ride("VAN", 1.2, seat: true, waitMin: 7)));
    }

    [Fact]
    public void Calc_ShortRide_ReturnsMinimumFare()
    {
        Ride ride = Ride("ECO");
        ride.DropX = 1;
        ride.DropY = 0;

        Assert.Equal(70, _sut.Calc(ride));
    }

    // Характеризаційний тест: 98,5 грн округлюється до 98 — Math.Round за замовчуванням
    // округлює до парного, а не як каса (D-16).
    [Fact]
    public void Calc_HalfHryvnia_RoundsToEven()
    {
        Assert.Equal(98, _sut.Calc(Ride("ECO")));
    }

    [Fact]
    public void Calc_WaitingExactly3Minutes_HasNoWaitingFee()
    {
        // 40 + 6,5 × 18,5 = 160,25 → 160
        Assert.Equal(160, _sut.Calc(Ride("BUSINESS", waitMin: 3)));
    }

    [Fact]
    public void Calc_UnknownClass_UsesEcoRate()
    {
        Assert.Equal(98, _sut.Calc(Ride("MOON")));
    }

    [Theory]
    [InlineData("COMFORT", 5.0, 100)]
    [InlineData("ECO", 1.0, 70)]
    [InlineData("BUSINESS", 10.0, 225)]
    public void Estimate_ClassAndKm_ReturnsCapturedValue(string carClass, double km, double expected)
    {
        Assert.Equal(expected, _sut.Estimate(carClass, km));
    }

    [Theory]
    [InlineData(RideStatus.Assigned, "ECO", 40)]
    [InlineData(RideStatus.Assigned, "VAN", 80)]
    [InlineData(RideStatus.Created, "ECO", 0)]
    public void CancelFee_StatusAndClass_ReturnsCapturedValue(
        RideStatus status, string carClass, double expected)
    {
        Ride ride = Ride(carClass);
        ride.Status = status;

        Assert.Equal(expected, _sut.CancelFee(ride));
    }

    [Theory]
    [InlineData(4.9, 12, "COMFORT", 100, 85)]
    [InlineData(4.9, 0, "BUSINESS", 100, 75)]
    [InlineData(4.8, 0, "ECO", 103, 82.4)]
    public void DriverPayout_RatingTripsClass_ReturnsCapturedValue(
        double rating, int trips, string carClass, double fare, double expected)
    {
        var driver = new Driver { Rating = rating, TripsToday = trips, CarClass = carClass };

        Assert.Equal(expected, _sut.DriverPayout(driver, fare));
    }
}
