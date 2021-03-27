namespace Fun.OData.Giraffe

open System
open System.Linq
open Microsoft.AspNet.OData
open Microsoft.AspNet.OData.Query
open Microsoft.AspNet.OData.Builder
open Microsoft.AspNet.OData.Extensions
open Microsoft.OData.Edm
open Giraffe


[<AutoOpen>]
module Extensions =
  type ODataConventionModelBuilder with
    member builder.SetEntityType<'T when 'T: not struct> setter =
      builder.EntitySet<'T>(typeof<'T>.Name).EntityType |> setter
      builder


module OData =
    let getODataResult<'T when 'T: not struct> (props: ODataProp<'T> list) =
        let ctx = props |> List.choose (function ODataProp.HttpContext x -> Some x | _ -> None) |> List.tryLast
        match ctx with
        | None -> { Count = Nullable(); Value = [].AsQueryable() }
        | Some ctx ->
            let entityClrType = typeof<'T>

            let props =
                try
                    let options = ctx.GetService<DefaultODataOptions>()
                    if options.UseGlobalEdmModel then
                        ODataProp.UseGlobalEdmModel::props
                    else
                        props
                with _ ->
                    props

            let configSettings    = props |> List.choose (function ODataProp.ConfigQuerySettings x -> Some x | _ -> None)
            let useGlobalEdmModel = props |> List.choose (function ODataProp.UseGlobalEdmModel -> Some() | _ -> None) |> List.isEmpty |> not
            let getData           = props |> List.choose (function ODataProp.GetFromContext x -> Some x | _ -> None) |> List.tryLast
            let source            = props |> List.choose (function ODataProp.Source x -> Some x | _ -> None) |> Seq.concat
            let byId              = props |> List.choose (function ODataProp.Single x -> Some x | _ -> None) |> List.tryLast
            let withCount         = props |> List.choose (function ODataProp.WithCount x -> Some x | _ -> None) |> List.tryLast

            let getModels () =
                let modelbuilder = ODataConventionModelBuilder(ctx.Request.HttpContext.RequestServices, isQueryCompositionMode = true)
                let configEntitySet = props |> List.choose (function ODataProp.ConfigEntitySet x -> Some x | _ -> None)
                modelbuilder.EntitySet<'T>(entityClrType.Name) |> ignore
                configEntitySet |> List.iter (fun config -> modelbuilder |> config)
                modelbuilder.GetEdmModel()

            let model =
                if useGlobalEdmModel then 
                    try ctx.GetService<IEdmModel>()
                    with _ -> getModels()
                else
                    getModels()

            let modelContext = ODataQueryContext(model, entityClrType, ctx.Request.ODataFeature().Path)
            let queryOption = ODataQueryOptions<'T>(modelContext, ctx.Request)
      
            let querySettings =
                ODataQuerySettings(
                    EnsureStableOrdering = false,
                    EnableConstantParameterization = true,
                    EnableCorrelatedSubqueryBuffering = true,
                    PageSize = Nullable(20))
            configSettings |> List.iter (fun config -> config querySettings)
          
            let result =
                match getData with
                | None -> source.AsQueryable()
                | Some h -> (h ctx).AsQueryable()

            let finalResult =
                match byId with
                | Some single -> (result |> single).AsQueryable()
                | None -> result

            let value = queryOption.ApplyTo(finalResult, querySettings)
            if queryOption.Count <> null && queryOption.Count.Value then
                let filterResult =
                    if queryOption.Filter <> null 
                    then queryOption.Filter.ApplyTo(finalResult, querySettings).Cast()
                    else finalResult
                let count =
                    match withCount with
                    | Some x -> Nullable x
                    | None -> queryOption.Count.GetEntityCount(filterResult)
                { Count = count; Value = value }
            else
                { Count = Nullable(); Value = value }


    let queryPro props: HttpHandler =
        fun nxt ctx ->
            let toJson = props |> List.choose (function ODataProp.ToJson x -> Some x | _ -> None) |> List.tryLast
            let result = getODataResult [ yield! props; ODataProp.HttpContext ctx ]
            let isById = props |> List.exists (function ODataProp.Single _ -> true | _ -> false)

            let writeJsonContent content =
                ctx.SetHttpHeader "content-type" "application/json; charset=utf-8"
                ctx.WriteStringAsync(content).Result |> ignore

            let buildResult (data: obj) =
                match toJson with
                | None ->
                    try
                        let serializer = ctx.GetService<IODataSerializer>()
                        writeJsonContent (serializer.SerializeToString data)
                        nxt ctx
                    with
                    | :? MissingDependencyException ->
                        json data nxt ctx
                    | e ->
                        raise e

                | Some toJson ->
                    writeJsonContent (toJson data)
                    nxt ctx

            if isById then result.Value.Cast<_>().FirstOrDefault() |> buildResult
            else buildResult result


    let fromServicePro (f: 'DbContext -> IQueryable<'T>) props =
        queryPro [
            yield! props
            ODataProp.GetFromContext (fun ctx ->
                let db = ctx.GetService<'DbContext>()
                f db)
        ]

    let fromServiceProi (f: 'DbContext -> 'Id -> IQueryable<'T>) props id =
        queryPro [
            yield! props
            ODataProp.GetFromContext (fun ctx ->
                let db = ctx.GetService<'DbContext>()
                f db id)
            ODataProp.Single (fun x -> x)
        ]


    /// Query data from sequence
    let query source  = queryPro [ ODataProp.Source source ]
    /// Query only one item by id
    let item f id     = queryPro [ ODataProp.Single (fun _ -> f id) ]
    /// Query data from DI service
    let fromService (f: 'Service -> IQueryable<'T>) = fromServicePro f []
    /// Query only one item from DI service by id
    let fromServicei (f: 'Service -> 'Id -> IQueryable<'T>) id = fromServicePro (fun ctx -> f ctx id) [ ODataProp.Single (fun x -> x.Take(1)) ]


type ODataQuery<'T when 'T: not struct>() =
    let mutable props: ODataProp<'T> list = []

    member this.ConfigQuerySettings config = props <- props@[ ODataProp.ConfigQuerySettings config ]; this
    member this.ConfigEntitySet config = props <- props@[ ODataProp.ConfigEntitySet config ]; this
    member this.FromContext find = props <- props@[ ODataProp.GetFromContext find ]; this
    member this.FromService<'Service> find = props <- props@[ ODataProp.GetFromContext (fun ctx -> ctx.GetService<'Service>() |> find) ]; this
    member this.Source source = props <- props@[ ODataProp.Source source ]; this
    member this.Single find = props <- props@[ ODataProp.Single find ]; this
    member _.Build() = OData.queryPro props
