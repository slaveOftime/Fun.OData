[<AutoOpen>]
module Client.App.Utils

open System
open Fable.SimpleJson
open Fable.SimpleHttp
open Fun.Result


let upperFirstChar x =
  if String.IsNullOrEmpty x then x
  elif x.Length = 1 then x.ToUpper()
  else Char.ToUpper(x.[0]).ToString() + x.Substring(1)

let inline fromJson<'T> str =
  try
    str
    |> SimpleJson.parse
    |> SimpleJson.mapKeys upperFirstChar
    |> Json.convertFromJsonAs<'T>
    |> Ok
  with ex ->
    ex |> string |> Error


let inline handleHttpJsonAsync<'T, 'Result> (onOk: 'T -> 'Result) (onError: string -> 'Result) request =
  request
  |> Http.send
  |> Async.map (fun resp ->
      match resp.statusCode with
      | NumBetweenE 200 399 -> resp.responseText |> fromJson<'T>
      | _ -> Error (sprintf "Request error, code %d" resp.statusCode))
  |> Async.map (function
      | Ok x -> onOk x
      | Error e -> onError e)
