open System

type WeatherData = { Rainfall: float; Temperature: float }
type SoilCondition = { Moisture: float }
type RiverState = { CurrentLevel: float; MaxCapacity: float }
type FloodWarning = | NoRisk | Warning of float | Flooding
type Season = | Spring | Summer | Autumn | Winter

let calculateRunoffCoefficient (moisture: float) = min 0.9 (max 0.1 (moisture / 100.0))
let updateRiverLevel (river: RiverState) (weather: WeatherData) (soil: SoilCondition) =
    let runoffCoefficient = calculateRunoffCoefficient soil.Moisture
    let inflow = weather.Rainfall * runoffCoefficient * 0.01
    let outflow = min (river.CurrentLevel * 0.05) 0.5
    let newLevel = max 0.0 (river.CurrentLevel + inflow - outflow)
    { river with CurrentLevel = min newLevel river.MaxCapacity }

let checkFloodRisk (river: RiverState) =
    let threshold = river.MaxCapacity * 0.8
    if river.CurrentLevel >= river.MaxCapacity then Flooding
    elif river.CurrentLevel >= threshold then Warning river.CurrentLevel
    else NoRisk

let simulateRiver (initialRiver: RiverState) (initialSoil: SoilCondition) (weatherData: WeatherData list) =
    let rec simulate (river: RiverState) (soil: SoilCondition) (weather: WeatherData list) (results: (RiverState * FloodWarning) list) =
        match weather with
        | [] -> List.rev results
        | currentWeather :: rest ->
            let newRiver = updateRiverLevel river currentWeather soil
            let evaporation = max 0.0 (currentWeather.Temperature * 0.03)
            let moistureChange = currentWeather.Rainfall * 0.1 - evaporation
            let newMoisture = max 0.0 (min 100.0 (soil.Moisture + moistureChange))
            let newSoil = { Moisture = newMoisture }
            let warning = checkFloodRisk newRiver
            simulate newRiver newSoil rest ((newRiver, warning) :: results)
    
    simulate initialRiver initialSoil weatherData []


// Évszak meghatározása a nap sorszáma alapján (egyszerűsített, 365 napos év)
let getSeason (day: int) =
    match (day - 1) % 365 with
    | d when d < 90 -> Spring   // Március-Május
    | d when d < 180 -> Summer  // Június-Augusztus
    | d when d < 270 -> Autumn  // Szeptember-November
    | _ -> Winter              // December-Február

let generateMonthlyWeather (random: Random) (day: int) =
    let month = ((day - 1) % 365) / 30 + 1  // egyszerűsített hónap-számítás
    let (rainChance, rainMax, tempMin, tempMax) =
        match month with
        | 1 ->  (0.3, 20.0, -10.0, 2.0)     // Január
        | 2 ->  (0.4, 25.0, -5.0, 5.0)      // Február
        | 3 ->  (0.5, 40.0, 0.0, 10.0)      // Március
        | 4 ->  (0.6, 50.0, 5.0, 15.0)      // Április
        | 5 ->  (0.7, 60.0, 10.0, 20.0)     // Május
        | 6 ->  (0.3, 70.0, 15.0, 30.0)     // Június
        | 7 ->  (0.2, 60.0, 18.0, 35.0)     // Július
        | 8 ->  (0.3, 50.0, 16.0, 32.0)     // Augusztus
        | 9 ->  (0.4, 40.0, 10.0, 20.0)     // Szeptember
        | 10 -> (0.5, 45.0, 5.0, 15.0)      // Október
        | 11 -> (0.5, 35.0, 0.0, 10.0)      // November
        | 12 -> (0.4, 30.0, -5.0, 5.0)      // December
        | _ ->  (0.0, 0.0, 0.0, 0.0)        // Biztonsági default
    
    let rainfall = if random.NextDouble() < rainChance 
                   then random.NextDouble() * rainMax 
                   else 0.0
    let temperature = tempMin + random.NextDouble() * (tempMax - tempMin)
    { Rainfall = rainfall; Temperature = temperature }


// Extrém időjárási esemény generálása (vihar vagy hőhullám)
let generateExtremeWeather (random: Random) (weather: WeatherData) =
    let chance = random.NextDouble()
    if chance < 0.05 then  // 5% esély viharra
        let extraRain = 50.0 + random.NextDouble() * 50.0  // +50–100 mm
        printfn "⚠️  Extrém vihar! +%.1f mm eső" extraRain
        { weather with Rainfall = weather.Rainfall + extraRain }
    elif chance > 0.95 then  // 5% esély hőhullámra
        let extraTemp = 5.0 + random.NextDouble() * 10.0  // +5–15 °C
        printfn "🔥 Hőhullám! +%.1f °C" extraTemp
        { weather with Temperature = weather.Temperature + extraTemp }
    else
        weather


// Random időjárási adatok generálása szezonális mintákkal
let generateRandomWeather (random: Random) (days: int) (startDay: int) =
    [ for day in startDay .. (startDay + days - 1) ->
        generateMonthlyWeather random day 
        |> generateExtremeWeather random ]

[<EntryPoint>]
let main argv =
    printfn "Hidrológiai szimuláció szezonális mintákkal"
    printfn "========================================"
    let initialRiver = { CurrentLevel = 2.0; MaxCapacity = 10.0 }
    let initialSoil = { Moisture = 50.0 }
    
    let random = Random()
    let simulationDays = 365  // Egyéves szimuláció
    let startDay = 1          // Kezdés az év elejétől (január 1.)
    let randomWeather = generateRandomWeather random simulationDays startDay
    
    printfn "Szimuláció %d napra szezonális időjárási adatokkal" simulationDays
    let simulationResults = simulateRiver initialRiver initialSoil randomWeather
    
    simulationResults |> List.iteri (fun i (river, warning) ->
        let season = getSeason (startDay + i)
        match warning with
        | Flooding -> Console.ForegroundColor <- ConsoleColor.Red
        | Warning _ -> Console.ForegroundColor <- ConsoleColor.Yellow
        | NoRisk -> Console.ForegroundColor <- ConsoleColor.Green
        printfn "Nap %d (%A): Vízszint: %.2f m, Csapadék: %.2f mm, Hőmérséklet: %.1f°C, Figyelmeztetés: %A" 
            (i + 1) season river.CurrentLevel randomWeather.[i].Rainfall randomWeather.[i].Temperature warning
        Console.ResetColor())
    
    // Statisztika
    let maxLevel = simulationResults |> List.map (fst >> fun r -> r.CurrentLevel) |> List.max
    let floodDays = simulationResults |> List.filter (snd >> function Flooding -> true | _ -> false) |> List.length
    printfn "\nSzimulációs statisztika:"
    printfn "Maximális vízszint: %.2f m" maxLevel
    printfn "Árvizes napok száma: %d" floodDays

    let rec monthSelectorLoop () =
        printfn "\nÍrd be, melyik hónap adataira vagy kíváncsi (1–12), vagy írj 'exit'-et a kilépéshez:"
        printf "> "
        let input = Console.ReadLine().Trim().ToLower()
        match input with
        | "exit" -> printfn "Kilépés..."; ()
        | _ ->
            match Int32.TryParse(input) with
            | (true, month) when month >= 1 && month <= 12 ->
                let daysInMonth =
                    [| 31; 28; 31; 30; 31; 30; 31; 31; 30; 31; 30; 31 |]
                let startDayOfMonth = Array.scan (+) 0 daysInMonth
                let startIdx = startDayOfMonth.[month - 1]
                let endIdx = startDayOfMonth.[month] - 1

                printfn "\n--- %d. hónap adatai ---" month
                printfn "Nap   | Vízszint (m) | Csapadék (mm) | Hőmérséklet (°C) | Figyelmeztetés"
                printfn "----------------------------------------------------------------------"
                simulationResults
                |> List.mapi (fun i (river, warning) -> i, river, randomWeather.[i], warning)
                |> List.filter (fun (i, _, _, _) -> i >= startIdx && i <= endIdx)
                |> List.iter (fun (i, river, weather, warning) ->
                    printfn "%-5d | %-12.2f | %-14.2f | %-18.1f | %A"
                        (i + 1)
                        river.CurrentLevel
                        weather.Rainfall
                        weather.Temperature
                        warning
                )
                monthSelectorLoop ()  // újra kérdezzük
            | _ ->
                printfn "❌ Hibás bemenet. Adj meg egy 1 és 12 közötti számot, vagy 'exit'-et a kilépéshez."
                monthSelectorLoop ()

    monthSelectorLoop ()
    
    0