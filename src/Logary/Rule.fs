namespace Logary

open System
open System.Text.RegularExpressions

open Logary

/// This is the accept filter that is before the log line is passed to the logger
/// instance.
type LineFilter = logline -> bool

/// This is the accept filter that is before the measure is passed to the logger
/// instance.
type MeasureFilter = ``measure`` -> bool

/// A rule specifies what log lines and metrics a target should accept.
[<CustomEquality; NoComparison>]
type rule =
  { /// This is the regular expression that the 'path' must match to be loggable
    hiera  : Regex
    /// This is the name of the target that this rule applies to
    target : string
    /// This is the accept filter that is before the log line is passed to the logger
    /// instance.
    lineFilter : LineFilter
    /// This is the accept filter that is before the measure is passed to the logger
    /// instance.
    measureFilter : MeasureFilter
    /// This is the level at which the target will accept log lines. It's inclusive, so
    /// anything below won't be accepted.
    level  : LogLevel }
    /// Create a new rule with the given hiera, target, accept function and min level
    /// acceptable.
  static member Create(hiera, target, ?lineFilter, ?measureFilter, ?level) =
    { hiera         = hiera
      target        = target
      lineFilter    = defaultArg lineFilter (fun _ -> true)
      measureFilter = defaultArg measureFilter (fun _ -> true)
      level         = defaultArg level Verbose }

  override x.GetHashCode () = hash (x.hiera.ToString(), x.target, x.level)

  override x.Equals other =
    match other with
    | null -> false
    | :? rule as o -> (x :> IEquatable<rule>).Equals(o)
    | _ -> false

  interface System.IEquatable<rule> with
    member x.Equals r = r.hiera.ToString() = x.hiera.ToString() && r.target = x.target && r.level = x.level

  override x.ToString() =
    sprintf "Rule { hiera=%O; target=%s; level=%O }" x.hiera x.target x.level


/// Module for dealing with rules. Rules take care of filtering too verbose
/// log lines and measures before they are sent to the targets.
module Rule =

  // Acceptors:

  let allowFilter _ = true

  /// Find all rules matching the name, from the list of rules passed.
  let matching (name : string) (rules : rule list) =
    rules |> List.filter (fun r -> r.hiera.IsMatch name)

  /////////////////////
  // Creating rules: //
  /////////////////////

  /// An empty rule; don't forget to give it a proper name or the configuration
  /// won't work, e.g. using the `forAny` method.
  let empty =
    { hiera         = Regex(".*")
      target        = ""
      lineFilter    = fun _ -> true
      measureFilter = fun _ -> true
      level         = Verbose }

  /// Create a rule with the given regex/hiera, accept function and level given a target configuration.
  let forTarget hiera lineFilter measureFilter level (targetName : string) =
    { hiera         = hiera
      target        = targetName
      lineFilter    = lineFilter
      measureFilter = measureFilter
      level         = level }

  /// Create a rule that accepts any input for a specified target (that's the
  /// name param).
  let forAny (name : string) =
    { empty with target = name }