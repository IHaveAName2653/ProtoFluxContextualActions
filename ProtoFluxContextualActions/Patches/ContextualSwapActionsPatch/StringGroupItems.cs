using System;
using System.Collections.Generic;
using ProtoFlux.Runtimes.Execution.Nodes;
using ProtoFlux.Runtimes.Execution.Nodes.Strings;
using ProtoFluxContextualActions.Utils;

namespace ProtoFluxContextualActions.Patches;

static partial class ContextualSwapActionsPatch
{
  static readonly HashSet<Type> StringGroup = [
    typeof(Substring),
    typeof(StringRemove),
    typeof(StringInsert)
  ];

  internal static IEnumerable<MenuItem> StringGroupItems(ContextualContext context)
  {
    if (StringGroup.Contains(context.NodeType))
    {
      foreach (var match in StringGroup)
      {
        yield return new MenuItem(match, group: "String Operations");
      }
    }
  }
}