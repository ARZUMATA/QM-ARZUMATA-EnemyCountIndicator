using HarmonyLib;
using MGSC;
using ModConfigMenu;
using QM_McmIniMerger;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace QM_EnemyCountIndicator
{
    public static class Plugin
    {
        public static Harmony harmony;

        public static ConfigDirectories ConfigDirectories = new ConfigDirectories();

        public static ModConfig Config { get; private set; }

        public static Logger Logger = new Logger();

        // MCM Related Start
        public static string ConfigPath = Path.Combine(ConfigDirectories.ModPersistenceFolder, "config.ini");
        public static string ConfigPathMCM = Path.Combine(ConfigDirectories.ModPersistenceFolder, "config_mcm.ini");
        // MCM Related End

        [Hook(ModHookType.AfterConfigsLoaded)]
        public static void AfterConfig(IModContext context)
        {
            Directory.CreateDirectory(ConfigDirectories.ModPersistenceFolder);
            Config = ModConfig.LoadConfig(ConfigDirectories.ConfigPath);

            UpdateCheck(); // If we update our embedded config.ini, we check existing here.
            UpdateNewEntries();

            bool flag = false;
            string text = string.Empty;

            try
            {
                flag = Plugin.RegisterToMCM();
                text = Plugin.GetMCMPath(); // ConfigPathMCM
            }
            catch (Exception)
            {
                Plugin.Logger.LogWarning("Loading without MCM.");
                flag = false;
                text = string.Empty;
            }

            Plugin.Logger.Log($"\t flag {flag}");

            Plugin.Logger.Log($"\t text {text}");
            Plugin.Logger.Log($"\t File.Exists(text) {File.Exists(text)}");

            if (flag && File.Exists(text))
            {
                Plugin.Config.LoadConfigMCM(text);
                return;
            }

            Plugin.Config.LoadConfigMCM(Plugin.ConfigPath);

            // MCM Related End
        }

        private static void UpdateNewEntries()
        {
            if (File.Exists(ConfigPathMCM))
            {
                // Try to load mcm file if it exists, to merge existing settings.
                MergeOptions mergeOptions = new MergeOptions();
                mergeOptions.AddNewSections = true;
                mergeOptions.AddNewEntries = true;
                mergeOptions.RemoveObsoleteSections = true;
                mergeOptions.RemoveObsoleteEntries = true;
                mergeOptions.UpdateMetadata = true;
                mergeOptions.UpdateValues = false;
                IniMerge.AdvancedMerge(ConfigPathMCM, ConfigPath, ConfigPathMCM, mergeOptions);
            }
   
        }

        private static void UpdateCheck()
        {
            /* This is a dirty way to determine updates, we don't merge new config file values to mcm created one, but just replacing it, invalidating customized settings.
             * New MCM will have the support so gotta wait. */

            // Handle embedded config.ini
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("QM_EnemyCountIndicator.config.ini");
            StreamReader reader = new StreamReader(stream);

            // Check MD5 of config.ini to determine if we need to replace with with new version.
            var existingMD5 = File.Exists(ConfigPath) ? Helpers.GetMd5HashFromFilePath(ConfigPath) : null;
            var newMD5 = Helpers.GetMd5HashFromStream(stream);
            stream.Seek(0, SeekOrigin.Begin);

            // MCM Related Start

            Logger.Log($"ConfigPath: {ConfigPath}");

            if (File.Exists(Plugin.ConfigPath) && existingMD5 != newMD5)
            {
                Plugin.Logger.Log($"config.ini exists but md5 doesnt match. Replacing: {ConfigPath}");
                File.Delete(Plugin.ConfigPath);
                //File.Delete(Plugin.ConfigPathMCM); 
            }

            if (!File.Exists(Plugin.ConfigPath))
            {
                Plugin.Logger.LogWarning($"config.ini does not exist. Adding: {ConfigPath}");

                // Assuming the resource name is "config.ini" and it's under Resources folder
                File.WriteAllText(Plugin.ConfigPath, reader.ReadToEnd());
            }

            reader.Close();
            stream.Close();
        }

        [Hook(ModHookType.AfterBootstrap)]
        public static void Bootstrap(IModContext context)
        {
            var version = Data.Global.BuildVersion.Split('.');

            bool beta = false;

            if (version.Length == 5)
            //if (version.Contains("0.9.2."))
            {
                // It's beta
                beta = true;
            }

            string mainDllPath = Assembly.GetExecutingAssembly().Location;
            string mainDllDirectory = Path.GetDirectoryName(mainDllPath);

            harmony = new Harmony("ARZUMATA_" + ConfigDirectories.ModAssemblyName);

            harmony.PatchAll();

            var stableDllPath = Path.Combine(mainDllDirectory, "QM_EnemyCountIndicator_Stable.dll");
            var betaDllPath = Path.Combine(mainDllDirectory, "QM_EnemyCountIndicator_Beta.dll");
            Console.WriteLine($"Exists Beta {File.Exists(betaDllPath)}");
            Console.WriteLine($"Exists Stable {File.Exists(stableDllPath)}");

            if (beta)
            {
                Console.WriteLine($"Patching Beta");

                Assembly assembly = Assembly.LoadFrom(betaDllPath);
                harmony.PatchAll(assembly); // Apply patches from the loaded DLL
            }
            else
            {
                Console.WriteLine($"Patching Stable");
                Assembly assembly = Assembly.LoadFrom(stableDllPath);
                harmony.PatchAll(assembly); // Apply patches from the loaded DLL
            }
            //Assembly assembly = Assembly.LoadFrom(dllPath);
            //harmony.PatchAll(assembly); // Apply patches from the loaded DLL


        }

        //[Hook(ModHookType.AfterBootstrap)]
        //public static void Bootstrap(IModContext context)
        //{
        //    new Harmony("ARZUMATA_" + ConfigDirectories.ModAssemblyName).PatchAll();
        //}

        // MCM Related Start
        private static bool RegisterToMCM()
        {
            ModConfigMenuAPI.RegisterModConfig("Enemy Indicator", Plugin.ConfigPath, delegate (Dictionary<string, object> properties)
            {
                Plugin.Config.LoadConfigMCM(properties);
            });
            return true;
        }

        private static string GetMCMPath()
        {
            return ModConfigMenuAPI.GetNameForConfigFile(Plugin.ConfigPath);
        }

        // MCM Related End
    }
}
