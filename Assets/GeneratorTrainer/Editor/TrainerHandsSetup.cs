using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
namespace GeneratorTrainer.Editor
{
    // Editor-only reflection keeps the base kit importable without XR dependencies.
    public static class TrainerHandsSetup
    {
        const string Prefix = "UnityEngine.XR.Interaction.Toolkit.";
        [MenuItem("Tools/Generator Trainer/2 - Enable Hand Poke on Selected Trainer")]
        public static void Configure()
        {
            if (EditorApplication.isPlaying) return;
            var selected = Selection.activeGameObject;
            var trainer = selected ? selected.GetComponentInParent<GeneratorTraining>() : null;
            if (!trainer || EditorUtility.IsPersistent(trainer)) { EditorUtility.DisplayDialog("Select trainer", "Select the GeneratorTrainer instance in the Hierarchy.", "OK"); return; }
            Type simpleType = FindType(Prefix + "XRSimpleInteractable"), filterType = FindType(Prefix + "Filtering.XRPokeFilter"), dataType = FindType(Prefix + "Filtering.PokeThresholdData"), datumType = FindType(Prefix + "Filtering.PokeThresholdDatumProperty");
            if (simpleType == null || filterType == null || dataType == null || datumType == null) { EditorUtility.DisplayDialog("XR packages", "Install XR Interaction Toolkit 2.5.4 and XR Hands 1.3.0. Import Hands Interaction Demo and Starter Assets. Follow SETUP.md.", "OK"); return; }
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(simpleType.Assembly);
            if (package == null || !package.version.StartsWith("2.5.", StringComparison.Ordinal)) { EditorUtility.DisplayDialog("XRI version", "This optional binding targets XRI 2.5.x. See SETUP.md. The base model does not require XRI.", "OK"); return; }
            Undo.IncrementCurrentGroup(); int undo = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Bind trainer hand controls");
            try
            {
                foreach (var ctrl in trainer.GetComponentsInChildren<TrainerControl>(true))
                {
                    var hit = ctrl.GetComponent<BoxCollider>();
                    Component simple = GetOrAdd(ctrl.gameObject, simpleType); Undo.RecordObject(simple, "Configure hand target");
                    IList list = (IList)RequireProperty(simpleType, "colliders").GetValue(simple); list.Clear(); list.Add(hit);
                    var mode = RequireProperty(simpleType, "selectMode"); mode.SetValue(simple, Enum.Parse(mode.PropertyType, "Single"));
                    BindOnce((UnityEventBase)RequireProperty(simpleType, "selectEntered").GetValue(simple), ctrl.Press);
                    BindOnce((UnityEventBase)RequireProperty(simpleType, "selectExited").GetValue(simple), ctrl.Release);
                    Component filter = GetOrAdd(ctrl.gameObject, filterType); Undo.RecordObject(filter, "Configure poke");
                    object data = Activator.CreateInstance(dataType); var direction = RequireProperty(dataType, "pokeDirection");
                    direction.SetValue(data, Enum.Parse(direction.PropertyType, ctrl.reversePoke ? "PositiveZ" : "NegativeZ"));
                    RequireProperty(dataType, "interactionDepthOffset").SetValue(data, .003f);
                    RequireProperty(dataType, "enablePokeAngleThreshold").SetValue(data, false);
                    RequireProperty(dataType, "pokeAngleThreshold").SetValue(data, 80f);
                    RequireProperty(filterType, "pokeConfiguration").SetValue(filter, Activator.CreateInstance(datumType, new object[] { data }));
                    RequireProperty(filterType, "pokeCollider").SetValue(filter, hit); RequireProperty(filterType, "pokeInteractable").SetValue(filter, simple);
                    EditorUtility.SetDirty(simple); EditorUtility.SetDirty(filter);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(simple); PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
                }
                Undo.RecordObject(trainer,"Disable desktop input"); trainer.desktopControls = false;
                PrefabUtility.RecordPrefabInstancePropertyModifications(trainer); EditorSceneManager.MarkSceneDirty(trainer.gameObject.scene); Undo.CollapseUndoOperations(undo);
                Debug.Log("Hand poke bound. Add the Hands Interaction Demo rig, disable DesktopCamera, and save the scene. This command does not install an XR rig or change OpenXR settings.", trainer);
            }
            catch (Exception e) { Undo.RevertAllDownToGroup(undo); Debug.LogException(e); }
        }
        [MenuItem("Tools/Generator Trainer/3 - Optional Grabbable Diesel Can")]
        public static void EnableCanGrab()
        {
            var selected = Selection.activeGameObject;
            var trainer = selected ? selected.GetComponentInParent<GeneratorTraining>() : null;
            var grabType = FindType(Prefix + "XRGrabInteractable");
            if (!trainer || grabType == null || EditorUtility.IsPersistent(trainer)) { Debug.LogWarning("Select a scene trainer and install XRI 2.5.4 first."); return; }
            var pkg = UnityEditor.PackageManager.PackageInfo.FindForAssembly(grabType.Assembly);
            if (pkg == null || !pkg.version.StartsWith("2.5.", StringComparison.Ordinal)) { Debug.LogWarning("Optional grab targets XRI 2.5.x."); return; }
            var can = trainer.GetComponentInChildren<DieselCan>();
            if (can == null) return;
            Undo.IncrementCurrentGroup(); int group = Undo.GetCurrentGroup(); Undo.SetCurrentGroupName("Enable physical fuel can");
            var hit = can.GetComponentInChildren<TrainerControl>();
            if (hit) { Undo.RecordObject(hit.gameObject,"Disable snap can target"); hit.gameObject.SetActive(false); }
            var rb = (Rigidbody)GetOrAdd(can.gameObject, typeof(Rigidbody)); Undo.RecordObject(rb,"Configure can body"); rb.useGravity = false; rb.isKinematic = true;
            Component grab = GetOrAdd(can.gameObject, grabType); Undo.RecordObject(grab,"Configure can grab");
            IList cols = (IList)RequireProperty(grabType,"colliders").GetValue(grab); cols.Clear();
            foreach (var col in can.GetComponentsInChildren<Collider>()) if (!col.isTrigger) cols.Add(col);
            // Instantaneous hand tracking is stable and adequate for a lightweight training prop.
            var movement = RequireProperty(grabType,"movementType"); movement.SetValue(grab, Enum.Parse(movement.PropertyType,"Instantaneous"));
            RequireProperty(grabType,"throwOnDetach").SetValue(grab,false);
            EditorUtility.SetDirty(grab); PrefabUtility.RecordPrefabInstancePropertyModifications(grab); PrefabUtility.RecordPrefabInstancePropertyModifications(rb);
            EditorSceneManager.MarkSceneDirty(trainer.gameObject.scene); Undo.CollapseUndoOperations(group);
            Debug.Log("Can grab enabled. At D3 pinch-grab, bring the can to the filler, tilt >60 degrees and hold for 5 seconds. Undo to restore snap-assisted can.");
        }
        static Component GetOrAdd(GameObject go, Type type)
        {
            Component value = go.GetComponent(type);
            return value != null ? value : Undo.AddComponent(go, type);
        }
        static Type FindType(string fullName)
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            { Type type = assembly.GetType(fullName, false); if (type != null) return type; }
            return null;
        }
        static PropertyInfo RequireProperty(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            if (property == null) throw new MissingMemberException(type.FullName, name);
            return property;
        }
        static void BindOnce(UnityEventBase target, UnityAction action)
        {
            for (int i = 0; i < target.GetPersistentEventCount(); i++)
                if (target.GetPersistentTarget(i) == (action.Target as UnityEngine.Object) &&
                    target.GetPersistentMethodName(i) == action.Method.Name) return;
            UnityEventTools.AddVoidPersistentListener(target, action);
        }
    }
}
