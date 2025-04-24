module Types

type SimulationRequest = {
    Date: string
    City: string
    Country: string
}

type SimulationResult = {
    AverageTemperature: float
    InitialSoilMoisture: float
    FloodRisk: string
    RiverLevels: float list
    Rainfall: float list
    SoilMoisture: float list
    PredictedTemps: float list
    ActualTemps: float list
    Dates: string list
    CurrentLevel: float
    MaxCapacity: float
}

type ChartData = {
    X: string list
    Y: float list
    Name: string
    Color: string
    Dash: string option
    YAxis: string option
}

type ChartLayout = {
    Title: string
    YAxisTitle: string
    YAxis2Title: string option
    ShowLegend: bool
} 