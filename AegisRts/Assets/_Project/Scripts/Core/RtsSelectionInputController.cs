using System;
using UnityEngine;

internal sealed class RtsSelectionInputController
{
    private readonly float dragThreshold;

    public bool IsDragging { get; private set; }
    public bool IsBoxSelecting => IsDragging && !isDirectMoveGesture;
    public Vector2 DragStart { get; private set; }
    public Vector2 DragCurrent { get; private set; }

    private bool isDirectMoveGesture;

    public RtsSelectionInputController(float threshold)
    {
        dragThreshold = threshold;
    }

    public void TickSelection(
        bool enabled,
        Func<bool> isPointerBlocked,
        Func<bool> tryBeginDirectMove,
        Action onSingleClick,
        Action onDragSelection,
        Action onDirectMove
    )
    {
        if (!enabled)
        {
            IsDragging = false;
            isDirectMoveGesture = false;
            return;
        }

        if (Input.GetMouseButtonDown(0))
        {
            if (isPointerBlocked())
            {
                return;
            }

            IsDragging = true;
            isDirectMoveGesture = tryBeginDirectMove != null && tryBeginDirectMove();
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

        bool wasDirectMoveGesture = isDirectMoveGesture;
        IsDragging = false;
        isDirectMoveGesture = false;
        DragCurrent = Input.mousePosition;

        if (isPointerBlocked())
        {
            return;
        }

        if (Vector2.Distance(DragStart, DragCurrent) >= dragThreshold)
        {
            if (wasDirectMoveGesture)
            {
                onDirectMove();
            }
            else
            {
                onDragSelection();
            }
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
