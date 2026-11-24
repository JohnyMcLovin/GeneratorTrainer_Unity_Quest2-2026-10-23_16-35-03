# Photon Fusion setup and patches

SDK installed: Fusion **2.1.4 Stable** (build 2500), on Unity **2023.1.22f1**.

## Photon App ID (every teammate, once per computer)

The repository is public, so the Photon App ID is **not committed**: `.gitignore` excludes
`Assets/Photon/Fusion/Resources/PhotonAppSettings.asset` (and its `.meta`). Ask the team for the App ID privately, then:

1. Open the project in Unity.
2. **Tools > Fusion > Fusion Hub**, paste the App ID on the Welcome page. "Step 1" turns green.

Everyone must use the **same** App ID, otherwise players cannot meet in a session.
Symptom if it is missing: *Create session* / *Join* fails with an error instead of showing a session code.
Builds made after this step contain the App ID, so players of a build need nothing.

## Local patches to the SDK

Local changes made to the Photon Fusion SDK in `Assets/Photon/`. **Re-importing or updating Fusion overwrites them**, so reapply each one afterwards.

### 1. `GetAssetFolderInfo` error on Unity 2023.1

**Symptom:** after every script reload the Console shows
`System.InvalidOperationException: ... UnityEditor.AssetDatabase.GetAssetFolderInfo ... No method found matching name "GetAssetFolderInfo"`.

**Cause:** Fusion's editor code looks up an internal Unity method by name. It assumes Unity renamed
`GetAssetFolderInfo` to `TryGetAssetFolderInfo` in Unity 6, but Unity 2023.1 already has the new name.
Unity 2023.1 is not on Fusion's officially supported list (2021.3, 2022.3, 6.0, 6.3).

**Fix:** in `Assets/Photon/Fusion/Editor/Fusion.Unity.Editor.cs`, in `UnityInternal.AssetDatabase`
(search for `"GetAssetFolderInfo"`), change

```csharp
#if UNITY_6000_0_OR_NEWER
        nameof(TryGetAssetFolderInfo)
```

to

```csharp
#if UNITY_2023_1_OR_NEWER
        nameof(TryGetAssetFolderInfo)
```

The patched line is marked with a `// PROJECT PATCH` comment.
