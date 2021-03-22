using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Inedo.Extensibility;

[assembly: AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]

[assembly: AssemblyTitle("Jenkins")]
[assembly: AssemblyDescription("Contains operations to get artifacts and trigger builds in Jenkins.")]
[assembly: AssemblyProduct("any")]
[assembly: AssemblyCompany("Inedo, LLC")]
[assembly: AssemblyCopyright("Copyright © Inedo 2021")]
[assembly: AssemblyVersion("1.11.0")]
[assembly: AssemblyFileVersion("1.11.0")]
[assembly: CLSCompliant(false)]
[assembly: ComVisible(false)]
[assembly: ScriptNamespace("Jenkins")]
