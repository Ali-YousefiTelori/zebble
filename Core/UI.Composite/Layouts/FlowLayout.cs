﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Olive;

namespace Zebble
{
    public class FlowLayout : Canvas, IAutoContentHeightProvider
    {
        readonly AsyncEvent HeightChanged = new AsyncEvent();

        AsyncEvent IAutoContentHeightProvider.Changed => HeightChanged;
        float IAutoContentHeightProvider.Calculate() => CurrentChildren.MaxOrDefault(x => x.ActualBottom) + Padding.Bottom();

        bool IAutoContentHeightProvider.DependsOnChildren() => true;

        public override async Task OnInitializing()
        {
            await base.OnInitializing();

            Width.Changed.Handle(LayoutChildren);
            Padding.Left.Changed.Handle(LayoutChildren);
            Padding.Top.Changed.Handle(LayoutChildren);
            Padding.Right.Changed.Handle(LayoutChildren);
            ParentSet.Handle(LayoutChildren);
        }

        protected override async Task ChildAdded(View view)
        {
            await base.ChildAdded(view);

            foreach (var length in new[] { view.Width , view.Height,
                view.Margin.Left, view.Margin.Right, view.Margin.Bottom, view.Margin.Top})
                length.Changed.Handle(LayoutChildren);

            LayoutChildren();
        }

        public void LayoutChildren()
        {
            if (parent == null) return;

            if (UIRuntime.IsDevMode)
            {
                if (CurrentChildren.Any(v => v.Width.AutoOption == Length.AutoStrategy.Container))
                    throw new Exception("Width of a child of FlowLayout cannot be based on Container.");

                if (CurrentChildren.Any(v => v.Height.AutoOption == Length.AutoStrategy.Container))
                    throw new Exception("Height of a child of FlowLayout cannot be based on Container.");
            }

            var paddingLeft = Padding.Left.CurrentValue;

            var x = paddingLeft;
            var y = Padding.Top.CurrentValue;
            var totalWidth = ActualWidth - Padding.Horizontal();

            var maxY = y;

            foreach (var child in CurrentChildren)
            {
                var marginHorizontal = child.Margin.Horizontal();
                var marginLeft = child.Margin.Left.CurrentValue;

                var childWidth = child.ActualWidth + marginHorizontal;
                if (x + childWidth > totalWidth)
                {
                    // Move to next line
                    x = paddingLeft;
                    y = maxY;
                }

                child.X(x + marginLeft).Y(y);

                maxY = Math.Max(child.ActualBottom + child.Margin.Bottom(), maxY);
                x = child.ActualRight + child.Margin.Right.CurrentValue;
                // if (x >= totalWidth) x = paddingLeft; // next line
            }
        }
    }
}