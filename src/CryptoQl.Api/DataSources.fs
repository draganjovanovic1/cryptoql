namespace CryptoQl.Api

module Types =

    type Currency =
        | USD
        | EUR
        | GBP
        | RUB
        | CAD
        | AUD
        | CHF
        | JPY
        member x.AsString =
            match x with
            | USD -> "USD"
            | EUR -> "EUR"
            | GBP -> "GBP"
            | RUB -> "RUB"
            | CAD -> "CAD"
            | AUD -> "AUD"
            | CHF -> "CHF"
            | JPY -> "JPY"

    type Supply =
        { Total: float option
          Available: float option }

    type MarketCap =
        { AmountUsd: float option
          Rank: int }

    type PriceChange =
        { LastHour: float option
          LastDay: float option
          LastWeek: float option }

    type Trading =
        { PriceUsd: float
          LastDayVolumeUsd: float option
          PriceBtc: float
          PriceChange: PriceChange }

    type Ticker =
        { Symbol: string
          Name: string
          Trading: Trading
          MarketCap: MarketCap
          Supply: Supply }

module DataSources =
    open System
    open System.Collections.Generic
    open System.Collections.Concurrent
    open Serilog
    open Types

    (*
        https://api.coinmarketcap.com/v1/ticker/

        [
            {
                "id": "bitcoin",
                "name": "Bitcoin",
                "symbol": "BTC",
                "rank": "1",
                "price_usd": "4214.14",
                "price_btc": "1.0",
                "24h_volume_usd": "2096990000.0",
                "market_cap_usd": "69632974411.0",
                "available_supply": "16523650.0",
                "total_supply": "16523650.0",
                "percent_change_1h": "-0.33",
                "percent_change_24h": "0.6",
                "percent_change_7d": "-3.32",
                "last_updated": "1503598479"
            },
            ...
        ]
    *)

    type Ticker =
        { id: string
          name: string
          symbol: string
          rank: int
          price_usd: float
          price_btc: float
          ``24h_volume_usd``: float option
          market_cap_usd: float option
          available_supply: float option
          total_supply: float option
          percent_change_1h: float option
          percent_change_24h: float option
          percent_change_7d: float option
          last_updated: int }

    (*
        http://api.fixer.io/latest?base=USD&symbols=EUR,GBP,RUB,CAD,AUD,CHF,JPY

        {
          "base": "USD",
          "date": "2017-08-24",
          "rates": {
            "AUD": 1.2664,
            "BGN": 1.6566,
            ...
          }
        }
    *)

    type ExchangeRates =
        { ``base``: string
          date: DateTime
          rates: IDictionary<string, float> }

    let loadTicker () =
        async {
            let! result =
                Http.downloadString "https://api.coinmarketcap.com/v1/ticker/"
                |> Async.map (Json.deserialize<Ticker list>)
                |> Async.Catch

            match result with
            | Choice1Of2 result ->
                let map x =
                    { Symbol = x.symbol
                      Name = x.name
                      Trading =
                        { PriceUsd = x.price_usd
                          LastDayVolumeUsd = x.``24h_volume_usd``
                          PriceBtc = x.price_btc
                          PriceChange =
                            { LastHour = x.percent_change_1h
                              LastDay = x.percent_change_24h
                              LastWeek = x.percent_change_7d } }
                      MarketCap =
                        { AmountUsd = x.market_cap_usd
                          Rank = x.rank }
                      Supply =
                        { Total = x.total_supply
                          Available = x.available_supply } }
                return
                    result
                    |> List.map (fun x -> x.symbol, map x)
                    |> dict
            | Choice2Of2 ex ->
                Log.Error (ex, "An error occurred while refreshing ticker")
                return dict []
        }

    let loadExchangeRates () =
        let extractValue (rates: IDictionary<string, float>) (c: Currency) =
            match rates.TryGetValue (c.AsString) with
            | true, rate -> c, (*) rate
            | _ -> c, (*) 0.

        async {
            let! result =
                Http.downloadString "http://api.fixer.io/latest?base=USD&symbols=EUR,GBP,RUB,CAD,AUD,CHF,JPY"
                |> Async.map (Json.deserialize<ExchangeRates>)
                |> Async.Catch

            match result with
            | Choice1Of2 result ->
                let rates =
                    [EUR; GBP; RUB; CAD; AUD; CHF; JPY]
                    |> List.map (extractValue result.rates)
                return dict rates
            | Choice2Of2 ex ->
                Log.Error (ex, "An error occurred while refreshing exchange rates")
                let defaultExchangeRates =
                    [EUR, (*) 0.
                     GBP, (*) 0.
                     RUB, (*) 0.
                     CAD, (*) 0.
                     AUD, (*) 0.
                     CHF, (*) 0.
                     JPY, (*) 0.]
                return dict defaultExchangeRates
        }

module Storage =
    open System
    open System.Collections.Generic
    open Microsoft.Extensions.Caching.Memory
    open Types
    open Serilog

    type T =
        { readTicker: unit -> IDictionary<string, Ticker>
          convertFromUsd: Currency -> float -> float }

    let private cache =
        let options =
            MemoryCacheOptions (ExpirationScanFrequency=TimeSpan.FromMinutes(0.5))
        new MemoryCache (options)

    let private read<'a> key lck (validFor: TimeSpan) reload =
        fun () ->
            let value = cache.Get<'a>(key)

            if not (obj.ReferenceEquals (null, value)) then
                value
            else
                lock lck <| fun _ ->
                    let value = cache.Get<'a>(key)
                    if not (obj.ReferenceEquals (null, value)) then
                        value
                    else
                        cache.GetOrCreate<'a> (
                            key,
                            fun x ->
                                x.SetAbsoluteExpiration (validFor) |> ignore
                                Async.RunSynchronously (reload ())
                        )

    let private lockTicker = obj ()
    let private lockExchange = obj ()

    let readTicker = read<IDictionary<string, Ticker>> "Ticker" lockTicker

    let readExchange = read<IDictionary<Currency, float -> float>> "ExchangeRate" lockExchange

    let convertFromUsd (readExchange: unit -> IDictionary<Currency, float -> float>) curr amount  =
        if curr = USD then
            amount
        else
            let rates = readExchange ()
            rates.[curr] amount