module Server.Startup

open System
open System.Linq
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.AspNet.OData.Extensions
open Microsoft.Extensions.DependencyInjection
open Giraffe
open Giraffe.Serialization.Json
open Db


let seedDb (db: DemoDbContext) =
    db.Database.EnsureCreated() |> ignore

    if db.Persons.Any() |> not then
      db.Persons.Add(Person(Name = "p1", CreatedDate = DateTime.Now, Roles = [ Role(Caption = "Admin", Credential = "2423") ].ToList())) |> ignore
      db.Persons.Add(Person(Name = "p2", CreatedDate = DateTime.Now, Roles = [ Role(Caption = "Admin", Credential = "2423") ].ToList())) |> ignore
      db.Persons.Add(Person(Name = "p3", CreatedDate = DateTime.Now, Roles = [ Role(Caption = "Guest", Credential = "1234") ].ToList())) |> ignore
      db.SaveChanges() |> ignore


[<EntryPoint>]
let main args =
  WebHost
    .CreateDefaultBuilder()
    .CaptureStartupErrors(true)
    .Configure(fun application ->
        application.ApplicationServices.CreateScope().ServiceProvider.GetService<DemoDbContext>() |> seedDb
        application
          .UseCors(fun op -> op.AllowAnyOrigin() |> ignore)
          .UseRouting() |> ignore
        application
          .UseGiraffe(Routes.mainRoutes) |> ignore
        application
          .UseEndpoints(fun builder ->
              builder.EnableDependencyInjection() |> ignore) |> ignore)
    .ConfigureServices(fun services ->
        services.AddCors() |> ignore
        services.AddOData() |> ignore
        services.AddGiraffe() |> ignore
        services.AddDbContext<DemoDbContext>() |> ignore
        services.AddSingleton<IJsonSerializer>(Serializer.FSharpLuJsonSerializer()) |> ignore)
    .UseUrls("http://localhost:5000")
    .UseIISIntegration()
    .Build()
    .Run()
  1
