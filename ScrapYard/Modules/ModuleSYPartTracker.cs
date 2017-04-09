﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScrapYard.Modules
{
    /// <summary>
    /// Applied to individual parts, it tracks how often that part has been used. Added and/or incremented by one each recovery.
    /// Strict comparisons between parts with different values will fail, but semi-soft comparisons will ignore this
    /// </summary>
    public class ModuleSYPartTracker : PartModule
    {
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true)]
        public string ID;
        [KSPField(isPersistant = true, guiActive = true, guiActiveEditor = true)]
        public int TimesRecovered = 0;


        public override void OnInitialize()
        {
            base.OnInitialize();
            if (string.IsNullOrEmpty(ID))
            {
                ID = Guid.NewGuid().ToString();
            }
        }
    }
}
