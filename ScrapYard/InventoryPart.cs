﻿using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ScrapYard.Utilities;
using ScrapYard.Modules;

namespace ScrapYard
{
    /// <summary>
    /// The strictness of comparing two parts for equivalency
    /// </summary>
    public enum ComparisonStrength
    {
        /// <summary>
        /// Equivalent if their names match
        /// </summary>
        NAME,
        /// <summary>
        /// EqualEquivalent if name and dry cost match
        /// </summary>
        COSTS,
        /// <summary>
        /// Equaivalent if name, dry cost, and Modules (except ModuleSYPartTracker) match
        /// </summary>
        MODULES,
        /// <summary>
        /// Equivalent if name, dry cost, Modules, and TimesRecovered match
        /// </summary>
        TRACKER,
        /// <summary>
        /// Equivalent if name, dry cost, Modules, TimesRecovered and IDs match
        /// </summary>
        STRICT
    }
    public class InventoryPart
    {
        [Persistent]
        private string _name = "";
        [Persistent]
        private float _dryCost = 0;

        public string Name { get { return _name; } }
        public float DryCost { get { return _dryCost; } }
        public bool DoNotStore { get; set; } = false;
        public TrackerModuleWrapper TrackerModule { get; private set; } = new TrackerModuleWrapper(null);
        public Guid? ID
        {
            get
            {
                return TrackerModule?.ID;
            }
        }


        private List<ConfigNode> savedModules = new List<ConfigNode>();
        private int _hash = 0;

        /// <summary>
        /// Creates an empty InventoryPart.
        /// </summary>
        public InventoryPart() { }

        /// <summary>
        /// Create an InventoryPart from an origin Part, extracting the name, dry cost, and relevant MODULEs
        /// </summary>
        /// <param name="originPart">The <see cref="Part"/> used as the basis of the <see cref="InventoryPart"/>.</param>
        public InventoryPart(Part originPart)
        {
            _name = originPart.partInfo.name;
            _dryCost = originPart.GetModuleCosts(originPart.partInfo.cost) + originPart.partInfo.cost;
            foreach (PartResource resource in originPart.Resources)
            {
                _dryCost -= (float)(resource.maxAmount * PartResourceLibrary.Instance.GetDefinition(resource.resourceName).unitCost);
            }

            //Save modules (once we know which modules we want to save)
            if (originPart.Modules != null)
            {
                foreach (PartModule module in originPart.Modules)
                {
                    ConfigNode saved = new ConfigNode("MODULE");
                    module.Save(saved);
                    storeModuleNode(_name, saved);
                }
            }
        }

        /// <summary>
        /// Create an InventoryPart from an origin ProtoPartSnapshot, extracting the name, dry cost, and relevant MODULEs
        /// </summary>
        /// <param name="originPartSnapshot">The <see cref="ProtoPartSnapshot"/> to use as the basis of the <see cref="InventoryPart"/>.</param>
        public InventoryPart(ProtoPartSnapshot originPartSnapshot)
        {
            _name = originPartSnapshot.partInfo.name;
            float fuelCost;
            ShipConstruction.GetPartCosts(originPartSnapshot, originPartSnapshot.partInfo, out _dryCost, out fuelCost);

            //Save modules
            if (originPartSnapshot.modules != null)
            {
                foreach (ProtoPartModuleSnapshot module in originPartSnapshot.modules)
                {
                    storeModuleNode(_name, module.moduleValues);
                }
            }
        }

        /// <summary>
        /// Create an InventoryPart from an origin ConfigNode, extracting the name, dry cost, and relevant MODULEs
        /// </summary>
        /// <param name="originPartConfigNode">The <see cref="ConfigNode"/> to use as the basis of the <see cref="InventoryPart"/>.</param>
        public InventoryPart(ConfigNode originPartConfigNode)
        {
            _name = ConfigNodeUtils.PartNameFromNode(originPartConfigNode);
            AvailablePart availablePartForNode = ConfigNodeUtils.AvailablePartFromNode(originPartConfigNode);
            if (availablePartForNode != null)
            {
                float dryMass, fuelMass, fuelCost;
                ShipConstruction.GetPartCostsAndMass(originPartConfigNode, availablePartForNode, out _dryCost, out fuelCost, out dryMass, out fuelMass);
            }

            if (originPartConfigNode.HasNode("MODULE"))
            {
                foreach (ConfigNode module in originPartConfigNode.GetNodes("MODULE"))
                {
                    storeModuleNode(_name, module);
                }
            }
        }

        /// <summary>
        /// Checks to see if the passed InventoryPart is identical to this one, excluding Quantity and Used by default
        /// </summary>
        /// <param name="comparedPart"></param>
        /// <returns></returns>
        public bool IsSameAs(InventoryPart comparedPart, ComparisonStrength strictness)
        {
            //Test that the name is the same
            if (Name != comparedPart.Name)
            {
                return false;
            }
            if (strictness == ComparisonStrength.NAME) //If we're just comparing name then we're done
            {
                return true;
            }

            //Verify the costs are the same
            if (DryCost != comparedPart.DryCost)
            {
                return false;
            }
            if (strictness == ComparisonStrength.COSTS)
            {
                return true;
            }

            //Test to ensure the number of saved modules are identical
            if (savedModules.Count == comparedPart.savedModules.Count)
            {
                //Compare the saved modules to ensure they are identical
                for (int index = 0; index < savedModules.Count; ++index)
                {
                    if (!savedModules[index].IsIdenticalTo(comparedPart.savedModules[index]))
                    {
                        return false;
                    }
                }
                //If everything has passed, they are considered equal
            }
            else
            {
                return false;
            }
            if (strictness == ComparisonStrength.MODULES)
            {
                return true;
            }

            //Tracker comparison, the times used must match
            if (TrackerModule == null || comparedPart.TrackerModule == null)
            {
                return false;
            }
            if (TrackerModule.TimesRecovered != comparedPart.TrackerModule.TimesRecovered)
            {
                return false;
            }
            if (TrackerModule.Inventoried != comparedPart.TrackerModule.Inventoried)
            {
                return false;
            }
            if (strictness == ComparisonStrength.TRACKER)
            {
                return true;
            }

            //Strict comparison, the ids must be the same
            if (ID != comparedPart.ID)
            {
                return false;
            }

            //Everything must match, they are the same
            return true;
        }

        /// <summary>
        /// Converts the InventoryPart into a Part using the stored modules
        /// </summary>
        /// <returns></returns>
        public Part ToPart()
        {
            //Part retPart = new Part();
            AvailablePart aPart = Utils.AvailablePartFromName(Name);
            Part retPart = aPart.partPrefab;


            //set the modules to the ones we've saved
            if (retPart.Modules?.Count > 0)
            {
                foreach (ConfigNode saved in savedModules)
                {
                    //look for this module on the partInfo and replace it
                    string moduleName = saved.GetValue("name");
                    if (retPart.Modules.Contains(moduleName))
                    {
                        PartModule correspondingModule = retPart.Modules[moduleName];
                        correspondingModule.Load(saved);
                    }
                }
            }
            //foreach (PartModule mod in retPart.Modules)
            //{
            //    foreach (string trackedModuleName in ScrapYard.Instance.Settings.TrackedModules)
            //    {
            //        if (mod.moduleName.ToUpper().Contains(trackedModuleName))
            //        {
            //            //replace the module with the version we've saved
            //            ConfigNode savedModule = savedModules.FirstOrDefault(c => c.GetValue("name").ToUpper().Contains(trackedModuleName));
            //            if (savedModule != null)
            //            {
            //                mod.Load(savedModule);
            //            }
            //        }
            //    }
            //}

            return retPart;
        }

        /// <summary>
        /// Gets the ConfigNode version of the InventoryPart, or sets the state of the InventoryPart from a ConfigNode
        /// </summary>
        public ConfigNode State
        {
            get
            {
                ConfigNode returnNode = ConfigNode.CreateConfigFromObject(this);

                returnNode.AddValue("_id", ID);
                returnNode.AddValue("_timesRecovered", TrackerModule.TimesRecovered);
                returnNode.AddValue("_inventoried", TrackerModule.Inventoried);

                //Add module nodes
                foreach (ConfigNode module in savedModules)
                {
                    returnNode.AddNode(module);
                }
                //if (TrackerModule?.HasModule == true) //uncomment if we decide to store the whole module again
                //{
                //    returnNode.AddNode(TrackerModule.TrackerNode);
                //}
                return returnNode;
            }
            set
            {
                try
                {
                    if (value == null)
                    {
                        return;
                    }
                    //  ConfigNode cnUnwrapped = node.GetNode(this.GetType().Name);
                    //plug it in to the object
                    ConfigNode.LoadObjectFromConfig(this, value);

                    //try to get tracker stuff
                    int timesRecovered = 0;
                    bool inventoried = false;
                    string idStr = null;

                    if (value.TryGetValue("_id", ref idStr) |
                        value.TryGetValue("_timesRecovered", ref timesRecovered) |
                        value.TryGetValue("_inventoried", ref inventoried)) // the single | makes all of them happen, we need at least one to succeed
                    {
                        Guid? idGuid = Utilities.Utils.StringToGuid(idStr);
                        if (idGuid.HasValue)
                        {
                            TrackerModule = new TrackerModuleWrapper(idGuid.Value, timesRecovered, inventoried);
                        }
                    }


                    savedModules = new List<ConfigNode>();
                    foreach (ConfigNode module in value.GetNodes("MODULE"))
                    {
                        if (module.GetValue("name").Equals("ModuleSYPartTracker")) //we still need to load it for a little while longer
                        {
                            TrackerModule = new TrackerModuleWrapper(module);
                        }
                        else
                        {
                            savedModules.Add(module);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Logging.Log("Error while loading InventoryPart from a ConfigNode. Error: \n" + ex.Message);
                }
            }
        }

        public override int GetHashCode()
        {
            if (_hash == 0)
            {
                foreach (char s in Name ?? string.Empty)
                {
                    _hash += s;
                }
                _hash *= 31;
            }
            return _hash;
        }

        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj);
        }

        private bool storeModuleNode(string partName, ConfigNode moduleNode)
        {
            //If it matches a template, save it
            if (ScrapYard.Instance.Settings.ModuleTemplates.CheckForMatch(partName, moduleNode))
            {
                savedModules.Add(moduleNode);
                return true;
            }

            //check if this is one of the forbidden modules, and if so then set DoNotStore
            if (ScrapYard.Instance.Settings.ForbiddenTemplates.CheckForMatch(partName, moduleNode))
            {
                DoNotStore = true;
                return false; //we're not storing this, so we still return false
            }

            //check for the part tracker and add it
            if (moduleNode.GetValue("name").Equals("ModuleSYPartTracker"))
            {
                TrackerModule = new TrackerModuleWrapper(moduleNode);
                return true;
            }

            return false;
        }
    }
}
