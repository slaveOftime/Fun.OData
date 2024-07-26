[<AutoOpen>]
module Fun.OData.Query.Filter

open System
open System.Web

let combineFilter operator filters = 
    filters
    |> Seq.filter (String.IsNullOrEmpty >> not)
    |> Seq.map (sprintf "(%s)") |> String.concat (sprintf " %s " operator)


let inline andQueries filters = combineFilter "and" filters

let andOptionQuries filters =
    filters
    |> List.choose id
    |> function
        | [] -> ""
        | ls -> andQueries ls

        
let inline orQueries filters = combineFilter "or" filters

let orOptionQuries filters =
    filters
    |> List.choose id
    |> function
        | [] -> ""
        | ls -> orQueries ls

        
let inline gt name value = sprintf "%s gt %s" name (string value)
let inline ge name value = sprintf "%s ge %s" name (string value)
let inline lt name value = sprintf "%s lt %s" name (string value)
let inline le name value = sprintf "%s le %s" name (string value)
let inline eq name value = sprintf "%s eq %s" name (string value |> HttpUtility.UrlEncode)
let inline ne name value = sprintf "%s ne %s" name (string value |> HttpUtility.UrlEncode)
let inline contains name (value: string) = sprintf "contains(%s, '%s')" name (HttpUtility.UrlEncode value)
