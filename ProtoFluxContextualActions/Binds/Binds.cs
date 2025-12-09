using Elements.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using static ProtoFluxContextualActions.BindGetters;

namespace ProtoFluxContextualActions;

public class Binds
{
  public static List<Bind> FluxBinds = [];

  internal static Target GetBind(ProtoFluxToolData data)
  {
    if (FluxBinds.Count == 0) return GetAltDefaultBind(data);
    Target? customBind = TryGetCustomBind(data);
    if (customBind == null) return GetAltDefaultBind(data);
    return customBind.Value;
  }

  internal static Target? TryGetCustomBind(ProtoFluxToolData data)
  {
    BindFile.ReadFromConfig();

    bool usingDesktop = ShouldUseDesktopBinds(data);
    List<Bind> filteredBinds = FluxBinds.FindAll((bind) => bind.IsDesktopBind == usingDesktop);

    foreach (Bind bind in filteredBinds)
    {
      if (bind.Inputs.Count == 0) continue;
      bool isValid = true;
      foreach (Control control in bind.Inputs)
      {
        SidedBinds sidedControl = control.Bind switch
        {
          ControlBind.Secondary => data.Secondary,
          ControlBind.Menu => data.Menu,
          ControlBind.Primary => data.Primary,
          ControlBind.Grip => data.Grip,
          ControlBind.Touch => data.Touch,
          _ => data.Secondary
        };
        BindData controlBind = control.IsPrimary switch
        {
          true => sidedControl.Primary,
          false => sidedControl.Opposite,
        };
        bool isTriggered = control.FireCondition.State switch
        {
          ConditionState.Pressed => controlBind.currentlyPressed,
          ConditionState.Held => controlBind.IsHeld,
          ConditionState.Press => controlBind.pressedThisUpdate,
          ConditionState.DoubleTap => controlBind.isDoublePress,

          _ => controlBind.currentlyPressed
        };
        bool output = isTriggered != control.FireCondition.Invert;
        isValid &= output;
        if (!isValid) break;
      }
      if (!isValid) continue;
      return bind.Action;
    }
    return null;
  }

  internal static Target GetDefaultOrAltBind(ProtoFluxToolData data)
  {
    if (UseAlternateDefaults())
    {
      return GetAltDefaultBind(data);
    }
    else
    {
      return GetDefaultBind(data);
    }
  }

  internal static Target GetDefaultBind(ProtoFluxToolData data)
  {
    if (data.Secondary.Primary.isDoublePress) return Target.Swap;

    if (data.Secondary.Primary.pressedThisUpdate && data.HasProxy) return Target.Select;

    if (data.Secondary.Primary.pressedThisUpdate && data.HoldingElement) return Target.Reference;

    return Target.None;
  }

  internal static Target GetAltDefaultBind(ProtoFluxToolData data)
  {
    // Desktop specific binds
    if (ShouldUseDesktopBinds(data))
    {
      if (data.GrabbedReference != null && data.Secondary.Opposite.pressedThisUpdate) return Target.Reference;
      if (data.Secondary.Opposite.pressedThisUpdate && data.Menu.Opposite.currentlyPressed) return Target.Swap;
      if (data.Secondary.Opposite.pressedThisUpdate && !data.Menu.Opposite.currentlyPressed) return Target.Select;
    }
    else
    {
      // VR specific binds (or desktop sometimes)
      if (data.Secondary.Opposite.currentlyPressed && data.Menu.Primary.pressedThisUpdate) return Target.Select;
      //if (data.Menu.Primary.IsHeld && data.Secondary.Opposite.currentlyPressed) return Target.Swap;
      //if (data.Menu.Primary.IsHeld && !data.Secondary.Opposite.currentlyPressed) return Target.Selection;
      if (data.Secondary.Opposite.IsHeld && data.Menu.Primary.pressedThisUpdate) return Target.Swap;

      if (data.GrabbedReference != null && data.Secondary.Opposite.IsHeld && data.Primary.Opposite.pressedThisUpdate) return Target.Reference;

      if (data.Secondary.Opposite.currentlyPressed && data.Primary.Opposite.pressedThisUpdate) return Target.Swap;

    }

    return Target.None;
  }

  internal static Dictionary<string, Bind> GetBindIDs()
  {
    Dictionary<string, Bind> outputDict = [];
    int inc = 0;
    for (int i = 0; i < FluxBinds.Count; i++)
    {
      Bind thisBind = FluxBinds[i];
      if (string.IsNullOrEmpty(thisBind.bindID))
      {
        thisBind.bindID = $"Bind_{thisBind.Action.ToString()}_{inc}";
        FluxBinds[i] = thisBind;
        inc++;
      }
      if (outputDict.ContainsKey(thisBind.bindID))
      {
        FluxBinds.RemoveAt(i);
        i--;
        continue;
      }
      
      outputDict.Add(thisBind.bindID, thisBind);
    }
    if (inc > 0) BindFile.WriteIntoConfig();
    return outputDict;
  }
}
