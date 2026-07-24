using System;
using UnityEngine;
using UnityEngine.EventSystems;

internal sealed class MinimapPointerHandler : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private RectTransform target;
    private Action<Vector2> navigate;

    public void Configure(RectTransform minimapTarget, Action<Vector2> navigationCallback)
    {
        target = minimapTarget;
        navigate = navigationCallback;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        Navigate(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        Navigate(eventData);
    }

    private void Navigate(PointerEventData eventData)
    {
        if (target == null ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                target,
                eventData.position,
                eventData.pressEventCamera,
                out Vector2 localPoint
            ))
        {
            return;
        }

        Rect rect = target.rect;
        Vector2 normalized = new Vector2(
            Mathf.InverseLerp(rect.xMin, rect.xMax, localPoint.x),
            Mathf.InverseLerp(rect.yMin, rect.yMax, localPoint.y)
        );
        navigate?.Invoke(normalized);
    }
}
