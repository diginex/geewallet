﻿namespace GWallet.Backend

open System
open System.Reflection

open Newtonsoft.Json

type DeserializationException (message:string, innerException: Exception) =
   inherit Exception (message, innerException)
type VersionMismatchDuringDeserializationException (message:string, innerException: Exception) =
   inherit DeserializationException (message, innerException)

module VersionHelper =
    let CurrentVersion ()=
        typedefof<DeserializationException>.GetTypeInfo().Assembly.GetName().Version.ToString()

type SerializableValue<'T>(value: 'T) =
    member val Version: string =
        VersionHelper.CurrentVersion() with get

    member val TypeName: string =
        typeof<'T>.FullName with get

    member val Value: 'T = value with get

type DeserializableValueInfo(version: string, typeName: string) =

    member this.Version
        with get() = version 

    member this.TypeName
        with get() = typeName 

type DeserializableValue<'T>(version, typeName, value: 'T) =
    inherit DeserializableValueInfo(version, typeName)

    member this.Value
        with get() = value


module Marshalling =

    let private currentVersion = VersionHelper.CurrentVersion()

    let ExtractType(json: string): Type =
        let fullTypeName = (JsonConvert.DeserializeObject<DeserializableValueInfo> json).TypeName
        Type.GetType(fullTypeName)

    let Deserialize<'S,'T when 'S:> DeserializableValue<'T>>(json: string): 'T =
        if (json = null) then
            raise (ArgumentNullException("json"))
        if (String.IsNullOrWhiteSpace(json)) then
            raise (ArgumentException("empty or whitespace json", "json"))

        let deserialized: 'S =
            try
                JsonConvert.DeserializeObject<'S>(json)
            with
            | ex ->
                let versionJsonTag = "\"Version\":\""
                if (json.Contains(versionJsonTag)) then
                    let jsonSinceVersion = json.Substring(json.IndexOf(versionJsonTag) + versionJsonTag.Length)
                    let endVersionIndex = jsonSinceVersion.IndexOf("\"")
                    let version = jsonSinceVersion.Substring(0, endVersionIndex)
                    if (version <> currentVersion) then
                        let msg = sprintf "Incompatible marshalling version found (%s vs. current %s) while trying to deserialize JSON"
                                          version currentVersion
                        raise (new VersionMismatchDuringDeserializationException(msg, ex))
                raise (new DeserializationException("Exception when trying to deserialize", ex))


        // HACK: this is because comparing to null in the F# world is a clusterfuck at compile-time
        try
            deserialized.Value.ToString() |> ignore
            deserialized.Value
        with
        | :? NullReferenceException ->
            failwith ("Could not deserialize from JSON: " + json)

    let Serialize<'S>(value: 'S): string =
        JsonConvert.SerializeObject(SerializableValue<'S>(value))