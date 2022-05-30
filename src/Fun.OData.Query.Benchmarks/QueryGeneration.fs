namespace Fun.OData.Query.Benchmarks

open System
open BenchmarkDotNet.Attributes
open Fun.OData.Query


type DemoData =
    {
        Id: int
        Name: string
        Description: string
        Price: decimal
        Items: Item list
        CreatedDate: DateTime
        LastModifiedDate: DateTime option
    }
and Item = { Id: int; Name: string; CreatedDate: DateTime }


type QueryType =
    | Simple
    | Pro
    | Fluent

    static member toQueryString =
        function
        | QueryType.Simple -> "demo"
        | QueryType.Fluent -> "demofluent"
        | QueryType.Pro -> "demopro"

type DemoDataBrief =
    {
        Id: int
        Name: string
        Price: decimal
        CreatedDate: DateTime
    }

type Filter =
    {
        PageSize: int
        Page: int
        SearchName: string option
        MinPrice: decimal option
        FromCreatedDate: DateTime option
        ToCreatedDate: DateTime option
        QueryType: QueryType
    }

    static member defaultValue =
        {
            PageSize = 5
            Page = 1
            SearchName = Some "search"
            MinPrice = Some 10m
            FromCreatedDate = Some DateTime.Now
            ToCreatedDate = Some DateTime.Now
            QueryType = QueryType.Simple
        }


[<MemoryDiagnoser>]
type QueryGeneration() =

    let testFilter = Filter.defaultValue


    [<Benchmark>]
    member _.AnonymousWithDU() : string =
        Query.generateFor<{| Id: int
                             Name: string
                             Test1: {| Id: Guid; Name: string; DemoData: DemoData |}
                             Test2: {| Id: Guid; Name: string |} option
                             Test3: {| Id: int |} list |}>
            []

    [<Benchmark>]
    member _.AnonymousWithCE() : string =
        odataDefaultQuery<{| Id: int
                             Name: string
                             Test1: {| Id: Guid; Name: string; DemoData: DemoData |}
                             Test2: {| Id: Guid; Name: string |} option
                             Test3: {| Id: int |} list |}> ()


    [<Benchmark>]
    member _.CustomQueryWithDU() : string =
        [
            SelectType typeof<DemoDataBrief>
            Skip((testFilter.Page - 1) * testFilter.PageSize)
            Take testFilter.PageSize
            Count
            External "etest1=123"
            External "etest2=456"
        ]
        |> Query.generate

    [<Benchmark>]
    member _.CustomQueryWithCE() : string =
        odataQuery<DemoDataBrief> {
            disableAutoExpand
            skip ((testFilter.Page - 1) * testFilter.PageSize)
            take testFilter.PageSize
            count
            keyValue "etest1" "123"
            keyValue "etest2" "456"
        }


    [<Benchmark>]
    member _.FilterWithList() : string =
        andQueries [
            match testFilter.MinPrice with
            | None -> ()
            | Some x -> gt "Price" x
            match testFilter.FromCreatedDate with
            | None -> ()
            | Some x -> lt "CreatedDate" (x.ToString("yyyy-MM-dd"))
            match testFilter.ToCreatedDate with
            | None -> ()
            | Some x -> lt "CreatedDate" (x.ToString("yyyy-MM-dd"))
        ]

    [<Benchmark>]
    member _.FilterWithReflectionCE() : string =
        filterAndQuery<DemoDataBrief> {
            gt (fun x -> x.Price) testFilter.MinPrice
            lt (fun x -> x.CreatedDate) (testFilter.FromCreatedDate |> Option.map (fun x -> x.ToString("yyyy-MM-dd")))
            lt (fun x -> x.CreatedDate) (testFilter.ToCreatedDate |> Option.map (fun x -> x.ToString("yyyy-MM-dd")))
        }


    [<Benchmark>]
    member _.FilterWithOptionList() : string =
        andOptionQuries [
            testFilter.MinPrice |> Option.map (gt "Price")
            testFilter.FromCreatedDate |> Option.map (fun x -> lt "CreatedDate" (x.ToString("yyyy-MM-dd")))
            testFilter.ToCreatedDate |> Option.map (fun x -> lt "CreatedDate" (x.ToString("yyyy-MM-dd")))
        ]

    [<Benchmark>]
    member _.FilterWithOptionPlainCE() : string =
        filterAndQuery<DemoDataBrief> {
            testFilter.MinPrice |> Option.map (Filter.gt "Price")
            testFilter.FromCreatedDate |> Option.map (fun x -> Filter.lt "CreatedDate" (x.ToString("yyyy-MM-dd")))
            testFilter.ToCreatedDate |> Option.map (fun x -> Filter.lt "CreatedDate" (x.ToString("yyyy-MM-dd")))
        }


    [<Benchmark>]
    member _.OverrideWithDU() : string =
        let otherQuery = [ Take 10; Skip 10 ]
        Query.generateFor<DemoDataBrief> [ Count; Take 5; yield! otherQuery ]

    [<Benchmark>]
    member _.OverrideWithCE() : string =
        let otherQuery =
            odata {
                take 10
                skip 10
            }
        odataQuery<DemoDataBrief> {
            count
            take 5
            otherQuery
        }
