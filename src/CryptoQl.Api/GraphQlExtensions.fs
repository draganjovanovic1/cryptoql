namespace CryptoQl.Api

module GraphQlExtensions =

    open System
    open System.Linq
    open GraphQL
    open Newtonsoft.Json
    open Newtonsoft.Json.Serialization

    type GraphQlExecutionError (message, target: string, code: string) =
        inherit ExecutionError (message)

        member val Target = target with get, set
        member val Code = code with get, set

    type GraphQlCamelCasePropertyNamesContractResolver () =
        inherit CamelCasePropertyNamesContractResolver ()

        override __.ResolveContractConverter (objectType) =
            if typeof<ExecutionResult>.IsAssignableFrom (objectType) then
                null
            else
                base.ResolveContractConverter (objectType)

    type GraphQlExecutionErrorJsonConverter () =
        inherit JsonConverter ()

        let [<Literal>] GraphQlExecutionErrorCode = "GRAPHQL_EXECUTION_ERROR"
        let [<Literal>] GraphQlExecutionExceptionCode = "GRAPHQL_EXECUTION_EXCEPTION"
        let [<Literal>] GraphQlValidationErrorCode = "GRAPHQL_VALIDATION_ERROR"

        let hasErrors (errors: ExecutionErrors) =
            let errorsCount =
                if isNull errors then 0
                else errors.Count
            errorsCount > 0

        let writeData (result: ExecutionResult, writer: JsonWriter, serializer: JsonSerializer) =
            let data = result.Data

            if not (hasErrors result.Errors && isNull data) then
                writer.WritePropertyName ("data")
                serializer.Serialize (writer, data)

        let writeErrors (errors: ExecutionErrors, writer: JsonWriter, serializer: JsonSerializer, exposeExceptions: bool) =

            let writeCommonProperties code target message =
                writer.WritePropertyName ("code")
                serializer.Serialize (writer, code)
                writer.WritePropertyName ("target")
                serializer.Serialize (writer, target)
                writer.WritePropertyName ("message")
                serializer.Serialize (writer, message)

            let writeGraphQlExecutionErorr (err: GraphQlExecutionError) =
                writeCommonProperties err.Code err.Target err.Message

            let writeExecutionError (err: ExecutionError) =
                writeCommonProperties GraphQlExecutionErrorCode "executionError" err.Message

                if exposeExceptions && (not (isNull err.InnerException)) then
                    writer.WritePropertyName ("details")
                    writer.WriteStartArray ()
                    writer.WriteStartObject ()
                    writeCommonProperties GraphQlExecutionExceptionCode "innerException" (err.ToString ())
                    writer.WriteEndObject ()
                    writer.WriteEndArray ()

            let writeErrors (errors: ExecutionErrors) =
                errors
                |> Seq.iter (fun ex ->
                    writer.WriteStartObject ()

                    match ex with
                    | :? GraphQlExecutionError as err ->
                        writeGraphQlExecutionErorr err
                    // errors from the underlying library graphql-dotnet
                    | :? Execution.InvalidValueException as err ->
                        writeCommonProperties GraphQlValidationErrorCode "invalidValueException" err.Message
                    | :? Validation.ValidationError as err ->
                        writeCommonProperties GraphQlValidationErrorCode "validationError" err.Message
                    | err ->
                        writeExecutionError err

                    if not (isNull ex.Locations) then
                        writer.WritePropertyName ("locations")
                        writer.WriteStartArray ()
                        ex.Locations
                        |> Seq.iter (fun loc ->
                            writer.WriteStartObject ()
                            writer.WritePropertyName ("line")
                            serializer.Serialize (writer, loc.Line)
                            writer.WritePropertyName ("column")
                            serializer.Serialize (writer, loc.Column)
                            writer.WriteEndObject ()
                        )
                        writer.WriteEndArray ()

                    writer.WriteEndObject ()
                )

            writer.WritePropertyName ("errors")
            writer.WriteStartArray ()
            writeErrors errors
            writer.WriteEndArray ()

        override __.WriteJson (writer, value, serializer) =
            let result = value :?> ExecutionResult
            writer.WriteStartObject ()
            writeData (result, writer, serializer)

            if hasErrors result.Errors then
                writeErrors (result.Errors, writer, serializer, result.ExposeExceptions)

            writer.WriteEndObject ()

        override __.ReadJson (reader, objectType, exsitingValue, serializer) =
            raise (NotImplementedException ())

        override __.CanConvert (objectType) = objectType = typeof<ExecutionResult>