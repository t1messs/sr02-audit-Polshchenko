namespace TaxiDispatch.Tests;

// Розрахунковий вузол варіанта 13: Dispatcher.Assign — вибір водія.
// Характеризаційні тести: очікування — фактична поведінка коду на тезі baseline.
public class AssignTests
{
    private readonly DispatchStore _store = new();
    private readonly Dispatcher _sut;

    public AssignTests()
    {
        _sut = new Dispatcher(_store);
    }

    [Fact]
    public void Assign_EcoRideNearIvan_ReturnsNearestEcoDriver()
    {
        // arrange
        int id = Rides.Create(_sut, 1.5, 1.5);

        // act
        int driverId = _sut.Assign(id, 3, false);

        // assert
        Ride ride = _store.FindRide(id);
        Driver driver = _store.FindDriver(1);
        Assert.Equal(1, driverId);
        Assert.Equal(RideStatus.Assigned, ride.Status);
        Assert.Equal(1.6, ride.PickupKm);
        Assert.Equal("onride", driver.Status);
        Assert.Equal(1, driver.ActiveRides);
    }

    [Fact]
    public void Assign_DriverExactlyAtMaxKm_IsChosen()
    {
        int id = Rides.Create(_sut, 2, 0);

        int driverId = _sut.Assign(id, 3, false);

        Assert.Equal(1, driverId);
        Assert.Equal(3.0, _store.FindRide(id).PickupKm);
    }

    // Характеризаційний тест. Коментар у коді обіцяє «мін. рейтинг 4.0», але умова
    // Rating <= minRating відкидає водія з рейтингом рівно 4.0 (Oleksii, стоїть у точці
    // подачі). Зафіксовано як наявна поведінку; див. D-14 і TD у реєстрі боргу.
    [Fact]
    public void Assign_EcoDriverWithRatingExactly4_IsSkipped()
    {
        int id = Rides.Create(_sut, 1, 1);

        int result = _sut.Assign(id, 0.5, false);

        Assert.Equal(-3, result);
    }

    // Характеризаційний тест. «Бізнес — від 4.7 включно», а Andrii Melnyk (4.7) у точці
    // подачі пропускається; призначено Serhii Tkachuk за 2,2 км. Див. D-14.
    [Fact]
    public void Assign_BusinessDriverWithRatingExactly47_IsSkipped()
    {
        int id = Rides.Create(_sut, 6, 6, "BUSINESS");

        int driverId = _sut.Assign(id, 3, false);

        Assert.Equal(7, driverId);
        Assert.Equal(2.2, _store.FindRide(id).PickupKm);
    }

    [Fact]
    public void Assign_ChildSeatRequired_SkipsDriverWithoutSeat()
    {
        // Ivan ближче (2,7 км), але без крісла; Natalia — 1,1 км, з кріслом.
        int id = Rides.Create(_sut, 4.5, 4, childSeat: true);

        int driverId = _sut.Assign(id, 3, false);

        Assert.Equal(3, driverId);
        Assert.Equal(1.1, _store.FindRide(id).PickupKm);
    }

    // Характеризаційний тест. Коментар: «даємо комфорт за ціною економу», але клас
    // поїздки змінюється на COMFORT назавжди, і Calc потім рахує за ставкою комфорту.
    [Fact]
    public void Assign_NoEcoButUpgradeAllowed_AssignsComfortAndChangesClass()
    {
        int id = Rides.Create(_sut, 7.5, 1);

        int driverId = _sut.Assign(id, 3, true);

        Assert.Equal(4, driverId);
        Assert.Equal("COMFORT", _store.FindRide(id).CarClass);
    }

    [Fact]
    public void Assign_NoCarAndNoUpgrade_ReturnsMinus3WithNote()
    {
        int id = Rides.Create(_sut, 7.5, 1);

        int result = _sut.Assign(id, 3, false);

        Ride ride = _store.FindRide(id);
        Assert.Equal(-3, result);
        Assert.Equal($"no car for ride #{id} within 3 km", _sut.LastNote);
        Assert.Equal("ECO", ride.CarClass);
        Assert.Equal(RideStatus.Created, ride.Status);
    }

    [Fact]
    public void Assign_DriverWithOneRide_TakesSecondButNotThird()
    {
        int first = Rides.Create(_sut, 2, 3);
        int second = Rides.Create(_sut, 2, 3);
        int third = Rides.Create(_sut, 2, 3);

        int a = _sut.Assign(first, 3, false);
        int b = _sut.Assign(second, 3, false);
        int c = _sut.Assign(third, 3, false);

        Assert.Equal(1, a);
        Assert.Equal(1, b);
        Assert.Equal(-3, c);
        Assert.Equal(2, _store.FindDriver(1).ActiveRides);
    }

    // OLD_TOWN заявлено в конфігурації як 1,15, але зона губиться в LoadZones (D-08),
    // тому коефіцієнт — 1,0, як для невідомої зони.
    [Theory]
    [InlineData("airport", 1.3)]
    [InlineData("Station ", 1.2)]
    [InlineData("suburb", 0.9)]
    [InlineData(null, 1.0)]
    [InlineData("moon", 1.0)]
    [InlineData("old_town", 1.0)]
    public void Assign_Zone_SetsZoneSurge(string? zone, double expected)
    {
        int id = Rides.Create(_sut, 2, 3, zone: zone);

        _sut.Assign(id, 3, false);

        Assert.Equal(expected, _store.FindRide(id).Surge, 3);
    }
}
