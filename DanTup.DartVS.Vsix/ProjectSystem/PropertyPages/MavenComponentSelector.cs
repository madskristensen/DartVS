﻿namespace DanTup.DartVS.ProjectSystem.PropertyPages
{
    using System;
    using System.Runtime.InteropServices;

    [ComVisible(true)]
    [Guid(DartProjectConstants.MavenComponentSelectorGuidString)]
    internal partial class MavenComponentSelector : ComponentSelectorControl
    {
        public MavenComponentSelector()
        {
            InitializeComponent();
        }

        protected override bool CanSelectItems
        {
            get
            {
                return true;
            }
        }

        protected override void InitializeItems()
        {
        }

        protected override void ClearSelection()
        {
        }

        protected override void SetSelectionMode(bool multiSelect)
        {
        }

        protected override ComponentSelectorData[] GetSelection()
        {
            return new ComponentSelectorData[0];
        }
    }
}
