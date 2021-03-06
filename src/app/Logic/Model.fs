﻿namespace TimeOff

open System

// Then our commands
type Command =
    | RequestTimeOff of TimeOffRequest
    | AskRequestCacellation of UserId * Guid
    | ValidateRequest of UserId * Guid
    | CancelRequest of UserId * Guid
    | RefusedRequestCancellation of UserId * Guid
    with
    member this.UserId =
        match this with
        | RequestTimeOff request -> request.UserId
        | AskRequestCacellation (userId, _) -> userId
        | ValidateRequest (userId, _) -> userId
        | CancelRequest (userId, _) -> userId
        | RefusedRequestCancellation (userId, _) -> userId

// And our events
type RequestEvent =
    | RequestCreated of TimeOffRequest
    | RequestCancellationAsked of TimeOffRequest
    | RequestValidated of TimeOffRequest
    | RequestCancelled of TimeOffRequest
    | RequestRefusalCancellation of TimeOffRequest
    with
    member this.Request =
        match this with
        | RequestCreated request -> request
        | RequestCancellationAsked request -> request
        | RequestValidated request -> request
        | RequestCancelled request -> request
        | RequestRefusalCancellation request -> request

// We then define the state of the system,
// and our 2 main functions `decide` and `evolve`
module Logic =

    let today = DateTime.Today;

    type RequestState =
        | NotCreated
        | PendingValidation of TimeOffRequest
        | PendingCancellation of TimeOffRequest
        | CancellationRefused of TimeOffRequest
        | Validated of TimeOffRequest
        | Canceled of TimeOffRequest with
        member this.Request =
            match this with
            | NotCreated -> invalidOp "Not created"
            | PendingValidation request
            | PendingCancellation request
            | CancellationRefused request
            | Validated request -> request
            | Canceled request -> request
        member this.IsActive =
            match this with
            | NotCreated -> false
            | PendingValidation _
            | PendingCancellation _
            | CancellationRefused _
            | Canceled _ -> false
            | Validated _ -> true

    type UserRequestsState = Map<Guid, RequestState>

    let evolveRequest state event =
        match event with
        | RequestCreated request -> PendingValidation request
        | RequestCancellationAsked request -> PendingCancellation request
        | RequestValidated request -> Validated request
        | RequestCancelled request -> Canceled request
        | RequestRefusalCancellation request -> CancellationRefused request

    let evolveUserRequests (userRequests: UserRequestsState) (event: RequestEvent) =
        let requestState = defaultArg (Map.tryFind event.Request.RequestId userRequests) NotCreated
        let newRequestState = evolveRequest requestState event
        userRequests.Add (event.Request.RequestId, newRequestState)

    let overlapsWith request1 request2 =
        if request1.Start.Date < request2.Start.Date then
            if request1.Start.Date <= request2.Start.Date && request2.Start.Date <= request1.End.Date then
                true
            else
                false
        else
            if request2.Start.Date <= request1.Start.Date && request1.Start.Date <= request2.End.Date then
                true
            else
                false

    let overlapsWithAnyRequest (otherRequests: TimeOffRequest seq) request =
        otherRequests |> Seq.exists (fun secondRequest -> overlapsWith request secondRequest)

    let createRequest activeUserRequests request today =
        if request |> overlapsWithAnyRequest activeUserRequests then
            Error "Overlapping request"
        elif request.Start.Date <= today then
            Error "The request starts in the past"
        else
            Ok [RequestCreated request]

    let validateRequest requestState =
        match requestState with
        | PendingValidation request ->
            Ok [RequestValidated request]
        | _ ->
            Error "Request cannot be validated"

    let cancelRequest requestState =
        match requestState with
        | PendingValidation request ->
            Ok [RequestCancelled request]
        | _ ->
            Error "Request cannot be canceled"

    let decide (userRequests: UserRequestsState) (user: User) (command: Command) =
        let relatedUserId = command.UserId
        match user with
        | Employee userId when userId <> relatedUserId ->
            Error "Unauthorized"
        | _ ->
            match command with
            | RequestTimeOff request ->
                let activeUserRequests =
                    userRequests
                    |> Map.toSeq
                    |> Seq.map (fun (_, state) -> state)
                    |> Seq.where (fun state -> state.IsActive)
                    |> Seq.map (fun state -> state.Request)

                createRequest activeUserRequests request today

            | AskRequestCacellation (_, requestId) ->
                let requestState = defaultArg (userRequests.TryFind requestId) NotCreated
                Ok [ RequestCancellationAsked requestState.Request]
                
            | ValidateRequest (_, requestId) ->
                if user <> Manager then
                    Error "Unauthorized"
                else
                    let requestState = defaultArg (userRequests.TryFind requestId) NotCreated
                    validateRequest requestState
            | CancelRequest (_, requestId) ->
                let requestState = defaultArg (userRequests.TryFind requestId) NotCreated
                cancelRequest requestState
            | RefusedRequestCancellation (_, requestId) ->
                if user <> Manager then
                    Error "Unauthorized"
                else
                    let requestState = defaultArg (userRequests.TryFind requestId) NotCreated
                    Ok [ RequestRefusalCancellation requestState.Request]
