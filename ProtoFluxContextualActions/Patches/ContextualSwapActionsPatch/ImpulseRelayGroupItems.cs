using System;
using System.Collections.Generic;
using ProtoFlux.Runtimes.Execution.Nodes;
using ProtoFluxContextualActions.Utils;

namespace ProtoFluxContextualActions.Patches;

static partial class ContextualSwapActionsPatch
{
  static readonly HashSet<Type> ImpulseRelayGroup = [
    typeof(CallRelay),
    typeof(ContinuationRelay)
  ];

  internal static IEnumerable<MenuItem> ImpulseRelayGroupItems(ContextualContext context)
  {
    if (ImpulseRelayGroup.Contains(context.NodeType))
    {
      foreach (var match in ImpulseRelayGroup)
      {
        yield return new MenuItem(match, group: "Relay");
      }
    }
  }
}