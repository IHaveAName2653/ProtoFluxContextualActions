using FrooxEngine;
using FrooxEngine.ProtoFlux;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Elements.Core;
using FrooxEngine.Undo;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProtoFlux.Core;
using ProtoFlux.Runtimes.Execution;
using ProtoFlux.Runtimes.Execution.Nodes;
using ProtoFlux.Runtimes.Execution.Nodes.Actions;
using ProtoFlux.Runtimes.Execution.Nodes.FrooxEngine.Slots;
using ProtoFluxContextualActions.Attributes;
using ProtoFluxContextualActions.Utils;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Reflection.Metadata;

namespace ProtoFluxContextualActions;

public class BindDynOverride : DynOverride
{
  public override bool InvokeOverride(string FunctionName, Slot hierarchy, DynamicVariableSpace? variableSpace, bool excludeDisabled, FrooxEngineContext context)
  {
    // this isnt functional yet.
    // todo: make the interface
    switch (FunctionName)
    {
      case "GetBind":
        return false;
      case "AddBind":
        return false;
      case "ListBinds":
        return false;
      case "RemoveBind":
        return false;
    }
    return true;
  }
}