// Enforce CLS compliance across the public API so the package is
// cleanly consumable from any .NET language (C#, F#, VB). With
// TreatWarningsAsErrors, any non-CLS public signature fails the build.
[assembly: System.CLSCompliant(true)]
