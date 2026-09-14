using DragonBound.Presentation;
using DragonBound.Runes;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class RuneDropZone : MonoBehaviour, IDropHandler
{
    private RuneWeaponPanelController controller;
    private Image runeImage;

    public string HeroId { get; private set; }
    public Image RuneImage => runeImage;

    public void Initialize(
        RuneWeaponPanelController owner,
        string heroId,
        Image targetImage)
    {
        controller = owner;
        HeroId = heroId;
        runeImage = targetImage;
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

    public void SetRune(RuneDefinition definition)
    {
        if (runeImage == null) return;

        if (definition == null)
        {
            runeImage.sprite = null;
            runeImage.enabled = true;
            runeImage.gameObject.SetActive(false);
            return;
        }

        string runtimeRuneId = RuneGameplayLoadoutAdapter.ResolveRuntimeRuneId(definition.RuneId);
        Sprite sprite = RuneUiSpriteCatalog.Load(runtimeRuneId);
        runeImage.sprite = sprite;
        runeImage.type = Image.Type.Simple;
        runeImage.preserveAspect = true;
        runeImage.color = Color.white;
        runeImage.enabled = true;
        runeImage.gameObject.SetActive(sprite != null);

        if (sprite == null)
        {
            Debug.LogError(
                $"WeaponPanel could not load the rune UI for '{definition.RuneId}' " +
                $"(runtime id '{runtimeRuneId}').",
                this);
        }
    }

    public void SetRuneImageVisible(bool visible)
    {
        if (runeImage != null) runeImage.enabled = visible;
    }
}
