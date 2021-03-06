module TimeOff.Tests

open Expecto
open System

let Given (events: RequestEvent list) = events
let ConnectedAs (user: User) (events: RequestEvent list) = events, user
let When (command: Command) (events: RequestEvent list, user: User) = events, user, command
let Then expected message (events: RequestEvent list, user: User, command: Command) =
    let evolveGlobalState (userStates: Map<UserId, Logic.UserRequestsState>) (event: RequestEvent) =
        let userState = defaultArg (Map.tryFind event.Request.UserId userStates) Map.empty
        let newUserState = Logic.evolveUserRequests userState event
        userStates.Add (event.Request.UserId, newUserState)

    let globalState = Seq.fold evolveGlobalState Map.empty events
    let userRequestsState = defaultArg (Map.tryFind command.UserId globalState) Map.empty
    let result = Logic.decide userRequestsState user command
    Expect.equal result expected message

open System

[<Tests>]
let overlapTests = 
  testList "Overlap tests" [
    test "A request overlaps with itself" {
      let request = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2019, 10, 1); HalfDay = AM }
        End = { Date = DateTime(2019, 10, 1); HalfDay = PM }
      }

      Expect.isTrue (Logic.overlapsWith request request) "A request should overlap with istself"
    }

    test "Requests on 2 distinct days don't overlap (1)" {
      let request1 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 1); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 3); HalfDay = PM }
      }

      let request2 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 6); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 8); HalfDay = PM }
      }

      Expect.isFalse (Logic.overlapsWith request1 request2) "The requests don't overlap"
    }

    test "Requests on 2 not distinct days overlap (1)" {
      let request1 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 1); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 3); HalfDay = PM }
      }

      let request2 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 2); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 5); HalfDay = PM }
      }

      Expect.isTrue (Logic.overlapsWith request1 request2) "The requests should overlap"
    }

    test "Requests on 2 not distinct days overlap (2)" {
      let request1 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 1); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 3); HalfDay = PM }
      }

      let request2 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 2); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 5); HalfDay = PM }
      }

      Expect.isTrue (Logic.overlapsWith request2 request1) "The requests should overlap"
    }

    test "Requests on 2 not distinct days overlap (3)" {
      let request1 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 1); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 3); HalfDay = PM }
      }

      let request2 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 5); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 8); HalfDay = PM }
      }

      let request3 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 10); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 13); HalfDay = PM }
      }

      let request4 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 2); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 4); HalfDay = PM }
      }

      let seq1 = [| request1; request2; request3 |] |> Seq.ofArray

      Expect.isTrue (Logic.overlapsWithAnyRequest seq1 request4) "The requests should overlap"
    }

    test "Requests on 2 distinct days don't overlap (2)" {
      let request1 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 1); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 3); HalfDay = PM }
      }

      let request2 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 5); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 8); HalfDay = PM }
      }

      let request3 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 10); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 13); HalfDay = PM }
      }

      let request4 = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 5, 15); HalfDay = AM }
        End = { Date = DateTime(2020, 5, 18); HalfDay = PM }
      }

      let seq1 = [| request1; request2; request3 |] |> Seq.ofArray

      Expect.isFalse (Logic.overlapsWithAnyRequest seq1 request4) "The requests shouldn't overlap"
    }
  ]

[<Tests>]
let creationTests =
  testList "Creation tests" [
    test "A request is created" {
      let request = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 12, 27); HalfDay = AM }
        End = { Date = DateTime(2020, 12, 27); HalfDay = PM } }

      Given [ ]
      |> ConnectedAs (Employee "jdoe")
      |> When (RequestTimeOff request)
      |> Then (Ok [RequestCreated request]) "The request should have been created"
    }

    // Un employé peut uniquement effectuer des demandes qui commencent à une date future (au moins le lendemain de la date à laquelle la demande est effectuée)
    test "Request can only be starting today or after" {
       let request = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 12, 27); HalfDay = AM }
        End = { Date = DateTime(2020, 12, 27); HalfDay = PM } }
        
      Given [ RequestCreated request ]
      |> ConnectedAs (Employee "jdoe")
      |> When (RequestTimeOff request)
      |> Then (Ok [RequestCreated request]) "The request should have been created"
    }
  ]

[<Tests>]
let validationTests =
  testList "Validation tests" [
    test "A request is validated" {
      let request = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2019, 12, 27); HalfDay = AM }
        End = { Date = DateTime(2019, 12, 27); HalfDay = PM } }

      Given [ RequestCreated request ]
      |> ConnectedAs Manager
      |> When (ValidateRequest ("jdoe", request.RequestId))
      |> Then (Ok [RequestValidated request]) "The request should have been validated"
    }
  ]

[<Tests>]
let askCacellationTests =
  testList "Ask request cancellation tests" [
    test "Ask a cancellation for a request in the past" {
      let request = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2019, 10, 14); HalfDay = AM }
        End = { Date = DateTime(2019, 10, 14); HalfDay = PM } }

      Given [ RequestCreated request ]
      |> ConnectedAs (Employee "jdoe")
      |> When (AskRequestCacellation ("jdoe", request.RequestId))
      |> Then (Ok [RequestCancellationAsked request]) "The request should have been saved"
    }
  ]

[<Tests>]
let cacellationRefused =
  testList "Refused cancellation tests" [
    test "A cancellation is refused" {
      let request = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2019, 12, 27); HalfDay = AM }
        End = { Date = DateTime(2019, 12, 27); HalfDay = PM } }

      Given [ RequestCreated request ]
      |> ConnectedAs Manager
      |> When (RefusedRequestCancellation ("jdoe", request.RequestId))
      |> Then (Ok [RequestRefusalCancellation request]) "The request should have refused cancellation"
    }
  ]

[<Tests>]
let cancellationTests =
  testList "Cancellation tests" [
    test "A request is cancelled" {
      let request = {
        UserId = "jdoe"
        RequestId = Guid.NewGuid()
        Start = { Date = DateTime(2020, 12, 27); HalfDay = AM }
        End = { Date = DateTime(2020, 12, 28); HalfDay = PM } }

      Given [ RequestCreated request ]
      |> ConnectedAs Manager
      |> When (CancelRequest ("jdoe", request.RequestId))
      |> Then (Ok [RequestCancelled request]) "The request should have been cancelled"
    }
  ]

    // test "Cancelling another's employee's request" {
    //   let request = {
    //     UserId = "jdoe"
    //     RequestId = Guid.NewGuid()
    //     Start = { Date = DateTime(2019, 10, 14); HalfDay = AM }
    //     End = { Date = DateTime(2019, 10, 14); HalfDay = PM } }

    //   Given [ RequestCreated request ]
    //   |> ConnectedAs (Employee "toto")
    //   |> When (AskRequestCacellation ("jdoe", request.RequestId))
    //   |> Then (Error [RequestCancellationAsked request]) "The can't be deleted by another Employee"
    // }