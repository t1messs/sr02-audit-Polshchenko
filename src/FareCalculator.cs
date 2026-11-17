namespace TaxiDispatch;

// Тарифи за наказом від 01.03.2021 (грн)
public class FareCalculator
{
    // Дороги не прямі: відстань по прямій множиться на 1,3.
    private const double RoadFactor = 1.3;

    // Ставки за кілометр за класом авто, грн.
    private const double EcoPerKm = 9.0;
    private const double ComfortPerKm = 12.0;
    private const double BusinessPerKm = 18.5;
    private const double VanPerKm = 14.0;

    private static readonly Dictionary<string, double> PerKmByClass = new Dictionary<string, double>
    {
        { "COMFORT", ComfortPerKm },
        { "BUSINESS", BusinessPerKm },
        { "VAN", VanPerKm },
    };

    // Посадка й мінімальна вартість поїздки, грн.
    private const double BaseFee = 40;
    private const double MinFare = 70;

    // Дитяче крісло, грн; очікування: перші 3 хв безкоштовно, далі 3 грн/хв.
    private const double ChildSeatFee = 30;
    private const int FreeWaitMinutes = 3;
    private const int WaitFeePerMinute = 3;

    // Штраф за скасування після призначення, грн.
    private const double StandardCancelFee = 40;
    private const double PremiumCancelFee = 80;

    // Комісія сервісу: базова 20 %, 15 % для водія з рейтингом 4,9+ і 10+ поїздками за зміну,
    // +5 % для бізнес-класу (оренда авто).
    private const double BaseCommission = 0.2;
    private const double LoyalCommission = 0.15;
    private const double LoyalRating = 4.9;
    private const int LoyalTrips = 10;
    private const double BusinessCarRent = 0.05;

    // вартість після завершення поїздки
    public double Calc(Ride r)
    {
        double fare = BaseFare(r.CarClass, RouteKm(r));
        fare = fare * r.Surge;
        if (r.ChildSeat)
            fare = fare + ChildSeatFee;
        int waitMin = (int)(r.StartedAt - r.AssignedAt).TotalMinutes;
        if (waitMin > FreeWaitMinutes)
            fare = fare + (waitMin - FreeWaitMinutes) * WaitFeePerMinute;
        return Math.Round(fare);
    }

    // ціна відкритої поїздки для звіту: тариф × коефіцієнт, без крісла й очікування
    public double OpenRidePrice(Ride r)
    {
        return Math.Round(BaseFare(r.CarClass, RouteKm(r)) * r.Surge);
    }

    // попередня ціна (без очікування і крісла)
    public double Estimate(string cls, double km)
    {
        return Math.Round(BaseFare(cls, km));
    }

    // тариф: посадка + ставка класу × км, не менше мінімальної ціни; без коефіцієнта
    private static double BaseFare(string cls, double km)
    {
        // невідомий клас (і null) — за ставкою економу, як і раніше
        double perKm = EcoPerKm;
        if (cls != null && PerKmByClass.TryGetValue(cls, out double rate))
            perKm = rate;
        double fare = BaseFee + km * perKm;
        if (fare < MinFare)
            fare = MinFare;
        return fare;
    }

    // довжина маршруту дорогами, км
    private static double RouteKm(Ride r)
    {
        double dx = r.DropX - r.PickupX, dy = r.DropY - r.PickupY;
        return Math.Sqrt(dx * dx + dy * dy) * RoadFactor;
    }

    // штраф за скасування після призначення
    public double CancelFee(Ride r)
    {
        if (r.Status != RideStatus.Assigned)
            return 0;
        if (r.CarClass == "BUSINESS" || r.CarClass == "VAN")
            return PremiumCancelFee;
        return StandardCancelFee;
    }

    // виплата водію: вартість мінус комісія сервісу
    public double DriverPayout(Driver d, double fare)
    {
        double commission = BaseCommission;
        if (d.Rating >= LoyalRating && d.TripsToday >= LoyalTrips)
            commission = LoyalCommission;
        if (d.CarClass == "BUSINESS")
            commission = commission + BusinessCarRent;
        return Math.Round(fare * (1 - commission), 2);
    }
}
