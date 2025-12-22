using Elements.Core;
using HarmonyLib;
using ProtoFluxContextualActions.Attributes;
using ResoniteModLoader;
using System;
using System.Linq;
using System.Reflection;

namespace ProtoFluxContextualActions;

using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FrooxEngine;
using FrooxEngine.UIX;
using global::ProtoFluxContextualActions.Utils;
using Renderite.Shared;

#if DEBUG
using ResoniteHotReloadLib;
#endif

public class ProtoFluxContextualActions : ResoniteMod
{
  private static Assembly ModAssembly => typeof(ProtoFluxContextualActions).Assembly;

  public override string Name => ModAssembly.GetCustomAttribute<AssemblyTitleAttribute>()!.Title;
  public override string Author => ModAssembly.GetCustomAttribute<AssemblyCompanyAttribute>()!.Company;
  public override string Version => ModAssembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
  public override string Link => ModAssembly.GetCustomAttributes<AssemblyMetadataAttribute>().First(meta => meta.Key == "RepositoryUrl").Value!;

  internal static string HarmonyId => $"dev.bree.{ModAssembly.GetName()}";

  private static readonly Harmony harmony = new(HarmonyId);

  internal static ModConfiguration? Config;

  #region All Config Values

  #region Page Config

  public static readonly ModConfigKey<int> MaxPerPage = new("Max Per Page", "How many items can show up on one page", 8);

  public static readonly ModConfigKey<bool> UseTypeColor = new("Use Type Color", "If the menu items should use the type color instead of the node type color", true);
  #endregion

  #region Selection Config

  public static readonly ModConfigKey<bool> UseNullProxies = new("Use Null Proxies", "If the selection action can be triggered with no proxy held", false);

  #endregion

  #region Bind Config

  #region Trigger Settings

  public static readonly ModConfigKey<float>
    DoubleTapSpeed = new("Double Tap Speed", "How fast a doubletap is", 0.5f),
    HoldTime = new("Hold Time", "How long to trigger a 'hold'", 0.5f);

  #endregion

  #region Custom Bind Settings

  public static readonly ModConfigKey<bool>
    AlternateDefaults = new("Alternate Defaults", "If the activation binds are the alternate set.", false),

    OnlyUseCustomBinds = new("Only Use Custom Binds", "If only custom binds will be used. if true, actions will only activate if a custom bind is set.", false),

    DesktopUsesVRBinds = new("Desktop Uses VR Binds", "If desktop should use the same binds as VR", false);

  #endregion

  #region Desktop Bindings

  public static readonly ModConfigKey<Key>
    SecondaryKey = new("Secondary Key", "What key to use for 'opposite' secondary", Key.BackQuote),
    MenuKey = new("Menu Key", "What key to use for 'opposite' menu", Key.LeftShift),
    GrabKey = new("Grab Key", "What key to use for 'opposite' grab", Key.LeftAlt),
    PrimaryKey = new("Primary Key", "What key to use for 'opposite' primary", Key.Tab);
  #endregion

  #endregion

  #region Dyn Hook Config

  public static readonly ModConfigKey<bool>
    EnableDynHooking = new("Enable Dyn Hooking", "If dynamic impulse hooks should be enabled", true),
    ArgsUseIndexFirst = new("Arg Index First", "If Dyn Hook arguments use Index before Names", false);

  #endregion

  #region Actions Config

  public static readonly ModConfigKey<bool>
    EnableSelectionActions = new("Enable Selection Actions", "If selection actions should be enabled", true),
    EnableSwapActions = new("Enable Swap Actions", "If swap actions should be enabled", true),
    EnableReferenceActions = new("Enable Reference Actions", "If reference actions should be enabled", true),

    EnableDynVarMenu = new("Enable DynVar Menu", "If DynVars show up in the ProtoFlux context menu while holding a component", true),
    EnableSpatialMenu = new("Enable Spatial Variable Menu", "If Spatial Vars show up in the ProtoFlux context menu while holding a component", true);

  #endregion

  public static readonly Dictionary<string, Dictionary<string, List<ModConfigKey>>> SortedConfigKeys = new()
  {
    {
      "Page Config", new()
      {
        { "Base", [ MaxPerPage, UseTypeColor ] }
      }
    },
    {
      "Selection Config", new()
      {
        { "Base", [ UseNullProxies ] }
      }
    },
    {
      "Bind Config", new()
      {
        { "Trigger Settings", [ DoubleTapSpeed, HoldTime ] },
        { "Bind Settings",    [ OnlyUseCustomBinds, AlternateDefaults, DesktopUsesVRBinds ] },
        { "Desktop Binds",    [ SecondaryKey, MenuKey, GrabKey, PrimaryKey ] }
      }
    },
    {
      "Dyn Hook Config", new()
      {
        { "Base", [ EnableDynHooking, ArgsUseIndexFirst ] }
      }
    },
    {
      "Action Toggles", new()
      {
        { "Actions", [ EnableSelectionActions, EnableSwapActions, EnableReferenceActions ] },
        { "Menu", [ EnableDynVarMenu, EnableSpatialMenu ] }
      }
    }
  };

  #endregion

  public static readonly List<ModConfigKey> currentConfigKeys = [];

  private static readonly List<string> patchCategoryKeys = [];

  static ProtoFluxContextualActions()
  {
    // haha triple foreach
    foreach (var category in SortedConfigKeys.Values)
    {
      foreach (var configKeys in category.Values)
      {
        foreach (var configKey in configKeys)
        {
          currentConfigKeys.Add(configKey);
        }
      }
    }

    var types = AccessTools.GetTypesFromAssembly(ModAssembly);

    foreach (var type in types)
    {
      var patchCategory = type.GetCustomAttribute<HarmonyPatchCategory>();
      var tweakCategory = type.GetCustomAttribute<TweakCategoryAttribute>();
      if (patchCategory != null && tweakCategory != null)
      {
        string key = patchCategory.info.category;
        DebugFunc(() => $"Registering patch category {key}...");
        patchCategoryKeys.Add(key);
      }
    }
  }

  public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
  {
    builder.Version("2.0.0");
    builder.AutoSave(true);
    foreach (var key in currentConfigKeys)
    {
      builder.Key(key.ConfigKey);
    }
  }

  static readonly string configRoot = Path.Combine(Directory.GetCurrentDirectory(), "rml_config");

  public override void OnEngineInit()
  {
#if DEBUG
    HotReloader.RegisterForHotReload(this);
#endif

    Config = GetConfiguration()!;

    ConfigManager.SetModConfig(Config);

    Config?.OnThisConfigurationChanged += OnConfigChanged;

    Config?.Save(true);

    PatchCategories();
    harmony.PatchAllUncategorized(ModAssembly);

    if (!Path.Exists(configRoot))
    {
      Directory.CreateDirectory(configRoot);
    }

    FluxRecipeConfig.OnInit();
    BindFile.OnInit();
  }

  public static void OnConfigChanged(ConfigurationChangedEvent change)
  {
    ConfigManager.OnConfigChanged(change);
  }


#if DEBUG
  static void BeforeHotReload()
  {
    harmony.UnpatchAll(HarmonyId);
    PsuedoGenericTypesHelper.WorldPsuedoGenericTypes.Clear();
  }

  static void OnHotReload(ResoniteMod modInstance)
  {
    PatchCategories();
    harmony.PatchAllUncategorized(ModAssembly);

    FluxRecipeConfig.OnInit();
    BindFile.OnInit();
  }
#endif

  private static void UnpatchCategories()
  {
    foreach (var category in patchCategoryKeys)
    {
      harmony.UnpatchCategory(ModAssembly, category);
    }
  }

  private static void PatchCategories()
  {
    foreach (var category in patchCategoryKeys)
    {
      harmony.PatchCategory(ModAssembly, category);
    }
  }

  public static void ModSettings_BuildModUi(UIBuilder ui)
  {
    new ConfigUIBuilder().BuildConfigUI(ui);
  }
}