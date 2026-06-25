using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;

namespace CdpInspectorApp.Controls
{
    public class Accordion : ItemsControl
    {
        public static readonly StyledProperty<bool> IsExclusiveProperty =
            AvaloniaProperty.Register<Accordion, bool>(nameof(IsExclusive), true);

        public bool IsExclusive
        {
            get => GetValue(IsExclusiveProperty);
            set => SetValue(IsExclusiveProperty, value);
        }

        protected override Type StyleKeyOverride => typeof(Accordion);

        protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        {
            recycleKey = null;
            return !(item is AccordionItem);
        }

        protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        {
            return new AccordionItem();
        }

        protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
        {
            base.PrepareContainerForItemOverride(container, item, index);
            if (container is AccordionItem accordionItem)
            {
                accordionItem.ParentAccordion = this;
            }
        }

        protected override void ClearContainerForItemOverride(Control container)
        {
            base.ClearContainerForItemOverride(container);
            if (container is AccordionItem accordionItem)
            {
                accordionItem.ParentAccordion = null;
            }
        }

        public void OnItemExpanded(AccordionItem expandedItem)
        {
            if (!IsExclusive) return;

            // 1. Check Items collection (direct items)
            foreach (var itemObj in Items)
            {
                if (itemObj is AccordionItem item && item != expandedItem)
                {
                    item.IsExpanded = false;
                }
            }

            // 2. Check realized containers (for data-bound items)
            for (int i = 0; i < Items.Count; i++)
            {
                var container = ContainerFromIndex(i) as AccordionItem;
                if (container != null && container != expandedItem)
                {
                    container.IsExpanded = false;
                }
            }

            // 3. Check LogicalChildren as fallback
            foreach (var child in LogicalChildren)
            {
                if (child is AccordionItem item && item != expandedItem)
                {
                    item.IsExpanded = false;
                }
            }
        }
    }

    public class AccordionItem : HeaderedContentControl
    {
        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<AccordionItem, bool>(nameof(IsExpanded), false);

        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        private Accordion? _parentAccordion;
        public Accordion? ParentAccordion
        {
            get => _parentAccordion;
            set
            {
                _parentAccordion = value;
                if (_parentAccordion != null && IsExpanded)
                {
                    _parentAccordion.OnItemExpanded(this);
                }
            }
        }

        protected override Type StyleKeyOverride => typeof(AccordionItem);

        static AccordionItem()
        {
            IsExpandedProperty.Changed.AddClassHandler<AccordionItem>((x, e) => x.OnIsExpandedChanged(e));
        }

        protected override void OnAttachedToLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnAttachedToLogicalTree(e);
            if (e.Parent is Accordion parent)
            {
                ParentAccordion = parent;
            }
        }

        protected override void OnDetachedFromLogicalTree(LogicalTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromLogicalTree(e);
            ParentAccordion = null;
        }

        private void OnIsExpandedChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is bool isExpanded && isExpanded)
            {
                ParentAccordion?.OnItemExpanded(this);
            }
        }
    }
}
