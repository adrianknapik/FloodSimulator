open System

type WeatherData = { Rainfall: float; Temperature: float }
type SoilCondition = { Moisture: float }
type RiverState = { CurrentLevel: float; MaxCapacity: float }
type FloodWarning = | NoRisk | Warning of float | Flooding

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
        | [] -> 
            printfn "Rekurzió vége"
            List.rev results
        | currentWeather :: rest ->
            printfn "Hátralévő időjárási adatok: %d" (List.length rest)
            printfn "Csapadék: %.2f mm" currentWeather.Rainfall
            let newRiver = updateRiverLevel river currentWeather soil
            let newSoil = { Moisture = min 100.0 (soil.Moisture + currentWeather.Rainfall * 0.1) }
            let warning = checkFloodRisk newRiver
            simulate newRiver newSoil rest ((newRiver, warning) :: results)

    simulate initialRiver initialSoil weatherData []

[<EntryPoint>]
let main argv =
    printfn "Hidrológiai szimuláció"
    printfn "========================="
    let initialRiver = { CurrentLevel = 2.0; MaxCapacity = 10.0 }
    let initialSoil = { Moisture = 50.0 }
    let weatherForecast = [
        { Rainfall = 20.0; Temperature = 15.0 }
        { Rainfall = 30.0; Temperature = 14.0 }
        { Rainfall = 10.0; Temperature = 16.0 }
    ]
    printfn "Időjárási adatok száma: %d" (List.length weatherForecast)
    let simulationResults = simulateRiver initialRiver initialSoil weatherForecast
    printfn "Szimuláció eredményeinek száma: %d" (List.length simulationResults)
    simulationResults |> List.iteri (fun i (river, warning) ->
        match warning with
        | Flooding -> Console.ForegroundColor <- ConsoleColor.Red
        | Warning _ -> Console.ForegroundColor <- ConsoleColor.Yellow
        | NoRisk -> Console.ForegroundColor <- ConsoleColor.Green
        printfn "Óra %d: Vízszint: %.2f m, Figyelmeztetés: %A" (i + 1) river.CurrentLevel warning
        Console.ResetColor())
    0