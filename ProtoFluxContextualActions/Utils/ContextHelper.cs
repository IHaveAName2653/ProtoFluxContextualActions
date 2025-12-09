using Elements.Core;
using FrooxEngine;
using FrooxEngine.ProtoFlux;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtoFluxContextualActions.Utils
{
  internal static class ContextHelper
  {
    internal static void AddMenuFolder(this ContextMenu menu, string folderName, colorX color, Action setup)
    {
      var label = (LocaleString)folderName;
      var menuItem = menu.AddItem(in label, (Uri?)null, color);
      menuItem.Button.LocalPressed += (button, data) =>
      {
        setup();
      };
    }

    internal static async Task<ContextMenu> CreateContext(this ProtoFluxTool tool)
    {
      var newMenu = await tool.LocalUser.OpenContextMenu(tool, tool.Slot);
      Traverse.Create(newMenu).Field<float?>("_speedOverride").Value = 10; // Should allow for consistent flicking
      return newMenu;
    }
  }
}
