namespace CryptoQl.Api

module Json =
    open System.Collections.Generic
    open Newtonsoft.Json
    open Newtonsoft.Json.Converters

    type DictionaryConverter () =
        inherit CustomCreationConverter<IDictionary<string, obj>> ()

        override __.Create _ =
            new Dictionary<string, obj> () :> _
        override __.CanConvert (objectType) =
            objectType = typeof<obj> || base.CanConvert (objectType)
        override __.ReadJson (reader, objectType, existingValue, serializer) =
            if reader.TokenType = JsonToken.StartObject
                || reader.TokenType = JsonToken.Null then
                base.ReadJson (reader, objectType, existingValue, serializer)
            else
                serializer.Deserialize (reader)

    let serializerSettings =
        JsonSerializerSettings (
            ContractResolver = GraphQlExtensions.GraphQlCamelCasePropertyNamesContractResolver (),
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            NullValueHandling = NullValueHandling.Ignore,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            Formatting = Formatting.Indented,
            Converters = [|Fable.JsonConverter (); DictionaryConverter (); GraphQlExtensions.GraphQlExecutionErrorJsonConverter ()|]
        )

    let serialize x = JsonConvert.SerializeObject (x, serializerSettings)
    let deserialize<'a> x = JsonConvert.DeserializeObject<'a> (x, serializerSettings)