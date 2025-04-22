open System
open System.Threading.Tasks
open WeatherApi

type WeatherData = { Rainfall: float; Temperature: float }
type SoilCondition = { Moisture: float }
type RiverState = { CurrentLevel: float; MaxCapacity: float }
type FloodWarning = | NoRisk | Warning of float | Flooding

let calculateRunoffCoefficient (moisture: float) = min 0.9 (max 0.1 (moisture / 100.0))
let updateRiverLevel (river: RiverState) (weather: WeatherData) (soil: SoilCondition) =
    let runoffCoefficient = calculateRunoffCoefficient soil.Moisture
    let inflow = weather.Rainfall * runoffCoefficient * 0.1
    let outflow = min (river.CurrentLevel * 0.05) 0.5
    let newLevel = max 0.0 (river.CurrentLevel + inflow - outflow)
    { river with CurrentLevel = min newLevel river.MaxCapacity }

let checkFloodRisk (river: RiverState) =
    let threshold = river.MaxCapacity * 0.8
    if river.CurrentLevel >= river.MaxCapacity then Flooding
    elif river.CurrentLevel >= threshold then Warning river.CurrentLevel
    else NoRisk

let simulateRiver (initialRiver: RiverState) (initialSoil: SoilCondition) (weatherData: WeatherData list) (predictedTemps: float list) =
    let rec simulate (river: RiverState) (soil: SoilCondition) (weather: WeatherData list) (temps: float list) (results: (RiverState * FloodWarning) list) =
        match weather, temps with
        | [], _ | _, [] -> List.rev results
        | currentWeather :: restWeather, temp :: restTemps ->
            let newWeather = { currentWeather with Temperature = temp }
            let newRiver = updateRiverLevel river newWeather soil
            let evaporation = max 0.0 (newWeather.Temperature * 0.03)
            let moistureChange = newWeather.Rainfall * 0.1 - evaporation
            let newMoisture = max 0.0 (min 100.0 (soil.Moisture + moistureChange))
            let newSoil = { Moisture = newMoisture }
            let warning = checkFloodRisk newRiver
            simulate newRiver newSoil restWeather restTemps ((newRiver, warning) :: results)
    
    simulate initialRiver initialSoil weatherData predictedTemps []

let generateMonthlyWeather (random: Random) (day: int) =
    let month = ((day - 1) % 365) / 30 + 1
    let (rainChance, rainMax) =
        match month with
        | 1 ->  (0.3, 20.0)
        | 2 ->  (0.4, 25.0)
        | 3 ->  (0.5, 40.0)
        | 4 ->  (0.6, 50.0)
        | 5 ->  (0.7, 60.0)
        | 6 ->  (0.3, 70.0)
        | 7 ->  (0.2, 60.0)
        | 8 ->  (0.3, 50.0)
        | 9 ->  (0.4, 40.0)
        | 10 -> (0.5, 45.0)
        | 11 -> (0.5, 35.0)
        | 12 -> (0.4, 30.0)
        | _ ->  (0.0, 0.0)
    
    let rainfall = if random.NextDouble() < rainChance then random.NextDouble() * rainMax else 0.0
    { Rainfall = rainfall; Temperature = 0.0 } // Hőmérséklet az API-ból jön

let generateRandomWeather (random: Random) (days: int) (startDay: int) =
    [ for day in startDay .. (startDay + days - 1) -> generateMonthlyWeather random day ]

let estimateFloodRiskFromTemp (simulationResults: (RiverState * FloodWarning) list) =
    let floodDays = simulationResults |> List.filter (snd >> function Flooding -> true | _ -> false) |> List.length
    let warningDays = simulationResults |> List.filter (snd >> function Warning _ -> true | _ -> false) |> List.length
    if floodDays > 0 then Flooding
    elif warningDays > 0 then Warning (simulationResults |> List.map (fst >> fun r -> r.CurrentLevel) |> List.max)
    else NoRisk

let rec getUserDate () =
    printf "Adja meg a dátumot (YYYY-MM-DD): "
    let input = Console.ReadLine().Trim()
    match DateTime.TryParse(input) with
    | true, _ -> input
    | false, _ ->
        printfn "Érvénytelen dátumformátum. Próbálja újra."
        getUserDate ()

[<EntryPoint>]
let main argv =
    let endDate = getUserDate ()
    let initialRiver = { CurrentLevel = 2.0; MaxCapacity = 10.0 }
    let initialSoil = { Moisture = 50.0 }
    let random = Random()
    let simulationDays = 14
    
    let startDay = DateTime.Parse(endDate).DayOfYear
    let randomness = Random()
    let randomWeather = generateRandomWeather randomness simulationDays startDay
    
    let lat, lon = 23.81, 90.41 //47.5, 19.0
    let predictedTemps = 
        getPredictedTemps lat lon endDate simulationDays 
        |> Async.RunSynchronously
        |> Option.defaultValue (List.replicate simulationDays 20.0)
    
    printfn "Hőmérsékleti adatok az átlag kiszámításához:"
    predictedTemps |> List.iteri (fun i temp -> 
        let simDate = DateTime.Parse(endDate).AddDays(float i).ToString("yyyy-MM-dd")
        printfn "%s: %.1f°C" simDate temp)
    
    let simulationResults = simulateRiver initialRiver initialSoil randomWeather predictedTemps
    let floodRisk = estimateFloodRiskFromTemp simulationResults
    
    printfn "Átlaghőmérséklet (%d nap, %s-tól): %.2f°C" simulationDays endDate (predictedTemps |> List.average)
    printfn "Árvízkockázat: %A" floodRisk
    
    simulationResults |> List.iteri (fun i (river, warning) ->
        let simDate = DateTime.Parse(endDate).AddDays(float i).ToString("yyyy-MM-dd")
        printfn "%s: Vízszint: %.2f m, Csapadék: %.2f mm, Hőmérséklet: %.1f°C, Figyelmeztetés: %A" 
            simDate river.CurrentLevel randomWeather.[i].Rainfall predictedTemps.[i] warning)
    
    0