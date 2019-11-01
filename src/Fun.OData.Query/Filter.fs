[<AutoOpen>]
module Fun.OData.Query.Filter

    let combineFilter operator filters =
        filters
        |> List.fold 
            (fun s x ->
                match s, x with
                | Some s, x    -> Some (sprintf "(%s %s %s)" s operator x)
                | None, _      -> Some (sprintf "(%s)" x))
            None
        |> Option.defaultValue ""

    let andQueries = combineFilter "and"
    let orQueries  = combineFilter "or"

    let gt name value       = sprintf "%s gt %A" name value
    let lt name value       = sprintf "%s lt %A" name value
    let eq name value       = sprintf "%s eq %A" name value
    let contains name value = sprintf "contains(%s, '%s')" name value
