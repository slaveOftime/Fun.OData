[<AutoOpen>]
module Fun.OData.Query.Filter

let combineFilter operator filters = filters |> Seq.map (sprintf "(%s)") |> String.concat (sprintf " %s " operator)


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
let inline lt name value = sprintf "%s lt %s" name (string value)
let inline eq name value = sprintf "%s eq %s" name (string value)
let inline ne name value = sprintf "%s ne %s" name (string value)
let inline contains name value = sprintf "contains(%s, '%s')" name value
