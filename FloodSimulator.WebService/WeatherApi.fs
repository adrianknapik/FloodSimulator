module WeatherApi

open System
open System.Net.Http
open System.Text.Json
open System.Threading.Tasks
open Types
open DomainTypes

/// Calculates the runoff coefficient based on soil moisture
let calculateRunoffCoefficient (moisture: float) = min 0.9 (max 0.1 (moisture / 100.0))

/// Updates the river level based on weather conditions and soil moisture
let updateRiverLevel (river: RiverState) (weather: WeatherData) (soil: SoilCondition) =
    let runoffCoefficient = calculateRunoffCoefficient soil.Moisture
    
    // Inflow calculation - more realistic response to rainfall
    let inflow = 
        if weather.Rainfall > 0.0 then
            // Convert mm of rain to meters and apply a more realistic multiplier
            // 1000mm = 1m, so we divide by 1000 to convert to meters
            // Then multiply by 0.1 to account for drainage area and other factors
            (weather.Rainfall / 1000.0) * runoffCoefficient * 0.1
        else
            // Base inflow from groundwater and tributaries
            // This prevents the river from continuously declining
            max 0.001 (soil.Moisture * 0.002)
    
    // Dynamic outflow calculation based on current level
    let baseOutflow = river.CurrentLevel * 0.02  // Reduced from 0.03 to 0.02 for even slower drainage
    let outflowMultiplier = 
        if river.CurrentLevel > river.MaxCapacity * 0.8 then 1.5  // Faster drainage at high levels
        elif river.CurrentLevel > river.MaxCapacity * 0.5 then 1.2
        else 1.0
    
    let outflow = baseOutflow * outflowMultiplier
    let newLevel = max 1.0 (river.CurrentLevel + inflow - outflow)  // Minimum level of 1.0 meters
    { 
        river with 
            CurrentLevel = min newLevel river.MaxCapacity
            SoilMoisture = soil.Moisture 
    }

/// Updates soil moisture based on weather conditions
let updateSoilMoisture (soil: SoilCondition) (weather: WeatherData) =
    let evaporation = max 0.0 (weather.Temperature * 0.03)
    let moistureChange = weather.Rainfall * 0.1 - evaporation
    let newMoisture = max 0.0 (min 100.0 (soil.Moisture + moistureChange))
    { Moisture = newMoisture }

/// Checks the flood risk based on the current river level
let checkFloodRisk (river: RiverState) =
    let threshold = river.MaxCapacity * 0.8
    if river.CurrentLevel >= river.MaxCapacity then Flooding
    elif river.CurrentLevel >= threshold then Warning river.CurrentLevel
    else NoRisk

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
                allMoisture.Add(moist.GetDouble())
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
let predictTemps (avg: float) (trend: float) (days: int) =
    let random = Random()
    let dayOfYear = DateTime.Now.DayOfYear
    let amplitude = 5.0  // Temperature variation amplitude
    
    [ for i in 1 .. days -> 
        // Base temperature with trend
        let baseTemp = avg + trend * float i
        
        // Add seasonal variation (sine wave)
        let seasonalVariation = amplitude * Math.Sin(2.0 * Math.PI * float (dayOfYear + i) / 365.0)
        
        // Add random daily variation (-2 to +2 degrees)
        let dailyVariation = random.NextDouble() * 4.0 - 2.0
        
        // Add weather system influence (random walk)
        let weatherSystem = random.NextDouble() * 3.0 - 1.5
        
        baseTemp + seasonalVariation + dailyVariation + weatherSystem
    ]

/// Main function that retrieves predicted temperatures and soil moisture for a given location and date
let getPredictedData (city: string) (country: string) (endDate: string) (forecastDays: int) = async {
    try
        let! coordinates = getCoordinates city country
        match coordinates with
        | Some (lat, lon) ->
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
        | None -> return None
    with
    | ex ->
        printfn "❌ Date processing error: %s" ex.Message
        return None
}

/// Generates random weather data for a specified number of days
let generateRandomWeather (random: Random) (days: int) (startDay: int) =
    let month = ((startDay - 1) % 365) / 30 + 1
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
    
    [ for day in startDay .. (startDay + days - 1) ->
        let rainfall = if random.NextDouble() < rainChance then random.NextDouble() * rainMax else 0.0
        { Rainfall = rainfall; Temperature = 0.0 }
    ]

/// Simulates river behavior over time
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

/// Estimates flood risk based on simulation results
let estimateFloodRiskFromTemp (simulationResults: (RiverState * FloodWarning) list) =
    let floodDays = simulationResults |> List.filter (snd >> function Flooding -> true | _ -> false) |> List.length
    let warningDays = simulationResults |> List.filter (snd >> function Warning _ -> true | _ -> false) |> List.length
    if floodDays > 0 then Flooding
    elif warningDays > 0 then Warning (simulationResults |> List.map (fst >> fun r -> r.CurrentLevel) |> List.max)
    else NoRisk 