using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GeneratorTrainer.Editor
{
    // Milestone 7: builds the multiplayer scene as a Meta Quest 2 app (.apk). The same scene and the same Photon
    // App ID as the desktop .exe, so desktop and VR players can join the same session. In the headset the app starts
    // in VR mode automatically (SessionLauncher detects the active headset).
    //
    // Settings used for Quest: Android, IL2CPP, ARM64 only, minimum API 29, linear colour, internet permission
    // (needed for Photon). The OpenXR / Meta Quest settings themselves live in Project Settings > XR Plug-in Management.
    // Note: building switches the editor's platform to Android (a one-time re-import that can take several minutes).
    // Switch back with File > Build Settings > Windows before building the desktop .exe again.
    //
    // Batch: Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod GeneratorTrainer.Editor.QuestBuild.Build
    public static class QuestBuild
    {
        const string Output = "Builds/Quest/GeneratorTrainerVR.apk";

        [MenuItem("Tools/Generator Trainer/8 - Build Quest App (APK)")]
        public static void Build() { Run(false); }

        [MenuItem("Tools/Generator Trainer/9 - Build and Run on Connected Quest")]
        public static void BuildAndRun() { Run(true); }

        static void Run(bool runOnDevice)
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("Exit Play Mode first."); return; }
            if (!Application.isBatchMode && EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUtility.DisplayDialog("Quest build",
                    "This switches the editor's platform to Android (a one-time re-import that can take several minutes). " +
                    "Switch back to Windows in File > Build Settings before building the desktop .exe again.\n\nContinue?",
                    "Build for Quest", "Cancel"))
                return;

            var android = NamedBuildTarget.Android;
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.forceInternetPermission = true;   // Photon needs internet access
            PlayerSettings.SetApplicationIdentifier(android, "com.xrproject.generatortrainer");
            PlayerSettings.colorSpace = ColorSpace.Linear;

            Directory.CreateDirectory(Path.GetDirectoryName(Output));
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MultiplayerSceneBuilder.ScenePath },
                locationPathName = Output,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = runOnDevice ? BuildOptions.AutoRunPlayer : BuildOptions.None,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            Debug.Log("[QuestBuild] result=" + s.result + " errors=" + s.totalErrors + " size=" + s.totalSize +
                      " time=" + s.totalTime + " output=" + s.outputPath);
            if (s.result != BuildResult.Succeeded)
            {
                if (Application.isBatchMode) EditorApplication.Exit(1);
                else throw new Exception("Quest build failed: " + s.result);
            }
        }
    }
}
