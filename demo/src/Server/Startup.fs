module Server.Startup

open System
open System.Linq
open Microsoft.AspNetCore
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNet.OData.Extensions
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
    .Configure(fun app ->
        app.ApplicationServices.CreateScope().ServiceProvider.GetService<DemoDbContext>() |> seedDb
        app
          .UseCors(fun op -> op.AllowAnyOrigin() |> ignore)
          .UseMvc(fun op -> op.EnableDependencyInjection())
          .UseGiraffe Routes.mainRoutes)
    .ConfigureServices(fun services ->
        services.AddMvc() |> ignore
        services.AddOData() |> ignore
        services.AddGiraffe() |> ignore
        services.AddDbContext<DemoDbContext>() |> ignore
        services.AddSingleton<IJsonSerializer>(Serializer.FSharpLuJsonSerializer()) |> ignore)
    .UseUrls("http://localhost:5000")
    .UseIISIntegration()
    .Build()
    .Run()
  1
