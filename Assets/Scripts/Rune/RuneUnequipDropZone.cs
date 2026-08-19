using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class RuneUnequipDropZone : MonoBehaviour, IDropHandler
{
    private RuneWeaponPanelController controller;

    public void Initialize(RuneWeaponPanelController owner)
    {
        controller = owner;
    }

    public void OnDrop(PointerEventData eventData)
    {
        RuneEquippedDragItem item = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<RuneEquippedDragItem>()
            : null;
        if (item == null || controller == null) return;

        if (controller.RequestUnequipRune(item.HeroId))
        {
            item.Consume();
        }
    }
}
