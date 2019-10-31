namespace Fun.OData.Giraffe

open System
open System.Linq
open Microsoft.AspNetCore.Http
open Microsoft.AspNet.OData.Query


type ODataResult =
  { Count: Nullable<int>
    Value: IQueryable }

type ODataQueryHandler<'T> = HttpContext -> ODataQueryOptions<'T> -> IQueryable<'T>
