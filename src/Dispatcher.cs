namespace TaxiDispatch;

// Диспетчер: замовлення, призначення водіїв, статуси
public class Dispatcher
{
    // Коефіцієнт попиту: надбавка за кожне замовлення понад кількість вільних машин,
    // надбавка в години пік і стеля коефіцієнта.
    private const double SurgePerExtraRide = 0.15;
    private const double RushHourSurge = 0.2;
    private const double MaxSurge = 2.5;

    // Години пік: [7; 10) і [17; 20) за місцевим часом.
    private const int MorningRushStart = 7;
    private const int MorningRushEnd = 10;
    private const int EveningRushStart = 17;
    private const int EveningRushEnd = 20;

    // Рейтинг водія має бути строго вищим за поріг (див. TD-03 у реєстрі боргу).
    private const double MinRating = 4.0;
    private const double MinBusinessRating = 4.7;

    // Водій у дорозі бере ще одне замовлення, доки активних поїздок менше ніж 2.
    private const int MaxActiveRides = 2;

    // Телефон після нормалізації: +380XXXXXXXXX.
    private const int PhoneLength = 13;

    private DispatchStore repo;
    private FareCalculator fares = new FareCalculator();
    public string LastNote;     // причина відмови для оператора

    public Dispatcher(DispatchStore store)
    {
        repo = store;
    }

    // Шов для тестів (М5, прийом 2): тест підміняє годинник у класі-нащадку.
    protected virtual DateTime Now()
    {
        return DateTime.Now;
    }

    // new order from operator or website; returns id or -1
    public int CreateRide(string name, string phone, double px, double py, double dx, double dy,
        string carClass, bool childSeat, string zone)
    {
        LastNote = "";
        if (name == null || name.Trim().Length == 0)
        {
            LastNote = "name is empty";
            return -1;
        }
        string p = phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (p.StartsWith("0"))
            p = "+38" + p;
        if (p.Length != PhoneLength)
        {
            LastNote = "invalid phone " + phone;
            return -1;
        }
        // temp: old website sends "standard", "eco", "" - treat as ECO
        string cls = carClass == null ? "ECO" : carClass.Trim().ToUpper();
        if (cls != "COMFORT" && cls != "BUSINESS" && cls != "VAN")
            cls = "ECO";
        Ride r = new Ride();
        r.Id = DispatchStore.nextRideId++;
        r.Passenger = name.Trim();
        r.Phone = p;
        r.PickupX = px;
        r.PickupY = py;
        r.DropX = dx;
        r.DropY = dy;
        r.CarClass = cls;
        r.ChildSeat = childSeat;
        r.Zone = zone == null ? "CENTER" : zone.Trim().ToUpper();
        repo.Rides.Add(r);
        AuditLog.Write("new ride #" + r.Id + " " + p + " " + cls);
        return r.Id;
    }

    // Призначення водія. Повертає Id водія або
    // -1 (немає замовлення), -2 (вже не нове), -3 (немає машини)
    public int Assign(int rideId, double maxKm, bool allowUpgrade)
    {
        LastNote = "";
        Ride ride = repo.FindRide(rideId);
        if (ride == null)
        {
            LastNote = "ride #" + rideId + " not found";
            return -1;
        }
        if (ride.Status != RideStatus.Created)
        {
            LastNote = "ride #" + ride.Id + " is " + ride.Status;
            return -2;
        }
        // коефіцієнт: зона + нестача машин
        double surge = 1.0;
        if (DispatchStore.ZoneSurge.ContainsKey(ride.Zone))
            surge = DispatchStore.ZoneSurge[ride.Zone];
        int waitingRides = repo.CountWaiting();
        int freeDrivers = CountFree();
        if (waitingRides > freeDrivers)
        {
            surge = surge + SurgePerExtraRide * (waitingRides - freeDrivers);
            if (IsRushHour())
                surge = surge + RushHourSurge;
        }
        ride.Surge = Math.Min(surge, MaxSurge);

        Driver nearest = null;
        double nearestKm = maxKm;
        double minRating = ride.CarClass == "BUSINESS" ? MinBusinessRating : MinRating;
        foreach (Driver driver in repo.Drivers)
        {
            if (driver.CarClass != ride.CarClass || driver.Rating <= minRating)
                continue;
            // вільний або везе, але бере ще одне (до 2)
            bool canTakeRide = driver.Status == "free"
                || driver.Status == "onride" && driver.ActiveRides < MaxActiveRides;
            if (canTakeRide)
            {
                bool hasRequiredSeat = !ride.ChildSeat || driver.HasChildSeat;
                if (hasRequiredSeat)
                {
                    double distance = Km(driver.X, driver.Y, ride.PickupX, ride.PickupY);
                    if (distance <= nearestKm)
                    {
                        nearest = driver;
                        nearestKm = distance;
                    }
                }
            }
        }

        // економу немає - даємо комфорт за ціною економу
        if (nearest == null && allowUpgrade && ride.CarClass == "ECO")
        {
            ride.CarClass = "COMFORT";
            Console.WriteLine("  upgrade: ride #" + ride.Id + " ECO -> COMFORT");
            return Assign(rideId, maxKm, false);
        }
        if (nearest == null)
        {
            LastNote = "no car for ride #" + ride.Id + " within " + maxKm + " km";
            return -3;
        }
        ride.DriverId = nearest.Id;
        ride.PickupKm = Math.Round(nearestKm, 1);
        ride.Status = RideStatus.Assigned;
        ride.AssignedAt = Now();
        nearest.Status = "onride";
        nearest.ActiveRides++;
        AuditLog.Write("ride #" + ride.Id + " -> driver " + nearest.Id + ", surge " + ride.Surge);
        return nearest.Id;
    }

    // додаток водія: на місці -> в дорозі -> завершено
    public bool ChangeStatus(int rideId, RideStatus to)
    {
        Ride r = repo.FindRide(rideId);
        if (r == null)
            return false;
        bool ok = false;
        switch (r.Status)
        {
            case RideStatus.Created:
                ok = to == RideStatus.Cancelled;    // призначення - лише Assign
                break;
            case RideStatus.Assigned:
                ok = to == RideStatus.InProgress || to == RideStatus.Cancelled;
                break;
            case RideStatus.InProgress:
                ok = to == RideStatus.Completed;
                break;
        }
        if (!ok)
        {
            Console.WriteLine("  ! ride #" + r.Id + ": " + r.Status + " -> " + to + " rejected");
            return false;
        }
        Driver d = repo.FindDriver(r.DriverId);
        if (to == RideStatus.InProgress)
            r.StartedAt = Now();
        if (to == RideStatus.Completed)
        {
            // До Completed веде лише Assigned -> InProgress, тож водій уже призначений.
            System.Diagnostics.Debug.Assert(d != null, "Completed ride must have a driver");
            r.Fare = fares.Calc(r);
            d.ActiveRides--;
            if (d.ActiveRides == 0)
                d.Status = "free";
            d.X = r.DropX;     // водій там, де висадив
            d.Y = r.DropY;
            d.TripsToday++;
            d.EarnedToday = d.EarnedToday + fares.DriverPayout(d, r.Fare);
        }
        if (to == RideStatus.Cancelled)
            r.Fare = fares.CancelFee(r);   // водія звільняє додаток
        r.Status = to;
        AuditLog.Write("ride #" + r.Id + " " + to);
        return true;
    }

    public int CountFree()
    {
        int n = 0;
        foreach (Driver d in repo.Drivers)
            if (d.Status == "free")
                n++;
        return n;
    }

    private bool IsRushHour()
    {
        int h = Now().Hour;
        bool isMorningRush = h >= MorningRushStart && h < MorningRushEnd;
        bool isEveningRush = h >= EveningRushStart && h < EveningRushEnd;
        return isMorningRush || isEveningRush;
    }

    public static double Km(double x1, double y1, double x2, double y2)
    {
        return Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
    }
}
