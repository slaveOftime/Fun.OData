module Dtos.DemoData

open System


type ODataResult<'T> =
  { Count: int option
    Value: 'T list }


type DemoData =
  { Id: int
    Name: string
    Description: string
    Price: decimal
    Items: Item list
    CreatedDate: DateTime
    LastModifiedDate: DateTime option }
and Item =
  { Id: int
    Name: string
    CreatedDate: DateTime }
