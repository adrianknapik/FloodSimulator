module Visualization

open System
open XPlot.Plotly
open Types

/// Creates a line chart showing river levels, rainfall, and soil moisture over time
let createRiverLevelChart (simulationResults: (RiverState * FloodWarning) list) (weatherData: WeatherData list) (dates: DateTime list) =
    let riverLevels = simulationResults |> List.map (fun (state, _) -> state.CurrentLevel)
    let rainfall = weatherData |> List.map (fun w -> w.Rainfall)
    let soilMoisture = simulationResults |> List.map (fun (state, _) -> state.SoilMoisture)
    let dateStrings = dates |> List.map (fun d -> d.ToString("yyyy-MM-dd"))

    let riverTrace =
        Scatter(
            x = dateStrings,
            y = riverLevels,
            name = "Potential river Level (m)",
            line = Line(color = "blue")
        )

    let rainfallTrace =
        Scatter(
            x = dateStrings,
            y = rainfall,
            name = "Rainfall (mm)",
            yaxis = "y2",
            line = Line(color = "lightblue", dash = "dot")
        )

    let moistureTrace =
        Scatter(
            x = dateStrings,
            y = soilMoisture,
            name = "Soil Moisture (%)",
            yaxis = "y2",
            line = Line(color = "green", dash = "dot")
        )

    let layout = Layout(
        title = "River Level, Rainfall, and Soil Moisture Over Time",
        yaxis = Yaxis(title = "River Level (m)"),
        yaxis2 = Yaxis(
            title = "Rainfall (mm) / Soil Moisture (%)",
            overlaying = "y",
            side = "right"
        ),
        showlegend = true
    )

    [ riverTrace; rainfallTrace; moistureTrace ]
    |> Chart.Plot
    |> Chart.WithLayout layout
    |> Chart.WithWidth 900
    |> Chart.WithHeight 500

/// Creates a bar chart showing the current flood risk level
let createFloodRiskGauge (currentLevel: float) (maxCapacity: float) =
    let percentage = (currentLevel / maxCapacity) * 100.0
    let color = 
        if percentage >= 100.0 then "red"
        elif percentage >= 80.0 then "orange"
        else "green"

    let barTrace =
        Bar(
            x = ["Flood Risk"],
            y = [percentage],
            name = "Flood Risk Level",
            marker = Marker(color = color)
        )

    let layout = Layout(
        title = "Flood Risk Level",
        yaxis = Yaxis(
            title = "Risk Percentage (%)",
            range = [| 0; 120 |]
        ),
        showlegend = false
    )

    [ barTrace ]
    |> Chart.Plot
    |> Chart.WithLayout layout
    |> Chart.WithWidth 400
    |> Chart.WithHeight 300

/// Creates a temperature chart showing predicted vs actual temperatures
let createTemperatureChart (predictedTemps: float list) (actualTemps: float list) (dates: DateTime list) =
    let dateStrings = dates |> List.map (fun d -> d.ToString("yyyy-MM-dd"))
    let avgTemp = predictedTemps |> List.average

    let predictedTrace =
        Scatter(
            x = dateStrings,
            y = predictedTemps,
            name = "Predicted Temperature",
            line = Line(color = "red")
        )

    let actualTrace =
        Scatter(
            x = dateStrings,
            y = actualTemps,
            name = "Actual Temperature",
            line = Line(color = "orange", dash = "dot")
        )

    let avgTrace =
        Scatter(
            x = dateStrings,
            y = List.replicate dateStrings.Length avgTemp,
            name = sprintf "Average Temperature (%.1f°C)" avgTemp,
            line = Line(color = "green", dash = "dash")
        )

    let layout = Layout(
        title = "Temperature Comparison",
        yaxis = Yaxis(title = "Temperature (°C)"),
        showlegend = true
    )

    [ predictedTrace; actualTrace; avgTrace ]
    |> Chart.Plot
    |> Chart.WithLayout layout
    |> Chart.WithWidth 900
    |> Chart.WithHeight 400

/// Saves all visualization charts to HTML files
let saveVisualization (simulationResults: (RiverState * FloodWarning) list) 
                     (weatherData: WeatherData list) 
                     (dates: DateTime list)
                     (predictedTemps: float list)
                     (actualTemps: float list)
                     (outputPath: string) =
    // Create the charts
    let riverChart = createRiverLevelChart simulationResults weatherData dates
    let riskGauge = createFloodRiskGauge (List.last simulationResults |> fst).CurrentLevel (List.last simulationResults |> fst).MaxCapacity
    let tempChart = createTemperatureChart predictedTemps actualTemps dates
    let avgTemp = predictedTemps |> List.average
    
    // Get initial soil moisture
    let initialMoisture = 
        simulationResults 
        |> List.head 
        |> fst 
        |> fun state -> state.SoilMoisture

    // Save to HTML files
    let saveChart (chart: PlotlyChart) (filePath: string) =
        let html = String.concat "" [
            "<!DOCTYPE html><html><head>"
            "<script src='https://cdn.plot.ly/plotly-latest.min.js'></script>"
            "</head><body>"
            chart.GetInlineHtml()
            "</body></html>"
        ]
        System.IO.File.WriteAllText(filePath, html)

    saveChart riverChart (System.IO.Path.Combine(outputPath, "river_levels.html"))
    saveChart riskGauge (System.IO.Path.Combine(outputPath, "flood_risk.html"))
    saveChart tempChart (System.IO.Path.Combine(outputPath, "temperatures.html"))

    // Create an index file that combines all visualizations
    let indexHtml = String.concat "" [
        "<!DOCTYPE html><html><head>"
        "<title>Flood Simulation Results</title>"
        "<style>body{font-family:Arial,sans-serif;margin:20px}.chart-container{margin-bottom:30px}h1,h2{color:#333}.avg-temp{font-size:1.2em;color:#2c3e50;margin:10px 0}.moisture{font-size:1.2em;color:#2c3e50;margin:10px 0}</style>"
        "</head><body>"
        "<h1>Flood Simulation Results</h1>"
        sprintf "<div class='avg-temp'>Projected Average Temperature: <strong>%.1f°C</strong></div>" avgTemp
        sprintf "<div class='moisture'>Initial Soil Moisture: <strong>%.1f%%</strong></div>" initialMoisture
        "<div class='chart-container'><h2>River Levels, Rainfall, and Soil Moisture</h2>"
        "<iframe src='river_levels.html' width='100%' height='520px' frameborder='0'></iframe></div>"
        "<div class='chart-container'><h2>Current Flood Risk</h2>"
        "<iframe src='flood_risk.html' width='100%' height='320px' frameborder='0'></iframe></div>"
        "<div class='chart-container'><h2>Temperature Analysis</h2>"
        "<iframe src='temperatures.html' width='100%' height='420px' frameborder='0'></iframe></div>"
        "</body></html>"
    ]
    System.IO.File.WriteAllText(System.IO.Path.Combine(outputPath, "index.html"), indexHtml)