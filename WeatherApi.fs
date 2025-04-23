/// WeatherApi module - Handles fetching and processing weather data from external APIs
/// Provides functions for retrieving historical temperature data and making predictions

module WeatherApi

open System
open System.Net.Http
open System.Text.Json
open System.Threading.Tasks

/// Gets coordinates (latitude and longitude) for a given city and country
/// Uses OpenStreetMap Nominatim API to geocode the location
let getCoordinates (city: string) (country: string) = async {
    use client = new HttpClient()
    try
        // Format the query and encode it for URL
        let query = sprintf "%s, %s" city country
        let encodedQuery = System.Web.HttpUtility.UrlEncode(query)
        let url = sprintf "https://nominatim.openstreetmap.org/search?q=%s&format=json&limit=1" encodedQuery
        
        // Add user agent header (required by Nominatim)
        client.DefaultRequestHeaders.Add("User-Agent", "FloodSimulator/1.0")
        
        let! response = client.GetAsync(url) |> Async.AwaitTask
        if response.IsSuccessStatusCode then
            let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
            let jsonDoc = JsonDocument.Parse(content)
            let root = jsonDoc.RootElement
            
            if root.GetArrayLength() > 0 then
                let firstResult = root.[0]
                let latStr = firstResult.GetProperty("lat").GetString()
                let lonStr = firstResult.GetProperty("lon").GetString()
                match Double.TryParse(latStr), Double.TryParse(lonStr) with
                | (true, lat), (true, lon) -> return Some (lat, lon)
                | _ -> 
                    printfn "❌ Invalid coordinate format: lat=%s, lon=%s" latStr lonStr
                    return None
            else
                printfn "❌ No coordinates found for %s, %s" city country
                return None
        else
            printfn "❌ Geocoding API error: HTTP Status Code: %d (%s)" (int response.StatusCode) response.ReasonPhrase
            return None
    with
    | ex -> 
        printfn "❌ Geocoding error: %s" ex.Message
        return None
}

/// Fetches historical temperature and soil moisture data from Open-Meteo API
/// Retrieves data for the past 5 years within a ±3 day window of the target date
let fetchHistoricalData (lat: float) (lon: float) (endDate: string) = async {
    use client = new HttpClient()
    try
        let endDateParsed = DateTime.Parse(endDate)
        let results = ResizeArray<string>()
        // Get data for the past 5 years
        for yearOffset in -5 .. -1 do
            let targetDate = endDateParsed.AddYears(yearOffset)
            let startDate = targetDate.AddDays(-3.0).ToString("yyyy-MM-dd")
            let endDate = targetDate.AddDays(3.0).ToString("yyyy-MM-dd")
            let url = sprintf "https://archive-api.open-meteo.com/v1/archive?latitude=%.2f&longitude=%.2f&start_date=%s&end_date=%s&hourly=temperature_2m,soil_moisture_0_to_7cm" lat lon startDate endDate
            let! response = client.GetAsync(url) |> Async.AwaitTask
            if response.IsSuccessStatusCode then
                let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                results.Add(content)
            else
                let! errorContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                printfn "❌ API error (%s): HTTP Status Code: %d (%s)" (targetDate.ToString("yyyy-MM-dd")) (int response.StatusCode) response.ReasonPhrase
                printfn "Response content: %s" (if String.IsNullOrEmpty(errorContent) then "(empty)" else errorContent)
        return if results.Count > 0 then Some (results |> Seq.toList) else None
    with
    | ex -> 
        printfn "❌ API error: Exception: %s" ex.Message
        printfn "Response content: (not available due to exception)"
        return None
}

/// Converts soil moisture from m³/m³ to percentage
let convertMoistureToPercentage (moisture: float) =
    // Convert from m³/m³ to percentage (multiply by 100)
    // Typical soil moisture ranges from 0.1 to 0.4 m³/m³
    // We'll scale this to a 0-100% range
    let minMoisture = 0.1  // Minimum typical soil moisture
    let maxMoisture = 0.4  // Maximum typical soil moisture
    let percentage = ((moisture - minMoisture) / (maxMoisture - minMoisture)) * 100.0
    max 0.0 (min 100.0 percentage)  // Ensure result is between 0 and 100

/// Parses temperature and soil moisture data from JSON responses
/// Combines data from multiple JSON objects
let parseHistoricalData (jsonList: string list) : (float list * float list) option =
    try
        let allTemps = ResizeArray<float>()
        let allMoisture = ResizeArray<float>()
        for json in jsonList do
            let jsonDoc = JsonDocument.Parse(json)
            let hourly = jsonDoc.RootElement.GetProperty("hourly")
            let temps = hourly.GetProperty("temperature_2m").EnumerateArray()
            let moisture = hourly.GetProperty("soil_moisture_0_to_7cm").EnumerateArray()
            
            for temp in temps do
                allTemps.Add(temp.GetDouble())
            for moist in moisture do
                allMoisture.Add(convertMoistureToPercentage (moist.GetDouble()))
        Some (allTemps |> Seq.toList, allMoisture |> Seq.toList)
    with
    | ex -> 
        printfn "❌ JSON processing error: %s" ex.Message
        None

/// Calculates average temperature, temperature trend, and average soil moisture
let calculateStats (temps: float list) (moisture: float list) =
    let avgTemp = temps |> List.average
    let avgMoisture = moisture |> List.average
    
    let tempTrend = 
        let n = float temps.Length
        let x = [1.0 .. n]
        let sumX = x |> List.sum
        let sumY = temps |> List.sum
        let sumXY = List.zip x temps |> List.sumBy (fun (x, y) -> x * y)
        let sumXX = x |> List.sumBy (fun x -> x * x)
        let slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX)
        slope
    
    (avgTemp, tempTrend, avgMoisture)

/// Predicts temperatures for future days based on historical data
/// Uses the average temperature and trend to make predictions
let predictTemps (avg: float) (trend: float) (days: int) =
    [ for i in 1 .. days -> 
        avg + trend * float i ]

/// Main function that retrieves predicted temperatures and soil moisture for a given location and date
let getPredictedData (lat: float) (lon: float) (endDate: string) (forecastDays: int) = async {
    try
        let! jsonData = fetchHistoricalData lat lon endDate
        match jsonData with
        | Some jsonList ->
            match parseHistoricalData jsonList with
            | Some (temps, moisture) ->
                let avgTemp, tempTrend, avgMoisture = calculateStats temps moisture
                printfn "📊 API average temperature (historical data): %.2f°C" avgTemp
                printfn "📊 API average soil moisture (historical data): %.2f%%" avgMoisture
                let predictedTemps = predictTemps avgTemp tempTrend forecastDays
                return Some (predictedTemps, avgMoisture)
            | None -> return None
        | None -> return None
    with
    | ex ->
        printfn "❌ Date processing error: %s" ex.Message
        return None
}