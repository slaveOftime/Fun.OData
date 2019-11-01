namespace Fun.OData.Giraffe

open System
open System.Linq
open Microsoft.AspNet.OData.Query
open Microsoft.AspNet.OData.Builder
open Microsoft.AspNetCore.Http


type ODataResult =
  { Count: Nullable<int64>
    Value: IQueryable }


[<RequireQualifiedAccess>]
type ODataProp<'T when 'T: not struct> =
  | ConfigQuerySettings of (ODataQuerySettings -> unit)
  | ConfigEntitySet of (ODataConventionModelBuilder -> unit)
  | GetData of (HttpContext -> IQueryable<'T>)
  | Source of IQueryable<'T>
  | Filter of (IQueryable<'T> -> IQueryable<'T>)
  | HttpContext of HttpContext
