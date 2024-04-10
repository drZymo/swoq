using System.Collections.Immutable;

namespace Swoc2024Server;

internal record Player(string Name, Guid Id, Position StartPosition, IImmutableDictionary<string, Snake> Snakes, int Score);
