using System;
using System.Collections.Generic;
using System.Linq;
using FrooxEngine.ProtoFlux;
using HarmonyLib;
using Elements.Core;
using static ProtoFluxContextualActions.Patches.ContextualSelectionActionsPatch;
using FrooxEngine;
using System.Threading.Tasks;

namespace ProtoFluxContextualActions.NewScripts;

internal static class ContextUtils
{
  internal static async Task<ContextMenu> CreateContextMenu(ProtoFluxTool tool)
  {
    var menu = await tool.LocalUser.OpenContextMenu(tool, tool.Slot);
    Traverse.Create(menu).Field<float?>("_speedOverride").Value = 10; // faster for better swiping
    return menu;
  }

  internal static void AddMenuItem(this ContextMenu menu, string name, colorX? color, Action onClicked, Uri? icon = null)
  {
    var label = (LocaleString)name;
    var menuItem = menu.AddItem(in label, icon, color);
    menuItem.Button.LocalPressed += (button, data) =>
    {
      onClicked();
    };
  }
}