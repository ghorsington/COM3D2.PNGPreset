using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using B83.Win32;
using UnityEngine;

namespace COM3D2.PNGPreset.Managed
{
    public static class PNGPreset
    {
        private static byte[] IEND_MAGIC = { 73, 69, 78, 68 };

        private static readonly UnityDragAndDropHook dragAndDropHook = new UnityDragAndDropHook();

        public static void StartDragAndDrop()
        {
            dragAndDropHook.OnDroppedFiles += OnFilesDragDropped;
            dragAndDropHook.InstallHook();
        }

        public static void StopDragAndDrop()
        {
            dragAndDropHook.UninstallHook();
            dragAndDropHook.OnDroppedFiles -= OnFilesDragDropped;
        }

        private static void OnFilesDragDropped(List<string> apathnames, POINT adroppoint)
        {
            var needUpdate = false;

            foreach (var apathname in apathnames)
            {
                if(Path.GetExtension(apathname) != ".png")
                    continue;
                if(Path.GetDirectoryName(apathname) == PresetPath)
                    continue;

                var preset = GameMain.Instance.CharacterMgr.PresetLoad(apathname);

                if(preset == null)
                    continue;

                try
                {
                    File.Copy(apathname,
                        Path.Combine(PresetPath,
                            $"{Path.GetFileNameWithoutExtension(apathname)}_{DateTime.Now.Ticks}.png"));
                }
                catch (Exception)
                {
                }
                needUpdate = true;
            }

            if(needUpdate)
                BaseMgr<PresetMgr>.Instance.UpdatePresetList();
        }

        public static void PresetListLoadHook(CharacterMgr mgr, ref List<CharacterMgr.Preset> list)
        {
            list.AddRange(Directory.GetFiles(PresetPath, "*.png").Select(mgr.PresetLoad).Where(item => item != null));
        }

        public static bool PresetLoadHook(CharacterMgr mgr, out CharacterMgr.Preset result, BinaryReader br, string fileName)
        {
            result = null;

            if (fileName == null || Path.GetExtension(fileName) != ".png")
                return false;

            var stream = br.BaseStream;

            var buf = new byte[IEND_MAGIC.Length];

            var pos = 0;
            for (;; stream.Position = ++pos)
            {
                var len = stream.Read(buf, 0, buf.Length);

                if (len != IEND_MAGIC.Length)
                    return true;

                if (buf.SequenceEqual(IEND_MAGIC))
                    break;
            }

            // Skip CRC
            stream.Position += 4;

            var prevPos = stream.Position;

            if (br.ReadString() != "CM3D2_PRESET")
                return true;

            stream.Position = prevPos;

            result = mgr.PresetLoad(br, null);
            result.strFileName = fileName;

            return true;
        }

        public static bool PresetSaveHook(CharacterMgr mgr, out CharacterMgr.Preset preset, Maid maid, CharacterMgr.PresetType presetType)
        {
            if(!Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.LeftControl))
            {
                preset = null;
                return false;
            }

            preset = new CharacterMgr.Preset
            {
                ePreType = presetType,
                texThum = ThumShot.ShotThumPreset(maid),
            };

            var bigThumTex = ThumShot.ShotThumCard(maid);

            if (!Directory.Exists(PresetPath))
                Directory.CreateDirectory(PresetPath);

            using (var bw = new BinaryWriter(File.Create(Path.Combine(PresetPath,
                $"pre_{maid.status.lastName}{maid.status.firstName}_{DateTime.Now:yyyyMMddHHmmss}.png"))))
            {
                bw.Write(bigThumTex.EncodeToPNG());
                bw.Write(mgr.PresetSaveNotWriteFile(maid, presetType));
            }

            GameMain.Instance.SysDlg.Show("Saved image as a preset card\n(If you want to save a normal preset, don't hold [CTRL] while saving)", SystemDialog.TYPE.OK);

            return true;
        }

        private static readonly string PresetPath = Path.Combine(UTY.gameProjectPath, "Preset");

        private static ThumShot ThumShot => GameMain.Instance.ThumCamera.GetComponent<ThumShot>();
    }
}
