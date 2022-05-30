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

|                  Method |       Mean |     Error |    StdDev |   Gen 0 | Allocated |
|------------------------ |-----------:|----------:|----------:|--------:|----------:|
|         AnonymousWithDU | 197.666 us | 3.7536 us | 3.5111 us | 14.4043 |     60 KB |
|         AnonymousWithCE | 134.004 us | 2.6483 us | 4.0442 us |  7.5684 |     32 KB |
|       CustomQueryWithDU |  18.422 us | 0.3611 us | 0.4567 us |  2.1667 |      9 KB |
|       CustomQueryWithCE |  14.120 us | 0.1454 us | 0.1215 us |  1.0834 |      4 KB |
|          FilterWithList |   1.299 us | 0.0250 us | 0.0288 us |  0.4425 |      2 KB |
|  FilterWithReflectionCE |  91.523 us | 1.3325 us | 1.2464 us | 13.1836 |     54 KB |
|    FilterWithOptionList |   1.349 us | 0.0231 us | 0.0216 us |  0.4826 |      2 KB |
| FilterWithOptionPlainCE |   1.056 us | 0.0211 us | 0.0347 us |  0.5188 |      2 KB |
|          OverrideWithDU |  34.866 us | 0.6247 us | 0.5216 us |  2.8076 |     11 KB |
|          OverrideWithCE |  23.646 us | 0.4698 us | 0.3923 us |  1.4038 |      6 KB |


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
