﻿module Logary.Tests.Metrics

open Fuchu
open Logary
open Logary.Metrics.Reservoir

type Assert =
  static member FloatEqual(msg, expected, actual, ?epsilon) =
    let epsilon = defaultArg epsilon 0.001
    if expected <= actual + epsilon && expected >= actual - epsilon then
      ()
    else
      Tests.failtestf "Expected %f to be %f within %f epsilon. %s"
        actual expected epsilon msg

[<Tests>]
let snapshot =
  let sample = Snapshot.create [| 5L; 1L; 2L; 3L; 4L |]
  let empty = Snapshot.create [||]

  testList "calculating snapshot values" [
    testCase "small quantiles are first value" <| fun _ ->
      Assert.FloatEqual("should be the one", 1., Snapshot.quantile sample 0.)
    testCase "big quantiles are last values" <| fun _ ->
      Assert.FloatEqual("should be the five", 5., Snapshot.quantile sample 1.)
    testCase "median" <| fun _ ->
      Assert.FloatEqual("should have median", 3., Snapshot.median sample)
    testCase "75th percentile" <| fun _ ->
      Assert.FloatEqual("should have 75th percentile", 4.5, Snapshot.percentile75th sample)
    testCase "95th percentile" <| fun _ ->
      Assert.FloatEqual("should have 95th percentile", 5., Snapshot.percentile95th sample)
    testCase "98th percentile" <| fun _ ->
      Assert.FloatEqual("should have 98th percentile", 5., Snapshot.percentile98th sample)
    testCase "99th percentile" <| fun _ ->
      Assert.FloatEqual("should have 99th percentile", 5., Snapshot.percentile99th sample)
    testCase "999th percentile" <| fun _ ->
      Assert.FloatEqual("should have 999th percentile", 5., Snapshot.percentile999th sample)
    testCase "has values" <| fun _ ->
      Assert.Equal("should have values ordered", [| 1L; 2L; 3L; 4L; 5L |], Snapshot.values sample)
    testCase "has size" <| fun _ ->
      Assert.Equal("should have five size", 5, Snapshot.size sample)
    testCase "has mimimum value" <| fun _ ->
      Assert.Equal("has a mimimum value", 1L, Snapshot.min sample)
    testCase "has maximum value" <| fun _ ->
      Assert.Equal("has a maximum value", 5L, Snapshot.max sample)
    testCase "has mean value" <| fun _ ->
      Assert.Equal("has a mean value", 3., Snapshot.mean sample)
    testCase "has stdDev" <| fun _ ->
      Assert.FloatEqual("has stdDev", 1.5811, Snapshot.stdDev sample, 0.0001)
    testCase "empty: min" <| fun _ ->
      Assert.Equal("zero", 0L, Snapshot.min empty)
    testCase "empty: max" <| fun _ ->
      Assert.Equal("zero", 0L, Snapshot.max empty)
    testCase "empty: mean" <| fun _ ->
      Assert.FloatEqual("zero", 0., Snapshot.mean empty)
    testCase "empty: std dev" <| fun _ ->
      Assert.FloatEqual("zero", 0., Snapshot.mean empty)
    ]

[<Tests>]
let reservoirs =
  testList "reservoirs" [
    testCase "uniform: update 1000 times" <| fun _ ->
      let state =
        [ 0L .. 999L ]
        |> List.fold Uniform.update (Uniform.create 100)
      Assert.Equal("should have 100L size", 100, Uniform.size state)

      let snap = Uniform.snapshot state
      Assert.Equal("snapshot has as many", 100, Snapshot.size snap)
      for v in snap.values do
        Assert.Equal(sprintf "'%d' should be in [0, 999]" v, true, 0L <= v && v <= 999L)

    testCase "sliding: small" <| fun _ ->
      let state =
        [ 1L; 2L ]
        |> List.fold SlidingWindow.update (SlidingWindow.create 3)
      let snap = SlidingWindow.snapshot state
      Assert.Equal("has two", 2I, state.count)
      Assert.Equal("size has two", 2, SlidingWindow.size state)
      Assert.Equal("snap has two", 2, snap.values.Length)
      Assert.Equal("should have correct order", [| 1L; 2L; |], Snapshot.values snap)

    testCase "sliding: only last values" <| fun _ ->
      let state =
        [ 1L..5L ]
        |> List.fold SlidingWindow.update (SlidingWindow.create 3)
      let snap = SlidingWindow.snapshot state
      Assert.Equal("should have correct order", [| 3L..5L |], Snapshot.values snap)

    testCase "exponentially weighted moving average" <| fun _ ->
      let flip f a b = f b a
      let passMinute s =
        [ 1..12 ] |> List.fold (fun s' t -> ExpWeightedMovAvg.tick s') s

      let initState =
        ExpWeightedMovAvg.oneMinuteEWMA
        |> (flip ExpWeightedMovAvg.update) 3L
        |> ExpWeightedMovAvg.tick

      let expectations =
        [ 0.6
          0.22072766
          0.08120117
          0.02987224
          0.01098938
          0.00404277
          0.00148725
          0.00054713
          0.00020128
          0.00007405
          0.00002724
          0.00001002
          0.00000369
          0.00000136
          0.00000050
          0.00000018 ]

      let actual =
        [ for i in 1..expectations.Length - 1 do yield i ]
        |> List.scan (fun s t -> passMinute s) initState
        |> List.map (ExpWeightedMovAvg.rate Seconds)

      List.zip expectations actual
      |> List.iteri (fun index (expected, actual) ->
//        System.Diagnostics.Debugger.Log(5, "", sprintf "expected %f, actual %f\n" expected actual)
        Assert.FloatEqual(sprintf "Index %d, should calculate correct EWMA" index,
                          expected,  actual, epsilon = 0.000001))
    ]