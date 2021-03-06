﻿using System.Linq;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils;


namespace VVVV.Packs.Messaging.Nodes
{
 
    #region PluginInfo
    [PluginInfo(Name = "Edit", 
        Category = "Message", 
        AutoEvaluate = true, 
        Help = "Adds or edits multiple Message FieldNames", 
        Version = "Formular", 
        Tags = "Formular, Bin",
        Author = "velcrome")]
    #endregion PluginInfo
    public class MessageEditNode : TypeablePinsNode
    {
        [Input("Input", Order = 0)] 
        protected IDiffSpread<Message> FInput;

        [Input("Force", Order = int.MaxValue-1, IsSingle = true, IsToggle = true, DefaultBoolean = true, Visibility = PinVisibility.Hidden)]
        protected IDiffSpread<bool> FForce;

        [Input("Update", IsToggle = true, Order = int.MaxValue, DefaultBoolean = true)]
        protected IDiffSpread<bool> FUpdate;

        [Output("Output", AutoFlush = false)]
        protected Pin<Message> FOutput;

        protected override IOAttribute SetPinAttributes(FormularFieldDescriptor field)
        {
            var attr = new InputAttribute(field.Name);

            attr.BinVisibility = PinVisibility.Hidden;
            attr.BinSize = field.DefaultSize;

            attr.Order = DynPinCount;
            attr.BinOrder = DynPinCount+1;

            attr.CheckIfChanged = true;

            return attr;
        }


        public override void Evaluate(int SpreadMax)
        {
            InitDX11Graph();

            bool warnPinSafety = false;
            if (RemovePinsFirst) warnPinSafety = RetryConfig();

            if (FInput.IsAnyInvalid())
            {
                FOutput.FlushNil();

                if (warnPinSafety)
                    throw new PinConnectionException("Manually remove unneeded links first! [Edit]. ID = [" + PluginHost.GetNodePath(false) + "]");
                return;
            }

            bool doFlush = false;

            // is any Update slice checked?
            var anyUpdate = FUpdate.Any();

            // Flush upstream changes through the plugin
            if (FInput.IsChanged)
            {
                FOutput.SliceCount = 0;
                FOutput.AssignFrom(FInput); // push change from upstream if valid
                doFlush = true;
            }
            else if (!anyUpdate)
            {
                if (warnPinSafety)
                    throw new PinConnectionException("Manually remove unneeded links first! [Edit]. ID = [" + PluginHost.GetNodePath(false) + "]");

                return; // if no update and no change, no need to flush! 
            }

            bool newData = FPins.Any(pinName => pinName.Value.ToISpread().IsChanged); // changed pins
            newData |= FForce[0]; // assume newData, if AutoSense is off.

            if (anyUpdate && newData) {
                int messageIndex = 0;
                foreach (var message in FInput)
                {
                    if (FUpdate[messageIndex])
                    doFlush |= CopyFromPins(message, messageIndex, !FForce[0]);
                        
                    messageIndex++;
                }
            }

            if (doFlush) FOutput.Flush();

            if (warnPinSafety)
                throw new PinConnectionException("Manually remove unneeded links first! [Edit]. ID = [" + PluginHost.GetNodePath(false) + "]");


        }
    }
}
