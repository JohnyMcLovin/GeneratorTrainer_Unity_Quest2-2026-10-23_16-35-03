using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Command-line Quest 2 smoke build:
// Unity.exe -batchmode -quit -projectPath . -buildTarget Android -executeMethod QuestSmokeBuild.Build
public static class QuestSmokeBuild
{
    const string Scene = "Assets/Scenes/KeyboardTest.unity";
    const string Output = "Builds/QuestSmoke.apk";

    [MenuItem("Tools/Generator Trainer/Build Quest Smoke APK")]
    public static void Build()
    {
        var android = NamedBuildTarget.Android;
        PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
        PlayerSettings.SetApplicationIdentifier(android, "com.xrproject.generatortrainer");
        PlayerSettings.colorSpace = ColorSpace.Linear;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { Scene },
            locationPathName = Output,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.Development
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;
        Debug.Log($"[QuestSmokeBuild] result={s.result} errors={s.totalErrors} warnings={s.totalWarnings} " +
                  $"size={s.totalSize} time={s.totalTime} output={s.outputPath}");
        if (s.result != BuildResult.Succeeded)
        {
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw new Exception("Quest smoke build failed: " + s.result);
        }
    }
}
