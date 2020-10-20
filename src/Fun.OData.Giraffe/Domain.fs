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
    | HttpContext of HttpContext
    | GetFromContext of (HttpContext -> IQueryable<'T>)
    | Source of IQueryable<'T>
    | Single of (IQueryable<'T> -> IQueryable<'T>)
    | ToJson of (obj -> string)
    | WithCount of int64


type IODataSerializer =
    abstract member SerializeToString: obj -> string
