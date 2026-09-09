using System.Runtime.CompilerServices;

// Lets test and editor-tool assemblies use internal helpers without widening the public API.
[assembly: InternalsVisibleTo("Core.Editor")]
[assembly: InternalsVisibleTo("RL")]
[assembly: InternalsVisibleTo("RL.Runtime")]
[assembly: InternalsVisibleTo("Tests.EditMode")]
[assembly: InternalsVisibleTo("Tests.PlayMode")]
