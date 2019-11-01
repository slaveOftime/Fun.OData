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


let secureDemoData (builder: ODataConventionModelBuilder) =
    builder.EntitySet<DemoData>("DemoData").EntityType.Ignore(fun x -> x.Price)
  

let configEntitSetCredential =
    ODataProp.ConfigEntitySet (fun builder ->
      //builder.EntitySet<Person>("Person").EntityType.Ignore(fun x -> x.Roles)
      builder.EntitySet<Role>("Role").EntityType.Ignore(fun x -> x.Credential))


let mainRoutes: HttpHandler =
    choose
        [
            GET >=> routeCi  "/demo"      >=> OData.query (demoData.AsQueryable())
            GET >=> routeCif "/demo(%i)"     (OData.item (fun id -> demoData.Where(fun x -> x.Id = id).AsQueryable()))

            GET >=> routeCi  "/demofluent"  >=> ODataQuery()
                                                  .source(demoData.AsQueryable())
                                                  .configEntitySet(secureDemoData).query()

            GET >=> routeCif "/demofluent(%i)"  (fun id -> ODataQuery()
                                                              .filter(fun _ -> demoData.Where(fun x -> x.Id = id).AsQueryable())
                                                              .configEntitySet(secureDemoData)
                                                              .query())

            GET >=> routeCi  "/demopro"   >=> OData.queryPro [
                                                ODataProp.Source (demoData.AsQueryable())
                                                ODataProp.ConfigEntitySet secureDemoData
                                              ]

            GET >=> routeCif "/demopro(%i)"  (fun id -> OData.queryPro
                                                          [
                                                            ODataProp.ConfigEntitySet secureDemoData
                                                            ODataProp.Filter (fun _ -> demoData.Where(fun x -> x.Id = id).AsQueryable())
                                                          ])

            GET >=> routeCi  "/person"     >=> OData.queryFromService [ configEntitSetCredential ] (fun (db: DemoDbContext) -> db.Persons.AsQueryable())
            GET >=> routeCi  "/personSvc"  >=> OData.fromService (fun (db: DemoDbContext) -> db.Persons.AsQueryable())
            GET >=> routeCif "/personSvc(%i)" (OData.fromServicei (fun (db: DemoDbContext) id -> db.Persons.Where(fun x -> x.Id = id).AsQueryable()))
        ]
