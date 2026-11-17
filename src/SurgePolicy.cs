namespace TaxiDispatch;

// Коефіцієнт попиту: зона, нестача вільних машин і година пік.
public class SurgePolicy
{
    // Надбавка за кожне замовлення понад кількість вільних машин, надбавка в години пік
    // і стеля коефіцієнта.
    private const double SurgePerExtraRide = 0.15;
    private const double RushHourSurge = 0.2;
    private const double MaxSurge = 2.5;

    // Години пік: [7; 10) і [17; 20) за місцевим часом.
    private const int MorningRushStart = 7;
    private const int MorningRushEnd = 10;
    private const int EveningRushStart = 17;
    private const int EveningRushEnd = 20;

    public double For(string zone, int waitingRides, int freeDrivers, DateTime now)
    {
        double surge = 1.0;
        if (DispatchStore.ZoneSurge.ContainsKey(zone))
            surge = DispatchStore.ZoneSurge[zone];
        if (waitingRides > freeDrivers)
        {
            surge = surge + SurgePerExtraRide * (waitingRides - freeDrivers);
            if (IsRushHour(now))
                surge = surge + RushHourSurge;
        }
        return Math.Min(surge, MaxSurge);
    }

    private static bool IsRushHour(DateTime now)
    {
        int h = now.Hour;
        bool isMorningRush = h >= MorningRushStart && h < MorningRushEnd;
        bool isEveningRush = h >= EveningRushStart && h < EveningRushEnd;
        return isMorningRush || isEveningRush;
    }
}
