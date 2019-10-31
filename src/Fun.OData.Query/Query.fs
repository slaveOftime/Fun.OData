namespace Fun.OData.Query

open System
open Microsoft.FSharp.Reflection


type FieldName = string 


type Query =
    | Select of string
    | SelectType of Type
    | OrderBy of string
    | OrderByDesc of string
    | Count
    | Filter of string
    | Take of int
    | Skip of int
    | Expand of string
    | ExpandEx of (FieldName * Query list) list
    | External of string
    | Id of int64

    static member combineQuery spliter qs =
        qs
        |> List.distinct
        |> List.fold (fun s q ->
            match q with
            | Select x      -> s + spliter + "$select=" + x
            | SelectType t  -> s + spliter + "$select=" + (Query.generateSelectQueryByType(t))
            | OrderBy x     -> s + spliter + "$orderBy=" + x
            | OrderByDesc x -> s + spliter + "$orderBy=" + x + " desc"
            | Count         -> s + spliter + "$count=true"
            | Filter x      -> if String.IsNullOrEmpty x then s else s + spliter + "$filter=" + x
            | Take x        -> s + spliter + "$top=" + (string x)
            | Skip x        -> s + spliter + "$skip=" + (string x)
            | Expand x      -> s + spliter + "$expand=" + x
            | ExpandEx ls   -> s + spliter + "$expand=" + (ls
                                                           |> List.map (fun (fieldName, qs) -> 
                                                                sprintf "%s%s" 
                                                                        fieldName 
                                                                        (if qs.Length = 0 
                                                                         then "" 
                                                                         else sprintf "(%s)" (Query.combineQuery ";" qs)))
                                                           |> fun x -> String.Join(",", x))
            | External x    -> s + spliter + x
            | Id _          -> s)
            ""
        |> fun x -> x.Substring(1, x.Length - 1)

    static member generate qs =
        qs 
        |> List.tryPick (function 
            | Id x -> Some ("(" + string x + ")")
            | _ -> None)
        |> Option.defaultValue ""
        |> fun x -> x + "?" + Query.combineQuery "&" qs

    static member generateSelectQueryByType(sourceType, ?lowerFirstCase) =
        FSharpType.GetRecordFields sourceType
        |> Seq.map (fun x -> x.Name)
        |> Seq.map (fun x ->
            match lowerFirstCase with
            | Some true ->
                if String.IsNullOrEmpty x then x
                elif x.Length = 1 then x.ToLower()
                else Char.ToLower(x.[0]).ToString() + x.Substring(1)
            | _ -> x)
        |> fun x -> String.Join(",", x)

    static member inline generateSelectQuery<'T>() = Query.generateSelectQueryByType(typeof<'T>)

    static member inline generateFor<'T> qs =
        let select = Query.generateSelectQuery<'T> () |> Select
        Query.generate (qs@[select])
        
    static member combineFilter operator filters =
        filters
        |> List.fold 
            (fun s x ->
                match s, x with
                | Some s, x    -> Some (sprintf "(%s %s %s)" s operator x)
                | None, _      -> Some (sprintf "(%s)" x))
            None
        |> Option.defaultValue ""

    static member combineAndFilter = Query.combineFilter "and"
    static member combineOrFilter  = Query.combineFilter "or"
