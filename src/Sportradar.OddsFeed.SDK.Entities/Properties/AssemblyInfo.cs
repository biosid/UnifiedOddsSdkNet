/*
* Copyright (C) Sportradar AG. See LICENSE for full license governing this code
*/
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("Sportradar.OddsFeed.SDK.API")]
[assembly: InternalsVisibleTo("Sportradar.OddsFeed.SDK.API.Test")]
[assembly: InternalsVisibleTo("Sportradar.OddsFeed.SDK.Test.Shared")]
[assembly: InternalsVisibleTo("Sportradar.OddsFeed.SDK.Entities.Test")]
[assembly: InternalsVisibleTo("Sportradar.OddsFeed.SDK.Entities.REST.Test")]

// Setting ComVisible to false makes the types in this assembly not visible
// to COM components.  If you need to access a type in this assembly from
// COM, set the ComVisible attribute to true on that type.
[assembly: ComVisible(false)]

// The following GUID is for the ID of the typelib if this project is exposed to COM
[assembly: Guid("fe5efe22-1bf7-4bb5-8ca4-a8b1eeb735c1")]

[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]