module Simulation

open System
open Types
open DomainTypes
open WeatherApi

let runSimulation (request: SimulationRequest) =
    let random = Random()
    let forecastDays = 14
    
    // Generate random weather data
    let weatherData = generateRandomWeather random forecastDays 1
    
    // Get predicted temperatures
    let predictedTemps = getPredictedData request.City request.Country request.Date forecastDays
                        |> Async.RunSynchronously
                        |> Option.defaultValue ([], 0.0)
    
    let predictedTempsList, avgMoisture = predictedTemps
    
    // Initialize river and soil conditions with more realistic values
    let initialRiver = { 
        CurrentLevel = 2.0  // Start with a lower initial level
        MaxCapacity = 10.0  // Reduced max capacity for more realistic scale
        SoilMoisture = avgMoisture 
    }
    let initialSoil = { 
        Moisture = max 20.0 (min 80.0 avgMoisture)  // Ensure soil moisture is between 20% and 80%
    }
    
    // Run simulation
    let simulationResults = simulateRiver initialRiver initialSoil weatherData predictedTempsList
    
    // Calculate average temperature
    let avgTemp = predictedTempsList |> List.average
    
    // Create simulation result
    {
        AverageTemperature = avgTemp
        InitialSoilMoisture = initialSoil.Moisture
        FloodRisk = "Low"  // This will be updated based on simulation results
        RiverLevels = simulationResults |> List.map (fst >> fun r -> r.CurrentLevel)
        Rainfall = weatherData |> List.map (fun w -> w.Rainfall)
        SoilMoisture = simulationResults |> List.map (fst >> fun r -> r.SoilMoisture)
        PredictedTemps = predictedTempsList
        ActualTemps = []  // This will be populated with actual temperatures when available
        Dates = []  // This will be populated with dates when available
        CurrentLevel = initialRiver.CurrentLevel
        MaxCapacity = initialRiver.MaxCapacity
    } 