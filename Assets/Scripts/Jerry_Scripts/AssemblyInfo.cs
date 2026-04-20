using System.Runtime.CompilerServices;

// Allow EditMode test assembly to call internal members (test-only helper methods
// like DamageResolver.InjectRarityTable are marked internal to keep them out of
// production code surface while still being test-reachable).
[assembly: InternalsVisibleTo("EditModeTests")]
