using System;
using Avalonia.Controls;

namespace CdpInspectorApp.Controls
{
    public class EditableComboBox : ComboBox
    {
        public EditableComboBox()
        {
            IsEditable = true;
        }

        protected override Type StyleKeyOverride => typeof(EditableComboBox);
    }
}
