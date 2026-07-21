using System;
using UnityEngine;

internal sealed class RtsSelectionInputController
{
    private readonly float dragThreshold;

    public bool IsDragging { get; private set; }
    public Vector2 DragStart { get; private set; }
    public Vector2 DragCurrent { get; private set; }

    public RtsSelectionInputController(float threshold)
    {
        dragThreshold = threshold;
    }

    public void TickSelection(
        bool enabled,
        Func<bool> isPointerBlocked,
        Action onSingleClick,
        Action onDragSelection
    )
    {
        if (!enabled)
        {
            IsDragging = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (isPointerBlocked())
            {
                return;
            }

            IsDragging = true;
            DragStart = Input.mousePosition;
            DragCurrent = DragStart;
        }

        if (IsDragging && Input.GetMouseButton(0))
        {
            DragCurrent = Input.mousePosition;
        }

        if (!IsDragging || !Input.GetMouseButtonUp(0))
        {
            return;
        }

        IsDragging = false;
        DragCurrent = Input.mousePosition;

        if (isPointerBlocked())
        {
            return;
        }

        if (Vector2.Distance(DragStart, DragCurrent) >= dragThreshold)
        {
            onDragSelection();
        }
        else
        {
            onSingleClick();
        }
    }

    public bool ConsumeCommandClick(bool enabled, Func<bool> isPointerBlocked)
    {
        return enabled && Input.GetMouseButtonDown(1) && !isPointerBlocked();
    }
}
