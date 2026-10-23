using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// Part A step 4: open the control panel door using the handle.
// Sits on Part_DoorPivot (pivot is at the hinge); DoorHandle is the clickable part.
public class PanelDoor : MonoBehaviour
{
    [SerializeField] XRSimpleInteractable handle;   // drag DoorHandle here
    [SerializeField] float openAngle = -100f;       // sign = opens outward
    [SerializeField] Vector3 axis = Vector3.up;     // local hinge axis

    public bool IsOpen { get; private set; }

    Quaternion closedRotation;

    void Awake() => closedRotation = transform.localRotation;
    void OnEnable() => handle.selectEntered.AddListener(OnSelect);
    void OnDisable() => handle.selectEntered.RemoveListener(OnSelect);

    void OnSelect(SelectEnterEventArgs _)
    {
        IsOpen = !IsOpen;
        transform.localRotation = closedRotation * Quaternion.AngleAxis(IsOpen ? openAngle : 0f, axis);
        Debug.Log($"[Door] {(IsOpen ? "opened" : "closed")}");
    }
}

