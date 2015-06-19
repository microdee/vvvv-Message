﻿using System;
using System.Collections;
using System.Linq;
using VVVV.Packs.Messaging;
using VVVV.PluginInterfaces.V2;
using VVVV.PluginInterfaces.V2.NonGeneric;
using VVVV.Utils;
using System.Globalization;


namespace VVVV.Packs.Messaging.Nodes
{
    [PluginInfo(Name = "Write", AutoEvaluate = true, Category = "Message", Help = "Writes into Fields of arbitrary Type", Version = "Dynamic", Tags = "Formular, Bin", Author = "velcrome")]
    public class MessageWriteNode : DynamicPinNode
    {
#pragma warning disable 649, 169
        [Input("Update only on data change", Order = -1, IsSingle = true, IsToggle = true, DefaultBoolean = false, Visibility=PinVisibility.OnlyInspector)]
        IDiffSpread<bool> FDetectChange;
        
        [Input("Update", IsToggle = true, Order = int.MaxValue, DefaultBoolean = true)]
        IDiffSpread<bool> FUpdate;
#pragma warning restore

        protected override IOAttribute DefinePin(FormularFieldDescriptor field)
        {
            var attr = new InputAttribute("Field");
            attr.BinVisibility = PinVisibility.Hidden;
            
            attr.Order = 4;
            attr.BinOrder = 5;
            attr.BinSize = 1;

            attr.CheckIfChanged = true;

            return attr;
        }


        public override void Evaluate(int SpreadMax)
        {
            // any of the update slices is true -> update the plugin.
            var anyUpdate = FUpdate.Any(x => x);
            
            // Flush upstream changes through the plugin
            if (FInput.IsChanged)
            {
                FOutput.SliceCount = 0;

                if (FInput.IsAnyInvalid())
                {
                    if (FOutput.SliceCount > 0)
                    {
                        FOutput.Flush();
                        return; // if no input, no further calculation.
                    }
                }
                else
                {
                    FOutput.AssignFrom(FInput); // push change from upstream if valid
                    FOutput.Flush();
                }
            }
            else // no change from upstream.
            {
                if (anyUpdate) // push change that Write will be performing
                {
                    FOutput.AssignFrom(FInput);
                    FOutput.Flush();
                }
            }

            if (!anyUpdate) return; // all is pushed before hand, so preempt here, if no change will occur in Write

           if (!FDetectChange[0] &&
               !FInput.IsChanged && 
                !FConfig.IsChanged && 
                !FKey.IsChanged && 
                !FValue.ToISpread().IsChanged 
            ) return; // if no change, no furter calc

            SpreadMax = FInput.SliceCount;

            var keyCount = FKey.SliceCount;
            var ValueSpread = FValue.ToISpread();

            for (int i = 0; i < SpreadMax; i++)
            {
                Message message = FInput[i];

                var keyIndex = 0;
                foreach (var key in FKey)
                {
                    var fieldIndex = i * keyCount + keyIndex;
                    var value = (ValueSpread[fieldIndex] as ISpread);

                    if (!message.Fields.Contains(key)) message[key] = BinFactory.New(TargetDynamicType);

                    if (value.SliceCount > 0)
                    {
                        if (message[key].GetInnerType().IsAssignableFrom(TargetDynamicType))
                        {
                            // check if any relevant change occurred
                            if (FUpdate[fieldIndex] && !message[key].Equals(value as IEnumerable)) message.AssignFrom(key, value);
                        }

                        else
                        {
                            if (!FUpdate[fieldIndex]) continue;

                            IList casted = new ArrayList();
                            foreach (var slice in value)
                                casted.Add(Convert.ChangeType(slice, message[key].GetInnerType()));

                            // check if any relevant change occurred
                            if (!message[key].Equals(casted as IEnumerable)) message.AssignFrom(key, casted);
                        }

                    }
                    else message[key].Clear();
                    keyIndex++;

                }
            }
        }

    }

}
