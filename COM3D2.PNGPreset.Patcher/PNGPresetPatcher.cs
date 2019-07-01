using System.IO;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Inject;

namespace COM3D2.PNGPreset.Patcher
{
    public static class PNGPresetPatcher
    {
        public static readonly string[] TargetAssemblyNames = { "Assembly-CSharp.dll" };
        public const string MANAGED_NAME = "COM3D2.PNGPreset.Managed";

        public static void Patch(AssemblyDefinition ad)
        {
            var managedAd = AssemblyLoader.LoadAssembly(Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), $"{MANAGED_NAME}.dll"));

            var md = ad.MainModule;
            var hookMd = managedAd.MainModule;

            var characterMgrTd = md.GetType("CharacterMgr");
            var pngPresetTd = hookMd.GetType($"{MANAGED_NAME}.PNGPreset");

            var presetSaveMethDef = characterMgrTd.GetMethod("PresetSave");
            var presetSaveHookMethDef = pngPresetTd.GetMethod("PresetSaveHook");
            presetSaveMethDef.InjectWith(presetSaveHookMethDef, flags: InjectFlags.ModifyReturn | InjectFlags.PassInvokingInstance | InjectFlags.PassParametersVal);

            var presetListLoadMethDef = characterMgrTd.GetMethod("PresetListLoad");
            var presetListLoadHookMethDef = pngPresetTd.GetMethod("PresetListLoadHook");
            presetListLoadMethDef.InjectWith(presetListLoadHookMethDef, -1, flags: InjectFlags.PassInvokingInstance | InjectFlags.PassLocals, localsID: new []{ 0 });

            var presetLoadMethDef = characterMgrTd.GetMethod("PresetLoad", "System.IO.BinaryReader", "System.String");
            var presetLoadHookDef = pngPresetTd.GetMethod("PresetLoadHook");
            presetLoadMethDef.InjectWith(presetLoadHookDef, flags: InjectFlags.ModifyReturn | InjectFlags.PassInvokingInstance | InjectFlags.PassParametersVal);

            var sceneEditTd = md.GetType("SceneEdit");

            var startMethDef = sceneEditTd.GetMethod("Start");
            var startDragDropMethDef = pngPresetTd.GetMethod("StartDragAndDrop");
            startMethDef.InjectWith(startDragDropMethDef);

            var onEndSceneMethDef = sceneEditTd.GetMethod("OnEndScene");
            var stopDragDropMethDef = pngPresetTd.GetMethod("StopDragAndDrop");
            onEndSceneMethDef.InjectWith(stopDragDropMethDef);
        }
    }
}
