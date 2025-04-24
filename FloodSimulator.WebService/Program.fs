module Program

open System
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.StaticFiles
open Giraffe
open System.Text.Json
open Types
open Simulation

let jsonSerializerOptions = JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)

let simulateHandler : HttpHandler =
    fun (next: HttpFunc) (ctx: HttpContext) ->
        task {
            try
                let request = {
                    Date = ctx.Request.Query["date"].ToString()
                    City = ctx.Request.Query["city"].ToString()
                    Country = ctx.Request.Query["country"].ToString()
                }
                let result = runSimulation request
                return! ctx.WriteJsonAsync(result, jsonSerializerOptions)
            with
            | ex ->
                ctx.SetStatusCode 400
                return! ctx.WriteJsonAsync({| error = ex.Message |}, jsonSerializerOptions)
        }

let webApp : HttpHandler =
    choose [
        GET >=> route "/api/simulate" >=> simulateHandler
    ]

let configureApp (app: IApplicationBuilder) =
    let provider = FileExtensionContentTypeProvider()
    provider.Mappings[".js"] <- "application/javascript"
    provider.Mappings[".css"] <- "text/css"
    provider.Mappings[".html"] <- "text/html"
    provider.Mappings[".json"] <- "application/json"
    
    let staticFileOptions = StaticFileOptions(
        ContentTypeProvider = provider,
        DefaultContentType = "application/octet-stream",
        ServeUnknownFileTypes = true
    )
    
    app.UseDefaultFiles()
       .UseStaticFiles(staticFileOptions)
       .UseCors(fun builder ->
           builder.WithOrigins("http://localhost:5000", "http://localhost:5001", "http://127.0.0.1:5500")
                 .AllowAnyMethod()
                 .AllowAnyHeader()
                 |> ignore)
       .UseGiraffe webApp

let configureServices (services: IServiceCollection) =
    services.AddGiraffe()
            .AddSingleton<JsonSerializerOptions>(jsonSerializerOptions)
            .AddCors()
    |> ignore

[<EntryPoint>]
let main args =
    let builder = WebApplication.CreateBuilder(args)
    configureServices builder.Services
    let app = builder.Build()
    configureApp app
    app.Run()
    0 