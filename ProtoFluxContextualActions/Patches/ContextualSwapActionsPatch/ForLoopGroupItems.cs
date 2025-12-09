using System;
using System.Collections.Generic;
using ProtoFlux.Runtimes.Execution.Nodes;
using ProtoFlux.Runtimes.Execution.Nodes.Actions;

namespace ProtoFluxContextualActions.Patches;

static partial class ContextualSwapActionsPatch
{
  // todo: async
  static readonly HashSet<Type> ForLoopGroup = [
        typeof(For),
        typeof(While),
        typeof(RangeLoopInt),
        typeof(FireWhileTrue),
        typeof(LocalFireWhileTrue),
        typeof(AsyncFor),
        typeof(AsyncWhile),
        typeof(AsyncRangeLoopInt),
    ];

  internal static IEnumerable<MenuItem> ForLoopGroupItems(ContextualContext context)
  {
    if (ForLoopGroup.Contains(context.NodeType))
    {
      foreach (var match in ForLoopGroup)
      {
        yield return new MenuItem(match, connectionTransferType: ConnectionTransferType.ByMappingsLossy);
      }
    }
  }
}