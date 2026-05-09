// Assets/Scripts/World/Interaction/IInteractable.cs

/// Будь-який обʼєкт в 3D сцені що може реагувати на клік гравця.
/// Реалізується на компоненті що знаходиться на root GO або будь-якому дочірньому.
/// InteractionRouter знаходить його через GetComponentInParent.
public interface IInteractable
{
    /// Чи може взаємодіяти зараз (враховує GameState, EditMode тощо)
    bool CanInteract { get; }

    /// Виклик при кліку
    void OnInteract();
}