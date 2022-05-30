# Fun.OData

Build query string for OData.

### With CE (fsharp computation expression)

This can provide better type safety, but maybe slower because it use reflection.  
But you can also use plain text if you want to get better performance than DU.  
You can check **demos/ODataDemo.Server** which contains how to setup <span style="color: green">OData + asp.net core MVC with fsharp + swagger support.</span>  

```fsharp
odata<DemoDataBrief> {
    skip ((testFilter.Page - 1) * testFilter.PageSize)
    take testFilter.PageSize
    count
    keyValue "etest1" "123" // your own query key value
    keyValue "etest2" "456"
    filter (
        odataOr {
            contains (fun x -> x.Name) testFilter.SearchName
            odataAnd { // you can also nest filter
                gt (fun x -> x.Price) testFilter.MinPrice
                lt (fun x -> x.CreatedDate) (testFilter.FromCreatedDate |> Option.map (fun x -> x.ToString("yyyy-MM-dd")))
                lt (fun x -> x.CreatedDate) (testFilter.ToCreatedDate |> Option.map (fun x -> x.ToString("yyyy-MM-dd")))
            }
        }
    )
}
```

```fsharp
odata<{| Id: int
         Name: string
         Test1: {| Id: Guid; Name: string; DemoData: DemoData |}
         Test2: {| Id: Guid; Name: string |} option
         Test3: {| Id: int |} list |}> {
    empty
}
```

By default it will auto expand record, record of array, record of list and record of option.  
But you can also override its behavior:


```fsharp
odata<Person> {
    expandPoco (fun x -> x.Contact)
    expandList (fun x -> x.Addresses) (
        odata { // you can also nest
            filter ...
        }
    )
}
```

You can also disable auto expand for better performance, if you do not want any for plain object.

```fsharp
odata<Person> {
    disableAutoExpand
}
```

The **odata<'T> { ... }** will generate ODataQueryContext which you can call **ToQuery()** to generate the final string and combine with your logic.  
Please check  **demos/ODataDemo.Wasm/Hooks.fs** for an example.


## With DU (fsharp discriminated union) list

This is old implementation but it works fine. Personally I'd prefer CE style because better type safety.

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
```

With Query.generateFor some type, you can get SelectType and ExpandEx automatically. It supports expand record, record of array, record of list and record of option.

```fsharp
Query.generateFor<
                {| Id: int
                   Name: string
                   Test1: {| Id: Guid; Name: string; DemoData: DemoData |}
                   Test2: {| Id: Guid; Name: string |} option
                   Test3: {| Id: int |} []
                   Test4: {| Id: int |} list |}> []
// ?$expand=Test1($expand=DemoData($expand=Items($select=Id,Name,CreatedDate);$select=Id,Name,Description,Price,Items,CreatedDate,LastModifiedDate);$select=DemoData,Id,Name),Test2($select=Id,Name),Test3($select=Id),Test4($select=Id)&$select=Id,Name,Test1,Test2,Test3,Test4
```

## Benchmarks

|                  Method |         Mean |       Error |      StdDev |  Gen 0 |  Gen 1 | Allocated |
|------------------------ |-------------:|------------:|------------:|-------:|-------:|----------:|
|         AnonymousWithDU | 168,550.8 ns | 3,297.87 ns | 3,238.95 ns | 9.5215 |      - |     60 KB |
|         AnonymousWithCE | 117,002.9 ns | 2,257.73 ns | 3,014.01 ns | 5.1270 |      - |     32 KB |
|       CustomQueryWithDU |  15,625.7 ns |   303.09 ns |   372.23 ns | 1.4343 |      - |      9 KB |
|       CustomQueryWithCE |  11,403.4 ns |    98.77 ns |    87.55 ns | 0.7172 |      - |      4 KB |
|          FilterWithList |   1,193.4 ns |    18.17 ns |    16.11 ns | 0.2956 |      - |      2 KB |
|  FilterWithReflectionCE |  84,145.8 ns | 1,454.71 ns | 1,289.56 ns | 8.7891 | 0.1221 |     54 KB |
|    FilterWithOptionList |   1,238.5 ns |    22.89 ns |    21.41 ns | 0.3223 |      - |      2 KB |
| FilterWithOptionPlainCE |     931.5 ns |    18.60 ns |    25.46 ns | 0.3462 | 0.0010 |      2 KB |


## Server side [Deprecated]

1. Set OData service for asp.net core + giraffe
2. Use it like:
   ```fsharp
    // For any sequence
    GET >=> routeCi  "/demo"    >=> OData.query (demoData.AsQueryable())
    GET >=> routeCif "/demo(%i)"   (OData.item  (fun id -> demoData.Where(fun x -> x.Id = id).AsQueryable()))

    // With entityframework core
    GET >=> routeCi  "/person"  >=> OData.fromService  (fun (db: DemoDbContext) -> db.Persons.AsQueryable())
    GET >=> routeCif "/person(%i)" (OData.fromServicei (fun (db: DemoDbContext) id -> db.Persons.Where(fun x -> x.Id = id).AsQueryable()))
   ```
