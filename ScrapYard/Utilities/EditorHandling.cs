﻿using ScrapYard.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ScrapYard.Utilities
{
    public static class EditorHandling
    {
        private static float costCache = 0;


        /// <summary>
        /// Verifies that the inventory parts on the ship in the editor are valid
        /// </summary>
        public static void VerifyEditorShip()
        {
            //make a copy of the inventory
            PartInventory copy = new PartInventory(true);
            copy.State = ScrapYard.Instance.TheInventory.State;

            foreach (Part part in EditorLogic.fetch?.ship?.Parts ?? new List<Part>())
            {
                InventoryPart iPart = new InventoryPart(part);
                if (iPart.ID == null)
                {
                    (part.Modules["ModuleSYPartTracker"] as ModuleSYPartTracker).MakeFresh();
                }
                if (iPart.TrackerModule.Inventoried)
                {
                    InventoryPart inInventory = copy.RemovePart(iPart, ComparisonStrength.STRICT); //strict, we only remove parts that are exact
                    if (inInventory == null)
                    {
                        //reset their tracker status
                        Logging.DebugLog($"Found inventory part on vessel that is not in inventory. Resetting. {iPart.Name}:{iPart.ID}");
                        (part.Modules["ModuleSYPartTracker"] as ModuleSYPartTracker).MakeFresh();
                    }
                }
                else
                {
                    //check that we're not sharing an ID with something in the inventory
                    if (iPart.ID.HasValue)
                    {
                        InventoryPart inInventory = copy.FindPart(iPart.ID.Value);
                        if (inInventory != null)
                        {
                            //found a part that is sharing an ID but shouldn't be
                            Logging.DebugLog($"Found part on vessel with same ID as inventory part, but not matching. Resetting. {iPart.Name}:{iPart.ID}");
                            (part.Modules["ModuleSYPartTracker"] as ModuleSYPartTracker).MakeFresh();
                        }
                    }
                }
            }

            //update the part list if visible
            if (ScrapYard.Instance.InstanceSelectorUI.IsVisible)
            {
                ScrapYard.Instance.InstanceSelectorUI.InstanceVM?.UpdatePartList();
            }
        }

        public static void UpdateEditorCost()
        {
            if (!ScrapYard.Instance.Settings.CurrentSaveSettings.OverrideFunds)
            {
                return;
            }
            float dry, fuel;
            float totalCost = EditorLogic.fetch.ship.GetShipCosts(out dry, out fuel);

            foreach (Part part in EditorLogic.fetch?.ship?.Parts ?? new List<Part>())
            {
                InventoryPart iPart = new InventoryPart(part);
                if (iPart.TrackerModule.Inventoried)
                {
                    totalCost -= iPart.DryCost;
                }
            }
            //set visible cost in editor UI
            UpdateCostUI(totalCost);
        }

        /// <summary>
        /// Updates the cost UI with the cached cost value
        /// </summary>
        public static void UpdateCostUI()
        {
            if (!ScrapYard.Instance.Settings.CurrentSaveSettings.OverrideFunds)
            {
                return;
            }
            CostWidget widget = UnityEngine.Object.FindObjectOfType<CostWidget>();
            if (widget != null)
            {
                MethodInfo costMethod = widget.GetType().GetMethod("onCostChange", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                costMethod?.Invoke(widget, new object[] { costCache });
            }
        }

        /// <summary>
        /// Updates the cost UI with the provided value and caches it
        /// </summary>
        /// <param name="cost">The new cost value</param>
        public static void UpdateCostUI(float cost)
        {
            costCache = cost;
            UpdateCostUI();
        }

        /// <summary>
        /// Takes a list of InventoryParts and removes any that are in use by the current vessel
        /// </summary>
        /// <param name="sourceList">The list of parts to search in</param>
        /// <returns>A List of parts that aren't being used</returns>
        public static IList<InventoryPart> FilterOutUsedParts(IEnumerable<InventoryPart> sourceList)
        {
            List<InventoryPart> retList = new List<InventoryPart>(sourceList);

            foreach (Part part in EditorLogic.fetch.ship)
            {
                InventoryPart iPart = new InventoryPart(part);
                InventoryPart found = retList.FirstOrDefault(ip => ip.IsSameAs(iPart, ComparisonStrength.STRICT));
                if (found != null)
                {
                    retList.Remove(found);
                }
            }

            if (EditorLogic.SelectedPart != null)
            {
                foreach (Part part in EditorLogic.FindPartsInChildren(EditorLogic.SelectedPart))
                {
                    InventoryPart iPart = new InventoryPart(part);
                    InventoryPart found = retList.FirstOrDefault(ip => ip.IsSameAs(iPart, ComparisonStrength.STRICT));
                    if (found != null)
                    {
                        retList.Remove(found);
                    }
                }
            }
            return retList;
        }
    }
}
