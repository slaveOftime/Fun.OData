module Client.App.States

open System
open Elmish
open Fable.SimpleHttp
open Fun.Result
open Fun.OData.Query
open Dtos.DemoData


let serverHost = "http://localhost:5000"


let init() =
  { ErrorInfo = None
    Filter = Filter.defaultValue
    IsLoading = false
    TotalCount = 0
    Data = []
    Detail = None
    ODataQuery = None }
  , Cmd.none


let update msg state =
    match msg with
    | OnError e -> { state with ErrorInfo = e; IsLoading = false }, Cmd.none

    | OnFilterChange f -> { state with Filter = f }, Cmd.none

    | LoadData ->
        let filter = state.Filter
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
        { state with
            IsLoading = true
            ErrorInfo = None
            ODataQuery = Some query }
        , Http.request (sprintf "%s/%s%s" serverHost (QueryType.toQueryString filter.QueryType) query)
            |> Http.method GET
            |> handleHttpJsonAsync LoadedData (Some >> OnError)
            |> Cmd.OfAsync.result

    | LoadedData data ->
        { state with
            IsLoading = false
            TotalCount = data.Count |> Option.defaultValue 0
            Data = data.Value }
        , Cmd.none

    | LoadDataById id ->
        let expected = "?$expand=Test1($expand=DemoData($expand=Items($select=Id,Name,CreatedDate);$select=Id,Name,Description,Price,Items,CreatedDate,LastModifiedDate);$select=DemoData,Id,Name),Test2($select=Id,Name),Test3($select=Id),Test4($select=Id)&$select=Id,Name,Test1,Test2,Test3,Test4"
        let actual =
            Query.generateFor<
                {| Id: int
                   Name: string
                   Test1: {| Id: Guid; Name: string; DemoData: DemoData |}
                   Test2: {| Id: Guid; Name: string |} option
                   Test3: {| Id: int |} []
                   Test4: {| Id: int |} list |}> []
        if expected <> actual then
            Browser.Dom.console.error $"Query.generateFor is not working.\nExpected:{expected}\nActual: {actual}"

        let query = [ Id (string id) ] |> Query.generateFor<DemoData>
        { state with
            IsLoading = true
            ErrorInfo = None
            ODataQuery = Some query }
        , Http.request (sprintf "%s/%s%s" serverHost (QueryType.toQueryString state.Filter.QueryType) query)
            |> Http.method GET
            |> handleHttpJsonAsync LoadedDataById (Some >> OnError)
            |> Cmd.OfAsync.result

    | LoadedDataById data ->
        { state with
            IsLoading = false
            Detail = Some data }
        , Cmd.none
