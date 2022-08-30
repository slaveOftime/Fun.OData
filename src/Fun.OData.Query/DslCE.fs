[<AutoOpen>]
module Fun.OData.Query.DslCE

open System
open System.Text
open System.Linq.Expressions
open System.Collections.Generic
open Microsoft.FSharp.Reflection
open Fun.OData.Query.Internal


[<AutoOpen>]
module Internal =

    type FilterCombinator = delegate of StringBuilder -> StringBuilder

    let emptyFilterCombinator = FilterCombinator(fun x -> x)


    let rec generateQuery
        (ty: Type)
        (combinator: string)
        (disableAutoExpand)
        (loopDeepthMax)
        (loopDeepth)
        (simpleQuries: Dictionary<string, string>)
        (expands: Dictionary<string, string>)
        (filter: List<string>)
        (excludedFields: string list)
        =
        let sb = StringBuilder()
        let fields = FSharpType.GetRecordFields(ty)


        sb.Append("$select=") |> ignore

        let mutable i = 0
        for field in fields do
            if excludedFields |> List.contains field.Name |> not then
                if i > 0 then sb.Append(",") |> ignore
                sb.Append(field.Name) |> ignore
                i <- i + 1


        if simpleQuries <> null && simpleQuries.Count > 0 then
            for KeyValue (k, v) in simpleQuries do
                sb.Append(combinator).Append(k).Append("=").Append(v) |> ignore


        let mutable expands = expands

        if not disableAutoExpand then
            if expands = null then expands <- Dictionary<string, string>()

            for field in fields do
                if expands.ContainsKey field.Name |> not && not (excludedFields |> List.contains field.Name) then
                    if FSharpType.IsRecord field.PropertyType then
                        if field.PropertyType = ty then failwith "Recursive record is not supported"
                        expands[field.Name] <-
                            (generateQuery field.PropertyType ";" false loopDeepthMax loopDeepth null null null List.Empty).ToString()
                    elif FSharpType.IsRecordOption field.PropertyType
                         && (field.PropertyType.GenericTypeArguments[0] <> ty || loopDeepth <= loopDeepthMax) then
                        let nextLoopDeepth =
                            if field.PropertyType.GenericTypeArguments[0] = ty then
                                loopDeepth + 1
                            else
                                loopDeepth
                        expands[field.Name] <-
                            (generateQuery field.PropertyType.GenericTypeArguments[0] ";" false loopDeepthMax nextLoopDeepth null null null List.Empty)
                                .ToString()
                    else
                        match FSharpType.TryGetIEnumeralbleGenericType field.PropertyType with
                        | Some ty when field.PropertyType <> ty || loopDeepth <= loopDeepthMax ->
                            let nextLoopDeepth = if field.PropertyType = ty then loopDeepth + 1 else loopDeepth
                            expands[field.Name] <- (generateQuery ty ";" false loopDeepthMax nextLoopDeepth null null null List.Empty).ToString()
                        | _ -> ()

        let filteredExpands =
            if expands = null then
                []
            else
                expands |> Seq.filter (fun (KeyValue (k, _)) -> excludedFields |> List.contains k |> not) |> Seq.toList

        if filteredExpands.Length > 0 then
            let mutable i = 0
            sb.Append(combinator).Append("$expand=") |> ignore
            for KeyValue (k, v) in filteredExpands do
                if i > 0 then sb.Append(",") |> ignore
                if String.IsNullOrEmpty v |> not then
                    sb.Append(k).Append("(").Append(v).Append(")") |> ignore
                else
                    sb.Append(k) |> ignore
                i <- i + 1


        if filter <> null && filter.Count > 0 then
            let mutable i = 0
            for filterStr in filter do
                if i > 0 then sb.Append(" and ") |> ignore
                if String.IsNullOrEmpty filterStr |> not then
                    if i = 0 then sb.Append(combinator).Append("$filter=") |> ignore
                    sb.Append("(").Append(filterStr).Append(")") |> ignore
                    i <- i + 1


        sb.ToString()


type ODataQueryContext<'T>() =

    member val SimpleQuries = Dictionary<string, string>()
    member val Expand = Dictionary<string, string>()
    member val Filter = List<string>()
    member val ExcludedFields = List<string>()
    member val DisableAutoExpand = false with get, set
    member val MaxLoopDeepth = 1 with get, set

    member ctx.ToQuery(?combinator) =
        generateQuery
            typeof<'T>
            (defaultArg combinator "&")
            ctx.DisableAutoExpand
            ctx.MaxLoopDeepth
            0
            ctx.SimpleQuries
            ctx.Expand
            ctx.Filter
            (ctx.ExcludedFields |> Seq.toList)

    member ctx.MergeInto(target: ODataQueryContext<'T>) =
        for KeyValue (k, v) in ctx.SimpleQuries do
            target.SimpleQuries[ k ] <- v
        for KeyValue (k, v) in ctx.Expand do
            target.Expand[ k ] <- v
        target.Filter.AddRange ctx.Filter
        target.DisableAutoExpand <- ctx.DisableAutoExpand
        target.ExcludedFields.AddRange ctx.ExcludedFields

        target

    member inline ctx.SetOrderBy(value) =
        let key = "$orderby"
        match ctx.SimpleQuries.TryGetValue key with
        | true, str -> ctx.SimpleQuries[ key ] <- str + "," + value
        | _ -> ctx.SimpleQuries.Add(key, value)
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

    /// With this we can support use case like backward compatibility of database changes
    [<CustomOperation("excludeSelect")>]
    member inline _.ExcludeSelect(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>) =
        ctx.ExcludedFields.Add(getExpressionName prop)
        ctx


    [<CustomOperation("count")>]
    member inline _.Count(ctx: ODataQueryContext<'T>) =
        ctx.SimpleQuries[ "$count" ] <- "true"
        ctx

    [<CustomOperation("take")>]
    member inline _.Take(ctx: ODataQueryContext<'T>, num: int) =
        ctx.SimpleQuries[ "$top" ] <- num.ToString()
        ctx

    [<CustomOperation("take")>]
    member inline this.Take(ctx: ODataQueryContext<'T>, num: int option) =
        match num with
        | None -> ctx
        | Some num -> this.Take(ctx, num)

    [<CustomOperation("skip")>]
    member inline _.Skip(ctx: ODataQueryContext<'T>, num: int) =
        ctx.SimpleQuries[ "$skip" ] <- num.ToString()
        ctx

    [<CustomOperation("skip")>]
    member inline this.Skip(ctx: ODataQueryContext<'T>, num: int option) =
        match num with
        | None -> ctx
        | Some num -> this.Skip(ctx, num)


    [<CustomOperation("orderBy")>]
    member inline _.OrderBy(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>) = ctx.SetOrderBy(getExpressionName prop)

    [<CustomOperation("orderBy")>]
    member inline _.OrderBy(ctx: ODataQueryContext<'T>, x: string) = ctx.SetOrderBy x

    [<CustomOperation("orderByDesc")>]
    member inline _.OrderByDesc(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>) = ctx.SetOrderBy(getExpressionName prop + " desc")

    [<CustomOperation("orderByDesc")>]
    member inline _.OrderByDesc(ctx: ODataQueryContext<'T>, x: string) = ctx.SetOrderBy(x + " desc")


    [<CustomOperation("expandPoco")>]
    member inline _.Expand(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>) =
        ctx.Expand[ getExpressionName prop ] <- sprintf "$select=%s" (generateSelectQueryByType false typeof<'Prop>)
        ctx

    [<CustomOperation("expandPoco")>]
    member inline _.Expand(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>, expandCtx: ODataQueryContext<'Prop>) =
        ctx.Expand[ getExpressionName prop ] <- expandCtx.ToQuery(";")
        ctx

    [<CustomOperation("expandPoco")>]
    member inline _.Expand(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop option>>, expandCtx: ODataQueryContext<'Prop>) =
        ctx.Expand[ getExpressionName prop ] <- expandCtx.ToQuery(";")
        ctx

    [<CustomOperation("expandList")>]
    member inline _.ExpandList(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, IEnumerable<'Prop>>>) =
        ctx.Expand[ getExpressionName prop ] <- sprintf "$select=%s" (generateSelectQueryByType false typeof<'Prop>)
        ctx

    [<CustomOperation("expandList")>]
    member inline _.ExpandList(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, IEnumerable<'Prop>>>, expandCtx: ODataQueryContext<'Prop>) =
        ctx.Expand[ getExpressionName prop ] <- expandCtx.ToQuery(";")
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
        ctx.SimpleQuries[ key ] <- value
        ctx



type ODataFilterBuilder<'T>(oper: string) =

    let buildFilter (ctx: FilterCombinator) (value: obj) (builder) =
        if isNull value then
            ctx
        else
            let handle (value: obj) =
                match value with
                | :? string as x -> builder (box ("'" + x + "'"))
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

    member inline this.Yield(x: string) = FilterCombinator(fun sb -> sb.Append(this.Operator).Append("(").Append(x).Append(")"))

    member inline this.Yield(expressions: string seq) =
        FilterCombinator(fun sb ->
            for expression in expressions do
                sb.Append(this.Operator).Append("(").Append(expression).Append(")") |> ignore
            sb
        )

    member inline this.Yield(x: ODataFilterContext<'T>) =
        FilterCombinator(fun sb -> sb.Append(this.Operator).Append("(").Append(x.ToQuery()).Append(")"))

    member inline this.Yield(x: string option) =
        match x with
        | None -> emptyFilterCombinator
        | Some x -> FilterCombinator(fun sb -> sb.Append(this.Operator).Append("(").Append(x).Append(")"))


    member inline _.For(ctx: FilterCombinator, [<InlineIfLambda>] fn: unit -> FilterCombinator) =
        FilterCombinator(fun sb -> fn().Invoke(ctx.Invoke(sb)))

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


    [<CustomOperation("gt")>]
    member this.GT(ctx: FilterCombinator, name: string, value: obj) =
        let builder (x: obj) = FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append(name).Append(" gt ").Append(x))
        buildFilter ctx value builder

    [<CustomOperation("gt")>]
    member this.GT(ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: obj) = this.GT(ctx, getExpressionName prop, value)


    [<CustomOperation("contains")>]
    member inline this.Contains([<InlineIfLambda>] ctx: FilterCombinator, name: string, value: string) =
        if isNull value then
            ctx
        else
            FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append("contains(").Append(name).Append(", '").Append(value).Append("')"))

    [<CustomOperation("contains")>]
    member inline this.Contains([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: string) =
        this.Contains(ctx, getExpressionName prop, value)

    [<CustomOperation("contains")>]
    member inline this.Contains([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, value: string option) =
        match value with
        | None -> ctx
        | Some x -> this.Contains(ctx, prop, x)


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
        | None -> ctx
        | Some x -> FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append("(").Append(x).Append(")"))


type ODataAndFilterBuilder<'T>() =
    inherit ODataFilterBuilder<'T> " and "

type ODataOrFilterBuilder<'T>() =
    inherit ODataFilterBuilder<'T> " or "


type OData<'T> = ODataQueryBuilder<'T>
type ODataOr<'T> = ODataOrFilterBuilder<'T>
type ODataAnd<'T> = ODataAndFilterBuilder<'T>


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

/// Create a OData query string for a type with default settings
let odataDefault<'T> () = ODataQueryBuilder<'T>().Yield()

let odataDefaultQuery<'T> () = odataDefault<'T>().ToQuery()
