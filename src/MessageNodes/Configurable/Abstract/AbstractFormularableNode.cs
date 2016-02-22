using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VVVV.Packs.Messaging;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils;

namespace VVVV.Packs.Messaging.Nodes
{
    public abstract class AbstractFormularableNode : ConfigurableNode, IPluginEvaluate, IPartImportsSatisfiedNotification
    {
        [Input("Message Formular", DefaultEnumEntry = "None", EnumName = "VVVV.Packs.Message.Core.Formular", Order = 2)]
        public IDiffSpread<EnumEntry> FFormular;
        protected bool FirstFrame = true;

        [Import]
        protected IHDEHost FHDEHost;

        protected override void InitializeWindow()
        {
            // don't call inherited InitializeWindow, so the placeholder pic will disappear
            
            FWindow = new FormularLayoutPanel();
            Controls.Add(FWindow);
        }

        public override void OnImportsSatisfied()
        {
            base.OnImportsSatisfied();

            // base provider of Formulars
            var reg = MessageFormularRegistry.Instance;
            reg.TypeChanged += FormularChanged;

            // dummy enum, will be populated from registry
            EnumManager.UpdateEnum(reg.RegistryName, reg.Keys.First(), reg.Keys.ToArray());


            // defer usage of all Formular related config to the end of the frame, when all nodes have finished at least once
            FHDEHost.MainLoop.OnRender += InitConfig;

            // events are always deferred to end of frame, so all potential participators have finished
            FFormular.Changed += (e) => FHDEHost.MainLoop.OnRender += HandleChangeOfFormular;
            ((FormularLayoutPanel)FWindow).OnChange += (s, e) => FHDEHost.MainLoop.OnRender += HandleChangeInWindow;
        }


        #region node formular update during runtime
        private void InitConfig(object sender, System.EventArgs e)
        {
            FirstFrame = false;
            FHDEHost.MainLoop.OnRender -= InitConfig;

            SetWindowFromConfig();
            SetWindowFromRegistry();
        }

        private void HandleChangeOfFormular(object sender, System.EventArgs e)
        {
            FHDEHost.MainLoop.OnRender -= HandleChangeOfFormular;
            var isDynamic = FFormular[0] == MessageFormular.DYNAMIC;

            if (!isDynamic) SetWindowFromRegistry();
            ((FormularLayoutPanel)FWindow).CanEditDescription = isDynamic;

            FConfig[0] = GetConfigurationFromWindow();
        }

        private void HandleChangeInWindow(object sender, System.EventArgs e)
        {
            FHDEHost.MainLoop.OnRender -= HandleChangeInWindow;
            FConfig[0] = GetConfigurationFromWindow();
        }

        private void FormularChanged(MessageFormularRegistry sender, MessageFormularChangedEvent e)
        {
            if (FFormular.IsAnyInvalid()) return;

            var used = false;

            foreach (var type in FFormular)
                if (type.Name == e.Formular.Name) used = true;

            if (used)
            {
                SetWindowFromRegistry();
                FConfig[0] = GetConfigurationFromWindow();
            }
        }
        #endregion node update during runtime

        #region update gui
        public virtual MessageFormular SetWindowFromConfig()
        {
            var formular = new MessageFormular(FConfig[0]);
            return SetFormular(formular, true);
        }

        public virtual MessageFormular SetWindowFromRegistry()
        {
            if (FFormular.IsAnyInvalid() || FirstFrame || FFormular[0]==MessageFormular.DYNAMIC) return SetWindowFromConfig();

            var form = FFormular[0].Name;
            var formular = MessageFormularRegistry.Instance[form];

            return SetFormular(formular);
        }

        protected virtual MessageFormular SetFormular(MessageFormular formular, bool forceChecked = false) 
        {
            var fieldDef = FWindow as FormularLayoutPanel;
            fieldDef.LayoutByFormular(formular, forceChecked);
            fieldDef.CanEditDescription = (FFormular.SliceCount == 0) || FFormular[0].Name == MessageFormular.DYNAMIC;
            return formular; 
        }
        #endregion update gui

        #region utils
        private string GetConfigurationFromWindow()
        {
            var window = FWindow as FormularLayoutPanel;
            var desc = window.CheckedDescriptors;
            var result = string.Join(", ", desc.Select(d => d.ToString()));
            return result;
        }
        #endregion utils

    }
}