module DomainTypes

type WeatherData = {
    Rainfall: float
    Temperature: float
}

type RiverState = {
    CurrentLevel: float
    MaxCapacity: float
    SoilMoisture: float
}

type SoilCondition = {
    Moisture: float
}

type FloodWarning =
    | NoRisk
    | Warning of float
    | Flooding 