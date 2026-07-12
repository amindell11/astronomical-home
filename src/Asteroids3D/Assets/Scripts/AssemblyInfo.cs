using System.Runtime.CompilerServices;

// Lets test and editor-tool assemblies use internal helpers without widening the public API.
[assembly: InternalsVisibleTo("Game.Core.Editor")]
[assembly: InternalsVisibleTo("Tests.EditMode")]
[assembly: InternalsVisibleTo("Tests.PlayMode")]
