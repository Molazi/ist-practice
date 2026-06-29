using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class ShovelController : MonoBehaviour
{
    public Transform toolTip;
    public TerrainDeformer deformer;
    public float strength = 0.8f;
    public float brushSize = 2.5f;
    public Mode currentMode = Mode.Dig;

    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    private bool isHeld = false;
    private bool isActivating = false;

    public enum Mode { Dig, Raise }

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnGrab);
            interactable.selectExited.AddListener(OnRelease);
            interactable.activated.AddListener(OnActivate);
            interactable.deactivated.AddListener(OnDeactivate);
        }
        if (deformer == null)
            deformer = FindAnyObjectByType<TerrainDeformer>();
        if (toolTip == null)
            Debug.LogError("ToolTip not assigned");
    }

    void OnDestroy()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnGrab);
            interactable.selectExited.RemoveListener(OnRelease);
            interactable.activated.RemoveListener(OnActivate);
            interactable.deactivated.RemoveListener(OnDeactivate);
        }
    }

    void Update()
    {
        if (isHeld && isActivating && toolTip != null && deformer != null)
        {
            float eff = (currentMode == Mode.Dig) ? -strength : strength;
            deformer.Deform(toolTip.position, eff * Time.deltaTime, brushSize);
        }
    }

    private void OnGrab(SelectEnterEventArgs args) => isHeld = true;
    private void OnRelease(SelectExitEventArgs args) => isHeld = false;
    private void OnActivate(ActivateEventArgs args) => isActivating = true;
    private void OnDeactivate(DeactivateEventArgs args) => isActivating = false;

    public void ToggleMode() => currentMode = currentMode == Mode.Dig ? Mode.Raise : Mode.Dig;
    public void SetStrength(float val) => strength = val;
    public void SetBrushSize(float val) => brushSize = val;
}