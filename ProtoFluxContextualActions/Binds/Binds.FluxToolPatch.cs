using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using FrooxEngine.Undo;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtoFlux.Core;
using ProtoFlux.Runtimes.Execution;
using ProtoFluxContextualActions.Attributes;
using ProtoFluxContextualActions.Extensions;
using ProtoFluxContextualActions.Patches;
using ProtoFluxContextualActions.Utils.ProtoFlux;
using Renderite.Shared;
using ResoniteModLoader;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace ProtoFluxContextualActions;

[HarmonyPatch(typeof(ProtoFluxTool), nameof(ProtoFluxTool.Update))]
internal class FluxBindPatch
{
  internal static bool Prefix(ProtoFluxTool __instance, SyncRef<ProtoFluxElementProxy> ____currentProxy)
  {
    // Get Bind information
    var data = additionalData.GetOrCreateValue(__instance);
    // Update bind variables for this update loop
    data.UpdateBinds(__instance, ____currentProxy.Target);

    Target? targetFunction = null;
    // dynamic variable checker
    bool dynOverrides = false;

    List<DynamicValueVariable<bool>> dynOverrideComponents = __instance.Slot.GetComponents<DynamicValueVariable<bool>>();

    Dictionary<string, DynamicValueVariable<bool>> dynComponentsMap = dynOverrideComponents.ToDictionary((c) => c.VariableName.Value);

    if (dynComponentsMap.TryGetValue("PFCA/Override", out var overrideValue) && overrideValue.Value.Value)
    {
      dynOverrides = true;

      bool GetFromMap(string varName)
      {
        bool found = dynComponentsMap.TryGetValue(varName, out var thisVariable);
        if (!found) return false;
        if (thisVariable == null) return false; // how
        bool state = thisVariable.Value.Value;
        if (state) thisVariable.Value.ForceSet(false);
        return state;
      }

      bool doSelect = GetFromMap("PFCA/Select");
      bool doSwap = GetFromMap("PFCA/Swap");
      bool doReference = GetFromMap("PFCA/Reference");

      List<bool> actions = [doSelect, doSwap, doReference];
      if (actions.Sum(v => v ? 1 : 0) == 1)
      {
        targetFunction = doSelect ? Target.Select : doSwap ? Target.Swap : doReference ? Target.Reference : Target.None;
      }
    }


    if (!dynOverrides) targetFunction = Binds.GetBind(data);

    if (targetFunction == null) return true;
    if (targetFunction == Target.None) return true;
    if (data.GrabbedReference == null && targetFunction == Target.Reference) return true; // Dont try reference if nothing held

    if (__instance.LocalUser.IsContextMenuOpen()) __instance.LocalUser.CloseContextMenu(__instance);

    ProtoFluxElementProxy? proxy = ____currentProxy.Target;

    // Call the functions
    return targetFunction switch
    {
      // Select with dragged wire
      Target.Select => ContextualSelectionActionsPatch.GetSelectionActions(__instance, proxy),
      // Swap highlighted node
      Target.Swap => ContextualSwapActionsPatch.GetSwapActions(__instance, proxy),
      // nodes from held reference type
      Target.Reference => ContextualReferenceActionsPatch.GetReferenceActions(__instance),
      // No function
      _ => true,
    };
  }

  private static readonly ConditionalWeakTable<ProtoFluxTool, ProtoFluxToolData> additionalData = [];

}
