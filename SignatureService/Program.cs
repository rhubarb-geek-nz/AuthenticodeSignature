// Copyright (c) 2024 Roger Brown.
// Licensed under the MIT License.

using System.Management.Automation;
using System.Management.Automation.Runspaces;
using RhubarbGeekNz.AspNetForPowerShell;

var iss = InitialSessionState.CreateDefault();

iss.AddAspNetForPowerShellCmdlets();

using (PowerShell powerShell = PowerShell.Create(iss))
{
    powerShell.AddCommand("./Program.ps1");

    powerShell.AddArgument(args);

    powerShell.Invoke();
}
