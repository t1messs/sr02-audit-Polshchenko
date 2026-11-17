namespace TaxiDispatch;

// Диспетчер: замовлення, призначення водіїв, статуси
public class Dispatcher
{
    // Рейтинг водія має бути строго вищим за поріг (див. TD-03 у реєстрі боргу).
    private const double MinRating = 4.0;
    private const double MinBusinessRating = 4.7;

    // Водій у дорозі бере ще одне замовлення, доки активних поїздок менше ніж 2.
    private const int MaxActiveRides = 2;

    // Телефон після нормалізації: +380XXXXXXXXX.
    private const int PhoneLength = 13;

    private DispatchStore repo;
    private FareCalculator fares = new FareCalculator();
    private SurgePolicy surgePolicy = new SurgePolicy();
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
        string p = NormalizePhone(phone);
        if (p.Length != PhoneLength)
        {
            LastNote = "invalid phone " + phone;
            return -1;
        }
        string cls = NormalizeCarClass(carClass);
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

    // прибирає пробіли, дефіси й дужки; місцевий номер 0XXXXXXXXX доповнює до +380XXXXXXXXX
    private static string NormalizePhone(string phone)
    {
        string p = phone.Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "");
        if (p.StartsWith("0"))
            p = "+38" + p;
        return p;
    }

    // старий сайт надсилає "standard", "eco", "" — усе, крім відомих класів, вважаємо ECO
    private static string NormalizeCarClass(string carClass)
    {
        string cls = carClass == null ? "ECO" : carClass.Trim().ToUpper();
        if (cls != "COMFORT" && cls != "BUSINESS" && cls != "VAN")
            cls = "ECO";
        return cls;
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
        ride.Surge = SurgeFor(ride);
        Driver nearest = FindNearestDriver(ride, maxKm, out double nearestKm);

        // економу немає - даємо комфорт (клас поїздки змінюється, див. TD-04)
        if (nearest == null && CanUpgrade(ride, allowUpgrade))
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
        return AssignDriver(ride, nearest, nearestKm);
    }

    // коефіцієнт: зона + нестача машин + година пік (правило — у SurgePolicy)
    private double SurgeFor(Ride ride)
    {
        return surgePolicy.For(ride.Zone, repo.CountWaiting(), CountFree(), Now());
    }

    // найближчий придатний водій у радіусі maxKm (межа включно)
    private Driver FindNearestDriver(Ride ride, double maxKm, out double nearestKm)
    {
        Driver nearest = null;
        nearestKm = maxKm;
        foreach (Driver driver in repo.Drivers)
        {
            if (!IsEligible(driver, ride))
                continue;
            double distance = Km(driver.X, driver.Y, ride.PickupX, ride.PickupY);
            if (distance <= nearestKm)
            {
                nearest = driver;
                nearestKm = distance;
            }
        }
        return nearest;
    }

    // водій підходить: той самий клас, рейтинг вище порогу, вільний чи бере другу поїздку,
    // має крісло, якщо воно потрібне
    private static bool IsEligible(Driver driver, Ride ride)
    {
        if (driver.CarClass != ride.CarClass || driver.Rating <= MinRatingFor(ride.CarClass))
            return false;
        bool canTakeRide = driver.Status == "free"
            || driver.Status == "onride" && driver.ActiveRides < MaxActiveRides;
        bool hasRequiredSeat = !ride.ChildSeat || driver.HasChildSeat;
        return canTakeRide && hasRequiredSeat;
    }

    private static bool CanUpgrade(Ride ride, bool allowUpgrade)
    {
        return allowUpgrade && ride.CarClass == "ECO";
    }

    // фіксує призначення: поїздка, водій, журнал
    private int AssignDriver(Ride ride, Driver driver, double pickupKm)
    {
        ride.DriverId = driver.Id;
        ride.PickupKm = Math.Round(pickupKm, 1);
        ride.Status = RideStatus.Assigned;
        ride.AssignedAt = Now();
        driver.Status = "onride";
        driver.ActiveRides++;
        AuditLog.Write("ride #" + ride.Id + " -> driver " + driver.Id + ", surge " + ride.Surge);
        return driver.Id;
    }

    // Дозволені переходи стану; призначення водія — лише через Assign.
    private static readonly HashSet<(RideStatus From, RideStatus To)> AllowedTransitions =
        new HashSet<(RideStatus From, RideStatus To)>
        {
            (RideStatus.Created, RideStatus.Cancelled),
            (RideStatus.Assigned, RideStatus.InProgress),
            (RideStatus.Assigned, RideStatus.Cancelled),
            (RideStatus.InProgress, RideStatus.Completed),
        };

    // додаток водія: на місці -> в дорозі -> завершено
    public bool ChangeStatus(int rideId, RideStatus to)
    {
        Ride r = repo.FindRide(rideId);
        if (r == null)
            return false;
        bool ok = AllowedTransitions.Contains((r.Status, to));
        if (!ok)
        {
            Console.WriteLine("  ! ride #" + r.Id + ": " + r.Status + " -> " + to + " rejected");
            return false;
        }
        Driver d = repo.FindDriver(r.DriverId);
        if (to == RideStatus.InProgress)
            r.StartedAt = Now();
        if (to == RideStatus.Completed)
            CompleteRide(r, d);
        if (to == RideStatus.Cancelled)
            r.Fare = fares.CancelFee(r);   // водія звільняє додаток
        r.Status = to;
        AuditLog.Write("ride #" + r.Id + " " + to);
        return true;
    }

    private static double MinRatingFor(string carClass)
    {
        return carClass == "BUSINESS" ? MinBusinessRating : MinRating;
    }

    // завершення: вартість, звільнення водія, його нове місце й заробіток
    private void CompleteRide(Ride r, Driver d)
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

    public int CountFree()
    {
        int n = 0;
        foreach (Driver d in repo.Drivers)
            if (d.Status == "free")
                n++;
        return n;
    }

    public static double Km(double x1, double y1, double x2, double y2)
    {
        return Math.Sqrt((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2));
    }
}
