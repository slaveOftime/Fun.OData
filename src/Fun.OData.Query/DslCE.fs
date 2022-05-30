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
        disableAutoExpand
        (simpleQuries: Dictionary<string, string>)
        (expands: Dictionary<string, string>)
        (filter: List<string>)
        =
        let sb = StringBuilder()
        let fields = FSharpType.GetRecordFields(ty)


        sb.Append("$select=") |> ignore

        let mutable i = 0
        for field in fields do
            if i > 0 then sb.Append(",") |> ignore
            sb.Append(field.Name) |> ignore
            i <- i + 1


        if simpleQuries <> null && simpleQuries.Count > 0 then
            for KeyValue (k, v) in simpleQuries do
                sb.Append("&").Append(k).Append("=").Append(v) |> ignore


        let mutable expands = expands

        if not disableAutoExpand then
            if expands = null then expands <- Dictionary<string, string>()

            for field in fields do
                if expands.ContainsKey field.Name |> not then
                    if FSharpType.IsRecord field.PropertyType then
                        expands[field.Name] <- (generateQuery field.PropertyType false null null null).ToString()
                    elif FSharpType.IsRecordOption field.PropertyType then
                        expands[field.Name] <- (generateQuery field.PropertyType.GenericTypeArguments[0] false null null null).ToString()
                    else
                        match FSharpType.TryGetIEnumeralbleGenericType field.PropertyType with
                        | Some ty -> expands[field.Name] <- (generateQuery ty false null null null).ToString()
                        | None -> ()

        if expands <> null && expands.Count > 0 then
            let mutable i = 0
            sb.Append("&$expand=") |> ignore
            for KeyValue (k, v) in expands do
                if i > 0 then sb.Append(",") |> ignore
                if String.IsNullOrEmpty v |> not then
                    sb.Append(k).Append("(").Append(expands[k]).Append(")") |> ignore
                else
                    sb.Append(k) |> ignore
                i <- i + 1


        if filter <> null && filter.Count > 0 then
            let mutable i = 0
            sb.Append("&$filter=") |> ignore
            while i < filter.Count do
                if i > 0 then sb.Append(" and ") |> ignore
                let filterStr = filter[i]
                if String.IsNullOrEmpty filterStr |> not then
                    sb.Append("(").Append(filterStr).Append(")") |> ignore
                    i <- i + 1


        sb.ToString()


type ODataQueryContext<'T>() =

    member val SimpleQuries = Dictionary<string, string>()
    member val Expand = Dictionary<string, string>()
    member val Filter = List<string>()
    member val DisableAutoExpand = false with get, set

    member ctx.ToQuery() = generateQuery typeof<'T> ctx.DisableAutoExpand ctx.SimpleQuries ctx.Expand ctx.Filter

    member ctx.MergeInto(target: ODataQueryContext<'T>) =
        for KeyValue (k, v) in ctx.SimpleQuries do
            target.SimpleQuries[ k ] <- v
        for KeyValue (k, v) in ctx.Expand do
            target.Expand[ k ] <- v
        target.Filter.AddRange ctx.Filter
        target.DisableAutoExpand <- ctx.DisableAutoExpand

        target


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

    member inline _.Run(filter: ODataFilterContext<'T>) =
        let ctx = ODataQueryContext<'T>()
        ctx.Filter.Add(filter.ToQuery())
        ctx

    member inline _.Yield() = ODataQueryContext<'T>()

    member inline _.Yield(_: unit) = ODataQueryContext<'T>()

    member inline _.Yield(x: ODataQueryContext<'T>) = x

    member inline _.Yield(x: ODataFilterContext<'T>) = x

    member inline _.Delay([<InlineIfLambda>] fn) = fn ()

    member inline _.For(ctx: ODataQueryContext<'T>, [<InlineIfLambda>] fn: unit -> ODataQueryContext<'T>) = fn().MergeInto(ctx)

    member inline _.For(ctx: ODataQueryContext<'T>, [<InlineIfLambda>] fn: unit -> ODataFilterContext<'T>) =
        ctx.Filter.Add(fn().ToQuery())
        ctx

    member inline _.Combine(x: ODataQueryContext<'T>, y: ODataQueryContext<'T>) = y.MergeInto(x)

    member inline _.Combine(ctx: ODataQueryContext<'T>, filter: ODataFilterContext<'T>) =
        ctx.Filter.Add(filter.ToQuery())
        ctx

    member inline _.Combine(filter: ODataFilterContext<'T>, ctx: ODataQueryContext<'T>) =
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
    member inline _.OrderBy(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>) =
        ctx.SimpleQuries[ "$orderBy" ] <- getExpressionName prop
        ctx

    [<CustomOperation("orderBy")>]
    member inline _.OrderBy(ctx: ODataQueryContext<'T>, x: string) =
        ctx.SimpleQuries[ "$orderBy" ] <- x
        ctx

    [<CustomOperation("orderByDesc")>]
    member inline _.OrderByDesc(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>) =
        ctx.SimpleQuries[ "$orderBy" ] <- getExpressionName prop + " desc"
        ctx

    [<CustomOperation("orderByDesc")>]
    member inline _.OrderByDesc(ctx: ODataQueryContext<'T>, x: string) =
        ctx.SimpleQuries[ "$orderBy" ] <- x + " desc"
        ctx


    [<CustomOperation("expandPoco")>]
    member inline _.Expand(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>) =
        ctx.Expand[ getExpressionName prop ] <- sprintf "$select=%s" (generateSelectQueryByType false typeof<'Prop>)
        ctx

    [<CustomOperation("expandPoco")>]
    member inline _.Expand(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, 'Prop>>, expandCtx: ODataQueryContext<'Prop>) =
        ctx.Expand[ getExpressionName prop ] <- expandCtx.ToQuery()
        ctx

    [<CustomOperation("expandList")>]
    member inline _.ExpandList(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, IEnumerable<'Prop>>>) =
        ctx.Expand[ getExpressionName prop ] <- sprintf "$select=%s" (generateSelectQueryByType false typeof<'Prop>)
        ctx

    [<CustomOperation("expandList")>]
    member inline _.ExpandList(ctx: ODataQueryContext<'T>, prop: Expression<Func<'T, IEnumerable<'Prop>>>, expandCtx: ODataQueryContext<'Prop>) =
        ctx.Expand[ getExpressionName prop ] <- expandCtx.ToQuery()
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
    member inline _.Filter(ctx: ODataQueryContext<'T>, filter: ODataFilterContext<'T>) =
        ctx.Filter.Add(filter.ToQuery())
        ctx


    [<CustomOperation("keyValue")>]
    member inline _.KeyValue(ctx: ODataQueryContext<'T>, key: string, value: string) =
        ctx.SimpleQuries[ key ] <- value
        ctx



type ODataFilterBuilder<'T>(oper: string) =

    member val Operator = oper


    member inline this.Run(ctx: FilterCombinator) = ODataFilterContext<'T>(this.Operator, ctx)


    member inline _.Yield(_: unit) = emptyFilterCombinator

    member inline this.Yield(x: string) = FilterCombinator(fun sb -> sb.Append(this.Operator).Append("(").Append(x).Append(")"))

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
    member inline this.EQ([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: string) =
        FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append(getExpressionName prop).Append(" eq ").Append(x))

    [<CustomOperation("eq")>]
    member inline this.EQ([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: string option) =
        match x with
        | None -> ctx
        | Some x -> this.EQ(ctx, prop, x)

    [<CustomOperation("eq")>]
    member inline this.EQ([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: 'Prop) = this.EQ(ctx, prop, x.ToString())

    [<CustomOperation("eq")>]
    member inline this.EQ([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: 'Prop option) =
        this.EQ(ctx, prop, Option.map string x)


    [<CustomOperation("gt")>]
    member inline this.GT([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: string) =
        FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append(getExpressionName prop).Append(" gt ").Append(x))

    [<CustomOperation("gt")>]
    member inline this.GT([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: string option) =
        match x with
        | None -> ctx
        | Some x -> this.GT(ctx, prop, x)

    [<CustomOperation("gt")>]
    member inline this.GT([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: 'Prop) = this.GT(ctx, prop, x.ToString())

    [<CustomOperation("gt")>]
    member inline this.GT([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: 'Prop option) =
        this.GT(ctx, prop, Option.map string x)


    [<CustomOperation("lt")>]
    member inline this.LT([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: string) =
        FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append(getExpressionName prop).Append(" lt ").Append(x))

    [<CustomOperation("lt")>]
    member inline this.LT([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: string option) =
        match x with
        | None -> ctx
        | Some x -> this.LT(ctx, prop, x)

    [<CustomOperation("lt")>]
    member inline this.LT([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: 'Prop) = this.LT(ctx, prop, x.ToString())

    [<CustomOperation("lt")>]
    member inline this.LT([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: 'Prop option) =
        this.LT(ctx, prop, Option.map string x)


    [<CustomOperation("contains")>]
    member inline this.Contains([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: string) =
        FilterCombinator(fun sb ->
            ctx
                .Invoke(sb)
                .Append(this.Operator)
                .Append("contains(")
                .Append(getExpressionName prop)
                .Append(", '")
                .Append(x)
                .Append("')")
        )

    [<CustomOperation("contains")>]
    member inline this.Contains([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, x: string option) =
        match x with
        | None -> ctx
        | Some x -> this.Contains(ctx, prop, x)


    [<CustomOperation("custom")>]
    member inline this.Custom([<InlineIfLambda>] ctx: FilterCombinator, prop: Expression<Func<'T, 'Prop>>, fn: string -> string) =
        FilterCombinator(fun sb -> ctx.Invoke(sb).Append(this.Operator).Append("(").Append(fn (getExpressionName prop)).Append(")"))

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
    member _.Run(ctx: ODataFilterContext<'T>) = base.Run(ctx).ToQuery()

type OdataOrQuery<'T>() =
    inherit ODataOrFilterBuilder<'T>()

    member _.Run(ctx) = base.Run(ctx).ToQuery()

type OdataAndQuery<'T>() =
    inherit ODataAndFilterBuilder<'T>()

    member _.Run(ctx) = base.Run(ctx).ToQuery()

/// Generate odata query context
let odata<'T> = OData<'T>()
/// Generate odata filter context with or operator
let odataOr<'T> = ODataOr<'T>()
/// Generate odata filter context with and operator
let odataAnd<'T> = ODataAnd<'T>()
/// Generate odata query string
let odataQuery<'T> = OdataQuery<'T>()
/// Generate odata filter query string with or operator
let odataOrQuery<'T> = OdataOrQuery<'T>()
/// Generate odata filter query string with and operator
let odataAndQuery<'T> = OdataAndQuery<'T>()

/// Create a OData query string for a type with default settings
let odataSimple<'T> () = ODataQueryBuilder<'T>().Yield().ToQuery()
