﻿namespace GWallet.Backend

open System
open System.Linq

type NoneAvailableException (message:string, lastException: Exception) =
   inherit Exception (message, lastException)


type ResultInconsistencyException (totalNumberOfSuccesfulResultsObtained: int,
                                   maxNumberOfConsistentResultsObtained: int,
                                   numberOfConsistentResultsRequired: int) =
  inherit Exception ("Results obtained were not enough to be considered consistent" +
                      sprintf " (received: %d, consistent: %d, required: %d)"
                                  totalNumberOfSuccesfulResultsObtained
                                  maxNumberOfConsistentResultsObtained
                                  numberOfConsistentResultsRequired)

type internal ResultOrFailure<'R,'E when 'E :> Exception> =
    | Result of 'R
    | Failure of 'E

type FaultTolerantClient<'E when 'E :> Exception>(numberOfConsistentResponsesRequired: int) =
    do
        if typeof<'E> = typeof<Exception> then
            raise (ArgumentException("'E cannot be System.Exception, use a derived one", "'E"))
        if numberOfConsistentResponsesRequired < 1 then
            raise (ArgumentException("must be higher than zero", "numberOfConsistentResponsesRequired"))

    member self.Query<'T,'R when 'R : equality> (args: 'T) (funcs: list<'T->'R>): 'R =
        let rec queryInternal (args: 'T) (resultsSoFar: list<'R>) (lastEx: Exception) (funcs: list<'T->'R>) =
            match funcs with
            | [] ->
                match resultsSoFar with
                | [] ->
                    raise (NoneAvailableException("Not available", lastEx))
                | _ ->
                    let totalNumberOfSuccesfulResultsObtained = resultsSoFar.Count()
                    let resultsByCountOrdered =
                        resultsSoFar |> Seq.countBy id |> Seq.sortBy (fun (_,count:int) -> count)
                    let mostConsistentResult,maxNumberOfConsistentResultsObtained = resultsByCountOrdered.Last()
                    raise (ResultInconsistencyException(totalNumberOfSuccesfulResultsObtained,
                                                        maxNumberOfConsistentResultsObtained,
                                                        numberOfConsistentResponsesRequired))
            | head::tail ->
                let maybeResult:ResultOrFailure<'R,'E> =
                    try
                        Result(head(args))
                    with
                    | :? 'E as ex ->
                        if (Config.DebugLog) then
                            Console.Error.WriteLine (sprintf "Fault warning: %s: %s"
                                                         (ex.GetType().FullName)
                                                         ex.Message)
                        Failure(ex)

                match maybeResult with
                | Result(result) ->
                    let countSoFar =
                        resultsSoFar.Count(fun res -> res = result)
                    if (countSoFar + 1 = numberOfConsistentResponsesRequired) then
                        result
                    else
                        let newResults = result::resultsSoFar
                        queryInternal args newResults lastEx tail
                | Failure(ex) ->
                    queryInternal args resultsSoFar ex tail

        if not (funcs.Any()) then
            raise(ArgumentException("number of funcs must be higher than zero",
                                    "funcs"))
        if (funcs.Count() < numberOfConsistentResponsesRequired) then
            raise(ArgumentException("number of funcs must be equal or higher than numberOfConsistentResponsesRequired",
                                    "funcs"))
        queryInternal args [] null funcs