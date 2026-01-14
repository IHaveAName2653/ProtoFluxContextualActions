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
  public void WriteBindToSlot(Bind bind, Slot target)
  {
    Slot procSlot = target.FindChild(bind.bindID) ?? target.AddSlot(bind.bindID, false);

    if (procSlot.GetComponent<DynamicVariableSpace>() == null) procSlot.AttachComponent<DynamicVariableSpace>();
    var targetBind = procSlot.GetComponent<DynamicValueVariable<string>>() ?? procSlot.AttachComponent<DynamicValueVariable<string>>();
    targetBind.VariableName.Value = "Target Bind";
    targetBind.Value.Value = bind.Action.ToString();
    var desktopBind = procSlot.GetComponent<DynamicValueVariable<bool>>() ?? procSlot.AttachComponent<DynamicValueVariable<bool>>();
    desktopBind.VariableName.Value = "Is Desktop Bind";
    desktopBind.Value.Value = bind.IsDesktopBind;

    Slot inputSlot = procSlot.FindChild("Inputs") ?? procSlot.AddSlot("Inputs");

    for (int i = 0; i < bind.Inputs.Count; i++)
    {
      Control input = bind.Inputs[i];
      Slot thisInput = inputSlot.FindChild($"Input_{i}") ?? inputSlot.AddSlot($"Input_{i}");

      if (thisInput.GetComponent<DynamicVariableSpace>() == null) thisInput.AttachComponent<DynamicVariableSpace>();
      var controlVar = thisInput.GetComponent<DynamicValueVariable<string>>(comp => comp.VariableName.Value == "Control") ?? thisInput.AttachComponent<DynamicValueVariable<string>>();
      controlVar.VariableName.Value = "Control";
      controlVar.Value.Value = input.Bind.ToString();
      var primaryControlVar = thisInput.GetComponent<DynamicValueVariable<bool>>(comp => comp.VariableName.Value == "Is Primary Controller") ?? thisInput.AttachComponent<DynamicValueVariable<bool>>();
      primaryControlVar.VariableName.Value = "Is Primary Controller";
      primaryControlVar.Value.Value = input.IsPrimary;

      var actionVar = thisInput.GetComponent<DynamicValueVariable<string>>(comp => comp.VariableName.Value == "Action") ?? thisInput.AttachComponent<DynamicValueVariable<string>>();
      actionVar.VariableName.Value = "Action";
      actionVar.Value.Value = input.FireCondition.State.ToString();
      var invertedVar = thisInput.GetComponent<DynamicValueVariable<bool>>(comp => comp.VariableName.Value == "Is Inverted") ?? thisInput.AttachComponent<DynamicValueVariable<bool>>();
      invertedVar.VariableName.Value = "Is Inverted";
      invertedVar.Value.Value = input.FireCondition.Invert;
    }
  }

  public Bind? MakeBindFromSlot(Slot target)
  {
    string bindID = target.Name_Field.Value;
    string targetFunc = target.GetComponent<DynamicValueVariable<string>>().Value.Value;
    bool desktopBind = target.GetComponent<DynamicValueVariable<bool>>().Value.Value;


    if (!Enum.TryParse(targetFunc, true, out Target bindTarget)) return null;

    var bind = new Bind
    {
      bindID = bindID,
      IsDesktopBind = desktopBind,
      Action = bindTarget,
      Inputs = []
    };

    Slot inputs = target.FindChild("Inputs");

    foreach (Slot input in inputs.Children)
    {
      List<DynamicValueVariable<string>> stringVars = input.GetComponents<DynamicValueVariable<string>>();
      List<DynamicValueVariable<bool>> boolVars = input.GetComponents<DynamicValueVariable<bool>>();

      Dictionary<string, DynamicValueVariable<string>> stringMap = stringVars.ToDictionary((c) => c.VariableName.Value);
      Dictionary<string, DynamicValueVariable<bool>> boolMap = boolVars.ToDictionary((c) => c.VariableName.Value);

      if (!stringMap.TryGetValue("Control", out var controlDynvar)) return null;
      if (!boolMap.TryGetValue("Is Primary Controller", out var primaryDynvar)) return null;
      if (!stringMap.TryGetValue("Action", out var actionDynvar)) return null;
      if (!boolMap.TryGetValue("Is Inverted", out var invertedDynvar)) return null;

      string controlVar = controlDynvar.Value.Value;
      bool isPrimaryVar = primaryDynvar.Value.Value;
      string actionVar = actionDynvar.Value.Value;
      bool isInvertedVar = invertedDynvar.Value.Value;


      if (!Enum.TryParse(actionVar, true, out ConditionState conditionState)) return null;

      Condition bindCondition = new()
      {
        State = conditionState,
        Invert = isInvertedVar
      };

      if (!Enum.TryParse(controlVar, true, out ControlBind controlBind)) return null;

      Control thisControl = new()
      {
        IsPrimary = isPrimaryVar,
        Bind = controlBind,
        FireCondition = bindCondition
      };

      bind.Inputs.Add(thisControl);
    }

    return bind;
  }

  public override bool InvokeOverride(string FunctionName, Slot hierarchy, DynamicVariableSpace? variableSpace, bool excludeDisabled, FrooxEngineContext context)
  {
    if (variableSpace == null) return true;

    // this isnt functional yet.
    // todo: make the interface
    switch (FunctionName)
    {
      case "GetBind":
        {
          DynSpaceHelper.TryGetArgOrName(variableSpace, 0, "BindID", out string bindID);
          DynSpaceHelper.TryGetArgOrName(variableSpace, 1, "OutputSlot", out Slot outputSlot);
          if (string.IsNullOrEmpty(bindID)) return false;
          if (outputSlot == null) return false;

          var binds = Binds.GetBindIDs();

          if (!binds.TryGetValue(bindID, out Bind targetBind)) return false;

          WriteBindToSlot(targetBind, outputSlot);

          return false;
        }
      case "GetTemplate":
        {
          DynSpaceHelper.TryGetArgOrName(variableSpace, 1, "OutputSlot", out Slot outputSlot);
          if (outputSlot == null) return false;

          WriteBindToSlot(BindFile.TemplateBind, outputSlot);

          return false;
        }
      case "GetAllBinds":
        {
          DynSpaceHelper.TryGetArgOrName(variableSpace, 0, "OutputSlot", out Slot outputSlot);
          if (outputSlot == null) return false;

          var binds = Binds.GetBindIDs();

          foreach (var bind in binds)
          {
            WriteBindToSlot(bind.Value, outputSlot);
          }
          return false;
        }
      case "AddBind":
        {
          DynSpaceHelper.TryGetArgOrName(variableSpace, 0, "InputSlot", out Slot inputSlot);

          var bind = MakeBindFromSlot(inputSlot);
          if (bind == null) return false;
          Bind newBind = bind.Value;

          var matchedIndex = Binds.FluxBinds.FindIndex(thisBind => thisBind.bindID == newBind.bindID);
          if (matchedIndex != -1) Binds.FluxBinds.RemoveAt(matchedIndex);
          Binds.FluxBinds.Add(bind.Value);

          BindFile.WriteIntoConfig();
          
          DynSpaceHelper.ReturnFromFunc(variableSpace, 0, "BindID", newBind.bindID);
          return false;
        }
      case "ListBinds":
        {
          var binds = Binds.GetBindIDs();

          List<string> mapped = binds.Select(kv => kv.Key).ToList();

          string output = string.Join(",", mapped);

          DynSpaceHelper.ReturnFromFunc(variableSpace, 0, "Binds", output);
          return false;
        }
      case "RemoveBind":
        {
          DynSpaceHelper.TryGetArgOrName(variableSpace, 0, "BindID", out string bindID);
          if (string.IsNullOrEmpty(bindID)) return false;

          var binds = Binds.GetBindIDs();

          if (!binds.TryGetValue(bindID, out Bind target)) return false;

          Binds.FluxBinds.Remove(target);

          BindFile.WriteIntoConfig();
          return false;
        }
    }
    return true;
  }
}