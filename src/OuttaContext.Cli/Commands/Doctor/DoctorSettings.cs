using System.ComponentModel;
using Spectre.Console.Cli;

namespace OuttaContext.Cli.Commands.Doctor;

/// <summary>Options of `outta doctor`.</summary>
public sealed class DoctorSettings : GlobalSettings
{
    [CommandOption("--no-storage")]
    [Description("Skip testing storage logins.")]
    public bool NoStorage { get; init; }
}
