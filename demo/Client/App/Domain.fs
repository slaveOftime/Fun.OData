namespace rec Client.App

open System
open Dtos.DemoData


type State =
  { ErrorInfo: string option
    Filter: Filter
    IsLoading: bool
    TotalCount: int
    Data: DemoDataBrief list
    Detail: DemoData option
    ODataQuery: string option }

type Msg =
  | OnError of string option
  | OnFilterChange of Filter
  | LoadData
  | LoadedData of ODataResult<DemoDataBrief>

  | LoadDataById of int
  | LoadedDataById of DemoData


type Filter =
  { PageSize: int
    Page: int
    SearchName: string option
    MinPrice: decimal option
    FromCreatedDate: DateTime option
    ToCreatedDate: DateTime option
    QueryType : QueryType }
  static member defaultValue =
    { PageSize = 5
      Page = 1
      SearchName = None
      MinPrice = None
      FromCreatedDate = None
      ToCreatedDate = None
      QueryType = QueryType.Simple }

type QueryType =
  | Simple
  | Pro
  | Fluent
  static member toQueryString = function
    | QueryType.Simple -> "demo"
    | QueryType.Fluent -> "demofluent"
    | QueryType.Pro    -> "demopro"

type DemoDataBrief =
  { Id: int
    Name: string
    Price: decimal
    CreatedDate: DateTime }
