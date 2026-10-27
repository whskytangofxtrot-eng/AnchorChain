using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using BepInEx;
using SeaPower;
using UnityEngine;
using Mono.Cecil;

[assembly: AssemblyTitle("AnchorChain beta preloader compatibility update")]
[assembly: AssemblyCompany("WhskyTangoFxtrot")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.3.0")]

namespace AnchorChain.Preloader
{
    // Keep the existing public interface and plugin ID so AnchorChain 1.1 can
    // use this replacement without being recompiled or replaced itself.
    public interface IPluginLoader { void LoadPlugins(); }
    [BepInPlugin("io.github.seapower_modders.anchorchain_preloader","AnchorChain Preloader - Beta Compatibility","1.0.3")]
    public sealed class AnchorChainPreloader : BaseUnityPlugin
    {
        private static string[] enabledFolders;
        private static string anchorFile;
        private static Assembly anchorAssembly;
        private string[] failedMods = new string[0];
        private bool startupRetriesStopped;
        public static bool IsEnabledModFile(string path)
        {
            string full=Path.GetFullPath(path);
            return enabledFolders!=null&&enabledFolders.Any(folder=>full.StartsWith(folder,StringComparison.OrdinalIgnoreCase));
        }
        public static Assembly LoadSelectedModAssembly(string path)
        {
            if(!IsEnabledModFile(path))return typeof(AnchorChainPreloader).Assembly;
            if(string.Equals(Path.GetFullPath(path),anchorFile,StringComparison.OrdinalIgnoreCase))return anchorAssembly;
            return Assembly.LoadFile(path);
        }
        private static Assembly LoadFilteredAnchorChain(string path)
        {
            anchorFile=Path.GetFullPath(path);
            using(var definition=AssemblyDefinition.ReadAssembly(path))
            {
                var loader=definition.MainModule.Types.Single(t=>t.FullName=="AnchorChain.AnchorChainLoader");
                var load=loader.Methods.Single(m=>m.Name=="LoadPlugins");int changes=0;
                foreach(var instruction in load.Body.Instructions)
                {
                    var reference=instruction.Operand as MethodReference;
                    if(reference!=null&&reference.FullName=="System.Reflection.Assembly System.Reflection.Assembly::LoadFile(System.String)")
                    {instruction.Operand=definition.MainModule.ImportReference(typeof(AnchorChainPreloader).GetMethod("LoadSelectedModAssembly"));changes++;}
                }
                if(changes!=1)throw new InvalidOperationException("AnchorChain's loader layout changed; refusing to load unchecked code mods.");
                using(var output=new MemoryStream()){definition.Write(output);anchorAssembly=Assembly.Load(output.ToArray());}
            }
            return anchorAssembly;
        }
        private void Awake()
        {
            var initial=FileManager.Instance.Directories;
            StartCoroutine(LoadWhenReady());
        }
        private IEnumerator LoadWhenReady()
        {
            var generation=typeof(FileManager).GetProperty("RefreshGeneration");
            float started=Time.realtimeSinceStartup;
            while(generation!=null&&(int)generation.GetValue(FileManager.Instance,null)==0)
            {
                if(Time.realtimeSinceStartup-started>180f){Logger.LogError("Mod directory initialization timed out; no code mods were loaded.");yield break;}
                yield return null;
            }
            try { Load(); } catch(Exception error) {Logger.LogError("AnchorChain startup failed: "+error);}
            if(startupRetriesStopped) yield return ShowStartupFailureNotice();
        }
        private IEnumerator ShowStartupFailureNotice()
        {
            // The game's native dialog model is internal, although its public
            // properties and routed window commands are usable by mods.
            var messageType=typeof(FileManager).Assembly.GetType("SeapowerUI.ViewModels.MessageBoxViewModel",true);
            var message=Activator.CreateInstance(messageType);
            messageType.GetProperty("Header").SetValue(message,"Code mod failed to start",null);
            messageType.GetProperty("MessageBoxType").SetValue(message,SeapowerUI.ViewModels.MessageBoxType.Warning,null);
            messageType.GetProperty("Message").SetValue(message,StartupFailureReport.BuildNotice(failedMods),null);
            messageType.GetProperty("AcknowledgeCommandLabel").SetValue(message,"OK",null);
            float started=Time.realtimeSinceStartup;
            // Wait for the actual menu, after any launch-recovery prompt.
            while(SeapowerUI.MainMenuView.Instance==null ||
                !SeapowerUI.WindowHostCommands.OpenWindowCommand.CanExecute(message,SeapowerUI.MainMenuView.Instance))
            {
                if(Time.realtimeSinceStartup-started>180f){Logger.LogWarning("The mod failure notice could not be shown because the main menu did not become ready.");yield break;}
                yield return new WaitForSecondsRealtime(0.25f);
            }
            var context=SeapowerUI.MainMenuView.Instance;
            messageType.GetProperty("AcknowledgeCommand").SetValue(message,new DelegateCommand(_=>SeapowerUI.WindowHostCommands.CloseWindowCommand.Execute(message,context)),null);
            SeapowerUI.WindowHostCommands.OpenWindowCommand.Execute(message,context);
            Logger.LogInfo("Startup failure notice displayed for: "+string.Join(", ",failedMods));
        }
        private void Load()
        {
            var selected=FileManager.Instance.Directories.Where(d=>d.IsChecked&&d.DirectoryInfo.Exists).ToArray();
            enabledFolders=selected.Select(d=>Path.GetFullPath(d.DirectoryInfo.FullName).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var chainPaths=selected.Select(d=>Path.Combine(d.DirectoryInfo.FullName,"AnchorChain.dll")).Where(File.Exists).Distinct().ToArray();
            if(chainPaths.Length==0){Logger.LogInfo("AnchorChain is not selected or installed; nothing to load.");return;}
            if(chainPaths.Length!=1)throw new InvalidOperationException("More than one enabled AnchorChain copy was found.");
            var patches=selected.Where(d=>File.Exists(Path.Combine(d.DirectoryInfo.FullName,"wtf-dcm-beta.retrofit"))).ToArray();
            if(patches.Length>1)throw new InvalidOperationException("More than one enabled DCM compatibility patch was found.");
            Assembly chain;
            if(patches.Length==1)
            {
                string folder=patches[0].DirectoryInfo.FullName;
                var helper=Assembly.LoadFile(Path.Combine(folder,"SeaPower.BetaCompatibility.dll"));
                var entry=helper.GetType("Local.SeaPowerBeta.WorkshopBootstrap",true).GetMethod("PrepareAnchorChain");
                chain=(Assembly)entry.Invoke(null,new object[]{chainPaths[0],folder});
            }
            else { chain=LoadFilteredAnchorChain(chainPaths[0]);Logger.LogInfo("DCM compatibility patch is disabled or absent; original mod code will be loaded only from checked directories."); }
            var loader=chain.GetType("AnchorChain.AnchorChainLoader",true);
            AnchorChainStartupGuard.StartupStopped=()=>{
                startupRetriesStopped=true;
                Logger.LogError("Stopped AnchorChain startup retries after a code mod failed. The error above names the affected mod. Disable that mod and restart Sea Power before playing; it may have partially initialized.");
            };
            AnchorChainStartupGuard.Apply(loader);
            Logger.LogInfo("AnchorChain startup retry guard installed.");
            using(var report=new StartupFailureReport())
            {
                BepInEx.Logging.Logger.Listeners.Add(report);
                try { ((IPluginLoader)Activator.CreateInstance(loader)).LoadPlugins(); }
                finally { BepInEx.Logging.Logger.Listeners.Remove(report);failedMods=report.GetFailedModNames(); }
            }
        }
    }
}
