using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BAKKA_Editor
{
    public class Cursor
    {
        private enum RolloverState
        {
            None,
            Counterclockwise,
            Clockwise
        }

        public uint Position { get; private set; }
        public uint DragPosition { get; private set; }
        public uint Size { get; private set; }
        public uint MinimumSize { get; private set; }
        public uint MaximumSize { get; private set; }
        public uint Depth { get; private set; }
        public uint MinimumDepth { get; private set; }
        public uint MaximumDepth { get; private set; }
        public bool WasDragged { get; private set; }

        private uint _previousDragPosition = 0;
        private int _relativeDragPosition = 0;
        private uint _initialDragPosition = 0;
        private uint _dragCounter = 0;
        private RolloverState _dragRolloverState = RolloverState.None;

        private const uint DragDetectionThreshold = 5;

        public Cursor()
        {
            Position = 0;
            DragPosition = 0;
            Size = 4;
            MinimumSize = 4;
            MaximumSize = 60;
            Depth = 0;
            MinimumDepth = 0;
            MaximumDepth = 0;
            WasDragged = false;
        }

        /// <summary>
        /// Sets the cursor's size bounds. Resizes the cursor if it is outside the new bounds.
        /// </summary>
        /// <param name="minimumSize">Minimum cursor size</param>
        /// <returns>Updated cursor size</returns>
        public uint ConfigureSize(uint minimumSize)
        {
            MinimumSize = minimumSize;
            return Resize(Size);
        }

        /// <summary>
        /// Sets the cursor's depth bounds. Moves the cursor depth if it is outside the new bounds.
        /// </summary>
        /// <param name="maximumDepth">Maximum cursor depth</param>
        /// <returns>Updated cursor depth</returns>
        public uint ConfigureDepth(uint maximumDepth)
        {
            MaximumDepth = maximumDepth;
            return Dive(Depth);
        }

        /// <summary>
        /// Moves the cursor's position around the ring.
        /// </summary>
        /// <param name="position">Position around the circle where 0 is the start. Values increase counterclockwise.</param>
        /// <returns>Updated cursor position</returns>
        public uint Move(uint position)
        {
            Position = position;

            // If the cursor is moving, it is not being dragged. Sync it with the current position
            // to start at the correct position when a drag starts.
            DragPosition = position;
            _previousDragPosition = position;
            _relativeDragPosition = 0;
            _initialDragPosition = position;
            _dragRolloverState = RolloverState.None;
            _dragCounter = 0;
            WasDragged = false;

            return Position;
        }

        /// <summary>
        /// Moves the cursor's depth into the chart.
        /// </summary>
        /// <param name="depth">Depth into the chart where 0 is the outermost ring. Values increase deeper into the chart.
        /// This represents which beat the cursor is modifying.</param>
        /// <returns>Updated cursor depth</returns>
        public uint Dive(uint depth)
        {
            Depth = Math.Clamp(depth, MinimumDepth, MaximumDepth);
            return Depth;
        }

        /// <summary>
        /// Changes the cursor size.
        /// </summary>
        /// <param name="size">Cursor size</param>
        /// <returns>Updated cursor size</returns>
        public uint Resize(uint size)
        {
            Size = Math.Clamp(size, MinimumSize, MaximumSize);
            return Size;
        }

        /// <summary>
        /// Drags the cursor which changes its size and position.
        /// </summary>
        /// <param name="position">Position of the cursor during the drag</param>
        public void Drag(uint position)
        {
            // There are 2 ways that we detect that a drag occurred.
            // 1. This function should get called every time the mouse moves. Therefore, we can
            //    detect a drag by seeing if the moused moved enough. This covers the case where
            //    active cursor tracking is disabled (therefore, clicks do not always place notes),
            //    and the user wants to insert a size 1 note by dragging without the cursor
            //    changing position.
            // 2. The cursor moved to a different position.
            if (_dragCounter < DragDetectionThreshold)
            {
                _dragCounter++;
            }

            if (_dragCounter >= DragDetectionThreshold)
            {
                WasDragged = true;
            }

            if (position == DragPosition)
            {
                // Only update if the position changed. This ensures that the current and
                // previous drag positions are different to properly tell which direction
                // the cursor moved.
                return;
            }

            _previousDragPosition = DragPosition;
            DragPosition = position;
            WasDragged = true;

            // Rollover calculation is tricky. You could move the mouse through the center of the circle
            // which technically isn't moving clockwise or counterclockwise. Assume that the shorter of
            // clockwise vs counterclockwise deltas is the direction we moved in. If we moved perfectly
            // through the center of the circle such that the deltas are equal, choose counterclockwise.
            int deltaClockwise = ((int)_previousDragPosition + 60 - (int)DragPosition) % 60;
            int deltaCounterclockwise = ((int)DragPosition + 60 - (int)_previousDragPosition) % 60;
            bool movedClockwise = deltaClockwise < deltaCounterclockwise;

            switch (_dragRolloverState)
            {
                case RolloverState.Counterclockwise:
                {
                    // If rolled over counterclockwise, the mouse moved clockwise, and mouse down position
                    // is between the delta, we are no longer rolled over
                    int delta = (((int)_initialDragPosition + 60 - (int)DragPosition) % 60);
                    if (movedClockwise && delta <= deltaClockwise)
                    {
                        _dragRolloverState = RolloverState.None;
                        _relativeDragPosition -= delta;
                    }
                }
                break;

                case RolloverState.Clockwise:
                {
                    // If rolled over clockwise, the mouse moved counterclockwise, and mouse down position
                    // is between the delta, we are no longer rolled over
                    int delta = (((int)DragPosition + 60 - ((int)_initialDragPosition) - 1) % 60);
                    if (!movedClockwise && delta <= deltaCounterclockwise)
                    {
                        _dragRolloverState = RolloverState.None;
                        _relativeDragPosition += delta + 1;
                    }
                }
                break;

                default:
                {
                    if (movedClockwise)
                    {
                        _relativeDragPosition = Math.Max(_relativeDragPosition - deltaClockwise, -60);

                        if (_relativeDragPosition <= -60)
                        {
                            _dragRolloverState = RolloverState.Clockwise;
                        }
                    }
                    else
                    {
                        _relativeDragPosition = Math.Min(_relativeDragPosition + deltaCounterclockwise, 60);

                        if (_relativeDragPosition >= 60)
                        {
                            _dragRolloverState = RolloverState.Counterclockwise;
                        }
                    }
                }
                break;
            }

            // Calculate size and position based on mouse click position and relative drag position.
            // First, on a drag, the initial size is reset to the minimum size. If moving
            // counterclockwise, the size grows in that direction. Otherwise, if moving clockwise,
            // the position shifts clockwise by the minimum size before growing in that direction.
            // Essentially, we draw the cursor across the dragged positions which the initial shifting
            // accomplishes when moving clockwise.
            if (_relativeDragPosition >= 0)
            {
                // Counterclockwise of mouse down position
                Position = _initialDragPosition;
                Size = (uint)Math.Clamp(_relativeDragPosition + 1, (int)MinimumSize, MaximumSize);
            }
            else
            {
                // Clockwise of mouse down position
                if (_relativeDragPosition >= -(MinimumSize - 1))
                {
                    // Shift
                    Position = (uint)(((int)_initialDragPosition + 60 + _relativeDragPosition) % 60);
                    Size = MinimumSize;
                }
                else
                {
                    // Grow
                    Position = (uint)(((int)_initialDragPosition + 60 + _relativeDragPosition) % 60);
                    Size = (uint)Math.Min(MinimumSize - (_relativeDragPosition + (MinimumSize - 1)), MaximumSize);
                }
            }
            Debug.WriteLine($"{Size}");
        }
    }
}
