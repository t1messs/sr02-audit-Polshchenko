namespace TaxiDispatch.Tests;

public class DispatchStoreTests
{
    // Характеризаційний тест. Конфігурація містить OLD_TOWN=1,15; int.Parse падає, а
    // порожній catch ковтає виняток — зона мовчки зникає (D-08).
    [Fact]
    public void LoadZones_CommaInPercent_ZoneIsSilentlyLost()
    {
        _ = new DispatchStore();

        Assert.Equal(4, DispatchStore.ZoneSurge.Count);
        Assert.False(DispatchStore.ZoneSurge.ContainsKey("OLD_TOWN"));
        Assert.Equal(1.3, DispatchStore.ZoneSurge["AIRPORT"], 3);
    }

    [Fact]
    public void Constructor_SeedsShiftWithOneDriverOnBreak()
    {
        var store = new DispatchStore();

        Assert.Equal(9, store.Drivers.Count);
        Assert.Equal("break", store.FindDriver(5).Status);
        Assert.Equal(12, store.FindDriver(4).TripsToday);
        Assert.Null(store.FindDriver(42));
    }
}
