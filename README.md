# Fun.OData

Build query string for OData.

### With CE (fsharp computation expression)

This can provide better type safety, but maybe slower because it use reflection.  
But you can also use plain text if you want to get better performance than DU.  
You can check **demos/ODataDemo.Server** which contains how to setup <span style="color: green">OData + asp.net core MVC with fsharp + swagger support.</span>  

Let's see if you want to fetch below information:

```fsharp
type Part =
    {
        Nr: int
        Actions: Action list
    }
and Action = { Nr: int; Tid: int; AccountNrFromNavigation: AccountNrFromNavigation }
and AccountNrFromNavigation = { Nr: string; Caption: string }
```

You can just do it like:

```fsharp
http.GetStringAsync(
    "api/v1/Parts?" + 
    odataQuery<Part> {
        count
        take 2
        orderBy (fun x -> x.Nr)
        expandList (fun x -> x.Actions) (
            odata {
                take 3
                orderBy (fun x -> x.Tid)
                filterAnd {
                    gt (fun x -> x.Tid) 1
                    lt (fun x -> x.Tid) 10
                }
            }
        )
    }
)
```

Then it will send **GET** request with url:
```text
http://localhost:9090/api/v1/Parts?$select=Nr,Actions&$count=true&$top=2&$orderBy=Nr&$expand=Actions($select=Nr,Tid,AccountNrFromNavigation;$top=3;$orderBy=Tid;$expand=AccountNrFromNavigation($select=Nr,Caption);$filter=(Tid%20gt%201%20and%20Tid%20lt%2010))
```

Instead of use **odataQuery** you can use **odata**, because it will return you **ODataQueryContext** which you can call **ToQuery** to generate the final query string. But with this way, you can wrap it into a helper function:

```fsharp
type ODataResult<'T> =
    {
        [<JsonPropertyName("@odata.count")>]
        Count: int option
        Value: 'T list
    }

type HttpClient with
    member http.Get<'T>(path, queryContext: ODataQueryContext<'T>) =
        http.GetStringAsync(path + "?" + queryContext.ToQuery()) // You may need error handling in production
        |> fromJson<ODataResult<'T>> // json deserialize
```

To use it, you just call:

```fsharp
http.Get<Part> (
    odata {
        count
        take 2
        ...
    }
)
```

Below you can see more demos:

```fsharp
odata<DemoDataBrief> {
    skip ((testFilter.Page - 1) * testFilter.PageSize)
    take testFilter.PageSize
    count
    keyValue "etest1" "123" // your own query key value
    keyValue "etest2" "456"
    filterOr {
        contains (fun x -> x.Name) testFilter.SearchName
        filterAnd { // you can also nest filter
            gt (fun x -> x.Price) testFilter.MinPrice
            lt (fun x -> x.CreatedDate) (testFilter.FromCreatedDate |> Option.map (fun x -> x.ToString("yyyy-MM-dd")))
            lt (fun x -> x.CreatedDate) (testFilter.ToCreatedDate |> Option.map (fun x -> x.ToString("yyyy-MM-dd")))
        }
    }
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

|                  Method |         Mean |       Error |      StdDev |       Median |  Gen 0 |  Gen 1 | Allocated |
|------------------------ |-------------:|------------:|------------:|-------------:|-------:|-------:|----------:|
|         AnonymousWithDU | 170,828.6 ns | 3,402.75 ns | 3,640.90 ns | 170,353.6 ns | 9.5215 |      - |     60 KB |
|         AnonymousWithCE | 114,684.6 ns | 1,323.87 ns | 1,033.59 ns | 114,856.8 ns | 5.1270 |      - |     32 KB |
|       CustomQueryWithDU |  15,855.2 ns |   313.21 ns |   618.24 ns |  15,601.1 ns | 1.4343 |      - |      9 KB |
|       CustomQueryWithCE |  11,416.7 ns |   187.83 ns |   166.50 ns |  11,408.3 ns | 0.7172 |      - |      4 KB |
|          FilterWithList |   1,206.5 ns |    23.71 ns |    45.11 ns |   1,209.1 ns | 0.2956 |      - |      2 KB |
|  FilterWithReflectionCE |  82,849.4 ns | 1,553.95 ns | 1,662.71 ns |  82,346.8 ns | 8.7891 | 0.1221 |     54 KB |
|    FilterWithOptionList |   1,232.4 ns |    21.79 ns |    27.56 ns |   1,229.3 ns | 0.3223 |      - |      2 KB |
| FilterWithOptionPlainCE |     940.3 ns |    18.12 ns |    25.41 ns |     938.0 ns | 0.3462 | 0.0010 |      2 KB |
|          OverrideWithDU |  29,617.0 ns |   510.80 ns |   477.80 ns |  29,554.6 ns | 1.8616 |      - |     11 KB |
|          OverrideWithCE |  20,866.2 ns |   414.14 ns |   345.82 ns |  20,864.4 ns | 0.9155 |      - |      6 KB |


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
