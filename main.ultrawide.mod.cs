using HarmonyLib;
using Pug.RP;
using PugMod;
using System.Collections.Generic;
using Unity.Mathematics;

namespace Sashiri
{

    [HarmonyPatch(typeof(SendClientSubMapToPugMapSystem))]
    static class SendClientSubMapToPugMapSystem_Patch
    {
        static readonly System.Reflection.ConstructorInfo Int2Ctor =
             typeof(int2).GetConstructor(new[] { typeof(int), typeof(int) });

        static readonly System.Reflection.MethodInfo HorizontalScaleMethod =
            typeof(SendClientSubMapToPugMapSystem_Patch)
                .GetMethod(nameof(ScaleHorizontalVisibilityView),
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);

        [HarmonyPatch("ComputeShouldBeViewedMemo")]
        [HarmonyTranspiler]

        static IEnumerable<CodeInstruction> ComputeShouldBeViewedMemo(IEnumerable<CodeInstruction> instructions)
        {
            // Find instructions resembling `int2 @int = new int2(20, 16);`
            var codeMatcher = new CodeMatcher(instructions);
            codeMatcher.MatchStartForward(
                new CodeMatch(System.Reflection.Emit.OpCodes.Ldc_I4_S, (sbyte)20),
                new CodeMatch(System.Reflection.Emit.OpCodes.Ldc_I4_S, (sbyte)16),
                new CodeMatch(System.Reflection.Emit.OpCodes.Call, Int2Ctor)
            );

            if (!codeMatcher.IsValid)
            {
                throw new System.Exception("Sashiri.Ultrawide: Could not patch ComputeShouldBeViewedMemo, missing IL reference for view frustrum");
            }

            // Skip instruction to pass default horizontal size to`HorizontalScaleMethod`
            // return new value and continue the default flow 
            codeMatcher.Advance(1);
            codeMatcher.InsertAndAdvance(
                new CodeInstruction(System.Reflection.Emit.OpCodes.Call, HorizontalScaleMethod)
            );

            return codeMatcher.Instructions();
        }

        public static int ScaleHorizontalVisibilityView(int x)
        {
            var cam = API.Rendering.GameCamera;
            if (cam == null || cam.outputWidth == 0)
                return x;

            return (int)(x * ((float)cam.GetPixelWidth() / cam.outputWidth));
        }
    }

    public class UltraWide : IMod
    {
        private const float ASPECT_16_9 = 16f / 9f;

        public void EarlyInit()
        {
        }

        public void Init()
        {
            BurstDisabler.DisableBurstForSystemAndJobs<SendClientSubMapToPugMapSystem>();
            SetupCameraPreferences(API.Rendering.GameCamera);
            SetupCameraPreferences(API.Rendering.UICamera);
            DisableMenuBorders();
        }

        public void ModObjectLoaded(UnityEngine.Object obj) { }

        public void Shutdown()
        {
        }

        public void Update()
        {
            // Game camera has it's own ortho size calculation,
            // and I'm not gonna touch it because I don't know what they really
            // needed it for
            UpdateCamera(API.Rendering.GameCamera, false);
            UpdateCamera(API.Rendering.UICamera);
        }
        public bool CanBeUnloaded() => false;

        private void SetupCameraPreferences(PugCamera camera)
        {
            camera.SetPreferredOutputMode(OutputMode.MatchAspect);
            camera.minOutputWidth = camera.outputWidth;
        }

        private void UpdateCamera(PugCamera pugCamera, bool updateOrthographicSize = true)
        {
            if (pugCamera == null || pugCamera.outputWidth == 0)
                return;

            var srcCamera = pugCamera.camera;
            var aspect = UnityEngine.Mathf.Min(srcCamera.pixelRect.width / srcCamera.pixelRect.height, ASPECT_16_9);

            float scaledHeight = pugCamera.outputWidth / aspect;
            var height = (int)scaledHeight;
            if (height % 2 != 0)
            {
                height += 1;
            }
            pugCamera.outputHeight = height;

            if (updateOrthographicSize)
            {
                pugCamera.camera.orthographicSize = scaledHeight / 32;
            }
        }

        private void DisableMenuBorders()
        {
            var pauseMenuBorders = Manager.menu.pauseMenu.transform.Find("borders");
            if (pauseMenuBorders != null)
            {
                pauseMenuBorders.gameObject.SetActive(false);
            }
        }
    }
}
