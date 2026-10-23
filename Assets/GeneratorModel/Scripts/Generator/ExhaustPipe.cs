using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
// Part A: click the exhaust pipe to swing it 180° toward the vent.
[RequireComponent(typeof(XRSimpleInteractable))]
public class ExhaustPipe : MonoBehaviour
{
    XRSimpleInteractable interactable;

    void Awake() => interactable = GetComponent<XRSimpleInteractable>();
    void OnEnable() => interactable.selectEntered.AddListener(OnSelect);
    void OnDisable() => interactable.selectEntered.RemoveListener(OnSelect);

    void OnSelect(SelectEnterEventArgs _)
    {
        transform.Rotate(0f, -180f, 0f, Space.Self);
        Debug.Log("[Exhaust] rotated");
    }
}