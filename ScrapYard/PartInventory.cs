﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using ScrapYard.Modules;
using ScrapYard.Utilities;

namespace ScrapYard
{
    public class PartInventory
    {
        private bool disableEvents = false;
        private HashSet<InventoryPart> internalInventory = new HashSet<InventoryPart>();


        #region Properties
        public bool InventoryEnabled
        {
            get
            {
                return ScrapYard.Instance.Settings.EnabledForSave && ScrapYard.Instance.Settings.CurrentSaveSettings.UseInventory;
            }
        }
        #endregion Properties

        public PartInventory() { }
        /// <summary>
        /// Creates a new PartInventory that doesn't trigger events when the inventory changes
        /// </summary>
        /// <param name="DisableEvents">Disables event firing if true.</param>
        public PartInventory(bool DisableEvents)
        {
            disableEvents = DisableEvents;
        }

        public void AddPart(InventoryPart part)
        {
            if (!InventoryEnabled || part.DoNotStore) //if not using the inventory, or the part shouldn't be stored, then do not add it
            {
                return;
            }
            part.TrackerModule.Inventoried = true;
            internalInventory.Add(part);
            if (!disableEvents)
            {
                ScrapYardEvents.OnSYInventoryChanged.Fire(part, true);
            }
        }

        public void AddPart(Part part)
        {
            InventoryPart convertedPart = new InventoryPart(part);
            AddPart(convertedPart);
        }

        public void AddPart(ProtoPartSnapshot protoPartSnapshot)
        {
            InventoryPart convertedPart = new InventoryPart(protoPartSnapshot);
            AddPart(convertedPart);
        }

        public void AddPart(ConfigNode partNode)
        {
            InventoryPart convertedPart = new InventoryPart(partNode);
            AddPart(convertedPart);
        }

        public InventoryPart FindPart(InventoryPart part, ComparisonStrength strength = ComparisonStrength.MODULES)
        {
            if (!InventoryEnabled)
            {
                return null;
            }
            return internalInventory.FirstOrDefault(ip => ip.IsSameAs(part, strength));
        }

        public InventoryPart RemovePart(InventoryPart part, ComparisonStrength strength = ComparisonStrength.MODULES)
        {
            if (!InventoryEnabled)
            {
                return null;
            }
            InventoryPart found = FindPart(part, strength);
            if (found != null && internalInventory.Remove(found))
            {
                if (!disableEvents)
                {
                    ScrapYardEvents.OnSYInventoryChanged.Fire(found, false);
                }
                return found;
            }
            return null;
        }

        /// <summary>
        /// Returns a ConfigNode representing the current state, or sets the state from a ConfigNode
        /// </summary>
        public ConfigNode State
        {
            get
            {
                ConfigNode returnNode = new ConfigNode("PartInventory");
                //Add module nodes
                foreach (InventoryPart part in internalInventory)
                {
                    ConfigNode toAdd = part.State;
                    returnNode.AddNode(toAdd);
                }
                return returnNode;
            }
            internal set
            {
                try
                {
                    internalInventory = new HashSet<InventoryPart>();
                    if (value == null)
                    {
                        return;
                    }

                    foreach (ConfigNode inventoryPartNode in value.GetNodes(typeof(InventoryPart).FullName))
                    {
                        InventoryPart loading = new InventoryPart();
                        loading.State = inventoryPartNode;
                        internalInventory.Add(loading);
                    }
                }
                catch (Exception ex)
                {
                    Logging.LogException(ex);
                }
            }
        }
    }
}
