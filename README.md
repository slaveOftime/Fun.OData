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

|                  Method |         Mean |       Error |      StdDev |       Median |   Gen 0 |  Gen 1 | Allocated |
|------------------------ |-------------:|------------:|------------:|-------------:|--------:|-------:|----------:|
|         AnonymousWithDU | 177,451.1 ns | 3,316.00 ns | 6,146.42 ns | 175,928.9 ns | 10.0098 |      - |     62 KB |
|         AnonymousWithCE | 113,811.4 ns | 1,239.00 ns | 1,158.97 ns | 113,960.2 ns |  5.1270 |      - |     32 KB |
|       CustomQueryWithDU |  16,905.9 ns |   335.33 ns |   638.01 ns |  16,650.8 ns |  1.4648 |      - |      9 KB |
|       CustomQueryWithCE |  11,405.1 ns |   131.92 ns |   123.40 ns |  11,412.7 ns |  0.7172 |      - |      4 KB |
|          FilterWithList |   1,181.2 ns |    23.32 ns |    54.05 ns |   1,160.7 ns |  0.2956 |      - |      2 KB |
|  FilterWithReflectionCE | 103,187.4 ns | 1,537.92 ns | 1,438.57 ns | 102,927.2 ns |  9.3994 | 0.1221 |     58 KB |
|    FilterWithOptionList |   1,188.1 ns |    19.89 ns |    18.60 ns |   1,185.2 ns |  0.3223 |      - |      2 KB |
| FilterWithOptionPlainCE |     960.9 ns |    18.06 ns |    35.66 ns |     951.5 ns |  0.3452 |      - |      2 KB |
|          OverrideWithDU |  29,714.3 ns |   575.33 ns |   806.53 ns |  29,414.6 ns |  1.8921 |      - |     12 KB |
|          OverrideWithCE |  20,597.7 ns |   319.26 ns |   298.64 ns |  20,554.2 ns |  0.9155 |      - |      6 KB |

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
