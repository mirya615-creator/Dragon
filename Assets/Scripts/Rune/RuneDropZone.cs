using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class RuneDropZone : MonoBehaviour, IDropHandler
{
    private RuneWeaponPanelController controller;
    private TMP_Text runeNameText;

    public string HeroId { get; private set; }

    public void Initialize(
        RuneWeaponPanelController owner,
        string heroId,
        TMP_Text targetText)
    {
        controller = owner;
        HeroId = heroId;
        runeNameText = targetText;
    }

    public void OnDrop(PointerEventData eventData)
    {
        RuneDragItem item = eventData.pointerDrag != null
            ? eventData.pointerDrag.GetComponent<RuneDragItem>()
            : null;
        if (item == null || controller == null) return;

        if (controller.RequestEquipRune(HeroId, item.RuneId))
        {
            item.Consume();
        }
    }

    public void SetRuneName(string displayName)
    {
        if (runeNameText != null) runeNameText.text = displayName ?? string.Empty;
    }
}
