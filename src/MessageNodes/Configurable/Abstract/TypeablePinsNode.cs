﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;

using VVVV.PluginInterfaces.V2;
using VVVV.Utils;
using VVVV.Core.Logging;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.OLE.Interop;
using System.Windows.Forms;

namespace VVVV.Packs.Messaging.Nodes
{
    public abstract class DynamicPinsNode : AbstractFormularableNode, IWin32Window, ICustomQueryInterface
    {
        #region fields & pins
        protected const string Tags = "Formular";

        protected Dictionary<string, IIOContainer> FPins = new Dictionary<string, IIOContainer>();
        protected int DynPinCount = 5;

        protected bool RemovePinsFirst;

        protected FormularLayoutPanel LayoutPanel = new FormularLayoutPanel();

        #endregion fields & pins

        #region Initialisation

        [Import()]
        protected IIOFactory FIOFactory;

        public override void OnImportsSatisfied()
        {
            base.OnImportsSatisfied();

            Changed += formular => UpdateWindow(formular, false);
            Changed += TryDefinePins;

            LayoutPanel.Change += (e, i) => Formular = LayoutPanel.Formular;
        }


        #endregion Initialisation

        #region pin management

        protected bool SyncPins()
        {
            bool changed = false;
            foreach (string name in FPins.Keys)
            {
                var pin = FPins[name].ToISpread();
                pin.Sync();
                if (pin.IsChanged) changed = true;
            }
            return changed;
        }

        protected bool CopyFromPins(Message message, int index, bool checkPinForChange = false)
        {
            var hasCopied = false;

            foreach (string name in FPins.Keys)
            {
                // don't change if pin data still the same
                if (!checkPinForChange || FPins[name].ToISpread().IsChanged)
                {
                    var pinSpread = FPins[name].ToISpread();
                    var type = Formular[name].Type;

                    IEnumerable bin;

                    if (pinSpread.IsAnyInvalid()) bin = Enumerable.Empty<object>();
                    else bin = pinSpread[index] as IEnumerable;

                    // don't change if pin data equals the message data
                    if (!message.Fields.Contains(name))
                    {
                        message[name] = BinFactory.New(type);
                        hasCopied = true;
                    }

                    if (!message[name].Equals(bin))
                    {
                        message.AssignFrom(name, bin, type);
                        hasCopied = true;
                    }  
                }
            }
            return hasCopied;

        }

        protected IIOContainer CreatePin(FormularFieldDescriptor field)
        {
            IOAttribute attr = SetPinAttributes(field); // each implementation of DynamicPinsNode must create its own InputAttribute or OutputAttribute (

            Type pinType = typeof(ISpread<>).MakeGenericType((typeof(ISpread<>)).MakeGenericType(field.Type)); // the Pin is always a binsized one
            var pin = FPins[field.Name] = FIOFactory.CreateIOContainer(pinType, attr);

            DynPinCount += 2; // total pincount. always add two to account for data pin and binsize pin

            return pin;
        }

        protected bool RetryConfig()
        {
            if (RemovePinsFirst)
            {
                TryDefinePins(Formular);
            }

            if (RemovePinsFirst)
                throw new PinConnectionException("Manually remove unneeded links first!");
            else return true;
        }

        protected bool HasLink(IIOContainer pinContainer)
        {
            try
            {
                var connected = pinContainer.GetPluginIO().IsConnected;

                foreach (var associated in pinContainer.AssociatedContainers)
                {
                    connected &= associated.GetPluginIO().IsConnected;
                }
                return connected;
            }
            catch (Exception)
            {
                string nodePath = PluginHost.GetNodePath(false);
                FLogger.Log(LogType.Error, "Failed to protect a " + this.GetType().Name + " node: " + nodePath);
                return false;
            }
        }

        protected bool HasEndangeredLinks(MessageFormular newFormular)
        {
            // pin removals
            var danger = from field in Formular.FieldDescriptors
                         let fieldName = field.Name
                         where !newFormular.FieldNames.Contains(fieldName)
                         where HasLink(FPins[fieldName]) // first frame pin will not be initialized
                         select fieldName;

            // type changes - removal and recreate new
            var typeDanger = from field in Formular.FieldDescriptors
                             where newFormular.FieldNames.Contains(field.Name)
                             where newFormular[field.Name].Type != field.Type
                             where HasLink(FPins[field.Name]) // first frame pin will not be initialized
                             select field.Name;

            // ignore changes to binsize.

            return danger.Count() > 0 || typeDanger.Count() > 0;
        }

        /// <summary>
        /// Will try to apply the formular to the current pin layout
        /// </summary>
        /// <param name="newFormular"></param>
        /// <remarks>
        /// If any pin in the new formular is already present, it will preserve the pin.
        /// If any current pin is not contained in the new Formular, but is linked in the patch, it will force the node to raise exceptions every frame, until the link is manually deleted.
        /// </remarks>
        protected void TryDefinePins(MessageFormular newFormular)
        {
            if (HasEndangeredLinks(newFormular))
            {
                RemovePinsFirst = true;
                return;
            }
            else RemovePinsFirst = false;

            List<string> invalidPins = FPins.Keys.ToList();
            foreach (string field in newFormular.FieldNames)
            {
                if (FPins.ContainsKey(field) && FPins[field] != null)
                {
                    invalidPins.Remove(field);

                    if (Formular.FieldNames.Contains(field))
                    {
                        // same name, but types don't match
                        // todo: in fact eg float does match double here...
                        if (Formular[field].Type != newFormular[field].Type)
                        {
                            FPins[field].Dispose();
                            FPins[field] = CreatePin(newFormular[field]);
                        }
                    }
                    else
                    {
                        // key is in FPins, but no type defined. should never happen
                        FLogger.Log(LogType.Debug, "Internal Fault in Pin Layout detected. Override with " + newFormular.ToString());
                        FPins[field] = CreatePin(newFormular[field]);
                    }
                }
                else
                {
                    FPins[field] = CreatePin(newFormular[field]);
                }
            }

            // cleanup
            foreach (string name in invalidPins)
            {
                FPins[name].Dispose();
                FPins.Remove(name);
            }

            // reorder - does not work right now, sdk offers only read-only access
            //var names = Formular.FieldNames.ToArray();
            //for (int i = 0; i < Formular.FieldNames.Count(); i++)
            //{
            //    var name = names[i];
            //    var pin = FPins[name].GetPluginIO();
            //    pin.Order = i * 2 + 5;
            //}

        }

        #endregion pin management

        #region window management

        private void UpdateWindow(MessageFormular formular, bool append = false)
        {
            if (formular == null) return;

            var layoutPanel = LayoutPanel as FormularLayoutPanel;

            if (append)
            {
                var old = layoutPanel.Formular;
                foreach (var field in formular.FieldDescriptors)
                    old.Append(field, true);
                formular = old;
            }

            layoutPanel.Formular = formular;
            layoutPanel.CanEditFields = formular.IsDynamic;
        }
        #endregion

        #region Window utils

        public IntPtr Handle
        {
            get { return LayoutPanel.Handle; }
        }

        public CustomQueryInterfaceResult GetInterface(ref Guid iid, out IntPtr ppv)
        {
            if (iid.Equals(Guid.Parse("00000112-0000-0000-c000-000000000046")))
            {
                ppv = Marshal.GetComInterfaceForObject(LayoutPanel, typeof(IOleObject));
                return CustomQueryInterfaceResult.Handled;
            }
            else if (iid.Equals(Guid.Parse("458AB8A2-A1EA-4d7b-8EBE-DEE5D3D9442C")))
            {
                ppv = Marshal.GetComInterfaceForObject(LayoutPanel, typeof(IWin32Window));
                return CustomQueryInterfaceResult.Handled;
            }
            else
            {
                FLogger.Log(LogType.Debug, "missing: " + iid.ToString());
                ppv = IntPtr.Zero;
                return CustomQueryInterfaceResult.NotHandled;
            }
        }
        #endregion window utils

        #region abstract methods
        protected abstract IOAttribute SetPinAttributes(FormularFieldDescriptor config);
        #endregion abstract methods
    }

}
