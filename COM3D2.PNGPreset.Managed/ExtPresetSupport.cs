using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Xml;
using UnityEngine;

namespace COM3D2.PNGPreset.Managed
{
    internal static class ExtPresetSupport
    {
        public static readonly bool Enabled;

        private static readonly Action<Maid, CharacterMgr.Preset> loadExtPreset;
        private static readonly Action<XmlDocument> setXmlMemory;
        private static readonly Func<XmlDocument> getXmlMemory;

        static ExtPresetSupport()
        {
            Debug.Log("Checking if ExPreset is present...");
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            Debug.Log($"Got {assemblies.Length} assemblies");

            var extPresetAss = assemblies.FirstOrDefault(a =>
                a.GetName().Name.EndsWith("ExternalPreset.Managed", StringComparison.InvariantCultureIgnoreCase));

            Debug.Log($"Got assembly: {extPresetAss}");

            if (extPresetAss == null)
            {
                Debug.Log("No ExPreset found! Aborting...");
                return;
            }

            var exPresetType = extPresetAss.GetTypesSafe().FirstOrDefault(t => t.Name == "ExPreset");

            Debug.Log($"Got ExPreset type: {exPresetType}");

            if (exPresetType == null)
            {
                Debug.Log("No required ExPreset types found! Aborting...");
                return;
            }

            loadExtPreset = exPresetType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static)
                .AsDelegate<Action<Maid, CharacterMgr.Preset>>();

            Debug.Log($"Got Load method: {loadExtPreset}");

            var xmlMemoryField = exPresetType.GetField("xmlMemory", BindingFlags.NonPublic | BindingFlags.Static);

            Debug.Log($"Got xmlMemory: {xmlMemoryField}");
            setXmlMemory = CreateStaticFieldSetter<XmlDocument>(xmlMemoryField);
            getXmlMemory = CreateStaticFieldGetter<XmlDocument>(xmlMemoryField);

            Debug.Log("ExPreset detected!");

            Enabled = true;
        }

        private static Func<T> CreateStaticFieldGetter<T>(FieldInfo fi)
        {
            Debug.Log($"Creating getter with name {fi.Name}_{fi.DeclaringType.FullName.Replace(".", "_")}_get");

            var m = new DynamicMethod($"{fi.Name}_{fi.DeclaringType.FullName.Replace(".", "_")}_get", typeof(T), null, typeof(ExtPresetSupport), true);

            Debug.Log($"Created getter {m}");

            var il = m.GetILGenerator();

            il.Emit(OpCodes.Ldsfld, fi);
            il.Emit(OpCodes.Ret);

            return m.CreateDelegate(typeof(Func<T>)) as Func<T>;
        }

        private static Action<T> CreateStaticFieldSetter<T>(FieldInfo fi)
        {
            Debug.Log($"Creating setter with name {fi.Name}_{fi.DeclaringType.FullName.Replace(".", "_")}_set");

            var m = new DynamicMethod($"{fi.Name}_{fi.DeclaringType.FullName.Replace(".", "_")}_set", null, new[] {typeof(T)}, typeof(ExtPresetSupport), true);

            Debug.Log($"Created setter {m}");

            var il = m.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Stsfld, fi);
            il.Emit(OpCodes.Ret);

            Debug.Log("Created! Returning a delegate!");

            return m.CreateDelegate(typeof(Action<T>)) as Action<T>;
        }

        public static byte[] SaveExPresetData(Maid maid)
        {
            if (!Enabled)
                return null;

            Debug.Log("Saving ExtPreset data");

            // Since we save data using memory method, ExtPreset will fill the saved data using its temporary buffer
            var doc = getXmlMemory();

            if (doc == null)
                return null;

            setXmlMemory(null);

            using (var ms = new MemoryStream())
            {
                doc.Save(ms);
                ms.Position = 0;
                var compressed = LZMA.Compress(ms);

                Debug.Log($"Extra data: {ms.Length} bytes");
                Debug.Log($"Compressed data: {compressed.Length} bytes");

                return compressed;
            }
        }

        public static void LoadExPresetData(Stream s, Maid maid)
        {
            if (!Enabled)
                return;

            using (var ds = LZMA.Decompress(s, PNGPreset.EXT_DATA_END_MAGIC.Length))
            {
                Debug.Log($"Got decompressed file of length: {ds.Length}!");

                File.WriteAllBytes("wew.xml", ds.ToArray());

                ds.Position = 0;
                var doc = new XmlDocument();
                doc.Load(ds);
                Debug.Log("Loaded XML");
                setXmlMemory(doc);
            }

            Debug.Log("Loading preset");
            // If we load ex preset while using no file name, ExtPreset will use the temporary buffer
            loadExtPreset(maid, new CharacterMgr.Preset {strFileName = string.Empty});
            Debug.Log("Preset loaded");

            setXmlMemory(null);
        }

        private static T AsDelegate<T>(this MethodInfo mi) where T : class
        {
            return Delegate.CreateDelegate(typeof(T), mi) as T;
        }

        private static Type[] GetTypesSafe(this Assembly ass)
        {
            try
            {
                return ass.GetTypes();
            }
            catch (ReflectionTypeLoadException re)
            {
                return re.Types;
            }
        }
    }
}