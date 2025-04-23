module Types

/// Represents weather data including rainfall and temperature
type WeatherData = { Rainfall: float; Temperature: float }

/// Represents soil moisture conditions
type SoilCondition = { Moisture: float }

/// Represents the current state of a river
type RiverState = { 
    CurrentLevel: float; 
    MaxCapacity: float;
    SoilMoisture: float 
}

/// Represents different levels of flood risk
type FloodWarning = | NoRisk | Warning of float | Flooding 