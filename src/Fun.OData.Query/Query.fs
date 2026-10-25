namespace Fun.OData.Query

open System
open Microsoft.FSharp.Reflection
open Fun.OData.Query.Internal.Utils


type FieldName = string

type IExpandable =
    interface
    end

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
    | Compute of string


module Query =

    let inline generateSelectQuery<'T> () = generateSelectQueryByType false typeof<'T>


    let rec combineQuery spliter (qs: Query seq) =
        let safeAdd key map x state = if String.IsNullOrEmpty x then state else state |> Map.add key (map x)

        // Expand and order by are multi-value options. Collect them in declaration order so
        // multiple entries combine instead of the last one overwriting the previous ones.
        let expands = ResizeArray<string * string>()
        let orderBys = ResizeArray<string>()

        let addExpand field value =
            if not (String.IsNullOrEmpty field) && not (String.IsNullOrEmpty value) then
                match expands |> Seq.tryFindIndex (fun (key, _) -> key = field) with
                | Some i -> expands.[i] <- (field, value)
                | None -> expands.Add(field, value)

        let addOrderBy value =
            if not (String.IsNullOrEmpty value) then orderBys.Add value

        let maps =
            qs
            |> Seq.distinct
            |> Seq.fold
                (fun s q ->
                    match q with
                    | Id _ -> s
                    | External x -> s |> Map.add (sprintf "External-%d" s.Count) x
                    | Select x -> s |> safeAdd "Select" (sprintf "$select=%s") x
                    | SelectType t -> s |> Map.add "Select" (generateSelectQueryByType false t |> sprintf "$select=%s")
                    | OrderBy x ->
                        addOrderBy x
                        s
                    | OrderByDesc x ->
                        addOrderBy (if String.IsNullOrEmpty x then x else sprintf "%s desc" x)
                        s
                    | Count -> s |> Map.add "Count" "$count=true"
                    | Filter x -> s |> safeAdd (sprintf "Filter-%d" s.Count) (sprintf "%s") x
                    | Take x -> s |> safeAdd "Take" (sprintf "$top=%s") (string x)
                    | Skip x -> s |> safeAdd "Skip" (sprintf "$skip=%s") (string x)
                    | Compute x -> s |> safeAdd "Compute" (sprintf "$compute=%s") (string x)
                    | Expand x ->
                        addExpand x x
                        s
                    | ExpandEx ls ->
                        for fieldName, queries in ls do
                            addExpand
                                fieldName
                                (sprintf "%s%s" fieldName (if queries.Length = 0 then "" else sprintf "(%s)" (combineQuery ";" queries)))
                        s
                )
                Map.empty
            |> fun s ->
                if orderBys.Count = 0 then
                    s
                else
                    s |> Map.add "OrderBy" (sprintf "$orderBy=%s" (orderBys |> String.concat ","))
            |> fun s ->
                if expands.Count = 0 then
                    s
                else
                    s |> Map.add "Expand" (sprintf "$expand=%s" (expands |> Seq.map snd |> String.concat ","))

        [|
            yield! maps |> Map.filter (fun k _ -> k.StartsWith "Filter-" |> not) |> Map.toSeq |> Seq.map snd

            maps
            |> Map.filter (fun k _ -> k.StartsWith "Filter-")
            |> Map.toSeq
            |> Seq.map (snd >> sprintf "(%s)")
            |> String.concat " and "
            |> function
                | "" -> ""
                | x -> sprintf "$filter=%s" x
        |]
        |> Seq.filter (String.IsNullOrWhiteSpace >> not)
        |> String.concat spliter


    let generate (quries: Query seq) =
        quries
        |> Seq.tryPick (
            function
            | Id x -> Some("(" + string x + ")")
            | _ -> None
        )
        |> Option.defaultValue ""
        |> fun x -> x + "?" + combineQuery "&" quries


    // This will generate selected fields and expan nexted field according to the record structure
    let inline generateFor<'T> quries =
        let rec loop (ty: Type) : Query option =
            FSharpType.GetRecordFields ty
            |> Array.choose (fun x ->
                if FSharpType.IsRecord x.PropertyType then
                    Some(x.Name, x.PropertyType)
                elif FSharpType.IsRecordArray x.PropertyType then
                    Some(x.Name, x.PropertyType.GetElementType())
                elif FSharpType.IsRecordList x.PropertyType || FSharpType.IsRecordOption x.PropertyType then
                    Some(x.Name, x.PropertyType.GenericTypeArguments.[0])
                else
                    None
            )
            |> Array.fold
                (fun state (name, ty) ->
                    state
                    @ [
                        name,
                        [
                            SelectType ty
                            match loop ty with
                            | Some x -> x
                            | None -> ()
                        ]
                    ]
                )
                []
            |> function
                | [] -> None
                | x -> Some(ExpandEx x)

        [|
            generateSelectQuery<'T> () |> Select
            match loop typeof<'T> with
            | Some x -> x
            | None -> ()
            yield! quries
        |]
        |> generate
