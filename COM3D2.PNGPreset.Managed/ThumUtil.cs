using UnityEngine;

namespace COM3D2.PNGPreset.Managed
{
    public static class ThumUtil
    {
        private static readonly Size<int> CardSize = new Size<int>(300, 400);

        private static readonly Vector3 cameraOffset = new Vector3(0.6f, 2f, 0f);

        private static readonly RenderTexture thumCard1 =
            new RenderTexture(CardSize.width, CardSize.height, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear,
                antiAliasing = 8
            };

        private static readonly RenderTexture thumCard2 =
            new RenderTexture(CardSize.width, CardSize.height, 0, RenderTextureFormat.ARGB32);

        private static void MoveTargetCard(ThumShot thum, Maid maid)
        {
            var transform = CMT.SearchObjName(maid.body0.m_Bones.transform, "Bip01 HeadNub");
            if (transform != null)
            {
                thum.transform.position = transform.TransformPoint(transform.localPosition + cameraOffset);
                thum.transform.rotation = transform.rotation * Quaternion.Euler(90f, 0f, 90f);
            }
            else
            {
                Debug.LogError("Failed to find maid's head!");
            }
        }

        public static Texture2D MakeMaidThumbnail(Maid maid)
        {
            var thum = GameMain.Instance.ThumCamera.GetComponent<ThumShot>();
            var camera = thum.gameObject.GetComponent<Camera>();

            MoveTargetCard(thum, maid);
            camera.fieldOfView = 30f;
            return thum.RenderThum(camera, thumCard1, thumCard2, CardSize, true);
        }
    }
}