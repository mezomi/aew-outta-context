namespace OuttaContext.Core.Doctor;

/// <summary>One line of `outta doctor`: what was checked, how it went, and how to fix it.</summary>
public sealed record DoctorCheck(string Name, CheckState State, string Message, string Fix = "");
