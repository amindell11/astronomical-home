using System.Runtime.CompilerServices;

// Lets the test assemblies read internal solver state (e.g. Mpc's predicted-trajectory
// accessors) so solver behavior can be asserted directly without widening the public API.
[assembly: InternalsVisibleTo("Tests.EditMode")]
[assembly: InternalsVisibleTo("Tests.PlayMode")]
