﻿using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using VVVV.Core.Logging;
using VVVV.Packs.Messaging;
using VVVV.PluginInterfaces.V2;
using VVVV.Utils;

namespace VVVV.Packs.Messaging.Nodes
{
    #region PluginInfo
    [PluginInfo(Name = "Prune", Category = "Message", Help = "Removes all fields with the indicated Name of any Message ", Author = "velcrome")]
    #endregion PluginInfo
    public class MessagePruneNode : IPluginEvaluate
    {
#pragma warning disable 649, 169
        [Input("Input")]
        private IDiffSpread<Message> FInput;

        [Input("Remaining Fields", DefaultString = "Foo")]
        private ISpread<string> FFilter;

        [Output("Output", AutoFlush = false)]
        private ISpread<Message> FOutput;

        [Import()]
        protected ILogger FLogger;

#pragma warning restore

        public void Evaluate(int SpreadMax)
        {
            SpreadMax = FInput.IsAnyInvalid() ? 0 : FInput.SliceCount;

            if (SpreadMax <= 0)
                if (FOutput.SliceCount == 0)
                {
                    FOutput.SliceCount = 0;
                    FOutput.Flush();
                    return;
                }
                else return;

            var keepOnly = new HashSet<string>(FFilter);

            foreach (var message in FInput)
            {
                foreach (var fieldName in message.Fields.ToArray())
                    if (!keepOnly.Contains(fieldName))              
                        message.Remove(fieldName);
            }


            FOutput.SliceCount = 0;
            FOutput.AssignFrom(FInput);
//            FOutput.AssignFrom(FInput.Where(message => !message.IsEmpty));
            FOutput.Flush();
        }
    }
}