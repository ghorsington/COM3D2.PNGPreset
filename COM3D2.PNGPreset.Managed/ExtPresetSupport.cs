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
            Debug.Log("[PNGPreset] Checking if ExPreset is present...");
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            var extPresetAss = assemblies.FirstOrDefault(a =>
                a.GetName().Name.EndsWith("ExternalPreset.Managed", StringComparison.InvariantCultureIgnoreCase));

            if (extPresetAss == null)
            {
                Debug.Log("[PNGPreset] No ExtPreset found!");
                return;
            }

            var exPresetType = extPresetAss.GetTypesSafe().FirstOrDefault(t => t.Name == "ExPreset");

            if (exPresetType == null)
            {
                Debug.Log("[PNGPreset] No required ExPreset types found! Is your version too new/old?");
                return;
            }

            loadExtPreset = exPresetType.GetMethod("Load", BindingFlags.Public | BindingFlags.Static)
                .AsDelegate<Action<Maid, CharacterMgr.Preset>>();

            var xmlMemoryField = exPresetType.GetField("xmlMemory", BindingFlags.NonPublic | BindingFlags.Static);

            setXmlMemory = CreateStaticFieldSetter<XmlDocument>(xmlMemoryField);
            getXmlMemory = CreateStaticFieldGetter<XmlDocument>(xmlMemoryField);

            Debug.Log("[PNGPreset] ExtPreset detected!");

            Enabled = true;
        }

        private static Func<T> CreateStaticFieldGetter<T>(FieldInfo fi)
        {
            var m = new DynamicMethod($"{fi.Name}_{fi.DeclaringType.FullName.Replace(".", "_")}_get", typeof(T), null,
                typeof(ExtPresetSupport), true);

            var il = m.GetILGenerator();

            il.Emit(OpCodes.Ldsfld, fi);
            il.Emit(OpCodes.Ret);

            return m.CreateDelegate(typeof(Func<T>)) as Func<T>;
        }

        private static Action<T> CreateStaticFieldSetter<T>(FieldInfo fi)
        {
            var m = new DynamicMethod($"{fi.Name}_{fi.DeclaringType.FullName.Replace(".", "_")}_set", null,
                new[] {typeof(T)}, typeof(ExtPresetSupport), true);

            var il = m.GetILGenerator();

            il.Emit(OpCodes.Ldarg_0);
            il.Emit(OpCodes.Stsfld, fi);
            il.Emit(OpCodes.Ret);

            return m.CreateDelegate(typeof(Action<T>)) as Action<T>;
        }

        public static byte[] SaveExPresetData(Maid maid)
        {
            if (!Enabled)
                return null;

            Debug.Log("[PNGPreset] Saving ExtPreset data");

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

                return compressed;
            }
        }

        public static void LoadExPresetData(Stream s, Maid maid)
        {
            if (!Enabled)
                return;

            using (var ds = LZMA.Decompress(s, PNGPreset.EXT_DATA_END_MAGIC.Length))
            {
                ds.Position = 0;
                var doc = new XmlDocument();
                doc.Load(ds);
                setXmlMemory(doc);
            }

            Debug.Log("[PNGPreset] Loading ExtPreset data");
            // If we load ex preset while using no file name, ExtPreset will use the temporary buffer
            loadExtPreset(maid, new CharacterMgr.Preset {strFileName = string.Empty});
            Debug.Log("[PNGPreset] ExtPreset data loaded");

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