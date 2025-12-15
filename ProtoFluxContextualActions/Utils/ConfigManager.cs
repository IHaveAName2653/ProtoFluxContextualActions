using Elements.Core;
using FrooxEngine;
using FrooxEngine.UIX;
using HarmonyLib;
using ProtoFlux.Core;
using ProtoFluxContextualActions.Attributes;
using Renderite.Shared;
using ResoniteModLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

using Log = ProtoFluxContextualActions.ProtoFluxContextualActions;

namespace ProtoFluxContextualActions.Utils;

#region Config Types
public class ModConfigKey
{
  private readonly Type defaultType;
  public ModConfigurationKey ConfigKey;

  public string ConfigName;
  public string ConfigDescription;

  public ModConfigKey(string name, string description, Type type, ModConfigurationKey key)
  {
    defaultType = type;
    ConfigKey = key;
    ConfigName = name;
    ConfigDescription = description;

    ConfigManager.allConfigKeys.Add(this);
  }

  public Type ValueType() => defaultType;

  public virtual void OnConfigExists() { }
}

public class ModConfigKey<T> : ModConfigKey
{
  public ModConfigurationKey<T> TypedConfigKey;
  public T DefaultValue;
  public T Value = default;
  public bool ValueDefined = false;

  public ModConfigKey(string name, string description, T defaultValue): base(name, description, typeof(T), new ModConfigurationKey<T>(name.ToLowerInvariant().Replace(" ", "_"), description, () => defaultValue)) {
    DefaultValue = defaultValue;
    TypedConfigKey = (ModConfigurationKey<T>)ConfigKey!;
  }

  public T GetValue()
  {
    if (ValueDefined)
    {
      return Value;
    }
    ModConfiguration? config = ConfigManager.ThisConfig;
    if (config != null)
    {
      if (config.TryGetValue<T>(TypedConfigKey, out T? output))
      {
        if (output != null)
        {
          Value = output;
          ValueDefined = true;
          return output;
        }
      }
    }
    Value = DefaultValue;
    ValueDefined = true;
    return DefaultValue;
  }
  public void SetValue(T newValue)
  {
    Value = newValue;
    ValueDefined = true;
  }
}
#endregion

public static class ConfigManager
{
  public static ModConfiguration? ThisConfig;

  public static void SetModConfig(ModConfiguration? config)
  {
    ThisConfig = config;
  }

  #region Config Key Management
  public static readonly List<ModConfigKey> allConfigKeys = [];

  public static void OnConfigChanged(ConfigurationChangedEvent change)
  {
    var category = change.Key.Name;
  }

  #endregion
}

public struct BoolData
{
  public bool Value;
}

#region Config UI Builder
public class ConfigUIBuilder()
{
  public static MethodInfo? buildGeneric;

  public static ModConfiguration? ThisConfig => ConfigManager.ThisConfig;

  public static Dictionary<string, BoolData> SectionExpandedValues = [];
  public static Dictionary<string, Dictionary<string, BoolData>> GroupExpandedValues = [];

  public void BuildConfigUI(UIBuilder ui)
  {
    if (ThisConfig == null) return;
    
    buildGeneric = typeof(ConfigUIBuilder).GetMethod(nameof(BuildGenericField));
    if (buildGeneric == null) return;
    var configKeys = ProtoFluxContextualActions.SortedConfigKeys;
    foreach (var kv in configKeys)
    {
      BuildSection(ui, kv.Key, kv.Value);
    }
  }

  public void BuildSection(UIBuilder ui, string name, Dictionary<string, List<ModConfigKey>> SubGroups)
  {
    if (!SectionExpandedValues.TryGetValue(name, out BoolData thisSectionSync))
    {
      thisSectionSync = new BoolData();
      thisSectionSync.Value = true;
      SectionExpandedValues.Add(name, thisSectionSync);
      GroupExpandedValues.Add(name, []);
    }
    BuildTitle(ui, name, 48, thisSectionSync);
    if (!thisSectionSync.Value) return;
    foreach (var kv in SubGroups)
    {
      if (!GroupExpandedValues.TryGetValue(name, out var groupSyncs))
      {
        groupSyncs = new();
        GroupExpandedValues.Add(name, groupSyncs);
      }
      if (!groupSyncs.TryGetValue(kv.Key, out BoolData thisGroupSync))
      {
        thisGroupSync = new BoolData();
        thisGroupSync.Value = true;
        groupSyncs.Add(kv.Key, thisGroupSync);
      }
      if (kv.Key != "Base") BuildTitle(ui, kv.Key, 32, thisGroupSync);
      if (!thisGroupSync.Value) continue;
      foreach (var item in kv.Value)
      {
        BuildField(ui, item);
      }
    }
  }

  public static void BuildTitle(UIBuilder ui, string title, float size, BoolData ExpandedSetting)
  {
    RadiantUI_Constants.SetupDefaultStyle(ui);
    ui.Style.MinHeight = size;
    ui.Style.TextAutoSizeMax = size;
    var baseAlignment = ui.Style.TextAlignment;
    ui.Style.TextAlignment = Alignment.TopLeft;
    ui.Spacer(size);
    Slot root = ui.Empty("Title bar");
    ui.NestInto(root);
    var labelName = new LocaleString(title, "{0}", true, true, null);
    var text = ui.Text(labelName);
    var expandButton = text.Slot.AttachComponent<Button>();
    expandButton.LocalPressed += (IButton, ButtonEventData) =>
    {
      ExpandedSetting.Value = !ExpandedSetting.Value;
    };
    ui.NestOut();
    ui.Style.TextAlignment = baseAlignment;
  }

  public void BuildField(UIBuilder ui, ModConfigKey key)
  {
    if (buildGeneric == null) return;
    var method = buildGeneric.MakeGenericMethod(key.ValueType());
    object[] args = [ui, key];

    method.Invoke(this, args);
  }

  // Duplicated from ResoniteModSettings but modified for my own uses

  public static readonly float ITEM_HEIGHT = 24f;

  public void BuildGenericField<T>(UIBuilder ui, ModConfigKey baseKey)
  {
    ModConfigKey<T> key = (ModConfigKey<T>)baseKey;

    if (ThisConfig == null) return; // even though we already do this, the linter doesnt know that. this just tells it that ThisConfig is never null here.

    bool isType = typeof(T) == typeof(Type);
    if (!(isType || DynamicValueVariable<T>.IsValidGenericType)) return; // Check if supported type

    string configSlotName = $"dev.bree.ProtoFluxContextualActions.{key.ConfigName}";
    string configName = key.ConfigName;
    string configDescription = key.ConfigDescription;

    RadiantUI_Constants.SetupEditorStyle(ui);

    ui.Style.MinHeight = ITEM_HEIGHT;

    Slot root = ui.Empty(configSlotName);

    ui.NestInto(root);

    SyncField<T> syncField;

    FieldInfo? fieldInfo = null;


    if (!isType)
    {
      var dynvar = root.AttachComponent<DynamicValueVariable<T>>();
      dynvar.VariableName.Value = $"Config/{configSlotName}";

      syncField = dynvar.Value;
      fieldInfo ??= dynvar.GetSyncMemberFieldInfo(4);
    }
    else
    {
      var dynvar = root.AttachComponent<DynamicReferenceVariable<SyncType>>();
      dynvar.VariableName.Value = $"Config/{configSlotName}";

      var typeField = root.AttachComponent<TypeField>();
      dynvar.Reference.TrySet(typeField.Type);

      syncField = typeField.Type as SyncField<T>;
      fieldInfo ??= typeField.GetSyncMemberFieldInfo(3);
    }

    var initialValue = key.GetValue();

    syncField.Value = initialValue;
    syncField.OnValueChange += (syncF) => HandleConfigFieldChange(syncF, ThisConfig, key);

    // Validate the value changes
    // LocalFilter changes the value passed to InternalSetValue
    syncField.LocalFilter = (value, field) => ValidateConfigField(value, ThisConfig, key);


    RadiantUI_Constants.SetupDefaultStyle(ui);
    ui.Style.TextAutoSizeMax = 24f;

    // Build ui

    var localized = new LocaleString(key.ConfigName, "{0}", true, true, null);
    ui.HorizontalElementWithLabel<Component>(localized, 0.7f, () =>
    {// Using HorizontalElementWithLabel because it formats nicer than SyncMemberEditorBuilder with text
     // Get first split, then Text in that split
      Slot nameSlot = ui.Root.Parent[0][0];

      ui.HorizontalLayout(4f, childAlignment: Alignment.MiddleLeft).ForceExpandHeight.Value = false;

      ui.Style.FlexibleWidth = 10f;

      SyncMemberEditorBuilder.Build(syncField, null, fieldInfo, ui, 0f); // Using null for name makes it skip generating text
      ui.Style.FlexibleWidth = -1f;

      var memberActions = ui.Root[0]?.GetComponentInChildren<InspectorMemberActions>()?.Slot;
      if (memberActions != null && typeof(T) == typeof(dummy))
      {
        memberActions.Destroy();
      }
      if (memberActions != null && nameSlot != null && typeof(T) != typeof(dummy))
      {
        // Prevent desktop user getting stuck with context menu open
        var vrSync = memberActions.AttachComponent<DynamicValueVariableDriver<bool>>();
        vrSync.Target.TrySet(memberActions.ActiveSelf_Field);
        vrSync.VariableName.Value = "vr_active";

        memberActions.Parent = nameSlot.Parent;
        memberActions.OrderOffset = -1;

        var layout = memberActions.AttachComponent<LayoutElement>();

        layout.PreferredHeight.Value = ITEM_HEIGHT;
        layout.MinHeight.Value = ITEM_HEIGHT;
        layout.MinWidth.Value = ITEM_HEIGHT;

        nameSlot.CopyComponent(layout);

        var horizontal = nameSlot.Parent.AttachComponent<HorizontalLayout>();
        horizontal.Spacing.Value = 8f;
        horizontal.HorizontalAlign.Value = LayoutHorizontalAlignment.Left;
        horizontal.ForceExpand = false;

        nameSlot.AttachComponent<Button>();
        nameSlot.AttachComponent<FieldDriveReceiver<T>>().TryAssignField(syncField);
        nameSlot.AttachComponent<ValueReceiver<T>>().TryAssignField(syncField);

        //((IValueFieldProxySource)memberActions.AttachComponent<ValueFieldProxySource<T>>()).Field = syncField;
      }

      // Update the root layout element so I don't need to do checks for every field size
      var fieldElement = ui.Root[0]?.GetComponent<LayoutElement>();
      if (fieldElement != null)
      {
        // account for user's config value
        float diff = ITEM_HEIGHT / 24f;
        fieldElement.MinHeight.Value *= diff;

        root.GetComponent<LayoutElement>().MinHeight.Value = fieldElement.MinHeight.Value;


        // go over nested elements and apply new size
        var layouts = fieldElement.Slot.GetComponentsInChildren<LayoutElement>(element => element.MinHeight.Value == 24f);
        foreach (LayoutElement layout in layouts)
        {
          layout.MinHeight.Value = ITEM_HEIGHT;
        }
      }

      ui.NestOut();

      return null;
    });
    ui.NestOut();
    ui.NestInto(ui.Empty("Description"));
    ui.Style.TextAlignment = Alignment.MiddleLeft;
    ui.Style.TextAutoSizeMax = 16;
    ui.Text((LocaleString)$"<i>{key.ConfigDescription}</i>");

    ui.Style.MinHeight = -1f;
    ui.NestOut();
  }
  private static T ValidateConfigField<T>(T value, ModConfiguration modConfiguration, ModConfigKey<T> configKey)
  {
    bool isValid = false;

    try
    {
      isValid = configKey.ConfigKey.Validate(value);
    }
    catch (Exception e)
    {
      //optionsRoot.LocalUser.IsDirectlyInteracting()
    }

    if (!isValid)
    { // Fallback if validation fails
      return configKey.GetValue(); // Set to old value if is set Else set to default for that value
    }
    return value;
  }
  private static void HandleConfigFieldChange<T>(SyncField<T> syncField, ModConfiguration modConfiguration, ModConfigKey<T> configKey)
  {
    bool isSet = modConfiguration.TryGetValue(configKey.TypedConfigKey, out T configValue);
    if (isSet && (Equals(configValue, syncField.Value) || !Equals(syncField.Value, syncField.Value)))
    {
      configKey.SetValue(configValue);
      return; // Skip if new value is unmodified or is logically inconsistent (self != self)
    }

    try
    {
      if (!configKey.TypedConfigKey.Validate(syncField.Value)) return;
    }
    catch { return; }

    modConfiguration.Set(configKey.TypedConfigKey, syncField.Value, "ModSettingsScreen Custom Edit Value");
    configKey.SetValue(syncField.Value);

    modConfiguration.Save(true);
  }
}
  #endregion