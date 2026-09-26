using System.IO;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
// using ServerSync;

namespace StaffNShields
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class StaffNShieldsPlugin : BaseUnityPlugin
    {
        internal const string ModName = "StaffNShields";
        internal const string ModVersion = "1.0.0";
        internal const string Author = "RustyMods";
        private const string ModGUID = Author + "." + ModName;
        private static string ConfigFileName = ModGUID + ".cfg";
        private static string ConfigFileFullPath = Paths.ConfigPath + Path.DirectorySeparatorChar + ConfigFileName;
        internal static string ConnectionError = "";
        private readonly Harmony _harmony = new(ModGUID);

        public static readonly ManualLogSource StaffNShieldsLogger = BepInEx.Logging.Logger.CreateLogSource(ModName);
        

        // private static readonly ConfigSync ConfigSync = new(ModGUID)
        //     { DisplayName = ModName, CurrentVersion = ModVersion, MinimumRequiredVersion = ModVersion };
        
        // private static ConfigEntry<Toggle> _serverConfigLocked = null!;

        // public enum Toggle
        // {
        //     On = 1,
        //     Off = 0
        // }

        public void Awake()
        {
            // bool saveOnSet = Config.SaveOnConfigSet;
            // Config.SaveOnConfigSet =
            //     false; // This and the variable above are used to prevent the config from saving on startup for each config entry. This is speeds up the startup process.
            //
            // _serverConfigLocked = config("1 - General", "Lock Configuration", Toggle.On,
            //     "If on, the configuration is locked and can be changed by server admins only.");
            // _ = ConfigSync.AddLockingConfigEntry(_serverConfigLocked);

            Assembly assembly = Assembly.GetExecutingAssembly();
            _harmony.PatchAll(assembly);
            SetupWatcher();

            // if (saveOnSet)
            // {
            //     Config.SaveOnConfigSet = saveOnSet;
            //     Config.Save();
            // }
            
        }

        private void OnDestroy()
        {
            Config.Save();
        }
        
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        private static class Humanoid_EquipItem_Patch
        {
            private static bool Prefix(Humanoid __instance, ItemDrop.ItemData item, bool triggerEquipEffects, ref bool __result)
            {
                if (!__instance.IsPlayer()) return true;
                if (item.m_shared.m_dlc.Length > 0 && !DLCMan.instance.IsDLCInstalled(item.m_shared.m_dlc)) return true;
                if (__instance.IsItemEquiped(item) || 
                    !__instance.m_inventory.ContainsItem(item) || 
                    __instance.InAttack() ||
                    __instance.InDodge() || 
                    __instance.IsSwimming() || 
                    !__instance.IsOnGround() || 
                    (item.m_shared.m_useDurability && item.m_durability <= 0.0)) return true;
                
                if (__instance.m_leftItem?.m_shared.m_skillType is Skills.SkillType.Blocking &&
                    item.m_shared.m_skillType is 
                        Skills.SkillType.ElementalMagic or 
                        Skills.SkillType.BloodMagic)
                {
                    __instance.UnequipItem(__instance.m_rightItem, triggerEquipEffects);
                    __instance.m_rightItem = item;
                    if (__instance.m_visEquipment && __instance.m_visEquipment.m_isPlayer && FejdStartup.instance == null)
                    {
                        item.m_shared.m_equipEffect.Create(
                            __instance.m_visEquipment.m_rightHand.position,
                            __instance.m_visEquipment.m_rightHand.rotation,
                            gamepadEffectsExclusiveToPlayer: __instance.GetZDOID());
                    }
                    __instance.m_hiddenRightItem = null;
                    __instance.m_hiddenLeftItem = null;
                }
                else if (__instance.m_rightItem?.m_shared.m_skillType is
                             Skills.SkillType.ElementalMagic or
                             Skills.SkillType.BloodMagic &&
                         item.m_shared.m_skillType is Skills.SkillType.Blocking)
                {
                    __instance.UnequipItem(__instance.m_leftItem, triggerEquipEffects);
                    __instance.m_leftItem = item;

                    if (__instance.m_visEquipment && __instance.m_visEquipment.m_isPlayer && FejdStartup.instance == null)
                    {
                        item.m_shared.m_equipEffect.Create(
                            __instance.m_visEquipment.m_rightHand.position,
                            __instance.m_visEquipment.m_rightHand.rotation,
                            gamepadEffectsExclusiveToPlayer: __instance.GetZDOID());
                    }

                    __instance.m_hiddenRightItem = null;
                    __instance.m_hiddenLeftItem = null;
                }
                else
                {
                    return true;
                }

                if (__instance.IsItemEquiped(item))
                {
                    item.m_equipped = true;
                }
                __instance.SetupEquipment();
                if (triggerEquipEffects)
                {
                    __instance.TriggerEquipEffect(item);
                }
                __result = true;
                return false;
            }
        }

        private void SetupWatcher()
        {
            FileSystemWatcher watcher = new(Paths.ConfigPath, ConfigFileName);
            watcher.Changed += ReadConfigValues;
            watcher.Created += ReadConfigValues;
            watcher.Renamed += ReadConfigValues;
            watcher.IncludeSubdirectories = true;
            watcher.SynchronizingObject = ThreadingHelper.SynchronizingObject;
            watcher.EnableRaisingEvents = true;
        }

        private void ReadConfigValues(object sender, FileSystemEventArgs e)
        {
            if (!File.Exists(ConfigFileFullPath)) return;
            try
            {
                StaffNShieldsLogger.LogDebug("ReadConfigValues called");
                Config.Reload();
            }
            catch
            {
                StaffNShieldsLogger.LogError($"There was an issue loading your {ConfigFileName}");
                StaffNShieldsLogger.LogError("Please check your config entries for spelling and format!");
            }
        }
        

        private ConfigEntry<T> config<T>(string group, string name, T value, ConfigDescription description,
            bool synchronizedSetting = true)
        {
            ConfigDescription extendedDescription =
                new(
                    description.Description +
                    (synchronizedSetting ? " [Synced with Server]" : " [Not Synced with Server]"),
                    description.AcceptableValues, description.Tags);
            ConfigEntry<T> configEntry = Config.Bind(group, name, value, extendedDescription);
            //var configEntry = Config.Bind(group, name, value, description);

            // SyncedConfigEntry<T> syncedConfigEntry = ConfigSync.AddConfigEntry(configEntry);
            // syncedConfigEntry.SynchronizedConfig = synchronizedSetting;

            return configEntry;
        }

        private ConfigEntry<T> config<T>(string group, string name, T value, string description,
            bool synchronizedSetting = true)
        {
            return config(group, name, value, new ConfigDescription(description), synchronizedSetting);
        }
    }
}