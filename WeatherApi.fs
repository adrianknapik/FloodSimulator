module WeatherApi

open System
open System.Net.Http
open System.Text.Json
open System.Threading.Tasks

// Historikus hőmérsékleti adatok lekérdezése az Open-Meteo API-ból az endDate ±3 napos ablakában az elmúlt 5 évben
let fetchHistoricalTemps (lat: float) (lon: float) (endDate: string) = async {
    use client = new HttpClient()
    try
        let endDateParsed = DateTime.Parse(endDate)
        let results = ResizeArray<string>()
        // Az elmúlt 5 év adatai
        for yearOffset in -5 .. -1 do
            let targetDate = endDateParsed.AddYears(yearOffset)
            let startDate = targetDate.AddDays(-3.0).ToString("yyyy-MM-dd")
            let endDate = targetDate.AddDays(3.0).ToString("yyyy-MM-dd")
            let url = sprintf "https://archive-api.open-meteo.com/v1/archive?latitude=%.2f&longitude=%.2f&start_date=%s&end_date=%s&hourly=temperature_2m" lat lon startDate endDate
            let! response = client.GetAsync(url) |> Async.AwaitTask
            if response.IsSuccessStatusCode then
                let! content = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                results.Add(content)
            else
                let! errorContent = response.Content.ReadAsStringAsync() |> Async.AwaitTask
                printfn "❌ API hiba (%s): HTTP Státuszkód: %d (%s)" (targetDate.ToString("yyyy-MM-dd")) (int response.StatusCode) response.ReasonPhrase
                printfn "Válasz tartalma: %s" (if String.IsNullOrEmpty(errorContent) then "(üres)" else errorContent)
        return if results.Count > 0 then Some (results |> Seq.toList) else None
    with
    | ex -> 
        printfn "❌ API hiba: Kivétel: %s" ex.Message
        printfn "Válasz tartalma: (nem elérhető a kivétel miatt)"
        return None
}

// JSON válasz feldolgozása (több JSON objektum hőmérsékleti adatainak kombinálása)
let parseTemps (jsonList: string list) : float list option =
    try
        let allTemps = ResizeArray<float>()
        for json in jsonList do
            let jsonDoc = JsonDocument.Parse(json)
            let hourly = jsonDoc.RootElement.GetProperty("hourly")
            let temps = hourly.GetProperty("temperature_2m").EnumerateArray()
            for temp in temps do
                allTemps.Add(temp.GetDouble())
        Some (allTemps |> Seq.toList)
    with
    | ex -> 
        printfn "❌ JSON feldolgozási hiba: %s" ex.Message
        None

// Átlag és trend kiszámítása
let calculateTempStats (temps: float list) =
    let avg = temps |> List.average
    let trend = 
        let n = float temps.Length
        let x = [1.0 .. n]
        let sumX = x |> List.sum
        let sumY = temps |> List.sum
        let sumXY = List.zip x temps |> List.sumBy (fun (x, y) -> x * y)
        let sumXX = x |> List.sumBy (fun x -> x * x)
        let slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX) // Egyszerű lineáris regresszió meredeksége
        slope
    (avg, trend)

// Előrejelzés a következő napokra (lineáris extrapoláció nélkül szezonális korrekció)
let predictTemps (avg: float) (trend: float) (days: int) =
    [ for i in 1 .. days -> 
        avg + trend * float i ]

// Fő függvény, amely koordináták és végdátum alapján az elmúlt 5 év ±3 napos adataiból előrejelzett hőmérsékleteket ad vissza
let getPredictedTemps (lat: float) (lon: float) (endDate: string) (forecastDays: int) = async {
    try
        let! jsonData = fetchHistoricalTemps lat lon endDate
        match jsonData with
        | Some jsonList ->
            match parseTemps jsonList with
            | Some temps ->
                let avgTemp, tempTrend = calculateTempStats temps
                printfn "📊 API átlaghőmérséklet (historikus adatok): %.2f°C" avgTemp
                let predictedTemps = predictTemps avgTemp tempTrend forecastDays
                return Some predictedTemps
            | None -> return None
        | None -> return None
    with
    | ex ->
        printfn "❌ Dátumfeldolgozási hiba: %s" ex.Message
        return None
}