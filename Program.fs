/// Flood Simulator - A program that simulates river flooding based on weather conditions
/// and soil moisture. It uses historical weather data to predict potential flood risks.

module FloodSimulator

open System
open System.Threading.Tasks
open WeatherApi
open Visualization
open Types

/// Calculates the runoff coefficient based on soil moisture
/// The coefficient is constrained between 0.1 and 0.9
let calculateRunoffCoefficient (moisture: float) = min 0.9 (max 0.1 (moisture / 100.0))

/// Updates the river level based on weather conditions and soil moisture
/// Takes into account inflow from rainfall and outflow from the river
let updateRiverLevel (river: RiverState) (weather: WeatherData) (soil: SoilCondition) =
    let runoffCoefficient = calculateRunoffCoefficient soil.Moisture
    
    // Inflow calculation - more sensitive to actual rainfall
    let inflow = 
        if weather.Rainfall > 0.0 then
            weather.Rainfall * runoffCoefficient * 0.05  // Reduced multiplier for more realistic inflow
        else
            soil.Moisture * 0.001  // Very small base inflow from soil moisture when not raining
    
    // Dynamic outflow calculation based on current level
    let baseOutflow = river.CurrentLevel * 0.1  // Increased from 0.05 to 0.1 for faster drainage
    let outflowMultiplier = 
        if river.CurrentLevel > river.MaxCapacity * 0.8 then 1.5  // Faster drainage at high levels
        elif river.CurrentLevel > river.MaxCapacity * 0.5 then 1.2
        else 1.0
    
    let outflow = baseOutflow * outflowMultiplier
    let newLevel = max 0.0 (river.CurrentLevel + inflow - outflow)
    { 
        river with 
            CurrentLevel = min newLevel river.MaxCapacity
            SoilMoisture = soil.Moisture 
    }

/// Checks the flood risk based on the current river level
/// Returns NoRisk, Warning, or Flooding based on the river's capacity
let checkFloodRisk (river: RiverState) =
    let threshold = river.MaxCapacity * 0.8
    if river.CurrentLevel >= river.MaxCapacity then Flooding
    elif river.CurrentLevel >= threshold then Warning river.CurrentLevel
    else NoRisk

/// Updates soil moisture based on weather conditions
let updateSoilMoisture (soil: SoilCondition) (weather: WeatherData) =
    let evaporation = max 0.0 (weather.Temperature * 0.03)
    let moistureChange = weather.Rainfall * 0.1 - evaporation
    let newMoisture = max 0.0 (min 100.0 (soil.Moisture + moistureChange))
    { Moisture = newMoisture }

/// Simulates river behavior over time based on weather data and temperature predictions
/// Returns a list of river states and flood warnings for each time step
let simulateRiver (initialRiver: RiverState) (initialSoil: SoilCondition) (weatherData: WeatherData list) (predictedTemps: float list) =
    let rec simulate (river: RiverState) (soil: SoilCondition) (weather: WeatherData list) (temps: float list) (results: (RiverState * FloodWarning) list) =
        match weather, temps with
        | [], _ | _, [] -> List.rev results
        | currentWeather :: restWeather, temp :: restTemps ->
            let newWeather = { currentWeather with Temperature = temp }
            let newRiver = updateRiverLevel river newWeather soil
            let newSoil = updateSoilMoisture soil newWeather
            let warning = checkFloodRisk newRiver
            simulate 
                newRiver 
                newSoil 
                restWeather 
                restTemps 
                ((newRiver, warning) :: results)
    
    simulate initialRiver initialSoil weatherData predictedTemps []

/// Generates weather data for a specific day based on monthly patterns
/// Uses a random number generator to create realistic variations
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
    { Rainfall = rainfall; Temperature = 0.0 } // Temperature comes from API

/// Generates random weather data for a specified number of days
let generateRandomWeather (random: Random) (days: int) (startDay: int) =
    [ for day in startDay .. (startDay + days - 1) -> generateMonthlyWeather random day ]

/// Estimates flood risk based on simulation results
/// Returns the highest risk level found in the simulation
let estimateFloodRiskFromTemp (simulationResults: (RiverState * FloodWarning) list) =
    let floodDays = simulationResults |> List.filter (snd >> function Flooding -> true | _ -> false) |> List.length
    let warningDays = simulationResults |> List.filter (snd >> function Warning _ -> true | _ -> false) |> List.length
    if floodDays > 0 then Flooding
    elif warningDays > 0 then Warning (simulationResults |> List.map (fst >> fun r -> r.CurrentLevel) |> List.max)
    else NoRisk

/// Prompts the user to enter a date in YYYY-MM-DD format
/// Keeps asking until a valid date is provided
let rec getUserDate () =
    printf "Adja meg a dátumot (YYYY-MM-DD): "
    let input = Console.ReadLine().Trim()
    match DateTime.TryParse(input) with
    | true, _ -> input
    | false, _ ->
        printfn "Érvénytelen dátumformátum. Próbálja újra."
        getUserDate ()

/// Prompts the user to enter a city name
let rec getUserCity () =
    printf "Adja meg a város nevét: "
    let input = Console.ReadLine().Trim()
    if String.IsNullOrEmpty(input) then
        printfn "A város neve nem lehet üres. Próbálja újra."
        getUserCity()
    else
        input

/// Prompts the user to enter a country name
let rec getUserCountry () =
    printf "Adja meg az ország nevét: "
    let input = Console.ReadLine().Trim()
    if String.IsNullOrEmpty(input) then
        printfn "Az ország neve nem lehet üres. Próbálja újra."
        getUserCountry()
    else
        input

/// Asks the user if they want to try again
let rec askTryAgain () =
    printf "\nWould you like to try again? (yes/no): "
    let input = Console.ReadLine().Trim().ToLower()
    match input with
    | "yes" | "y" -> true
    | "no" | "n" -> false
    | _ ->
        printfn "Invalid input. Please enter 'yes' or 'no'."
        askTryAgain()

/// Runs the flood simulation with the given parameters
let runSimulation endDate lat lon =
    let initialRiver = { 
        CurrentLevel = 2.0; 
        MaxCapacity = 10.0;
        SoilMoisture = 0.0  // Will be updated with real data
    }
    let startDate = DateTime.Parse(endDate)
    let random = Random()
    let simulationDays = 14
    
    let startDay = startDate.DayOfYear
    let dates = [
        for i in 0 .. simulationDays - 1 ->
            startDate.AddDays(float i)
    ]
    
    let randomness = Random()
    let randomWeather = generateRandomWeather randomness simulationDays startDay
    
    let predictedData = 
        getPredictedData lat lon endDate simulationDays 
        |> Async.RunSynchronously
        |> Option.defaultValue (List.replicate simulationDays 20.0, 50.0)
    
    let predictedTemps, avgMoisture = predictedData
    let initialSoil = { Moisture = avgMoisture }
    let initialRiver = { initialRiver with SoilMoisture = avgMoisture }
    
    printfn "\nInitial soil moisture (from historical data): %.1f%%" initialSoil.Moisture
    printfn "\nTemperature data for average calculation:"
    predictedTemps |> List.iteri (fun i temp -> 
        let simDate = dates.[i].ToString("yyyy-MM-dd")
        printfn "%s: %.1f°C" simDate temp)
    
    let simulationResults = simulateRiver initialRiver initialSoil randomWeather predictedTemps
    let floodRisk = estimateFloodRiskFromTemp simulationResults
    
    // Print results
    printfn "\nAverage temperature (%d days, from %s): %.2f°C" simulationDays endDate (predictedTemps |> List.average)
    printfn "Flood risk: %A" floodRisk
    
    simulationResults |> List.iteri (fun i (river, warning) ->
        let simDate = dates.[i].ToString("yyyy-MM-dd")
        printfn "%s: Water level: %.2f m, Rainfall: %.2f mm, Temperature: %.1f°C, Warning: %A" 
            simDate river.CurrentLevel randomWeather.[i].Rainfall predictedTemps.[i] warning)
    
    // Generate visualizations
    let outputPath = "simulation_results"
    System.IO.Directory.CreateDirectory(outputPath) |> ignore
    
    Visualization.saveVisualization 
        simulationResults 
        randomWeather 
        dates 
        predictedTemps 
        (predictedTemps |> List.map (fun t -> t + random.NextDouble() * 2.0 - 1.0)) // Simulated actual temps
        outputPath
    
    printfn "\nVisualization saved to %s/index.html" outputPath

[<EntryPoint>]
let main argv =
    let rec runProgram () =
        let endDate = getUserDate()
        let city = getUserCity()
        let country = getUserCountry()
        
        printfn "\nKoordináták lekérése %s, %s számára..." city country
        let coordinates = 
            WeatherApi.getCoordinates city country 
            |> Async.RunSynchronously
        
        match coordinates with
        | None ->
            printfn "Nem sikerült megtalálni a koordinátákat. Használom az alapértelmezett értékeket."
            let lat, lon = 47.5, 19.0  // Default to Budapest
            runSimulation endDate lat lon
        | Some (lat, lon) ->
            printfn "Koordináták: %.2f, %.2f" lat lon
            runSimulation endDate lat lon
        
        if askTryAgain() then
            runProgram()
        else
            printfn "\nThank you for using the Flood Simulator!"
            0
    
    runProgram()