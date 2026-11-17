# Розділ 5. Виконання рефакторингів окремими комітами

Рефакторинг виконано на гілці `main` між тегами `safety-net` (`88c5fc4`) і
`after-refactoring` (`9e94f5c`): **21 коміт `refactor`, 13 різних рефакторингів** каталогу
(R01, R02, R03, R04, R05, R06, R08, R09, R10, R15, R16, R17, R18). Обов'язкові для варіанта
R17 Remove Dead Code і R01 Extract Method виконано відповідно 4 і 4 рази.

**Як виконувалися кроки.** Кожен крок — окремий файл змін у теці `refactor-steps/`
(у репозиторій не входить), який застосовував скрипт `refactor-steps/apply.sh`: застосувати
зміну → `dotnet build` → `dotnet test` → запустити сценарій і порівняти вивід із
`docs/out-before.txt` → коміт. Скрипт зупинявся б на першому кроці з червоним тестом або
зміненим виводом, тож кожен коміт таблиці доведено трьома перевірками, а не однією.

## 5.3. Таблиця виконаного

| № | Код | Рефакторинг | Файл | Коміт | Тестів зелених |
|---|---|---|---|---|---|
| 1 | R17 | Видалено недосяжну гілку UseZoneQueue | Dispatcher.cs | `44626cc` | 53 |
| 2 | R04 | Іменовані константи коефіцієнта й рейтингу | Dispatcher.cs | `ad1d530` | 53 |
| 3 | R17 | Видалено закоментовані блоки в Assign | Dispatcher.cs | `2cbd804` | 53 |
| 4 | R03 | Виділено змінні умов вибору водія й години пік | Dispatcher.cs | `ce73696` | 53 |
| 5 | R04 | Іменовані константи тарифу й комісій | FareCalculator.cs | `339e61b` | 53 |
| 6 | R17 | Видалено невикористану змінну est | Dispatcher.cs | `035634b` | 53 |
| 7 | R17 | Видалено невикористаний метод ScoreDriver | Dispatcher.cs | `f05aa48` | 53 |
| 8 | R18 | Твердження про водія завершеної поїздки | Dispatcher.cs | `c8d302a` | 53 |
| 9 | R02 | Перейменовано CountWaiting і змінні Assign | DispatchStore.cs, Dispatcher.cs | `2533479` | 53 |
| 10 | R15 | Поріг рейтингу замінено запитом MinRatingFor | Dispatcher.cs | `b30a970` | 53 |
| 11 | R06 | Запобіжні умови в циклі пошуку водія | Dispatcher.cs | `78be950` | 53 |
| 12 | R01 | Виділено методи BaseFare і RouteKm | FareCalculator.cs | `d91e12c` | 53 |
| 13 | R10 | Перенесено ціну відкритої поїздки у FareCalculator | ReportService.cs, FareCalculator.cs | `c7d6299` | 53 |
| 14 | R01 | Виділено з Assign три методи | Dispatcher.cs | `bae207c` | 53 |
| 15 | R05 | Умови вибору водія й апгрейду винесено в методи | Dispatcher.cs | `6b4e420` | 53 |
| 16 | R08 | Ставки за класом авто винесено в таблицю | FareCalculator.cs | `e5272fd` | 53 |
| 17 | R08 | Дозволені переходи стану винесено в таблицю | Dispatcher.cs | `17dd272` | 53 |
| 18 | R01 | Виділено метод CompleteRide | Dispatcher.cs | `a8e67e1` | 53 |
| 19 | R01 | Виділено NormalizePhone і NormalizeCarClass | Dispatcher.cs | `d34ea3e` | 53 |
| 20 | R09 | Виділено клас SurgePolicy | Dispatcher.cs, SurgePolicy.cs | `2eeb305` | 53 |
| 21 | R16 | Розділено цикл друку й підрахунку водіїв | ReportService.cs | `9e94f5c` | 53 |

Жоден коміт не змінював теку `Legacy.Tests` і очікувані значення тестів (розділ 6.4).
Дві зміни публічного API — видалення прапорця `UseZoneQueue` і перейменування
`count_waiting` — описано в журналі рішень (Р-02, Р-03).

## 5.4. Пари «було / стало»

### Пара 1. Модуль-донор `Assign`: R17 + R04 + R02 + R06 + R01 + R05 (кроки 1–15)

Було (`baseline`, фрагмент із 104 рядків методу):
```csharp
double k = 1.0;
if (DispatchStore.ZoneSurge.ContainsKey(r.Zone))
    k = DispatchStore.ZoneSurge[r.Zone];
...
r.Surge = Math.Min(k, 2.5);
//double k = 1 + repo.CountWaiting() * 0.05;
...
double est = fares.Estimate(r.CarClass,
    Km(r.PickupX, r.PickupY, r.DropX, r.DropY) * 1.3);
...
double minRating = r.CarClass == "BUSINESS" ? 4.7 : 4.0;
foreach (Driver d in repo.Drivers)
{
    if (d.CarClass != r.CarClass || d.Rating <= minRating)
        continue;
    if (d.Status == "free" || d.Status == "onride" && d.ActiveRides < 2)
    {
        if (!r.ChildSeat || d.HasChildSeat)
        {
            double dist = Km(d.X, d.Y, r.PickupX, r.PickupY);
            if (dist <= bestKm)
```
Стало (`after-refactoring`, весь метод — 18 логічних рядків):
```csharp
ride.Surge = SurgeFor(ride);
Driver nearest = FindNearestDriver(ride, maxKm, out double nearestKm);

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
```
`Assign` тепер читається як сценарій із чотирьох кроків: порахувати коефіцієнт, знайти
водія, за потреби підвищити клас, зафіксувати призначення. Мертві фрагменти (15 рядків
коментарів, недосяжна гілка, невикористана `est`) зникли, магічні числа стали константами,
а вкладеність 5 → 2. CC методу впала з 21 до 6, і для кожного виділеного методу тепер
вистачає 2–7 тестових випадків.

### Пара 2. Мертвий код: R17 (кроки 1, 3, 6, 7)

Було:
```csharp
// черга на стоянках - вимкнули після переходу на GPS
public static bool UseZoneQueue = false;
...
if (UseZoneQueue)
{
    // стоянка: бере перший у черзі
    best = repo.Drivers.Find(x => x.Zone == r.Zone);
    r.DriverId = best.Id;
    ...
    return best.Id;
}
```
Стало: фрагмента немає. Прапорець ніде не стає `true`, тож гілка недосяжна; разом із нею
зник клон K-2 запису призначення. Недосяжність видно й з покриття: на `safety-net`
рядки 103–112 мали 0 виконань, а після видалення покриття `Dispatcher` зросло з 90,0 % до
100 %.

### Пара 3. Таблиця переходів стану: R08 (крок 17)

Було:
```csharp
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
```
Стало:
```csharp
private static readonly HashSet<(RideStatus From, RideStatus To)>
    AllowedTransitions = new HashSet<(RideStatus From, RideStatus To)>
    {
        (RideStatus.Created, RideStatus.Cancelled),
        (RideStatus.Assigned, RideStatus.InProgress),
        (RideStatus.Assigned, RideStatus.Cancelled),
        (RideStatus.InProgress, RideStatus.Completed),
    };
...
bool ok = AllowedTransitions.Contains((r.Status, to));
```
Правило переходів — це дані, а не код: чотири рядки таблиці читаються як діаграма станів,
новий перехід додається одним рядком. Разом із R01 `CompleteRide` (крок 18) CC
`ChangeStatus` впала з 11 до 6.

### Пара 4. Третя копія тарифу: R01 + R10 (кроки 12–13)

Було (`ReportService.PrintRides`):
```csharp
double km = Dispatcher.Km(r.PickupX, r.PickupY, r.DropX, r.DropY) * 1.3;
double perKm = 9.0;
if (cls == "COMFORT")
    perKm = 12.0;
...
double fare = 40 + km * perKm;
if (fare < 70)
    fare = 70;
price = Math.Round(fare * r.Surge);
```
Стало:
```csharp
// ReportService.PrintRides
price = fares.OpenRidePrice(r);

// FareCalculator
public double OpenRidePrice(Ride r)
{
    return Math.Round(BaseFare(r.CarClass, RouteKm(r)) * r.Surge);
}
```
Тарифна сітка й коефіцієнт доріг `1.3` тепер живуть лише у `FareCalculator` — дробовик D-05
і клон K-1 усунуто. Порядок обчислень (тариф, потім коефіцієнт, потім округлення) збережено
навмисно: виклик `Estimate` округлював би до множення, і ціни у звіті змінилися б (Р-06).
