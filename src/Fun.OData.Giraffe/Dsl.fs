namespace Fun.OData.Giraffe

open System
open System.Linq
open Microsoft.AspNet.OData
open Microsoft.AspNet.OData.Query
open Microsoft.AspNet.OData.Builder
open Microsoft.AspNet.OData.Extensions
open Microsoft.AspNetCore.Http
open Giraffe


[<AutoOpen>]
module OData =
  let getODataResult<'T> (handler: ODataQueryHandler<'T>) (ctx: HttpContext) =
      let entityClrType = typeof<'T>
      let odataModelbuilder = ODataConventionModelBuilder(ctx.Request.HttpContext.RequestServices, isQueryCompositionMode = true)
      let entityTypeConfiguration = odataModelbuilder.AddEntityType(entityClrType)
      odataModelbuilder.AddEntitySet(entityClrType.Name, entityTypeConfiguration) |> ignore
      let model = odataModelbuilder.GetEdmModel()
      let modelContext = ODataQueryContext(model, entityClrType, ctx.Request.ODataFeature().Path)
      let queryOption = ODataQueryOptions<'T>(modelContext, ctx.Request)

      let querySettings =
        ODataQuerySettings(
          EnsureStableOrdering = false,
          EnableConstantParameterization = true,
          PageSize = Nullable(20))

      let result = handler ctx queryOption
      let value = queryOption.ApplyTo(result, querySettings)
      if queryOption.Count <> null && queryOption.Count.Value then
          let filterResult =
              if queryOption.Filter <> null 
              then queryOption.Filter.ApplyTo(result, querySettings).Cast()
              else result
          { Count = 
              queryOption.Count.GetEntityCount(filterResult)
              |> Option.ofNullable
              |> function
                  | Some x -> Nullable (int x)
                  | None -> Nullable()
            Value = value }
      else 
          { Count = Nullable()
            Value = value}


  let odataQ<'T> handler: HttpHandler =
      fun nxt ctx ->
          let result = getODataResult<'T> handler ctx
          json result nxt ctx

  let odataQs<'T> map handler: HttpHandler =
      fun nxt ctx ->
          let result = getODataResult<'T> (fun ctx' _ -> handler ctx') ctx |> map
          json result nxt ctx


  let odataItem<'T, 'Id> map handler (id: 'Id): HttpHandler =
      fun nxt ctx ->
          let result = 
              getODataResult<'T> (handler id) ctx
              |> fun x ->
                  match x.Value.Cast<_>() with
                  | x when x.Count() > 0 -> x.FirstOrDefault()
                  | _ -> null
              |> map
          json result nxt ctx


  let odataQEFm map (f: 'DbContext -> IQueryable<'T>) =
      odataQs map (fun ctx -> 
          let db = ctx.GetService<'DbContext>()
          f db)
  let odataQEF f = odataQEFm id f

  let odataQEFim map (f: 'DbContext -> 'Id -> IQueryable<'T>) id =
      odataItem map
          (fun id ctx opts -> 
              let db = ctx.GetService<'DbContext>()
              f db id)
          id
  let odataQEFi f = odataQEFim id f