using HarmonyLib;
using Pug.RP;
using PugMod;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Sashiri
{
    [HarmonyPatch]
    public class SendClientSubMapToPugMapSystem_GatherTileUpdates_Patch
    {
        public struct GatherTileUpdates_Job : IJob
        {
            public void Execute()
            {
                OriginalLambdaBody(ref this);
            }

            public NativeList<SendClientSubMapToPugMapSystem.LayerData> layerDataListLocal;
            public NativeList<SendClientSubMapToPugMapSystem.TileUpdate> tileUpdatesLocal;
            public NativeList<SendClientSubMapToPugMapSystem.TileOverride> tileOverridesLocal;
            public float2 cameraPos;
            public int2 subMapSize;
        }

        [HarmonyPatch("SendClientSubMapToPugMapSystem+GatherTileUpdates_Job", "OriginalLambdaBody")]
        [HarmonyReversePatch]
        static void OriginalLambdaBody(ref GatherTileUpdates_Job instance) => throw new NotImplementedException();
    }

    [HarmonyPatch(typeof(SendClientSubMapToPugMapSystem))]
    static class SendClientSubMapToPugMapSystem_Patch
    {
        [HarmonyPatch("GatherTileUpdates_Execute")]
        [HarmonyPrefix]
        static bool GatherTileUpdates_Execute(
            NativeList<SendClientSubMapToPugMapSystem.LayerData> layerDataListLocal,
            NativeList<SendClientSubMapToPugMapSystem.TileUpdate> tileUpdatesLocal,
            NativeList<SendClientSubMapToPugMapSystem.TileOverride> tileOverridesLocal,
            float2 cameraPos,
            int2 subMapSize,
            object __instance
        )
        {
            var gatherTileUpdates_Job = new SendClientSubMapToPugMapSystem_GatherTileUpdates_Patch.GatherTileUpdates_Job
            {
                layerDataListLocal = layerDataListLocal,
                tileUpdatesLocal = tileUpdatesLocal,
                tileOverridesLocal = tileOverridesLocal,
                cameraPos = cameraPos,
                subMapSize = subMapSize,
            };

            var __base = (SystemBase)__instance;
            __base.CheckedStateRef.Dependency = gatherTileUpdates_Job.Schedule(__base.CheckedStateRef.Dependency);
            return false;
        }

        [HarmonyPatch("ComputeShouldBeViewedMemo")]
        [HarmonyTranspiler]

        static IEnumerable<CodeInstruction> ComputeShouldBeViewedMemo(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);
            codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)20),
                    new CodeMatch(OpCodes.Ldc_I4_S, (sbyte)16),
                    new CodeMatch(OpCodes.Call, AccessTools.Constructor(typeof(int2), new[] { typeof(int), typeof(int) }))
                );
            if (codeMatcher.IsValid)
            {
                codeMatcher.Advance(1);
                codeMatcher.InsertAndAdvance(new CodeInstruction(
                    OpCodes.Call,
                    AccessTools.Method(typeof(SendClientSubMapToPugMapSystem_Patch), nameof(ScaleHorizontalVisibilityView))
                ));
            }

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
