using FrooxEngine;
using FrooxEngine.ProtoFlux;
using HarmonyLib;
using System.Collections.Generic;
using ProtoFlux.Runtimes.Execution.Nodes.Actions;
using System.Linq;

namespace ProtoFluxContextualActions;



public abstract class DynOverride
{
  public abstract bool InvokeOverride(string FunctionName, Slot target, DynamicVariableSpace? variableSpace, bool excludeDisabled, FrooxEngineContext context);
}



[HarmonyPatch(typeof(DynamicImpulseTrigger), "Trigger")]
public static class DynamicImpulseHook
{
  public static Dictionary<string, DynOverride> Overrides = new Dictionary<string, DynOverride>()
    {
        {"RecipeConfig", new RecipeConfigDynOverride() },
        {"RecipeData", new RecipeDataDynOverride() },
        {"RecipeMaker", new RecipeMakerDynOverride() },


        {"Binds", new BindDynOverride() }
    };
  public static Dictionary<string, DynOverride> LegacyOverrides = new Dictionary<string, DynOverride>()
    {
        {"FluxRecipe", new LegacyRecipeStringInterface() },
    };


  public static bool Prefix(Slot hierarchy, string tag, bool excludeDisabled, FrooxEngineContext context)
  {
    // ALWAYS return true, unless this impulse should not continue to the world.

    // If there isnt a tag, skip it!!!
    if (tag == null) return true;
    if (string.IsNullOrEmpty(tag.Trim() ?? "")) return true;
    // Determine if this is a legacy or new function.

    List<string> parts = [.. tag.Split("_")];
    if (parts.Count > 1 && parts[0] != "Invoke")
    {
      // This is a legacy function.
      // What "space" is this?
      string space = parts[0];
      parts.RemoveAt(0);

      if (!LegacyOverrides.TryGetValue(space, out DynOverride? targetLegacyOverride)) return true;

      return targetLegacyOverride.InvokeOverride(string.Join("_", parts), hierarchy, null, excludeDisabled, context);
    }

    if (parts.Count > 1 && parts[0] == "Invoke")
    {
      if (!Overrides.TryGetValue(parts[1], out DynOverride? targetOverride)) return true;
      DynamicVariableSpace argumentSpace = DynSpaceHelper.TryGetSpace(hierarchy, "DynHooks", true);

      if (!DynSpaceHelper.TryRead(argumentSpace, "Function", out string func, true)) return true;

      return targetOverride.InvokeOverride(func, hierarchy, argumentSpace, excludeDisabled, context);
    }
    else if (parts[0] == "Invoke")
    {
      DynamicVariableSpace argumentSpace = DynSpaceHelper.TryGetSpace(hierarchy, "DynHooks", true);

      if (!DynSpaceHelper.TryRead(argumentSpace, "Method", out string method, true)) return true;
      if (!Overrides.TryGetValue(method, out DynOverride? targetOverride)) return true;
      if (!DynSpaceHelper.TryRead(argumentSpace, "Function", out string func, true)) return true;

      return targetOverride.InvokeOverride(func, hierarchy, argumentSpace, excludeDisabled, context);
    }

    return true;
  }
}