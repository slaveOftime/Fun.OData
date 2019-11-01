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
    | Id of string


module Query =

    let generateSelectQueryByType lowerFirstCase sourceType  =
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

    let inline generateSelectQuery<'T>() = generateSelectQueryByType None typeof<'T>


    let rec combineQuery spliter qs =
        let safeAdd key map x state =
          if String.IsNullOrEmpty x then state
          else state |> Map.add key (map x)

        let maps =
          qs
          |> List.distinct
          |> List.fold (fun s q ->
              match q with
              | Id _         -> s
              | External x    -> s |> Map.add (sprintf "External-%d" s.Count) x
              | Select x      -> s |> safeAdd "Select"    (sprintf "$select=%s") x
              | SelectType t  -> s |> Map.add "Select"    (generateSelectQueryByType None t |> sprintf"$select=%s")
              | OrderBy x     -> s |> safeAdd "OrderBy"   (sprintf "$orderBy=%s") x
              | OrderByDesc x -> s |> safeAdd "OrderBy"   (sprintf "$orderBy=%s desc") x
              | Count         -> s |> Map.add "Count"     "$count=true"
              | Filter x      -> s |> safeAdd (sprintf "Filter-%d" s.Count) (sprintf "%s") x
              | Take x        -> s |> safeAdd "Take"      (sprintf "$top=%s") (string x)
              | Skip x        -> s |> safeAdd "Skip"      (sprintf "$skip=%s") (string x)
              | Expand x      -> s |> safeAdd "Expand"    (sprintf "$expand=%s") x
              | ExpandEx ls   -> s |> safeAdd "Expand"    (sprintf "$expand=%s") (ls
                                                                                 |> List.map (fun (fieldName, qs) -> 
                                                                                     sprintf "%s%s" 
                                                                                             fieldName 
                                                                                             (if qs.Length = 0 
                                                                                              then "" 
                                                                                              else sprintf "(%s)" (combineQuery ";" qs)))
                                                                                 |> String.concat ",")
              )
              Map.empty

        [
          yield! maps |> Map.filter (fun k _ -> k.StartsWith "Filter-" |> not) |> Map.toSeq |> Seq.map snd

          maps
          |> Map.filter (fun k _ -> k.StartsWith "Filter-")
          |> Map.toSeq
          |> Seq.map (snd >> sprintf "(%s)")
          |> String.concat " and "
          |> function
              | "" -> ""
              | x -> sprintf "$filter=%s" x
        ]
        |> Seq.filter (String.IsNullOrWhiteSpace >> not)
        |> String.concat spliter
          
    
    let generate qs =
        qs 
        |> List.tryPick (function 
            | Id x -> Some ("(" + string x + ")")
            | _ -> None)
        |> Option.defaultValue ""
        |> fun x -> x + "?" + combineQuery "&" qs


    let inline generateFor<'T> qs =
        let select = generateSelectQuery<'T> () |> Select
        generate (qs@[select])
