# Fun.OData

Build OData GET query strings with a typed F# DSL.

- Type-safe computation expression (`odataQuery` / `odata`)
- Lower-allocation DU/list API (`Query.generate` / `Query.generateFor`)
- Nested member paths, optional fields, and automatic record/list/array expansion
- Nested `$expand` with per-expand `$filter`, `$orderby`, and paging

## Install

```bash
dotnet add package Fun.OData.Query
```

## CE API

```fsharp
open Fun.OData.Query

type Part =
    {
        Nr: int
        Actions: Action list
    }
and Action = { Nr: int; Tid: int; AccountNrFromNavigation: AccountNrFromNavigation }
and AccountNrFromNavigation = { Nr: string; Caption: string }

let query =
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
                    custom (fun x -> x.Tid) (sprintf "%s eq 10")
                    // Some value -> included, None -> omitted, Some null -> "eq null"
                    eq (fun x -> x.Tid) (Some 1)
                    eq (fun x -> x.Tid) (Some null)
                    eq (fun x -> x.Tid) None
                }
            }
        )
    }

http.GetStringAsync("api/v1/Parts?" + query)
```

Generated query string:

```text
$select=Nr,Actions&$count=true&$top=2&$orderby=Nr&$expand=Actions($select=Nr,Tid,AccountNrFromNavigation;$top=3;$orderby=Tid;$expand=AccountNrFromNavigation($select=Nr,Caption);$filter=(Tid gt 1 and Tid lt 10 and (Tid eq 10) and Tid eq 1 and Tid eq null))
```

The library URL-encodes string literals. Your HTTP client is responsible for encoding spaces and other characters in the rest of the query string.

### Query contexts

`odataQuery` returns a string. `odata` returns an `ODataQueryContext` so you can build the query first and serialize it later:

```fsharp
let queryContext =
    odata<Part> {
        count
        take 20
    }

let queryString = queryContext.ToQuery()
```

This makes it easy to wrap requests in a typed helper:

```fsharp
type ODataResult<'T> =
    {
        [<JsonPropertyName("@odata.count")>]
        Count: int option
        Value: 'T list
    }

type HttpClient with
    member http.Get<'T>(path: string, query: ODataQueryContext<'T>) =
        http.GetStringAsync(path + "?" + query.ToQuery())
        |> fromJson<ODataResult<'T>>
```

```fsharp
http.Get<Part>(
    odata {
        count
        take 2
        orderBy (fun x -> x.Nr)
    }
)
```

### Nested members and optional values

Member expressions can point to nested records. For `option` and `Nullable<'T>` properties, append `.Value`; it is ignored when the OData field name is generated.

```fsharp
type Contact = { Phone: string; Email: string }
type Address = { Street: string; Room: int }

type Person =
    {
        Name: string
        Age: int
        Contact: Contact
        Addresses: Address list
    }

odataQuery<Person> {
    filterAnd<Person> { eq (fun x -> x.Contact.Phone) "123" }
    orderBy (fun x -> x.Contact.Phone)
    excludeSelect (fun x -> x.Contact.Phone)
}
// $select=Name,Age,Contact,Addresses&$orderby=Contact.Phone&$expand=Contact($select=Email),Addresses($select=Street,Room)&$filter=(Contact.Phone eq '123')

type PersonWithOptionalContact = { Name: string; Contact: Contact option }

odataQuery<PersonWithOptionalContact> {
    filterAnd<PersonWithOptionalContact> { eq (fun x -> x.Contact.Value.Phone) "123" }
}
// $select=Name,Contact&$expand=Contact($select=Phone,Email)&$filter=(Contact.Phone eq '123')
```

### Filter helpers

`filterAnd`, `filterOr`, and `filterNot` can be nested. `filterNotQuery` returns the filter string directly.

```fsharp
filterNotQuery<Contact> {
    filterAnd {
        "Phone eq '1'"
        filterNot { "Email eq '2'" }
    }
}
// not((Phone eq '1') and (not(Email eq '2')))
```

Use the string overloads when you want to avoid expression trees:

```fsharp
odataQuery<Person> {
    filterAnd {
        gt "Age" 10
        contains "Name" "search"
    }
}
```

### Filtering, paging, and yielding filters

```fsharp
odata<DemoDataBrief> {
    skip ((filter.Page - 1) * filter.PageSize)
    take filter.PageSize
    count
    keyValue "etest1" "123" // your own query key/value
    keyValue "etest2" "456"
    filterOr {
        contains (fun x -> x.Name) filter.SearchName
        filterAnd {
            gt (fun x -> x.Price) filter.MinPrice
            lt (fun x -> x.CreatedDate) (filter.FromCreatedDate |> Option.map (fun x -> x.ToString("yyyy-MM-dd")))
            lt (fun x -> x.CreatedDate) (filter.ToCreatedDate |> Option.map (fun x -> x.ToString("yyyy-MM-dd")))
        }
        for i in 1..3 do // loops can yield filters
            filterAnd<{| Address: string |}> {
                eq (fun x -> x.Address) (Some $"a{i}")
                eq (fun x -> x.Address) (Some $"b{i}")
            }
    }
}
```

`None` omits a filter, while `Some null` produces `eq null`.

### Automatic expansion

By default, records, record arrays, record lists, and record options are expanded automatically.

```fsharp
odata<{| Id: int
         Name: string
         Test1: {| Id: Guid; Name: string; DemoData: DemoData |}
         Test2: {| Id: Guid; Name: string |} option
         Test3: {| Id: int |} list |}> {
    empty
}
```

Override the default expansion when needed:

```fsharp
odata<Person> {
    expandPoco (fun x -> x.Contact)
    expandList (fun x -> x.Addresses) (
        odata {
            filterAnd { startsWith (fun x -> x.Street) "Main" }
            orderBy (fun x -> x.Room)
        }
    )
}
```

Disable auto expansion when the endpoint returns flat data:

```fsharp
odata<Person> {
    disableAutoExpand
}
```

For recursive records, set the maximum expansion depth with `maxLoopDeepth`.

### Expanding a DU query list

`expand` accepts a `Query` list; nested options are joined with `;` and `filter` conditions are combined correctly:

```fsharp
odataQuery<Part> {
    expand "Actions" [
        Select "Nr,Tid"
        Take 3
        OrderBy "Tid"
        Filter "Tid gt 1"
    ]
}
```

## DU/list API

The list API is convenient for hot paths because it does not build expression trees. `Query.generate` returns a string with a leading `?`.

```fsharp
open Fun.OData.Query

let query =
    [
        SelectType typeof<DemoDataBrief>
        Skip ((filter.Page - 1) * filter.PageSize)
        Take filter.PageSize
        Count
        External "etest1=123"
        External "etest2=56"
        OrderBy "Name"
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

Multiple `Expand`, `ExpandEx`, and `OrderBy` entries accumulate in declaration order. Single-value options such as `Take`, `Skip`, and `Select` keep their last value. Expanding the same field twice overrides the earlier expansion.

`Query.generateFor<'T>` derives `SelectType` and nested `ExpandEx` entries from the record shape:

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

Measured with BenchmarkDotNet 0.13.1 on .NET 6 using the in-process toolchain. Type metadata and nested expand shapes are cached, so repeated query generation does not re-run reflection.

| Method | Mean (µs) | Error | Gen 0 | Allocated |
|---|---:|---:|---:|---:|
| AnonymousWithDU | 15.50 | 0.60 | 3.2654 | 20 KB |
| AnonymousWithCE | 383.36 | 20.00 | 4.3945 | 28 KB |
| CustomQueryWithDU | 4.35 | 0.16 | 0.8774 | 5 KB |
| CustomQueryWithCE | 2.05 | 0.08 | 0.5455 | 3 KB |
| FilterWithList | 1.67 | 0.05 | 0.3185 | 2 KB |
| FilterWithReflectionCE | 138.68 | 3.59 | 9.2773 | 58 KB |
| FilterWithOptionList | 1.75 | 0.07 | 0.3452 | 2 KB |
| FilterWithOptionPlainCE | 1.16 | 0.03 | 0.3452 | 2 KB |
| OverrideWithDU | 3.62 | 0.12 | 0.7896 | 5 KB |
| OverrideWithCE | 92.66 | 3.42 | 0.7324 | 6 KB |
| MultiExpandWithDU | 18.97 | 0.83 | 3.8757 | 24 KB |
| MultiFilterWithDU | 5.22 | 0.23 | 1.2283 | 8 KB |
| NestedMemberFilterWithCE | 282.45 | 39.72 | 16.1133 | 101 KB |

The reflection-based CE benchmarks include the cost of constructing expression trees in the caller. Use the string overloads or the DU/list API when that cost matters.

Run the benchmarks with:

```bash
dotnet run -c Release --project src/Fun.OData.Query.Benchmarks -- --filter "*" --inProcess
```

## Server side [Deprecated]

1. Set up an OData service for ASP.NET Core + Giraffe.
2. Use it like:

```fsharp
// For any sequence
GET >=> routeCi  "/demo"    >=> OData.query (demoData.AsQueryable())
GET >=> routeCif "/demo(%i)"   (OData.item  (fun id -> demoData.Where(fun x -> x.Id = id).AsQueryable()))

// With Entity Framework Core
GET >=> routeCi  "/person"  >=> OData.fromService  (fun (db: DemoDbContext) -> db.Persons.AsQueryable())
GET >=> routeCif "/person(%i)" (OData.fromServicei (fun (db: DemoDbContext) id -> db.Persons.Where(fun x -> x.Id = id).AsQueryable()))
```
