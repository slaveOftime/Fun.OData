module Server.Routes

open System
open System.Linq
open Giraffe
open Microsoft.AspNet.OData.Builder
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


let ignorePrice (builder: EntitySetConfiguration<DemoData>) =
    builder.EntityType.Ignore(fun x -> x.Price)
  


let mainRoutes: HttpHandler =
    choose
        [
            GET >=> routeCi  "/demo"      >=> OData.query demoData
            GET >=> routeCif "/demo(%i)"     (OData.item (fun id -> demoData.FirstOrDefault(fun x -> x.Id = id)))

            GET >=> routeCi  "/demofluent"  >=> ODataQuery()
                                                  .withSource(demoData)
                                                  .configEntitySet(ignorePrice).query()

            GET >=> routeCif "/demofluent(%i)"  (fun id -> ODataQuery()
                                                              .byId(fun _ -> demoData.FirstOrDefault(fun x -> x.Id = id))
                                                              .configEntitySet(ignorePrice)
                                                              .query())

            GET >=> routeCi  "/demopro"   >=> OData.queryPro [
                                                ODataProp.Source demoData
                                                ODataProp.ConfigEntitySet (fun builder -> builder.EntityType.Ignore(fun x -> x.Price))
                                              ]

            GET >=> routeCif "/demopro(%i)"  (fun id -> OData.queryPro
                                                          [
                                                            ODataProp.ConfigEntitySet (fun builder -> builder.EntityType.Ignore(fun x -> x.Price))
                                                            ODataProp.ById (fun _ -> demoData.FirstOrDefault(fun x -> x.Id = id))
                                                          ])
        ]
