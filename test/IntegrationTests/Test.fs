namespace IntegrationTests

module AccountRequestTests =

    open System
    open System.Net
    open System.Net.Http
    open System.Text
    open Xunit
    open Microsoft.AspNetCore.Hosting
    open Microsoft.AspNetCore.TestHost
    open Microsoft.Extensions.DependencyInjection
    open CryptoQl.Api

    type T () =
        let server =
            new TestServer
                (WebHostBuilder()
                    .UseStartup<Program.Startup>()
                    // .ConfigureServices(fun x ->
                    //     x.AddSingleton<Storage.T>(storage)
                    //         .AddSingleton<GraphQl.Executor>(gql) |> ignore)
                )
        let client =
            let result = server.CreateClient ()
            result.BaseAddress <- Uri ("http://localhost:5000")
            result

        interface IDisposable
            with
            member __.Dispose () =
                client.Dispose ()
                server.Dispose ()