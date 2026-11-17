namespace TaxiDispatch;

// Звіти для старшого диспетчера
// TODO TD-11: звіти не покриті тестами (0 %), бо пишуть прямо в консоль; строк — до СР 3
public class ReportService
{
    private DispatchStore repo;
    private FareCalculator fares = new FareCalculator();

    public ReportService(DispatchStore repo)
    {
        this.repo = repo;
    }

    // відкриті поїздки показують попередню ціну за тарифом FareCalculator
    public void PrintRides()
    {
        foreach (Ride r in repo.Rides)
        {
            string drv = "-";
            Driver d = repo.FindDriver(r.DriverId);
            if (d != null)
                drv = d.Name;
            double price = r.Fare;
            if (r.Status != RideStatus.Completed && r.Status != RideStatus.Cancelled)
                price = fares.OpenRidePrice(r);
            Console.WriteLine("  #" + r.Id + " " + r.Passenger + ", " + r.CarClass + ", "
                + r.Status + ", driver " + drv + ", " + price + " UAH");
        }
    }

    // водії: статус і заробіток за зміну
    public void PrintFleet()
    {
        foreach (Driver d in repo.Drivers)
        {
            string st = d.Status;
            if (d.Status == "onride")
                st = st + " x" + d.ActiveRides;
            Console.WriteLine("  " + d.Id + " " + d.Name + ", " + d.CarClass + " " + d.Rating
                + ", " + st + ", earned " + d.EarnedToday);
        }
        int free = 0, busy = 0;
        foreach (Driver d in repo.Drivers)
        {
            if (d.Status == "free")
                free++;
            else if (d.Status == "onride")
                busy++;
        }
        Console.WriteLine("  free " + free + ", on ride " + busy + " of " + repo.Drivers.Count);
    }
}
