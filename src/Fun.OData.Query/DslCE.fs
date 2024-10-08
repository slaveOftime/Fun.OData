﻿[<AutoOpen>]
module Fun.OData.Query.DslCE

open System
open System.Web
open System.Text
open System.Linq.Expressions
open System.Collections.Generic
open System.Diagnostics
open Microsoft.FSharp.Reflection
open Fun.OData.Query.Internal


[<AutoOpen>]
module Internal =

    type FilterCombinator = delegate of StringBuilder -> StringBuilder

    let emptyFilterCombinator = FilterCombinator(fun x -> x)

    [<Literal>]
    let EmptyQuery = "<EMPTY>"

    let getNestedExcludes (name) (excludedFields: string seq) =
        excludedFields
        |> Seq.choose (fun x ->
            if x.StartsWith name && x.Length > name.Length + 1 then
                x.Substring(name.Length + 1) |> Some
            else
                None
        )
        |> Seq.toList

    let rec generateQuery
        (ty: Type)
        (combinator: string)
        (disableAutoExpand)
        (loopDeepthMax)
        (loopDeepth)
        (simpleQuries: Dictionary<string, string>)
        (expands: Dictionary<string, string>)
        (filter: List<string>)
        (selectFields: string list)
        (excludedFields: string list)
        =

        let fields =
            if selectFields.Length > 0 then
                List.empty
            else
                if FSharpType.IsRecord ty then
                    FSharpType.GetRecordFields ty
                else
                    ty.GetProperties()
                |> Seq.toList

        let getNestedExcludes x = getNestedExcludes x excludedFields

        let mutable expands = expands
        if not disableAutoExpand then
            if expands = null then expands <- Dictionary<string, string>()

            for field in fields do
                if expands.ContainsKey field.Name |> not && not (excludedFields |> List.contains field.Name) then
                    if FSharpType.IsRecord field.PropertyType then
                        if field.PropertyType = ty then
                            Debug.Write $"Recursive record is not supported: from type {ty.Name} to property {field.PropertyType.Name}"
                        else
                            expands[field.Name] <-
                                (generateQuery
                                    field.PropertyType
                                    ";"
                                    false
                                    loopDeepthMax
                                    loopDeepth
                                    null
                                    null
                                    null
                                    List.empty
                                    (getNestedExcludes field.Name))
                                    .ToString()
                    elif
                        FSharpType.IsRecordOption field.PropertyType
                        && (field.PropertyType.GenericTypeArguments[0] <> ty || loopDeepth <= loopDeepthMax)
                    then
                        let nextLoopDeepth =
                            if field.PropertyType.GenericTypeArguments[0] = ty then
                                loopDeepth + 1
                            else
                                loopDeepth
                        expands[field.Name] <-
                            (generateQuery
                                field.PropertyType.GenericTypeArguments[0]
                                ";"
                                false
                                loopDeepthMax
                                nextLoopDeepth
                                null
                                null
                                null
                                List.empty
                                (getNestedExcludes field.Name))
                                .ToString()
                    else
                        match FSharpType.TryGetIEnumeralbleGenericType field.PropertyType with
                        | Some ty when field.PropertyType <> ty || loopDeepth <= loopDeepthMax ->
                            let nextLoopDeepth = if field.PropertyType = ty then loopDeepth + 1 else loopDeepth
                            expands[field.Name] <-
                                (generateQuery
                                    ty
                                    ";"
                                    false
                                    loopDeepthMax
                                    nextLoopDeepth
                                    null
                                    null
                                    null
                                    List.empty
                                    (getNestedExcludes field.Name))
                                    .ToString()
                        | _ when field.PropertyType.GetInterfaces() |> Seq.exists ((=) typeof<IExpandable>) ->
                            expands[field.Name] <-
                                (generateQuery
                                    field.PropertyType
                                    ";"
                                    false
                                    loopDeepthMax
                                    (loopDeepth + 1)
                                    null
                                    null
                                    null
                                    List.empty
                                    (getNestedExcludes field.Name))
                                    .ToString()
                        | _ -> ()


        if excludedFields |> Seq.filter (fun x -> x.Contains "." |> not) |> Seq.length = fields.Length then
            EmptyQuery

        else
            let filteredExpands =
                if expands = null then
                    []
                else
                    expands |> Seq.filter (fun (KeyValue(k, _)) -> excludedFields |> List.contains k |> not) |> Seq.toList

            let isDirectExcluded name = excludedFields |> Seq.contains name
            let isNestedExcluded name = filteredExpands |> Seq.exists (fun x -> x.Key = name && x.Value = EmptyQuery)

            let selectFields =
                [|
                    yield! selectFields
                    yield! fields |> Seq.map (fun x -> x.Name)
                    yield! filteredExpands |> Seq.map (fun x -> x.Key)
                |]
                |> Seq.distinct
                |> Seq.filter (fun x -> not (isDirectExcluded x) && not (isNestedExcluded x))
                |> Seq.toList

            let sb = StringBuilder()

            if selectFields.Length > 0 then
                sb.Append("$select=") |> ignore
                let mutable isFirstAppend = true
                for field in selectFields do
                    if not isFirstAppend then sb.Append(",") |> ignore
                    sb.Append(field) |> ignore
                    isFirstAppend <- false

                if simpleQuries <> null && simpleQuries.Count > 0 then
                    for KeyValue(k, v) in simpleQuries do
                        sb.Append(combinator).Append(k).Append("=").Append(v) |> ignore

                if filteredExpands.Length > 0 then
                    let mutable isFirstAppend = true
                    sb.Append(combinator).Append("$expand=") |> ignore
                    for KeyValue(k, v) in filteredExpands do
                        if String.IsNullOrEmpty v |> not then
                            if v <> EmptyQuery then
                                if not isFirstAppend then sb.Append(",") |> ignore
                                sb.Append(k).Append("(").Append(v).Append(")") |> ignore
                                isFirstAppend <- false
                        else
                            if not isFirstAppend then sb.Append(",") |> ignore
                            sb.Append(k) |> ignore
                            isFirstAppend <- false

                if filter <> null && filter.Count > 0 then
                    let mutable i = 0
                    for filterStr in filter do
                        if i > 0 then sb.Append(" and ") |> ignore
                        if String.IsNullOrEmpty filterStr |> not then
                            if i = 0 then sb.Append(combinator).Append("$filter=") |> ignore
                            sb.Append("(").Append(filterStr).Append(")") |> ignore
                            i <- i + 1

                sb.ToString()

            else
                EmptyQuery


type ODataQueryContext<'T>() =

    member val SimpleQuries = Dictionary<string, string>()
    member val Expand = Dictionary<string, string>()
    member val Filter = List<string>()
    member val SelectFields = HashSet<string>()
    member val ExcludedFields = HashSet<string>()
    member val DisableAutoExpand = false with get, set
    member val MaxLoopDeepth = 1 with get, set

    member ctx.GetNestedExcludes(name) = Internal.getNestedExcludes name ctx.ExcludedFields

    member ctx.GetNestedDirectExcludes(name) =
        Internal.getNestedExcludes name ctx.ExcludedFields |> List.filter (fun x -> x.Contains "." |> not)

    member ctx.ToQuery(?combinator, ?finalize) =
        let finalize = defaultArg finalize true

        let query =
            generateQuery
                typeof<'T>
                (defaultArg combinator "&")
                ctx.DisableAutoExpand
                ctx.MaxLoopDeepth
                0
                ctx.SimpleQuries
                ctx.Expand
                ctx.Filter
                (ctx.SelectFields |> Seq.toList)
                (ctx.ExcludedFields |> Seq.toList)

        if query = EmptyQuery && finalize then String.Empty else query

    member ctx.MergeInto(target: ODataQueryContext<'T>) =
        for KeyValue(k, v) in ctx.SimpleQuries do
            target.SimpleQuries[k] <- v
        for KeyValue(k, v) in ctx.Expand do
            target.Expand[k] <- v
        target.Filter.AddRange ctx.Filter
        target.DisableAutoExpand <- ctx.DisableAutoExpand
        for field in ctx.ExcludedFields do
            target.ExcludedFields.Add field |> ignore

        target

    member inline ctx.SetOrderBy(value) =
        let key = "$orderby"
        match ctx.SimpleQuries.TryGetValue key with
        | true, str -> ctx.SimpleQuries[key] <- str + "," + value
        | _ -> ctx.SimpleQuries.Add(key, value)
        ctx


    member ctx.AddExpand<'Prop>(name: string) =
        let excluded = ctx.GetNestedDirectExcludes name
        let properties = getPropertiesForType typeof<'Prop>
        if properties.Length > excluded.Length then
            ctx.Expand[name] <- sprintf "$select=%s" (properties |> Seq.except excluded |> generateSelectQueryByPropertyNames false)
        ctx

    member ctx.AddExpand<'Prop>(name: string, expandCtx: ODataQueryContext<'Prop>) =
        let excluded = ctx.GetNestedExcludes name
        for property in excluded do
            if expandCtx.ExcludedFields.Contains(property) |> not then
                expandCtx.ExcludedFields.Add(property) |> ignore
        ctx.Expand[name] <- expandCtx.ToQuery(";", finalize = false)
        ctx


type ODataFilterContext<'T>(operator: string, filter: FilterCombinator) =

    member val Filter = filter
    member val Operator = operator

    member ctx.ToQuery() =
        let sb = StringBuilder()
        let str = ctx.Filter.Invoke(sb).ToString()

        if str.Length > ctx.Operator.Length then
            str.Substring(ctx.Operator.Length)
        else
            ""


type ODataQueryBuilder<'T>() =

    member inline _.Run(ctx: ODataQueryContext<'T>) = ctx

    member inline _.Run(filter: ODataFilterContext<'Filter>) =
        let ctx = ODataQueryContext<'T>()
        ctx.Filter.Add(filter.ToQuery())
        ctx

    member inline _.Yield() = ODataQueryContext<'T>()

    member inline _.Yield(_: unit) = ODataQueryContext<'T>()

    member inline _.Yield(x: ODataQueryContext<'T>) = x

    member inline _.Yield(x: ODataFilterContext<'Filter>) = x

    member inline _.Delay([<InlineIfLambda>] fn) = fn ()

    member inline _.For(ctx: ODataQueryContext<'T>, [<InlineIfLambda>] fn: unit -> ODataQueryContext<'T>) = fn().MergeInto(ctx)

    member inline _.For(ctx: ODataQueryContext<'T>, [<InlineIfLambda>] fn: unit -> ODataFilterContext<'Filter>) =
        ctx.Filter.Add(fn().ToQuery())
        ctx

    member inline _.Combine(x: ODataQueryContext<'T>, y: ODataQueryContext<'T>) = y.MergeInto(x)

    member inline _.Combine(ctx: ODataQueryContext<'T>, filter: ODataFilterContext<'Filter>) =
        ctx.Filter.Add(filter.ToQuery())
        ctx

    member inline _.Combine(filter: ODataFilterContext<'Filter>, ctx: ODataQueryContext<'T>) =
        ctx.Filter.Add(filter.ToQuery())
        ctx


    member inline _.Zero() = ODataQueryContext<'T>()


    /// With this, you can use CE but without add other quries.
    [<CustomOperation("empty")>]
    member inline _.Empty(ctx: ODataQueryContext<'T>) = ctx

    /// For some flat types, you can use this to improve performance
    [<CustomOperation("disableAutoExpand")>]
    member inline _.DisableAutoExpand(ctx: ODataQueryContext<'T>) =
        ctx.DisableAutoExpand <- true
        ctx

    [<CustomOperation("maxLoopDeepth")>]
    member inline _.maxLoopDeepth(ctx: ODataQueryContext<'T>, max: int) =
        ctx.MaxLoopDeepth <- if max > 0 then max else 0
        ctx


    // Override default select by type, auto expand will be ignored
    [<CustomOperation("select")>]
    member inline _.Select(ctx: ODataQueryContext<'T>, ty: Type) =
        for property in getPropertiesForType ty do
            ctx.SelectFields.Add(property) |> ignore
        ctx

    // Override default select by type, auto expand will be ignored
    [<CustomOperation("select")>]
    member inline _.Select(ctx: ODataQueryContext<'T>, fields: string seq) =
        for field in fields do
            ctx.SelectFields.Add(field) |> ignore
        ctx


    /// With this we can support use case like backward compatibility of database changes
    [<CustomOperation("excludeSelect")>]
    member inline _.ExcludeSelect(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>) =
        ctx.ExcludedFields.Add(getExpressionName prop) |> ignore
        ctx

    [<CustomOperation("excludeSelect")>]
    member inline _.ExcludeSelect(ctx: ODataQueryContext<'T>, fields: string seq) =
        for field in fields do
            ctx.ExcludedFields.Add(field) |> ignore
        ctx


    [<CustomOperation("count")>]
    member inline _.Count(ctx: ODataQueryContext<'T>) =
        ctx.SimpleQuries["$count"] <- "true"
        ctx

    [<CustomOperation("take")>]
    member inline _.Take(ctx: ODataQueryContext<'T>, num: int) =
        ctx.SimpleQuries["$top"] <- num.ToString()
        ctx

    [<CustomOperation("take")>]
    member inline this.Take(ctx: ODataQueryContext<'T>, num: int option) =
        match num with
        | None -> ctx
        | Some num -> this.Take(ctx, num)

    [<CustomOperation("skip")>]
    member inline _.Skip(ctx: ODataQueryContext<'T>, num: int) =
        ctx.SimpleQuries["$skip"] <- num.ToString()
        ctx

    [<CustomOperation("skip")>]
    member inline this.Skip(ctx: ODataQueryContext<'T>, num: int option) =
        match num with
        | None -> ctx
        | Some num -> this.Skip(ctx, num)


    [<CustomOperation("compute")>]
    member inline _.Compute(ctx: ODataQueryContext<'T>, str: string) =
        if not (String.IsNullOrEmpty str) then ctx.SimpleQuries["$compute"] <- str
        ctx

    [<CustomOperation("compute")>]
    member inline this.Compute(ctx: ODataQueryContext<'T>, str: string option) =
        match str with
        | None -> ctx
        | Some str -> this.Compute(ctx, str)


    [<CustomOperation("orderBy")>]
    member inline _.OrderBy(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>) = ctx.SetOrderBy(getExpressionName prop)

    [<CustomOperation("orderBy")>]
    member inline _.OrderBy(ctx: ODataQueryContext<'T>, x: string) = ctx.SetOrderBy x

    [<CustomOperation("orderByDesc")>]
    member inline _.OrderByDesc(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>) =
        ctx.SetOrderBy(getExpressionName prop + " desc")

    [<CustomOperation("orderByDesc")>]
    member inline _.OrderByDesc(ctx: ODataQueryContext<'T>, x: string) = ctx.SetOrderBy(x + " desc")

    [<CustomOperation("orderBy")>]
    member inline _.OrderBy(ctx: ODataQueryContext<'T>, descendingWithFields: (bool * string) option) =
        match descendingWithFields with
        | Some(descending, fields) -> ctx.SetOrderBy(if descending then fields + " desc" else fields)
        | _ -> ctx

    member inline _.OrderBy(ctx: ODataQueryContext<'T>, (descending, fields): bool * string) =
        ctx.SetOrderBy(if descending then fields + " desc" else fields)


    [<CustomOperation("expandPoco")>]
    member inline _.Expand(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>) = ctx.AddExpand<'Prop>(getExpressionName prop)

    [<CustomOperation("expandPoco")>]
    member inline _.Expand(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>, expandCtx: ODataQueryContext<'Prop>) =
        ctx.AddExpand(getExpressionName prop, expandCtx)

    [<CustomOperation("expandPoco")>]
    member inline _.Expand(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop option>>, expandCtx: ODataQueryContext<'Prop>) =
        ctx.AddExpand(getExpressionName prop, expandCtx)

    [<CustomOperation("expandList")>]
    member inline _.ExpandList(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, IEnumerable<'Prop>>>) =
        ctx.AddExpand<'Prop>(getExpressionName prop)

    [<CustomOperation("expandList")>]
    member inline _.ExpandList
        (
            ctx: ODataQueryContext<'T>,
            prop: Expression<Func<'T, IEnumerable<'Prop>>>,
            expandCtx: ODataQueryContext<'Prop>
        ) =
        ctx.AddExpand<'Prop>(getExpressionName prop, expandCtx)

    [<CustomOperation("expand")>]
    member inline _.Expand<'Prop>(ctx: ODataQueryContext<'T>, prop: string, expandCtx: ODataQueryContext<'Prop>) =
        ctx.AddExpand<'Prop>(prop, expandCtx)

    [<CustomOperation("expand")>]
    member inline _.Expand<'Prop>(ctx: ODataQueryContext<'T>, prop: string, queries: Query seq) =
        ctx.Expand[prop] <- Query.generate(queries).Substring(1)
        ctx


    [<CustomOperation("filter")>]
    member inline _.Filter(ctx: ODataQueryContext<'T>, filter: string) =
        if String.IsNullOrEmpty filter then
            ctx
        else
            ctx.Filter.Add(filter)
            ctx

    [<CustomOperation("filter")>]
    member inline this.Filter(ctx: ODataQueryContext<'T>, filter: string option) =
        match filter with
        | None -> ctx
        | Some filter -> this.Filter(ctx, filter)

    [<CustomOperation("filter")>]
    member inline _.Filter(ctx: ODataQueryContext<'T>, filter: ODataFilterContext<'Filter>) =
        ctx.Filter.Add(filter.ToQuery())
        ctx


    [<CustomOperation("keyValue")>]
    member inline _.KeyValue(ctx: ODataQueryContext<'T>, key: string, value: string) =
        ctx.SimpleQuries[key] <- value
        ctx

    [<CustomOperation("keyValue")>]
    member inline this.KeyValue(ctx: ODataQueryContext<'T>, key: string, value: string option) =
        match value with
        | None -> ctx
        | Some x -> this.KeyValue(ctx, key, x)


type ODataFilterBuilder<'T>(oper: string) =

    let buildFilter (ctx: FilterCombinator) (value: obj) (builder: obj -> FilterCombinator) =
        if isNull value then
            ctx
        else
            let handle (value: obj) =
                match value with
                | :? string as x -> builder (box ("'" + (HttpUtility.UrlEncode x) + "'"))
                | :? bool as x -> builder (x.ToString().ToLower())
                | null -> builder "null"
                | x -> builder (x)

            let ty = value.GetType()

            if FSharpType.IsUnion ty then
                let info, fields = FSharpValue.GetUnionFields(value, ty)
                if info.DeclaringType.FullName.StartsWith "Microsoft.FSharp.Core.FSharpOption`1" then
                    if info.Name = "Some" then handle (fields[0]) else ctx
                else
                    handle value
            else
                handle value


    member val Operator = oper

    member inline this.Run(ctx: FilterCombinator) = ODataFilterContext<'T>(this.Operator, ctx)


    member inline _.Yield(_: unit) = emptyFilterCombinator

    member inline this.Yield(x: string) =
        FilterCombinator(fun sb ->
            if String.IsNullOrEmpty x then
                sb
            else
                sb.Append(this.Operator).Append("(").Append(x).Append(")")
        )

    member inline this.Yield(expressions: string seq) =
        FilterCombinator(fun sb ->
            for expression in expressions do
                if String.IsNullOrEmpty expression |> not then
                    sb.Append(this.Operator).Append("(").Append(expression).Append(")") |> ignore
            sb
        )

    member inline this.Yield(x: ODataFilterContext<'T>) =
        FilterCombinator(fun sb ->
            let query = x.ToQuery()
            if String.IsNullOrEmpty query |> not then
                sb.Append(this.Operator).Append("(").Append(query).Append(")")
            else
                sb
        )

    member inline this.Yield(x: string option) =
        match x with
        | Some x when not (String.IsNullOrEmpty x) -> FilterCombinator(fun sb -> sb.Append(this.Operator).Append("(").Append(x).Append(")"))
        | _ -> emptyFilterCombinator


    member inline _.For(ctx: FilterCombinator, [<InlineIfLambda>] fn: unit -> FilterCombinator) =
        FilterCombinator(fun sb -> fn().Invoke(ctx.Invoke(sb)))

    member inline _.For(items: 'Input seq, [<InlineIfLambda>] fn: 'Input -> FilterCombinator) =
        FilterCombinator(fun sb -> items |> Seq.fold (fun sb item -> fn(item).Invoke(sb)) sb)

    member inline _.Delay([<InlineIfLambda>] fn: unit -> FilterCombinator) = FilterCombinator(fun sb -> fn().Invoke(sb))

    member inline _.Combine([<InlineIfLambda>] x: FilterCombinator, [<InlineIfLambda>] y: FilterCombinator) =
        FilterCombinator(fun sb -> y.Invoke(x.Invoke(sb)))

    member inline _.Zero() = emptyFilterCombinator


    [<CustomOperation("eq")>]
    member this.EQ(ctx: FilterCombinator, name: string, value: obj) =
        let builder (x: obj) = FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append(name).Append(" eq ").Append(x))
        buildFilter ctx value builder

    [<CustomOperation("eq")>]
    member this.EQ(ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: obj) = this.EQ(ctx, getExpressionName prop, value)


    [<CustomOperation("ne")>]
    member this.NE(ctx: FilterCombinator, name: string, value: obj) =
        let builder (x: obj) = FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append(name).Append(" ne ").Append(x))
        buildFilter ctx value builder

    [<CustomOperation("ne")>]
    member this.NE(ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: obj) = this.NE(ctx, getExpressionName prop, value)


    [<CustomOperation("lt")>]
    member this.LT(ctx: FilterCombinator, name: string, value: obj) =
        let builder (x: obj) = FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append(name).Append(" lt ").Append(x))
        buildFilter ctx value builder

    [<CustomOperation("lt")>]
    member this.LT(ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: obj) = this.LT(ctx, getExpressionName prop, value)

    [<CustomOperation("le")>]
    member this.LE(ctx: FilterCombinator, name: string, value: obj) =
        let builder (x: obj) = FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append(name).Append(" le ").Append(x))
        buildFilter ctx value builder

    [<CustomOperation("le")>]
    member this.LE(ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: obj) = this.LE(ctx, getExpressionName prop, value)


    [<CustomOperation("gt")>]
    member this.GT(ctx: FilterCombinator, name: string, value: obj) =
        let builder (x: obj) = FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append(name).Append(" gt ").Append(x))
        buildFilter ctx value builder

    [<CustomOperation("gt")>]
    member this.GT(ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: obj) = this.GT(ctx, getExpressionName prop, value)

    [<CustomOperation("ge")>]
    member this.GE(ctx: FilterCombinator, name: string, value: obj) =
        let builder (x: obj) = FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append(name).Append(" ge ").Append(x))
        buildFilter ctx value builder

    [<CustomOperation("ge")>]
    member this.GE(ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: obj) = this.GE(ctx, getExpressionName prop, value)


    [<CustomOperation("contains")>]
    member inline this.Contains([<InlineIfLambda>] ctx: FilterCombinator, name: string, value: string) =
        if isNull value then
            ctx
        else
            FilterCombinator(fun sb ->
                ctx
                    .Invoke(sb)
                    .Append(this.Operator)
                    .Append("contains(")
                    .Append(name)
                    .Append(", '")
                    .Append(HttpUtility.UrlEncode value)
                    .Append("')")
            )

    [<CustomOperation("contains")>]
    member inline this.Contains([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: string) =
        this.Contains(ctx, getExpressionName prop, value)

    [<CustomOperation("contains")>]
    member inline this.Contains([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: string option) =
        match value with
        | None -> ctx
        | Some x -> this.Contains(ctx, prop, x)

    [<CustomOperation("startsWith")>]
    member inline this.StartsWith([<InlineIfLambda>] ctx: FilterCombinator, name: string, value: string) =
        if isNull value then
            ctx
        else
            FilterCombinator(fun sb ->
                ctx
                    .Invoke(sb)
                    .Append(this.Operator)
                    .Append("startswith(")
                    .Append(name)
                    .Append(", '")
                    .Append(HttpUtility.UrlEncode value)
                    .Append("')")
            )

    [<CustomOperation("startsWith")>]
    member inline this.StartsWith([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: string) =
        this.StartsWith(ctx, getExpressionName prop, value)

    [<CustomOperation("startsWith")>]
    member inline this.StartsWith([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: string option) =
        match value with
        | None -> ctx
        | Some x -> this.StartsWith(ctx, prop, x)

    [<CustomOperation("endsWith")>]
    member inline this.EndsWith([<InlineIfLambda>] ctx: FilterCombinator, name: string, value: string) =
        if isNull value then
            ctx
        else
            FilterCombinator(fun sb ->
                ctx
                    .Invoke(sb)
                    .Append(this.Operator)
                    .Append("endswith(")
                    .Append(name)
                    .Append(", '")
                    .Append(HttpUtility.UrlEncode value)
                    .Append("')")
            )

    [<CustomOperation("endsWith")>]
    member inline this.EndsWith([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: string) =
        this.EndsWith(ctx, getExpressionName prop, value)

    [<CustomOperation("endsWith")>]
    member inline this.EndsWith([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: string option) =
        match value with
        | None -> ctx
        | Some x -> this.EndsWith(ctx, prop, x)


    [<CustomOperation("custom")>]
    member inline this.Custom([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, fn: string -> string) =
        let condition = fn (getExpressionName prop)
        if String.IsNullOrEmpty condition then
            ctx
        else
            FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append("(").Append(condition).Append(")"))

    [<CustomOperation("custom")>]
    member inline this.Custom([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, fn: string -> string option) =
        match fn (getExpressionName prop) with
        | Some x when not (String.IsNullOrEmpty x) ->
            FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append("(").Append(x).Append(")"))
        | _ -> ctx


type ODataAndFilterBuilder<'T>() =
    inherit ODataFilterBuilder<'T> " and "

type ODataOrFilterBuilder<'T>() =
    inherit ODataFilterBuilder<'T> " or "

type ODataNotFilterBuilder<'T>() =

    member inline _.Yield(_: unit) = emptyFilterCombinator

    member inline _.Yield(x: string) =
        FilterCombinator(fun sb ->
            if String.IsNullOrEmpty x then
                sb
            else
                sb.Append("not").Append("(").Append(x).Append(")")
        )

    member inline _.Yield(x: ODataFilterContext<'T>) =
        FilterCombinator(fun sb ->
            let query = x.ToQuery()
            if String.IsNullOrEmpty query |> not then
                sb.Append("not").Append("(").Append(query).Append(")")
            else
                sb
        )

    member inline _.Delay([<InlineIfLambda>] fn: unit -> FilterCombinator) =
        let ctx = FilterCombinator(fun sb -> fn().Invoke(sb))
        ODataFilterContext<'T>(String.Empty, ctx)

    member inline _.Combine([<InlineIfLambda>] x: FilterCombinator, [<InlineIfLambda>] y: FilterCombinator) =
        FilterCombinator(fun sb -> y.Invoke(x.Invoke(sb)))

    member inline _.Zero() = emptyFilterCombinator


type OData<'T> = ODataQueryBuilder<'T>
type ODataOr<'T> = ODataOrFilterBuilder<'T>
type ODataAnd<'T> = ODataAndFilterBuilder<'T>
type ODataNot<'T> = ODataNotFilterBuilder<'T>


type OdataQuery<'T>() =
    inherit ODataQueryBuilder<'T>()

    member _.Run(ctx: ODataQueryContext<'T>) = ctx.ToQuery()
    member _.Run(ctx: ODataFilterContext<'Filter>) = base.Run(ctx).ToQuery()

type OdataOrQuery<'T>() =
    inherit ODataOrFilterBuilder<'T>()

    member _.Run(ctx) = base.Run(ctx).ToQuery()

type OdataAndQuery<'T>() =
    inherit ODataAndFilterBuilder<'T>()

    member _.Run(ctx) = base.Run(ctx).ToQuery()


type OdataNotQuery<'T>() =
    inherit ODataNotFilterBuilder<'T>()


/// Generate odata query context
let odata<'T> = OData<'T>()
/// Generate odata query string
let odataQuery<'T> = OdataQuery<'T>()

/// Generate odata filter context with or operator
let filterOr<'T> = ODataOr<'T>()
/// Generate odata filter query string with or operator
let filterOrQuery<'T> = OdataOrQuery<'T>()

/// Generate odata filter context with and operator
let filterAnd<'T> = ODataAnd<'T>()
/// Generate odata filter query string with and operator
let filterAndQuery<'T> = OdataAndQuery<'T>()

/// Generate odata filter context with not operator
let filterNot<'T> = ODataNotFilterBuilder<'T>()
/// Generate odata filter query string with not operator
let filterNotQuery<'T> = OdataNotQuery<'T>()

/// Create a OData query string for a type with default settings
let odataDefault<'T> () = ODataQueryBuilder<'T>().Yield()

let odataDefaultQuery<'T> () = odataDefault<'T>().ToQuery()
