
// This file is used by Code Analysis to maintain SuppressMessage 
// attributes that are applied to this project.
// Project-level suppressions either have no target or are given 
// a specific target and scoped to a namespace, type, member, etc.

[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "RCS1090:Call 'ConfigureAwait(false)'.")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "U2U1105:Do not use string interpolation to concatenate strings")]
[assembly: System.Diagnostics.CodeAnalysis.SuppressMessage("Performance", "U2U1201:Local collections should be initialized with capacity", Justification = "There is no override that takes a comparer and a intial size", Scope = "member", Target = "~M:tusdotnet.test.Tests.WriteFileStreamsTests.Handles_Abrupt_Disconnects_Gracefully(System.String)~System.Threading.Tasks.Task")]

