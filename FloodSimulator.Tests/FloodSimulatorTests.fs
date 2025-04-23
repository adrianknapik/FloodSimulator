module FloodSimulator.Tests

open System
open Xunit
open FloodSimulator

[<Fact>]
let ``calculateRunoffCoefficient should return values between 0.1 and 0.9`` () =
    // Test with various moisture values
    let testMoisture = [0.0; 50.0; 100.0; 150.0]
    for moisture in testMoisture do
        let result = calculateRunoffCoefficient moisture
        Assert.True(result >= 0.1 && result <= 0.9)

[<Fact>]
let ``updateRiverLevel should not exceed max capacity`` () =
    let river = { CurrentLevel = 5.0; MaxCapacity = 10.0 }
    let weather = { Rainfall = 100.0; Temperature = 20.0 }
    let soil = { Moisture = 80.0 }
    
    let result = updateRiverLevel river weather soil
    Assert.True(result.CurrentLevel <= river.MaxCapacity)

[<Fact>]
let ``checkFloodRisk should return correct warning levels`` () =
    let river = { CurrentLevel = 0.0; MaxCapacity = 10.0 }
    
    // Test NoRisk
    let noRiskRiver = { river with CurrentLevel = 5.0 }
    Assert.Equal(NoRisk, checkFloodRisk noRiskRiver)
    
    // Test Warning
    let warningRiver = { river with CurrentLevel = 8.5 }
    match checkFloodRisk warningRiver with
    | Warning level -> Assert.True(level > 8.0)
    | _ -> Assert.True(false, "Expected Warning")
    
    // Test Flooding
    let floodingRiver = { river with CurrentLevel = 10.0 }
    Assert.Equal(Flooding, checkFloodRisk floodingRiver)

[<Fact>]
let ``simulateRiver should maintain list length`` () =
    let initialRiver = { CurrentLevel = 2.0; MaxCapacity = 10.0 }
    let initialSoil = { Moisture = 50.0 }
    let weatherData = [
        { Rainfall = 10.0; Temperature = 20.0 }
        { Rainfall = 20.0; Temperature = 25.0 }
    ]
    let predictedTemps = [20.0; 25.0]
    
    let results = simulateRiver initialRiver initialSoil weatherData predictedTemps
    Assert.Equal(2, List.length results)

[<Fact>]
let ``generateMonthlyWeather should return valid weather data`` () =
    let random = Random(42) // Fixed seed for reproducibility
    let weather = generateMonthlyWeather random 1
    Assert.True(weather.Rainfall >= 0.0)
    Assert.Equal(0.0, weather.Temperature) // Temperature is set to 0.0 as it comes from API

[<Fact>]
let ``estimateFloodRiskFromTemp should correctly identify flood risk`` () =
    let simulationResults = [
        ({ CurrentLevel = 5.0; MaxCapacity = 10.0 }, NoRisk)
        ({ CurrentLevel = 9.0; MaxCapacity = 10.0 }, Warning 9.0)
        ({ CurrentLevel = 10.0; MaxCapacity = 10.0 }, Flooding)
    ]
    
    let risk = estimateFloodRiskFromTemp simulationResults
    Assert.Equal(Flooding, risk) 