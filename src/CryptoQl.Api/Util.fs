namespace CryptoQl.Api

[<AutoOpen>]
module Util =

    module Http =
        open System
        open System.Net.Http

        let downloadString url =
            async {
                use client = new HttpClient ()
                return! client.GetStringAsync (Uri url) |> Async.AwaitTask
            }

    module Async =
        let inline map cont comp =
            async {
                let! res = comp
                return cont res
            }