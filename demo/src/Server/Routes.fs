module Server.Routes

open System
open System.Linq
open Giraffe
open Microsoft.AspNet.OData.Builder
open Fun.OData.Giraffe
open Dtos.DemoData
open Db


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


let configDemoData (builder: ODataConventionModelBuilder) =
    builder
      .SetEntityType<DemoData>(fun ty -> ty.Ignore(fun x -> x.Price))
    |> ignore

let configRole (builder: ODataConventionModelBuilder) =
    builder
      //.SetEntityType<Person>(fun ty -> ty.Ignore(fun x -> x.Roles))
      .SetEntityType<Role>(fun ty -> ty.Ignore(fun x -> x.Credential))
    |> ignore


let customerToJson (data: obj) =
    System.Text.Json.JsonSerializer.Serialize(data)
    

let mainRoutes: HttpHandler =
    choose [
        GET >=> routeCi  "/demo"                      >=> OData.query (demoData.AsQueryable())

        GET >=> routeCi  "/demo/serilization"         >=> OData.queryPro [
                                                            ODataProp.Source ([ for i in 1..10 -> {| Id = i |} ].AsQueryable())
                                                            ODataProp.ToJson (customerToJson)
                                                          ]

        GET >=> routeCif "/demo(%i)"                     (OData.item  (fun id -> demoData.Where(fun x -> x.Id = id).AsQueryable()))
        GET >=> routeCi  "/demopro"                   >=> OData.queryPro [ ODataProp.Source (demoData.AsQueryable()); ODataProp.ConfigEntitySet configDemoData; ]
        GET >=> routeCif "/demopro(%i)"        (fun id -> OData.queryPro [ ODataProp.Single (fun _ -> demoData.Where(fun x -> x.Id = id).AsQueryable()); ODataProp.ConfigEntitySet configDemoData ])
        GET >=> routeCi  "/demofluent"                >=> ODataQuery().Source(demoData.AsQueryable()).ConfigEntitySet(configDemoData).Build()
        GET >=> routeCif "/demofluent(%i)"     (fun id -> ODataQuery().Single(fun _ -> demoData.Where(fun x -> x.Id = id).AsQueryable()).ConfigEntitySet(configDemoData).Build())

        GET >=> routeCi  "/person"                    >=> OData.fromService     (fun (db: DemoDbContext) -> db.Persons.AsQueryable())
        GET >=> routeCif "/person(%i)"                   (OData.fromServicei    (fun (db: DemoDbContext) id -> db.Persons.Where(fun x -> x.Id = id).AsQueryable()))
        GET >=> routeCi  "/personpro"                 >=> OData.fromServicePro  (fun (db: DemoDbContext) -> db.Persons.AsQueryable()) [ ODataProp.ConfigEntitySet configRole ]
        GET >=> routeCif "/personpro(%i)"                (OData.fromServiceProi (fun (db: DemoDbContext) id -> db.Persons.Where(fun x -> x.Id = id).AsQueryable()) [ ODataProp.ConfigEntitySet configRole ])
        GET >=> routeCi  "/personfluent"              >=> ODataQuery().FromService<DemoDbContext>(fun db -> db.Persons.AsQueryable()).ConfigEntitySet(configRole).Build()
        GET >=> routeCif "/personfluent(%i)"   (fun id -> ODataQuery().FromService<DemoDbContext>(fun db -> db.Persons.AsQueryable()).Single(fun x -> x.Where(fun x -> x.Id = id)).ConfigEntitySet(configRole).Build())
    ]
     