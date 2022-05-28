#nowarn "0020"

open System
open System.Net.Http
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Components.WebAssembly.Hosting

let builder = WebAssemblyHostBuilder.CreateDefault(Environment.GetCommandLineArgs())

builder.RootComponents.Add(typeof<ODataDemo.Wasm.App.AppComp>, "#app")
builder.Services.AddFunBlazorWasm()

builder.Services.AddSingleton<HttpClient>(fun _ ->
    let httpClient = new HttpClient()
    httpClient.BaseAddress <- Uri builder.HostEnvironment.BaseAddress
    httpClient
)

builder.Build().RunAsync()
