using UnityEngine;

/// <summary>Muestra un pensamiento breve al examinar un objeto con E.</summary>
[DisallowMultipleComponent]
public sealed class ObjectThoughtInteractable : MonoBehaviour, IInteractable
{
    [SerializeField] private string objectName;
    [SerializeField, TextArea] private string thought;

    public string InteractionPrompt => objectName + "\nE: Examinar";

    private void Awake()
    {
        if (GetComponentInChildren<Collider>(true) != null) return;
        Renderer renderer = GetComponentInChildren<Renderer>(true);
        if (renderer == null) return;
        // Los modelos importados de la radio no traen collider.
        BoxCollider collider = renderer.gameObject.AddComponent<BoxCollider>();
        MeshFilter mesh = renderer.GetComponent<MeshFilter>();
        if (mesh != null && mesh.sharedMesh != null)
        {
            collider.center = mesh.sharedMesh.bounds.center;
            collider.size = mesh.sharedMesh.bounds.size;
        }
        collider.isTrigger = true;
    }

    public bool CanInteract(GameObject interactor)
    {
        return isActiveAndEnabled && interactor != null
            && interactor.GetComponent<PlayerInteractor>() != null
            && GameSessionManager.Instance != null && GameSessionManager.Instance.IsPlaying;
    }

    public void Interact(GameObject interactor)
    {
        if (CanInteract(interactor)) StoryProgression.Instance?.ShowObservation(thought);
    }
}
