[<AutoOpen>]
module ODataDemo.Wasm.Hooks

open System.Net.Http
open System.Text.Json
open System.Text.Json.Serialization
open Fun.Blazor
open Fun.Result
open Fun.OData.Query


let private jsonOptions =
    let options = JsonSerializerOptions()
    options.Converters.Add(JsonFSharpConverter())
    options.PropertyNameCaseInsensitive <- true
    options


type ODataResult<'T> =
    {
        [<JsonPropertyName "@odata.count">]
        Count: int option
        Value: 'T list
    }


type UserBrief = { Id: int; Name: string }

type Role = { Caption: string }

type User = { Id: int; Name: string; Roles: Role list }


type IComponentHook with

    member hook.ODataQuery<'T>(path, query: ODataQueryContext<'T>) =
        task {
            let http = hook.ServiceProvider.GetMultipleServices<HttpClient>()
            let! result = http.GetStringAsync(path + "?" + query.ToQuery())
            try
                return JsonSerializer.Deserialize<ODataResult<'T>>(result, jsonOptions) |> Ok
            with
            | ex -> return Error ex.Message
        }

    member hook.ODataSingle<'T>(path, query: ODataQueryContext<'T>) =
        task {
            let http = hook.ServiceProvider.GetMultipleServices<HttpClient>()
            let! result = http.GetStringAsync(path + "?" + query.ToQuery())
            try
                return JsonSerializer.Deserialize<'T>(result, jsonOptions) |> Ok
            with
            | ex -> return Error ex.Message
        }


    member hook.LoadUsers(?top) =
        hook.ODataQuery<UserBrief>(
            "/api/Users",
            odata () {
                orderBy (fun x -> x.Name)
                take top
            }
        )
        |> Task.map (
            function
            | Ok x -> DeferredState.Loaded x.Value
            | Error e -> DeferredState.LoadFailed e
        )


    member hook.LoadUserDetail(id: int) = hook.ODataSingle<User>($"/api/Users/{id}", odata () { empty }) |> Task.map DeferredState.ofResult
