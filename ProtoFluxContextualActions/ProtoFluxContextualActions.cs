using Elements.Core;
using HarmonyLib;
using ProtoFluxContextualActions.Attributes;
using ResoniteModLoader;
using System;
using System.Linq;
using System.Reflection;

namespace ProtoFluxContextualActions;

using System.Collections.Generic;
using FrooxEngine;
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

  private static readonly Dictionary<string, ModConfigurationKey<bool>> patchCategoryKeys = [];

  static ProtoFluxContextualActions()
  {
    DebugFunc(() => $"Static Initializing {nameof(ProtoFluxContextualActions)}...");

    var types = AccessTools.GetTypesFromAssembly(ModAssembly);

    foreach (var type in types)
    {
      var patchCategory = type.GetCustomAttribute<HarmonyPatchCategory>();
      var tweakCategory = type.GetCustomAttribute<TweakCategoryAttribute>();
      if (patchCategory != null && tweakCategory != null)
      {
        ModConfigurationKey<bool> key = new(
          name: patchCategory.info.category,
          description: tweakCategory.Description,
          computeDefault: () => tweakCategory.DefaultValue
        );

        DebugFunc(() => $"Registering patch category {key.Name}...");
        patchCategoryKeys[key.Name] = key;
      }
    }
  }

  public override void DefineConfiguration(ModConfigurationDefinitionBuilder builder)
  {
    foreach (var key in patchCategoryKeys.Values)
    {
      DebugFunc(() => $"Adding configuration key for {key.Name}...");
      builder.Key(key);
    }
  }


  public override void OnEngineInit()
  {
#if DEBUG
    HotReloader.RegisterForHotReload(this);
#endif

    Config = GetConfiguration()!;
    Config.OnThisConfigurationChanged += OnConfigChanged;

    PatchCategories();
    harmony.PatchAllUncategorized(ModAssembly);

    FluxRecipeConfig.OnInit();
    BindFile.OnInit();
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
    foreach (var category in patchCategoryKeys.Keys)
    {
      harmony.UnpatchCategory(ModAssembly, category);
    }
  }

  private static void PatchCategories()
  {
    foreach (var (category, key) in patchCategoryKeys)
    {
      if (Config?.GetValue(key) ?? true) // enable if fail?
      {
        harmony.PatchCategory(ModAssembly, category);
      }
    }
  }

  private static void OnConfigChanged(ConfigurationChangedEvent change)
  {
    var category = change.Key.Name;
    if (change.Key is ModConfigurationKey<bool> key && patchCategoryKeys.ContainsKey(category))
    {
      if (change.Config.GetValue(key))
      {
        DebugFunc(() => $"Patching {category}...");
        harmony.PatchCategory(category);
      }
      else
      {
        DebugFunc(() => $"Unpatching {category}...");
        harmony.UnpatchCategory(category);
      }
    }
  }


  [AutoRegisterConfigKey]
  public static readonly ModConfigurationKey<int> MaxPerPage = new ModConfigurationKey<int>("Max Per Page", "How many items can show up on one page", () => 8);

  [AutoRegisterConfigKey]
  public static readonly ModConfigurationKey<bool> UseTypeColor = new ModConfigurationKey<bool>("Use Type Color", "If the menu items should use the type color instead of the node type color", () => true);

  [AutoRegisterConfigKey]
  public static readonly ModConfigurationKey<float> DoubleTapSpeed = new ModConfigurationKey<float>("Double Tap Speed", "How fast a doubletap is", () => 0.5f);

  [AutoRegisterConfigKey]
  public static readonly ModConfigurationKey<float> HoldTime = new ModConfigurationKey<float>("Hold Time", "How long to trigger a 'hold'", () => 0.5f);

  [AutoRegisterConfigKey]
  public static readonly ModConfigurationKey<bool> UseNullProxies = new ModConfigurationKey<bool>("Use Null Proxies", "If the selection action can be triggered with no proxy held", () => false);


  [AutoRegisterConfigKey]
  public static readonly ModConfigurationKey<bool> OnlyUseCustomBinds = new ModConfigurationKey<bool>("Only Use Custom Binds", "If only custom binds will be used. if true, actions will only activate if a custom bind is set.", () => false);


  [AutoRegisterConfigKey]
  public static readonly ModConfigurationKey<bool> AlternateDefaults = new ModConfigurationKey<bool>("Alternate Defaults", "If the activation binds are the alternate set.", () => false);

  [AutoRegisterConfigKey]
  public static readonly ModConfigurationKey<bool> DesktopUsesVRBinds = new ModConfigurationKey<bool>("Desktop Uses VR Binds", "If desktop should use the same binds as VR", () => false);

  [AutoRegisterConfigKey]
  public static readonly ModConfigurationKey<Key> SecondaryKey = new ModConfigurationKey<Key>("Secondary Key", "What key to use for 'opposite' secondary", () => Key.BackQuote);
  [AutoRegisterConfigKey]
  public static readonly ModConfigurationKey<Key> MenuKey = new ModConfigurationKey<Key>("Menu Key", "What key to use for 'opposite' menu", () => Key.LeftShift);
  [AutoRegisterConfigKey]
  public static readonly ModConfigurationKey<Key> GrabKey = new ModConfigurationKey<Key>("Grab Key", "What key to use for 'opposite' grab", () => Key.LeftAlt);
  [AutoRegisterConfigKey]
  public static readonly ModConfigurationKey<Key> PrimaryKey = new ModConfigurationKey<Key>("Primary Key", "What key to use for 'opposite' primary", () => Key.Tab);

  [AutoRegisterConfigKey]
  public static readonly ModConfigurationKey<bool> ArgsUseIndexFirst = new ModConfigurationKey<bool>("Arg Index First", "If DynImp arguments use Index before Names", () => false);

  public static T GetConfig<T>(ModConfigurationKey<T> key, T Default)
  {
    ModConfiguration? config = Config;
    if (config != null) return config.GetValue(key) ?? Default;
    return Default;
  }
  public static int GetMaxPerPage()
  {
    return GetConfig(MaxPerPage, 8);
  }
  public static bool GetUseTypeColor()
  {
    return GetConfig(UseTypeColor, true);
  }
  public static float GetDoubleTapSpeed()
  {
    return GetConfig(DoubleTapSpeed, 0.5f);
  }
  public static float GetHoldTime()
  {
    return GetConfig(HoldTime, 0.5f);
  }
  public static bool GetUseNullProxies()
  {
    return GetConfig(UseNullProxies, true);
  }
  public static bool GetOnlyUseCustomBinds()
  {
    return GetConfig(OnlyUseCustomBinds, true);
  }
  public static bool UseAlternateDefaults()
  {
    return GetConfig(AlternateDefaults, false);
  }
  public static bool GetDesktopShouldUseVR()
  {
    return GetConfig(DesktopUsesVRBinds, false);
  }
  public static Key GetSecondaryKey()
  {

    return GetConfig(SecondaryKey, Key.BackQuote);
  }
  public static Key GetMenuKey()
  {
    return GetConfig(MenuKey, Key.LeftShift);
  }
  public static Key GetGrabKey()
  {
    return GetConfig(GrabKey, Key.LeftAlt);
  }
  public static Key GetPrimaryKey()
  {
    return GetConfig(PrimaryKey, Key.Tab);
  }
  public static bool ReadIndexFirst()
  {
    return GetConfig(ArgsUseIndexFirst, false);
  }
  
}