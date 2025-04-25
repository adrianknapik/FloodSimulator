# Flood Simulation System

## Overview

This project is a **preliminary** flood simulation system built with F# that models river behavior based on weather conditions, soil moisture, and historical data. Key features include **simulation methods** for predicting river levels and flood risks, and a **web service** integrating external APIs (OpenStreetMap Nominatim and Open-Meteo) for real-world data.

## Try It Live

Experience the application live at [http://knapkom.com/due/fsharp/](http://knapkom.com/due/fsharp/).

## Screenshots

Below are placeholders for screenshots of the application.

- **Dashboard**:  
  ![Homepage Screenshot](http://knapkom.com/due/fsharp/images/Dashboard.png)

- **Dashboard With Simulation Results**:  
  ![Simulation Results Screenshot](http://knapkom.com/due/fsharp/images/DashboardWStats.png)

- **Graph With Simulation Results**:
  ![Simulation Results Screenshot2](http://knapkom.com/due/fsharp/images/Graphs.png)

## Main Features

### Simulation Methods

The simulation methods model river dynamics and flood risk:

- **River Level Updates**: The `updateRiverLevel` function calculates inflow (rainfall, groundwater) and outflow based on river levels, using a runoff coefficient from soil moisture.
- **Soil Moisture Dynamics**: The `updateSoilMoisture` function adjusts moisture based on rainfall and evaporation.
- **Flood Risk Assessment**: The `checkFloodRisk` and `estimateFloodRiskFromTemp` functions evaluate risks using river level thresholds.
- **Historical Data Integration**: Historical temperature and soil moisture data are fetched and processed to inform predictions (`calculateStats`, `predictTemps`).

These are orchestrated in the `simulateRiver` function, running time-based simulations with random weather and predicted temperatures.

### Web Service

External APIs enhance simulation accuracy:

- **OpenStreetMap Nominatim API**: The `getCoordinates` function retrieves location-specific coordinates.
- **Open-Meteo API**: The `fetchHistoricalData` function pulls historical weather and soil moisture data, parsed via `parseHistoricalData`.
- Asynchronous integration ensures efficient data retrieval.

## Simulation Example

To run a simulation, the system uses a `SimulationRequest` object. Example:

```f#
let request = { City = "London"; Country = "UK"; Date = "2025-04-25"; ForecastDays = 14 }
let result = Simulation.runSimulation request
```

This returns a `SimulationResult` with average temperature, soil moisture, river levels, rainfall, and flood risk.

## Potential Future Development

As a preliminary application, future plans include building a **full-scale web application** using **WebSharper**:

- **Interactive UI**: Real-time visualizations of river levels and flood risks.
- **User Inputs**: Web forms for simulation parameters.
- **Real-Time Data**: Live weather feeds for dynamic assessments.
- **Scalability**: Support for multiple simulations via WebSharper’s reactive model.
- **Authentication**: User accounts for saving simulation data.
- **Deployment**: Cloud-hosted global access.
