namespace CryptoQl.Api

module GraphQl =

    open System
    open System.Collections.Generic
    open GraphQL
    open GraphQL.Validation
    open GraphQL.Execution
    open GraphQL.Http
    open GraphQL.Types
    open Types

    [<CLIMutable>]
    type GraphQlQuery =
        { Query: string
          OperationName: string
          Variables: Dictionary<string, obj> }
        with
            member x.Normalized =
                if isNull x.Variables then
                    { x with
                        Variables = Dictionary<string, obj>() }
                else x

    type Executor =
        { execute: Storage.T -> GraphQlQuery -> Async<obj> }

    type StorageContext =
        { storage: Storage.T }

    type AmountType () as x =
        inherit ObjectGraphType<float option> ()

        let resolveAmount curr (ctx: ResolveFieldContext<float option>) =
            let context = ctx.UserContext.As<StorageContext> ()
            ctx.Source
            |> Option.map (context.storage.convertFromUsd curr >> box)
            |> Option.toObj

        do
            x.Name <- "Amount"
            x.Description <- "FIAT currencies ammount"

            x.Field<FloatGraphType>()
                .Name("usd")
                .Description("USD amount")
                .Resolve(resolveAmount USD) |> ignore

            x.Field<FloatGraphType>()
                .Name("eur")
                .Description("EUR amount")
                .Resolve(resolveAmount EUR) |> ignore

            x.Field<FloatGraphType>()
                .Name("gbp")
                .Description("GBP amount")
                .Resolve(resolveAmount GBP) |> ignore

            x.Field<FloatGraphType>()
                .Name("rub")
                .Description("RUB amount")
                .Resolve(resolveAmount RUB) |> ignore

            x.Field<FloatGraphType>()
                .Name("cad")
                .Description("CAD amount")
                .Resolve(resolveAmount CAD) |> ignore

            x.Field<FloatGraphType>()
                .Name("aud")
                .Description("AUD amount")
                .Resolve(resolveAmount AUD) |> ignore

            x.Field<FloatGraphType>()
                .Name("chf")
                .Description("CHF amount")
                .Resolve(resolveAmount CHF) |> ignore

            x.Field<FloatGraphType>()
                .Name("jpy")
                .Description("JPY amount")
                .Resolve(resolveAmount JPY) |> ignore

    type MarketCapType () as x =
        inherit ObjectGraphType<MarketCap> ()

        do
            x.Name <- "MarketCap"
            x.Description <- "Cryptocurrency market capitalization"

            x.Field((fun x -> x.Rank), nullable = false)
                .Description("Overall rank by market capitalization") |> ignore

            x.Field<AmountType>()
                .Name("amount")
                .Description("Total market capitalization")
                .Resolve(fun ctx ->
                    box ctx.Source.AmountUsd
                ) |> ignore

    type PriceChangeType () as x =
        inherit ObjectGraphType<PriceChange> ()

        do
            x.Name <- "PriceChange"
            x.Description <- "Cryptocurrency price change"

            x.Field<FloatGraphType>()
                .Name("lastHour")
                .Description("Last 1h change %")
                .Resolve(fun ctx ->
                    ctx.Source.LastHour
                    |> Option.map box
                    |> Option.toObj
                ) |> ignore

            x.Field<FloatGraphType>()
                .Name("lastDay")
                .Description("Last 24h change %")
                .Resolve(fun ctx ->
                    ctx.Source.LastDay
                    |> Option.map box
                    |> Option.toObj
                ) |> ignore

            x.Field<FloatGraphType>()
                .Name("lastWeek")
                .Description("Last 7d change %")
                .Resolve(fun ctx ->
                    ctx.Source.LastWeek
                    |> Option.map box
                    |> Option.toObj
                ) |> ignore

    type SupplyType () as x =
        inherit ObjectGraphType<Supply> ()

        do
            x.Name <- "Supply"
            x.Description <- "Cryptocurrency token supply"

            x.Field<FloatGraphType>()
                .Name("available")
                .Description("Available token supply")
                .Resolve(fun ctx ->
                    ctx.Source.Available
                    |> Option.map box
                    |> Option.toObj
                ) |> ignore

            x.Field<FloatGraphType>()
                .Name("total")
                .Description("Total token supply")
                .Resolve(fun ctx ->
                    ctx.Source.Total
                    |> Option.map box
                    |> Option.toObj
                ) |> ignore

    type TradingType () as x =
        inherit ObjectGraphType<Trading> ()

        do
            x.Name <- "Trading"
            x.Description <- "Cryptocurrency trading info"

            x.Field<AmountType>()
                .Name("price")
                .Description("FIAT price")
                .Resolve(fun ctx ->
                    box (Some ctx.Source.PriceUsd)
                ) |> ignore

            x.Field<AmountType>()
                .Name("lastDayVolume")
                .Description("FIAT trading volume in last 24h")
                .Resolve(fun ctx ->
                    box ctx.Source.LastDayVolumeUsd
                ) |> ignore

            x.Field(fun x -> x.PriceBtc)
                .Description("BTC price") |> ignore

            x.Field<PriceChangeType>()
                .Name("priceChange")
                .Description("Price change")
                .Resolve(fun ctx ->
                    box ctx.Source.PriceChange
                ) |> ignore

    type TickerType () as x =
        inherit ObjectGraphType<Ticker> ()

        do
            x.Name <- "Ticker"
            x.Description <- "Cryptocurrency ticker"

            x.Field((fun x -> x.Symbol), nullable = false)
                .Description("Symbol") |> ignore

            x.Field((fun x -> x.Name), nullable = false)
                .Description("Name") |> ignore

            x.Field<TradingType>()
                .Name("tradingInfo")
                .Description("Trading info")
                .Resolve(fun ctx ->
                    box ctx.Source.Trading
                ) |> ignore

            x.Field<MarketCapType>()
                .Name("marketCap")
                .Description("Market capitalization")
                .Resolve(fun ctx ->
                    box ctx.Source.MarketCap
                ) |> ignore

            x.Field<SupplyType>()
                .Name("supply")
                .Description("Token supply")
                .Resolve(fun ctx ->
                    box ctx.Source.Supply
                ) |> ignore

    type Query (storage: Storage.T) as x =
        inherit ObjectGraphType ()

        let getTicker (ctx: ResolveFieldContext<_>) =
            storage.readTicker ()
            |> fun x -> x.Values
            |> box

        do
            x.Name <- "Query"
            x.Description <- "Cryptocurrencies ticker"

            x.Field<ListGraphType<TickerType>>(
                name = "ticker",
                description = "Ride details",
                resolve = Func<ResolveFieldContext<_>, _>(getTicker)
            ) |> ignore

    let executeQuery storage query =
        async {
            use schema = new Schema (Query = Query (storage))
            return!
                DocumentExecuter()
                    .ExecuteAsync(fun o ->
                        o.Schema <- schema
                        o.Query <- query.Query
                        o.OperationName <- query.OperationName
                        o.Inputs <- Inputs (query.Variables)
                        o.UserContext <- { storage = storage }
                    )
                |> Async.AwaitTask
        }