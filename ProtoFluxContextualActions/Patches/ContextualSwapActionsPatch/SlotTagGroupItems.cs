using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Slots;

namespace ProtoFluxContextualActions.Patches;

static partial class ContextualSwapActionsPatch
{
  static readonly FrozenSet<Type> SlotTagGroup = [
    typeof(GetTag),
    typeof(HasTag),
    typeof(SetTag),
  ];

  static readonly FrozenSet<Type> SlotNameGroup = [
    typeof(GetSlotName),
    typeof(SetSlotName),
  ];

  static readonly FrozenSet<Type> SlotPersistentGroup = [
      typeof(GetSlotPersistentSelf),
        typeof(SetSlotPersistentSelf),
    ];

  internal static IEnumerable<MenuItem> SlotTagGroupItems(ContextualContext context) =>
  [
    .. MatchNonGenericTypes(SlotTagGroup, context.NodeType, group: "Slot Tagging"),
    .. MatchNonGenericTypes(SlotNameGroup, context.NodeType, group: "Slot Tagging"),
    .. MatchNonGenericTypes(SlotPersistentGroup, context.NodeType, group: "Slot Tagging")
  ];
}