namespace Fun.OData.Giraffe

open System
open System.Linq
open Microsoft.AspNet.OData
open Microsoft.AspNet.OData.Query
open Microsoft.AspNet.OData.Builder
open Microsoft.AspNet.OData.Extensions
open Giraffe


module OData =
  let getODataResult<'T when 'T: not struct> (props: ODataProp<'T> list) =
      let entityClrType = typeof<'T>
      let ctx = props |> List.choose (function ODataProp.HttpContext x -> Some x | _ -> None) |> List.tryLast
      match ctx with
      | None -> { Count = Nullable(); Value = [].AsQueryable() }
      | Some ctx ->
          let configEntitySet = props |> List.choose (function ODataProp.ConfigEntitySet x -> Some x | _ -> None)
          let configSettings  = props |> List.choose (function ODataProp.ConfigQuerySettings x -> Some x | _ -> None)
          let getData         = props |> List.choose (function ODataProp.GetData x -> Some x | _ -> None) |> List.tryLast
          let source          = props |> List.choose (function ODataProp.Source x -> Some x | _ -> None) |> Seq.concat
          let byId            = props |> List.choose (function ODataProp.Filter x -> Some x | _ -> None) |> List.tryLast

          let modelbuilder = ODataConventionModelBuilder(ctx.Request.HttpContext.RequestServices, isQueryCompositionMode = true)
          modelbuilder.EntitySet<'T>(entityClrType.Name) |> ignore
          configEntitySet |> List.iter (fun config -> modelbuilder |> config)

          let model = modelbuilder.GetEdmModel()
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
              { Count = queryOption.Count.GetEntityCount(filterResult)
                Value = value }
          else 
              { Count = Nullable()
                Value = value }


  let queryPro props: HttpHandler =
      fun nxt ctx ->
          let result = getODataResult [ yield! props; ODataProp.HttpContext ctx ]
          let isById = props |> List.exists (function ODataProp.Filter _ -> true | _ -> false)
          if isById then
            let temp = result.Value.Cast<_>().FirstOrDefault()
            json temp nxt ctx
          else json result nxt ctx


  let queryFromService props (f: 'DbContext -> IQueryable<'T>) =
      queryPro [
        yield! props
        ODataProp.GetData (fun ctx ->
          let db = ctx.GetService<'DbContext>()
          f db)
      ]


  /// Query data from sequence
  let query source  = queryPro [ ODataProp.Source source ]
  /// Query only one item by id
  let item f id     = queryPro [ ODataProp.Filter (fun _ -> f id) ]
  /// Query data from DI service
  let fromService (f: 'Service -> IQueryable<'T>) = queryFromService [] f
  /// Query only one item from DI service by id
  let fromServicei (f: 'Service -> 'Id -> IQueryable<'T>) id = queryFromService [ ODataProp.Filter (fun x -> x.Take(1)) ] (fun ctx -> f ctx id)


type ODataQuery<'T when 'T: not struct>() =
  let mutable props: ODataProp<'T> list = []

  member this.configQuerySettings config = props <- props@[ ODataProp.ConfigQuerySettings config ]; this
  member this.configEntitySet config = props <- props@[ ODataProp.ConfigEntitySet config ]; this
  member this.fromService find = props <- props@[ ODataProp.GetData find ]; this
  member this.source source = props <- props@[ ODataProp.Source source ]; this
  member this.filter find = props <- props@[ ODataProp.Filter find ]; this
  member _.query() = OData.queryPro props
