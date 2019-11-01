namespace Fun.OData.Giraffe

open System
open System.Linq


type ODataResult =
  { Count: Nullable<int64>
    Value: IQueryable }
