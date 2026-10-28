using System;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BelmontsCurseMasterOptimizer.Patches
{
    public class MenuCamFix : MonoBehaviour
    {
        private static MenuCamFix _instance;
        private Camera uiCam;
        private Canvas menu;
        private float origSize = -1f;
        private float refresh;

        public static bool IsMenuShowing
        {
            get
            {
                try
                {
                    if (MapPatches.IsGameMenuOpen) return true;
                    if ((UnityEngine.Object)(object)_instance != (UnityEngine.Object)null &&
                        (UnityEngine.Object)(object)_instance.menu != (UnityEngine.Object)null)
                    {
                        return ((Component)_instance.menu).gameObject.activeInHierarchy;
                    }
                }
                catch {}
                return false;
            }
        }

        public MenuCamFix(IntPtr p) : base(p)
        {
            _instance = this;
        }

        private void LateUpdate()
        {
            refresh -= Time.unscaledDeltaTime;
            if (((UnityEngine.Object)(object)uiCam == (UnityEngine.Object)null || (UnityEngine.Object)(object)menu == (UnityEngine.Object)null) && refresh <= 0f)
            {
                refresh = 1f;
                foreach (UnityEngine.Object item in (Il2CppArrayBase<UnityEngine.Object>)(object)Resources.FindObjectsOfTypeAll(Il2CppType.Of<Camera>()))
                {
                    Camera val = ((Il2CppObjectBase)item).Cast<Camera>();
                    if (((UnityEngine.Object)val).name == "WorldSpaceUICamera")
                    {
                        Scene scene = ((Component)val).gameObject.scene;
                        if (scene.IsValid())
                        {
                            uiCam = val;
                            origSize = val.orthographicSize;
                        }
                    }
                }
                foreach (UnityEngine.Object item2 in (Il2CppArrayBase<UnityEngine.Object>)(object)Resources.FindObjectsOfTypeAll(Il2CppType.Of<Canvas>()))
                {
                    Canvas val2 = ((Il2CppObjectBase)item2).Cast<Canvas>();
                    if (((UnityEngine.Object)val2).name == "GameMenu" && val2.isRootCanvas)
                    {
                        Scene scene = ((Component)val2).gameObject.scene;
                        if (scene.IsValid())
                        {
                            menu = val2;
                        }
                    }
                }
            }

            if (Plugin.FixMenuZoomConfig != null && !Plugin.FixMenuZoomConfig.Value)
            {
                return;
            }

            if ((UnityEngine.Object)(object)uiCam == (UnityEngine.Object)null || !uiCam.orthographic)
            {
                return;
            }

            if (origSize < 0f)
            {
                origSize = uiCam.orthographicSize;
            }

            if ((UnityEngine.Object)(object)menu != (UnityEngine.Object)null && ((Component)menu).gameObject.activeInHierarchy && (int)menu.renderMode == 2 && ((Behaviour)uiCam).isActiveAndEnabled)
            {
                RectTransform component = ((Component)menu).GetComponent<RectTransform>();
                Rect rect = component.rect;
                float height = rect.height * ((Transform)component).lossyScale.y;
                float width = rect.width * ((Transform)component).lossyScale.x;
                float targetSize = Mathf.Max(height * 0.5f, width * 0.5f / uiCam.aspect);
                if (targetSize > origSize)
                {
                    uiCam.orthographicSize = targetSize;
                }
            }
            else if (uiCam.orthographicSize != origSize)
            {
                uiCam.orthographicSize = origSize;
            }
        }
    }
}
