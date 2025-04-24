module App

open Fable.Core
open Fable.Core.JsInterop
open Browser.Dom
open Browser.Types
open Types
open Fable.Plotly

let private getElementById id = document.getElementById id :?> HTMLElement
let private getInputValue id = (document.getElementById id :?> HTMLInputElement).value

let private showElement (element: HTMLElement) = element.style.display <- "block"
let private hideElement (element: HTMLElement) = element.style.display <- "none"

let private updateText id text = 
    let element = getElementById id
    element.textContent <- text

let private createChart (elementId: string) (data: ChartData list) (layout: ChartLayout) =
    let traces = 
        data 
        |> List.map (fun d ->
            let trace = 
                {|
                    x = d.X
                    y = d.Y
                    name = d.Name
                    line = 
                        {|
                            color = d.Color
                            dash = d.Dash
                        |}
                |}
            
            match d.YAxis with
            | Some axis -> 
                {| trace with yaxis = axis |}
            | None -> trace
        )
        |> List.map (fun t -> t :> obj)
        |> List.toArray

    let layout = 
        {|
            title = layout.Title
            yaxis = 
                {|
                    title = layout.YAxisTitle
                |}
            yaxis2 = 
                layout.YAxis2Title 
                |> Option.map (fun title ->
                    {|
                        title = title
                        overlaying = "y"
                        side = "right"
                    |} :> obj
                )
            showlegend = layout.ShowLegend
        |} :> obj

    Plotly.newPlot(elementId, traces, layout)

let private displayResults (result: SimulationResult) =
    // Update summary cards
    updateText "avgTemp" (sprintf "%.1f°C" result.AverageTemperature)
    updateText "soilMoisture" (sprintf "%.1f%%" result.InitialSoilMoisture)
    updateText "floodRisk" result.FloodRisk

    // Create river chart
    let riverData = [
        {
            X = result.Dates
            Y = result.RiverLevels
            Name = "River Level (m)"
            Color = "blue"
            Dash = None
            YAxis = None
        }
        {
            X = result.Dates
            Y = result.Rainfall
            Name = "Rainfall (mm)"
            Color = "lightblue"
            Dash = Some "dot"
            YAxis = Some "y2"
        }
        {
            X = result.Dates
            Y = result.SoilMoisture
            Name = "Soil Moisture (%)"
            Color = "green"
            Dash = Some "dot"
            YAxis = Some "y2"
        }
    ]

    let riverLayout = {
        Title = "River Level, Rainfall, and Soil Moisture Over Time"
        YAxisTitle = "River Level (m)"
        YAxis2Title = Some "Rainfall (mm) / Soil Moisture (%)"
        ShowLegend = true
    }

    createChart "riverChart" riverData riverLayout

    // Create risk gauge
    let percentage = (result.CurrentLevel / result.MaxCapacity) * 100.0
    let color = 
        if percentage >= 100.0 then "red"
        elif percentage >= 80.0 then "orange"
        else "green"

    let riskData = [
        {
            X = ["Flood Risk"]
            Y = [percentage]
            Name = "Flood Risk Level"
            Color = color
            Dash = None
            YAxis = None
        }
    ]

    let riskLayout = {
        Title = "Flood Risk Level"
        YAxisTitle = "Risk Percentage (%)"
        YAxis2Title = None
        ShowLegend = false
    }

    createChart "riskGauge" riskData riskLayout

    // Create temperature chart
    let tempData = [
        {
            X = result.Dates
            Y = result.PredictedTemps
            Name = "Predicted Temperature"
            Color = "red"
            Dash = None
            YAxis = None
        }
        {
            X = result.Dates
            Y = result.ActualTemps
            Name = "Actual Temperature"
            Color = "orange"
            Dash = Some "dot"
            YAxis = None
        }
        {
            X = result.Dates
            Y = List.replicate result.Dates.Length result.AverageTemperature
            Name = sprintf "Average Temperature (%.1f°C)" result.AverageTemperature
            Color = "green"
            Dash = Some "dash"
            YAxis = None
        }
    ]

    let tempLayout = {
        Title = "Temperature Comparison"
        YAxisTitle = "Temperature (°C)"
        YAxis2Title = None
        ShowLegend = true
    }

    createChart "tempChart" tempData tempLayout

    // Show results container
    showElement (getElementById "resultContainer")

let private runSimulation (request: SimulationRequest) =
    async {
        try
            let! response = 
                Fetch.fetch "/api/simulate" [
                    Method HttpMethod.POST
                    Headers [ 
                        HttpRequestHeaders.ContentType "application/json"
                    ]
                    Body (toJson request)
                ]
                |> Async.AwaitPromise

            if not response.Ok then
                failwith "Simulation failed"

            let! result = response.json<SimulationResult>() |> Async.AwaitPromise
            displayResults result
        with
        | ex ->
            let errorMessage = getElementById "errorMessage"
            errorMessage.textContent <- ex.Message
            showElement errorMessage
    }

let private handleSubmit (e: Event) =
    e.preventDefault()
    
    let loading = getElementById "loading"
    let resultContainer = getElementById "resultContainer"
    let errorMessage = getElementById "errorMessage"

    showElement loading
    hideElement resultContainer
    hideElement errorMessage

    let request = {
        Date = getInputValue "date"
        City = getInputValue "city"
        Country = getInputValue "country"
    }

    runSimulation request
    |> Async.Start

let private init () =
    let form = getElementById "simulationForm" :?> HTMLFormElement
    form.addEventListener("submit", handleSubmit)

// Initialize the app when the DOM is loaded
document.addEventListener("DOMContentLoaded", fun _ -> init()) 