module Server.Routes

open System
open System.Linq
open Giraffe
open Fun.OData.Giraffe
open Dtos.DemoData


let demoData =
  [
    { Id = 1
      Name = "name 1"
      Description = "demo data 1"
      Price = 12M
      Items = [
        { Id = 1
          Name = "item 1"
          CreatedDate = DateTime.Now.AddDays(-5.) }
        { Id = 2
          Name = "item 2"
          CreatedDate = DateTime.Now.AddDays(-4.) }
        { Id = 3
          Name = "item 3"
          CreatedDate = DateTime.Now.AddDays(-3.) }
      ]
      CreatedDate = DateTime.Now.AddDays(-10.)
      LastModifiedDate = None }
    { Id = 2
      Name = "name 2"
      Description = "demo data 2"
      Price = 13M
      Items = [
        { Id = 4
          Name = "item 4"
          CreatedDate = DateTime.Now.AddDays(-4.) }
      ]
      CreatedDate = DateTime.Now.AddDays(-1.)
      LastModifiedDate = Some DateTime.Now }
    for i in 3..20 do
      { Id = i
        Name = "name " + string i
        Description = "demo data " + string i
        Price = 14M + decimal i
        Items = []
        CreatedDate = DateTime.Now.AddDays(-1. - float i)
        LastModifiedDate = None }
  ]


let mainRoutes: HttpHandler =
    choose
        [
            GET >=> routeCi  "/demo"      >=> odataQ (fun _ _ -> demoData.AsQueryable())
            GET >=> routeCif "/demo(%i)"     (odataItem id (fun id _ _ -> demoData.Where(fun x -> x.Id = id).AsQueryable()))
        ]

