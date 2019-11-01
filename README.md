# Fun.OData

* This is a little wrapper utils to help use odata functions in giraffe
* This is a simple queries generator for consume odata apis (only for GET)

# Server side
1. Set OData service for asp.net core
2. Use it like:
   ```fsharp
    // For any sequence
    GET >=> routeCi  "/demo"    >=> OData.query (demoData.AsQueryable())
    GET >=> routeCif "/demo(%i)"   (OData.item  (fun id -> demoData.Where(fun x -> x.Id = id).AsQueryable()))

    // With entityframework core
    GET >=> routeCi  "/person"  >=> OData.fromService  (fun (db: DemoDbContext) -> db.Persons.AsQueryable())
    GET >=> routeCif "/person(%i)" (OData.fromServicei (fun (db: DemoDbContext) id -> db.Persons.Where(fun x -> x.Id = id).AsQueryable()))
   ```

# Client side
Combine generated query in get request url
```fsharp
let query =
    [
        SelectType typeof<DemoDataBrief>
        Skip ((filter.Page - 1) * filter.PageSize)
        Take filter.PageSize
        Count
        External "etest1=123"
        External "etest2=56"
        Filter (filter.SearchName |> Option.map (contains "Name") |> Option.defaultValue "")
        Filter (andQueries [
            match filter.MinPrice with
            | None -> ()
            | Some x -> gt "Price" x
            match filter.FromCreatedDate with
            | None -> ()
            | Some x -> lt "CreatedDate" (x.ToString("yyyy-MM-dd"))
            match filter.ToCreatedDate with
            | None -> ()
            | Some x -> lt "CreatedDate" (x.ToString("yyyy-MM-dd"))
        ])
    ]
    |> Query.generate
    
Http.get (sprintf "%s/demo%s" serverHost query)
```