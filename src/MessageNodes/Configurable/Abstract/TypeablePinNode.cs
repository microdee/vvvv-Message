using System;
using System.ComponentModel.Composition;
using System.Linq;
using VVVV.Core.Logging;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.NonGeneric;
using VVVV.Utils;


namespace VVVV.Packs.Messaging.Nodes
{
    public abstract class TypeablePinNode : IPluginEvaluate, IPartImportsSatisfiedNotification
    {
        public const string TypeIdentityEnum = "TypeIdentityEnum";

        [Config("Configuration", DefaultString = "string Foo", Visibility = PinVisibility.True)]
        public IDiffSpread<string> FConfig;

        [Input("Input", Order = 0)] 
        protected IDiffSpread<Message> FInput;

        [Input("Type", EnumName = TypeIdentityEnum, IsSingle = true, Order = 1)]
        public IDiffSpread<EnumEntry> FAlias;
        
        [Input("Key", DefaultString = "Foo", Order = 2)]
        public IDiffSpread<string> FKey;

        [Output("Output", AutoFlush = false)] 
        protected Pin<Message> FOutput;

        protected IIOContainer FValue;

        protected Type TargetDynamicType;

        [Import()]
        protected IIOFactory FIOFactory;

        [Import()]
        protected ILogger FLogger;

        public void OnImportsSatisfied()
        {
            FConfig.Changed += OnConfigChange;

            var types = TypeIdentity.Instance.Aliases;
            EnumManager.UpdateEnum(TypeIdentityEnum, "string", types);
            
            FAlias.Changed += ConfigPin;
        }

        protected abstract IOAttribute DefinePin(FormularFieldDescriptor field);

        protected void ConfigPin(IDiffSpread spread)
        {
            string typeAlias = "string";
            if (!FAlias.IsAnyInvalid()) typeAlias = FAlias[0].Name;

            var newConfig = typeAlias + " Value";
            if (newConfig != FConfig[0]) FConfig[0] = newConfig; // first frame or user mistake will not reconfigure
        }

       protected virtual void OnConfigChange(IDiffSpread<string> configSpread)
        {
            var formular = new MessageFormular(MessageFormular.DYNAMIC, configSpread[0] ?? "string Value");
            if (formular.FieldNames.Count() < 1) return;

            if (FValue != null)
            {
                FValue.Dispose();
            }

            var name = formular.FieldNames.First();
            TargetDynamicType = formular[name].Type;
            
            IOAttribute attr = DefinePin(formular[name]); // each implementation of DynamicNode must create its own InputAttribute or OutputAttribute 
            Type pinType = typeof(ISpread<>).MakeGenericType((typeof(ISpread<>)).MakeGenericType(TargetDynamicType)); // the Pin is always a binsized one
  
           FValue = FIOFactory.CreateIOContainer(pinType, attr);
        }

        public abstract void Evaluate(int SpreadMax);


    }
}