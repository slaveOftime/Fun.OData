module Server.Startup

open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNet.OData.Extensions
open Giraffe


[<EntryPoint>]
let main args =
  WebHost
    .CreateDefaultBuilder()
    .CaptureStartupErrors(true)
    .Configure(fun app ->
        app
          .UseCors(fun op -> op.AllowAnyOrigin() |> ignore)
          .UseMvc(fun op -> op.EnableDependencyInjection())
          .UseGiraffe Routes.mainRoutes)
    .ConfigureServices(fun services ->
        services.AddMvc() |> ignore
        services.AddOData() |> ignore
        services.AddGiraffe() |> ignore)
    .UseIISIntegration()
    .Build()
    .Run()
  1
