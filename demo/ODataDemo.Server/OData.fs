[<AutoOpen>]
module ODataDemo.Server.OData

open System.Linq
open System.Text.Json.Serialization
open Microsoft.AspNetCore.OData.Query


type ODataResult =
    {
        [<JsonPropertyName "@odata.count">]
        Count: int64 option
        Value: IQueryable
    }


type ODataQueryOptions<'T> with

    member queryOptions.Query(source: IQueryable<'T>, ?set) =
        let querySettings =
            ODataQuerySettings(
                PageSize = 30,
                EnsureStableOrdering = false,
                EnableConstantParameterization = true,
                EnableCorrelatedSubqueryBuffering = true
            )

        (defaultArg set ignore) querySettings

        let count =
            if queryOptions.Count <> null then
                let filteredQuery =
                    if queryOptions.Filter = null then
                        source :> IQueryable
                    else
                        queryOptions.Filter.ApplyTo(source, querySettings)

                queryOptions.Count.GetEntityCount(filteredQuery) |> Option.ofNullable
            else
                None

        {
            Count = count
            Value = queryOptions.ApplyTo(source, querySettings)
        }
