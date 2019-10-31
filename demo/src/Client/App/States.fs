module Client.App.States

open Elmish
open Fable.SimpleHttp
open Fun.Result
open Fun.LightForm
open Fun.OData.Query
open Dtos.DemoData


let serverHost = "http://localhost:5000"


let init() =
  { ErrorInfo = None
    IsLoading = false
    FilterForm = generateFormByValue Filter.defaultValue 
    TotalCount = 0
    Data = []
    Detail = None
    ODataQuery = None }
  , Cmd.none


let update msg state =
    match msg with
    | OnError e -> { state with ErrorInfo = e; IsLoading = false }, Cmd.none

    | FilterFormMsg msg' ->
        { state with FilterForm = state.FilterForm |> updateFormWithMsg Map.empty msg' }
        , Cmd.none

    | LoadData ->
        match state.FilterForm |> tryGenerateValueByForm<Filter> with
        | Ok filter ->
            let query =
              [
                SelectType typeof<DemoData>
                Skip ((filter.Page - 1) * filter.PageSize)
                Take filter.PageSize
                Count
                Filter (Query.combineAndFilter [
                  match filter.SearchName with
                    | Some x -> sprintf "contains(Name, '%s')" x
                    | None -> ()
                  match filter.MinPrice with
                    | None -> ()
                    | Some x -> sprintf "Price gt %M" x
                  match filter.FromCreatedDate with
                    | None -> ()
                    | Some x -> sprintf "CreatedDate gt %s" (x.ToString("yyyy-MM-dd"))
                  match filter.ToCreatedDate with
                    | None -> ()
                    | Some x -> sprintf "CreatedDate lt %s" (x.ToString("yyyy-MM-dd"))
                ])
              ]
              |> Query.generate
            { state with
                IsLoading = true
                ErrorInfo = None
                ODataQuery = Some query }
            , Http.request (sprintf "%s/demo?%s" serverHost query)
              |> Http.method GET
              |> handleHttpJsonAsync LoadedData (Some >> OnError)
              |> Cmd.OfAsync.result
        | Error e ->
            state
            , Cmd.ofMsg (e |> string |> Some |> OnError)

    | LoadedData data ->
        { state with
            IsLoading = false
            TotalCount = data.Count |> Option.defaultValue 0
            Data = data.Value }
        , Cmd.none

    | LoadDataById id ->
        let query =
          [
            Id (int64 id)
            SelectType typeof<DemoData>
            ExpandEx [
              "Items", [ SelectType typeof<Item> ]
            ]
          ]
          |> Query.generate
        { state with
            IsLoading = true
            ErrorInfo = None
            ODataQuery = Some query }
        , Http.request (sprintf "%s/demo%s" serverHost query)
          |> Http.method GET
          |> handleHttpJsonAsync LoadedDataById (Some >> OnError)
          |> Cmd.OfAsync.result

    | LoadedDataById data ->
        { state with
            IsLoading = false
            Detail = Some data }
        , Cmd.none
