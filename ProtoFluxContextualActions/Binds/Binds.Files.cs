using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProtoFluxContextualActions;

internal class BindFile
{
  static readonly string configPath = Path.Combine(Directory.GetCurrentDirectory(), "rml_config", "__PFCA-Binds.json");
  public static void OnInit()
  {
    Binds.FluxBinds = [];
    ReadFromConfig();
  }

  public static void WriteIntoConfig()
  {
    if (!File.Exists(configPath))
    {
      File.Create(configPath).Close();
    }
    string content = StringFromData();
    lastFileHash = content.GetHashCode();
    hasLoadedFile = true;
    lastCheckedTime = DateTime.Now;
    File.WriteAllText(configPath, content, Encoding.Unicode);
  }

  public static void ReadFromConfig()
  {
    if (!File.Exists(configPath))
    {
      if (Binds.FluxBinds.Count == 0)
      {
        Binds.FluxBinds.Add(TemplateBind);
        WriteIntoConfig();
      }
      else File.Create(configPath).Close();
    }
    else
    {
      if (ShouldLoadFile())
      {
        string content = File.ReadAllText(configPath, Encoding.Unicode);
        int hash = content.GetHashCode();
        bool doLoad = ShouldReloadFile(hash);
        hasLoadedFile = true;
        if (doLoad) LoadFromString(content, false);
      }
    }
  }
  public static void LoadFromString(string content, bool WriteFile = true)
  {
    Binds.FluxBinds = JsonConvert.DeserializeObject<List<Bind>>(content) ?? [];
    if (WriteFile) WriteIntoConfig();
  }
  public static string StringFromData()
  {
    return JsonConvert.SerializeObject(Binds.FluxBinds, Formatting.Indented);
  }

  public static bool hasLoadedFile = false;
  public static int lastFileHash = 0;
  public static DateTime? lastCheckedTime;
  public static bool ShouldLoadFile()
  {
    if ((DateTime.Now - lastCheckedTime.GetValueOrDefault()).TotalSeconds < 5)
    {
      return false;
    }
    return true;
  }
  public static bool ShouldReloadFile(int hash)
  {
    if (!hasLoadedFile)
    {
      lastFileHash = hash;
      lastCheckedTime = DateTime.Now;
      return true;
    }
    if (lastFileHash != hash)
    {
      lastCheckedTime = DateTime.Now;
      return true;
    }
    return false;
  }

  public static Bind TemplateBind => new()
  {
    bindID = "Template_Select_0",
    Action = Target.Select,
    IsDesktopBind = true,
    Inputs = [
      new Control() {
        Bind = ControlBind.Grip,
        IsPrimary = false,
        FireCondition = new Condition() {
          Invert = false,
          State = ConditionState.Held
        }
      }
    ]
  };
}