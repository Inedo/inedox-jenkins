using System.Reflection;
using System.Runtime.InteropServices;
using Inedo.Extensibility;

[assembly: AppliesTo(InedoProduct.BuildMaster | InedoProduct.Otter)]

[assembly: AssemblyTitle("Jenkins")]
[assembly: AssemblyDescription("Contains operations to get artifacts and trigger builds in Jenkins.")]
[assembly: AssemblyProduct("any")]
[assembly: AssemblyCompany("Inedo, LLC")]
[assembly: AssemblyCopyright("Copyright © Inedo 2024")]
[assembly: AssemblyVersion("3.1.0")]
[assembly: AssemblyFileVersion("3.1.0")]
[assembly: CLSCompliant(false)]
[assembly: ComVisible(false)]
[assembly: ScriptNamespace("Jenkins")]
