namespace Fun.OData.Giraffe

open System
open System.Linq
open System.Collections.Generic
open Microsoft.AspNet.OData
open Microsoft.AspNet.OData.Query
open Microsoft.AspNet.OData.Builder
open Microsoft.AspNet.OData.Extensions
open Microsoft.AspNetCore.Http
open Giraffe


[<RequireQualifiedAccess>]
type ODataProp<'T when 'T: not struct> =
  | ConfigQuerySettings of (ODataQuerySettings -> unit)
  | ConfigEntitySet of (EntitySetConfiguration<'T> -> unit)
  | GetData of (HttpContext -> 'T seq)
  | Source of 'T seq
  | ById of ('T seq -> 'T)
  | HttpContext of HttpContext


module OData =
  let getODataResult<'T when 'T: not struct> (props: ODataProp<'T> list) =
      let entityClrType = typeof<'T>
      let ctx = props |> List.choose (function ODataProp.HttpContext x -> Some x | _ -> None) |> List.tryLast
      match ctx with
      | None -> { Count = Nullable(); Value = [].AsQueryable() }
      | Some ctx ->
          let configEntitySet = props |> List.choose (function ODataProp.ConfigEntitySet x -> Some x | _ -> None) |> List.tryLast |> Option.defaultValue ignore
          let getData         = props |> List.choose (function ODataProp.GetData x -> Some x | _ -> None) |> List.tryLast
          let source          = props |> List.choose (function ODataProp.Source x -> Some x | _ -> None) |> Seq.concat
          let byId            = props |> List.choose (function ODataProp.ById x -> Some x | _ -> None) |> List.tryLast

          let modelbuilder = ODataConventionModelBuilder(ctx.Request.HttpContext.RequestServices, isQueryCompositionMode = true)
          modelbuilder.EntitySet<'T>(entityClrType.Name) |> configEntitySet |> ignore

          let model = modelbuilder.GetEdmModel()
          let modelContext = ODataQueryContext(model, entityClrType, ctx.Request.ODataFeature().Path)
          let queryOption = ODataQueryOptions<'T>(modelContext, ctx.Request)
      
          let querySettings =
            ODataQuerySettings(
              EnsureStableOrdering = false,
              EnableConstantParameterization = true,
              PageSize = Nullable(20))
      
          let result =
            match getData with
            | None -> source.AsQueryable()
            | Some h -> (h ctx).AsQueryable()

          match byId with
          | Some byId -> { Count = Nullable(); Value = [ result |> byId ].AsQueryable() }
          | None ->
              let value = queryOption.ApplyTo(result, querySettings)
              if queryOption.Count <> null && queryOption.Count.Value then
                  let filterResult =
                      if queryOption.Filter <> null 
                      then queryOption.Filter.ApplyTo(result, querySettings).Cast()
                      else result
                  { Count = queryOption.Count.GetEntityCount(filterResult)
                    Value = value }
              else 
                  { Count = Nullable()
                    Value = value }


  let queryPro props: HttpHandler =
      fun nxt ctx ->
          let result = getODataResult [ yield! props; ODataProp.HttpContext ctx ]
          let isById = props |> List.exists (function ODataProp.ById _ -> true | _ -> false)
          if isById then
            let temp = result.Value.Cast<_>().FirstOrDefault()
            json temp nxt ctx
          else json result nxt ctx


  let queryEF props (f: 'DbContext -> IEnumerable<'T>) =
      queryPro [
        yield! props
        ODataProp.GetData (fun ctx ->
          let db = ctx.GetService<'DbContext>()
          f db)
      ]


  /// Query data from sequence
  let query source  = queryPro [ ODataProp.Source source ]
  /// Query only one item by id
  let item f id     = queryPro [ ODataProp.ById (fun _ -> f id) ]
  /// Query data from DI service
  let ef (f: 'Service -> IEnumerable<'T>) = queryEF [] f
  /// Query only one item from DI service by id
  let efi (f: 'Service -> 'Id -> 'T) id   = queryEF [ ODataProp.ById (fun data -> data.FirstOrDefault()) ] (fun ctx -> [ f ctx id ].AsEnumerable())


type ODataQuery<'T when 'T: not struct>() =
  let mutable props: ODataProp<'T> list = []

  member this.configQuerySettings config = props <- props@[ ODataProp.ConfigQuerySettings config ]; this
  member this.configEntitySet config = props <- props@[ ODataProp.ConfigEntitySet config ]; this
  member this.getData find = props <- props@[ ODataProp.GetData find ]; this
  member this.withSource source = props <- props@[ ODataProp.Source source ]; this
  member this.byId find = props <- props@[ ODataProp.ById find ]; this
  member _.query() = OData.queryPro props
