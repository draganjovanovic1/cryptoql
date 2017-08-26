namespace CryptoQl.Api

module Program =

    open System
    open System.Net
    open System.Collections.Generic
    open Microsoft.AspNetCore.Builder
    open Microsoft.AspNetCore.Hosting
    open Microsoft.AspNetCore.Http
    open Microsoft.Extensions.Logging
    open Microsoft.Extensions.DependencyInjection
    open Microsoft.AspNetCore.HttpOverrides
    open Microsoft.AspNetCore.Cors
    open Serilog
    open Giraffe.HttpContextExtensions
    open Giraffe.HttpHandlers
    open Giraffe.Middleware
    open Giraffe.Tasks
    open GraphQl

    let unhandledError ex _ =
        Log.Error (ex, "An unhandled exception has occurred while executing the request.")

        clearResponse
        >=> setStatusCode (int HttpStatusCode.InternalServerError)
        >=> text "An error occurred"

    let notFound =
        setStatusCode (int HttpStatusCode.NotFound)
        >=> text "URL not found"

    let handleGraphQl storage gql bindQuery : HttpHandler =
        fun (next: HttpFunc) (ctx: HttpContext) ->
            task {
                let! query = bindQuery ctx
                let! result = gql.execute storage query
                return! json result next ctx
            }

    let getQueryFromUrl (ctx: HttpContext) =
        task { return ctx.BindQueryString<GraphQlQuery> () }

    let getQueryFromBody (ctx: HttpContext) =
        task { return! ctx.BindJson<GraphQlQuery> () }

    let composeApp storage gql =
        let handleGraphQl = handleGraphQl storage gql
        choose [
            GET >=> route "/graphql" >=> handleGraphQl getQueryFromUrl
            POST >=> route "/graphql" >=> handleGraphQl getQueryFromBody
            notFound
        ]

    type Startup () =
        member __.Configure (app: IApplicationBuilder)
                            (loggerFactory: ILoggerFactory)
                            (storage: Storage.T)
                            (gql: GraphQl.Executor) =

            Json.applyGlobalJsonSettings ()
            loggerFactory.AddSerilog (Log.Logger) |> ignore

            app.UseCors (fun b ->
                b.AllowAnyOrigin()
                    .AllowAnyHeader()
                    .AllowAnyMethod () |> ignore
            ) |> ignore

            let forwardOptions = ForwardedHeadersOptions ()
            forwardOptions.ForwardedHeaders <- ForwardedHeaders.XForwardedFor ||| ForwardedHeaders.XForwardedProto

            app.UseForwardedHeaders(forwardOptions)
                .UseGiraffe(composeApp storage gql)

            app.UseGiraffeErrorHandler (unhandledError)

        member __.ConfigureServices (services: IServiceCollection) =
            services
                .AddCors()
                .AddAuthentication()
            |> ignore

    let storage: Storage.T =
        let readTicker = Storage.readTicker (TimeSpan.FromMinutes (0.5)) DataSources.loadTicker
        let readExchange = Storage.readExchange (TimeSpan.FromHours (1.)) DataSources.loadExchangeRates

        { readTicker = readTicker
          convertFromUsd = Storage.convertFromUsd readExchange }

    let gql: GraphQl.Executor =
        { execute = fun s q -> GraphQl.executeQuery s q |> Async.map box }

    [<EntryPoint>]
    let main _ =

        Serilog.Log.Logger <-
            LoggerConfiguration()
                .ReadFrom.Configuration(Configuration.root)
                .CreateLogger ()

        WebHostBuilder()
            .UseKestrel()
            .UseConfiguration(Configuration.root)
            .UseStartup<Startup>()
            .ConfigureServices(fun x ->
                x.AddSingleton<Storage.T>(storage)
                    .AddSingleton<GraphQl.Executor>(gql) |> ignore
            )
            .Build()
            .Run()

        0