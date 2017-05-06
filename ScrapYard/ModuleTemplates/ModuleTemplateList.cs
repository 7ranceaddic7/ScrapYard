﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ScrapYard.Utilities;

namespace ScrapYard
{
    public class ModuleTemplateList : List<ModuleTemplate>
    {

        /// <summary>
        /// Checks the list for a matching template and returns it if found
        /// </summary>
        /// <param name="moduleNode">A PartModule ConfigNode</param>
        /// <returns>The matching template or null if no match</returns>
        public ModuleTemplate FindMatchingTemplate(string partName, ConfigNode moduleNode)
        {
            foreach (ModuleTemplate template in this)
            {
                if (template.Matches(moduleNode))
                {
                    Part prefab = null;
                    string moduleName = moduleNode.GetValue("name");
                    if (template.StoreIfDefault) //if we store them even if they're the default, we can just save it
                    {
                        return template;
                    }
                    else if ((prefab = Utils.AvailablePartFromName(partName)?.partPrefab)?.Modules.Contains(moduleName) == true)
                    {
                        ConfigNode defaultNode = new ConfigNode();
                        prefab.Modules[moduleName].Save(defaultNode);
                        if (!moduleNode.IsIdenticalTo(defaultNode)) //don't save them when they're the default values
                        {
                            return template;
                        }
                    }
                    
                }
            }
            return null;
        }

        /// <summary>
        /// Checks if any templates match
        /// </summary>
        /// <param name="moduleNode">The PartModule ConfigNode</param>
        /// <returns>True if a match is found, false otherwise</returns>
        public bool CheckForMatch(string partName, ConfigNode moduleNode)
        {
            return (FindMatchingTemplate(partName, moduleNode) != null);
        }
    }
}
