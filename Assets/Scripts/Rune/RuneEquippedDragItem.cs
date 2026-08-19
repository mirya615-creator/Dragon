using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public sealed class RuneEquippedDragItem : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public string HeroId { get; private set; }
    public string RuneId { get; private set; }

    private RuneWeaponPanelController controller;
    private RuneDropZone dropZone;
    private RectTransform proxy;
    private bool consumed;

    public void Initialize(RuneWeaponPanelController owner, string heroId, RuneDropZone zone)
    {
        controller = owner;
        HeroId = heroId;
        dropZone = zone;
    }

    public void SetRuneId(string runeId)
    {
        RuneId = runeId;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(RuneId) || controller == null) return;

        consumed = false;
        proxy = controller.SpawnUnequipProxy(RuneId);
        if (dropZone != null) dropZone.SetRuneName(string.Empty);
        if (proxy != null) proxy.position = eventData.position;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (proxy != null) proxy.position = eventData.position;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        DestroyProxy();

        if (consumed) return;
        controller.RestoreHeroRuneNames();
    }

    private void OnDisable()
    {
        DestroyProxy();
    }

    public void Consume()
    {
        consumed = true;
    }

    private void DestroyProxy()
    {
        if (proxy == null) return;
        Destroy(proxy.gameObject);
        proxy = null;
    }
}
