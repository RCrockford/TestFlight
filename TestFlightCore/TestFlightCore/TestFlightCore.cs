﻿using System;
using System.Collections.Generic;

using UnityEngine;
using KSPPluginFramework;
using TestFlightAPI;

namespace TestFlightCore
{
    /// <summary>
    /// This is the core PartModule of the TestFlight system, and is the module that everything else plugins into.
    /// All relevant data for working in the system, as well as all usable API methods live here.
    /// </summary>
    public class TestFlightCore : PartModuleExtended, ITestFlightCore
    {
        private float lastFailureCheck = 0f;
        private float lastPolling = 0.0f;
        private TestFlightData currentFlightData;
        private List<TestFlightData> initialFlightData;
        private double currentReliability = 0.0f;
        private List<ITestFlightFailure> failureModules = null;
        private ITestFlightFailure activeFailure = null;
        private bool failureAcknowledged = false;

        [KSPField(isPersistant = true)]
        public float failureCheckFrequency = 0f;
        [KSPField(isPersistant = true)]
        public float pollingInterval = 0f;

        public bool AttemptRepair()
        {
            if (activeFailure == null)
                return true;

            if (activeFailure.CanAttemptRepair())
            {
                bool isRepaired = activeFailure.AttemptRepair();
                if (isRepaired)
                {
                    activeFailure = null;
                    failureAcknowledged = false;
                    return true;
                }
            }
            return false;
        }

        public bool AcknowledgeFailure()
        {
            failureAcknowledged = true;
        }

        public void HighlightPart(bool doHighlight)
        {
            Color highlightColor;
            if (activeFailure == null)
                highlightColor = XKCDColors.HighlighterGreen;
            else
            {
                if (activeFailure.GetFailureDetails().severity == "major")
                    highlightColor = XKCDColors.FireEngineRed;
                else
                    highlightColor = XKCDColors.OrangeYellow;
            }

            if (doHighlight)
            {
                this.part.SetHighlightDefault();
                this.part.SetHighlightType(Part.HighlightType.AlwaysOn);
                this.part.SetHighlight(true, false);
                this.part.SetHighlightColor(highlightColor);
                HighlightingSystem.Highlighter highlighter;
                highlighter = this.part.FindModelTransform("model").gameObject.AddComponent<HighlightingSystem.Highlighter>();
                highlighter.ConstantOn(highlightColor);
                highlighter.SeeThroughOn();
            }
            else
            {
                this.part.SetHighlightDefault();
                this.part.SetHighlightType(Part.HighlightType.AlwaysOn);
                this.part.SetHighlight(false, false);
                this.part.SetHighlightColor(XKCDColors.HighlighterGreen);
                Destroy(this.part.FindModelTransform("model").gameObject.GetComponent(typeof(HighlightingSystem.Highlighter)));
            }
        }

        public override void OnAwake()
        {
            base.OnAwake();
        }

        public override void OnStart(StartState state)
        {
            if (failureModules == null)
            {
                failureModules = new List<ITestFlightFailure>();
            }
            failureModules.Clear();
            foreach(PartModule pm in this.part.Modules)
            {
                LogFormatted_DebugOnly("TestFlightCore: Inspecting Module " + pm.moduleName + " for ITestFlightFailure interface");
                ITestFlightFailure failureModule = pm as ITestFlightFailure;
                if (failureModule != null)
                {
                    LogFormatted_DebugOnly("TestFlightCore: Added Failure Module " + pm.moduleName);
                    failureModules.Add(failureModule);
                }
            }
            base.OnStart(state);
        }

        public virtual TestFlightData GetCurrentFlightData()
        {
            return currentFlightData;
        }

        public virtual int GetPartStatus()
        {
            if (activeFailure == null)
                return 0;

            if (activeFailure.GetFailureDetails().severity == "minor")
                return 1;
            if (activeFailure.GetFailureDetails().severity == "failure")
                return 1;
            if (activeFailure.GetFailureDetails().severity == "major")
                return 1;

            return -1;
        }

        public virtual ITestFlightFailure GetFailureModule()
        {
            return activeFailure;
        }

        public bool IsFailureAcknowledged()
        {
            return failureAcknowledged;
        }

        public string GetRequirementsTooltip()
        {
            if (activeFailure == null)
                return "No Repair Neccesary";

            List<RepairRequirements> requirements = activeFailure.GetRepairRequirements();

            if (requirements == null)
                return "This repair has no requirements";

            string tooltip = "";

            foreach (RepairRequirements requirement in requirements)
            {
                if (requirement.requirementMet)
                    tooltip += "[*]";
                else
                    tooltip += "[ ]";
                if (requirement.optionalRequirement)
                    tooltip += String.Format("(OPTIONAL +{0:F2}%) ", requirement.repairBonus * 100.0f);
                else
                    tooltip += " ";
                tooltip += requirement.requirementMessage;
                tooltip += "\n";
            }

            return tooltip;
        }

        public virtual double GetCurrentReliability(double globalReliabilityModifier)
        {
            // Calculate reliability based on initial flight data, not current
            double totalReliability = 0.0;
            string scope;
            IFlightDataRecorder dataRecorder = null;
            foreach (PartModule pm in this.part.Modules)
            {
                IFlightDataRecorder fdr = pm as IFlightDataRecorder;
                if (fdr != null)
                {
                    dataRecorder = fdr;
                }
            }
            foreach (PartModule pm in this.part.Modules)
            {
                ITestFlightReliability reliabilityModule = pm as ITestFlightReliability;
                if (reliabilityModule != null)
                {
                    scope = String.Format("{0}_{1}", dataRecorder.GetDataBody(), dataRecorder.GetDataSituation());
                    TestFlightData flightData;
                    if (initialFlightData == null)
                    {
                        flightData = new TestFlightData();
                        flightData.scope = scope;
                        flightData.flightData = 0.0f;
                    }
                    else
                    {
                        flightData = initialFlightData.Find(fd => fd.scope == scope);
                    }
                    totalReliability = totalReliability + reliabilityModule.GetCurrentReliability(flightData);
                }
            }
            currentReliability = totalReliability * globalReliabilityModifier;
            return currentReliability;
        }

        public void InitializeFlightData(List<TestFlightData> allFlightData, double globalReliabilityModifier)
        {
            initialFlightData = new List<TestFlightData>(allFlightData);
            double totalReliability = 0.0;
            string scope;
            IFlightDataRecorder dataRecorder = null;
            foreach (PartModule pm in this.part.Modules)
            {
                IFlightDataRecorder fdr = pm as IFlightDataRecorder;
                if (fdr != null)
                {
                    dataRecorder = fdr;
                    fdr.InitializeFlightData(allFlightData);
                    currentFlightData = fdr.GetCurrentFlightData();
                }
            }
            // Calculate reliability based on initial flight data, not current
            foreach (PartModule pm in this.part.Modules)
            {
                ITestFlightReliability reliabilityModule = pm as ITestFlightReliability;
                if (reliabilityModule != null)
                {
                    scope = String.Format("{0}_{1}", dataRecorder.GetDataBody(), dataRecorder.GetDataSituation());
                    TestFlightData flightData;
                    if (initialFlightData == null)
                    {
                        flightData = new TestFlightData();
                        flightData.scope = scope;
                        flightData.flightData = 0.0f;
                    }
                    else
                    {
                        flightData = initialFlightData.Find(fd => fd.scope == scope);
                    }
                    totalReliability = totalReliability + reliabilityModule.GetCurrentReliability(flightData);
                }
            }
            currentReliability = totalReliability * globalReliabilityModifier;
        }


        public virtual void DoFlightUpdate(double missionStartTime, double flightDataMultiplier, double flightDataEngineerMultiplier, double globalReliabilityModifier)
        {
            // Check to see if its time to poll
            IFlightDataRecorder dataRecorder = null;
            string scope;
            float currentMet = (float)(Planetarium.GetUniversalTime() - missionStartTime);
            if (currentMet > (lastPolling + pollingInterval) && currentMet > 10)
            {
                // Poll all compatible modules in this order:
                // 1) FlightDataRecorder
                // 2) TestFlightReliability
                // 2A) Determine final computed reliability
                // 3) Determine if failure poll should be done
                // 3A) If yes, roll for failure check
                // 3B) If failure occurs, roll to determine which failure module
                foreach (PartModule pm in this.part.Modules)
                {
                    IFlightDataRecorder fdr = pm as IFlightDataRecorder;
                    if (fdr != null)
                    {
                        dataRecorder = fdr;
                        fdr.DoFlightUpdate(missionStartTime, flightDataMultiplier, flightDataEngineerMultiplier);
                        currentFlightData = fdr.GetCurrentFlightData();
                        break;
                    }
                }
                // Calculate reliability based on initial flight data, not current
                double totalReliability = 0.0;
                foreach (PartModule pm in this.part.Modules)
                {
                    ITestFlightReliability reliabilityModule = pm as ITestFlightReliability;
                    if (reliabilityModule != null)
                    {
                        scope = String.Format("{0}_{1}", dataRecorder.GetDataBody(), dataRecorder.GetDataSituation());
                        TestFlightData flightData;
                        if (initialFlightData == null)
                        {
                            flightData = new TestFlightData();
                            flightData.scope = scope;
                            flightData.flightData = 0.0f;
                        }
                        else
                        {
                            flightData = initialFlightData.Find(fd => fd.scope == scope);
                        }
                        totalReliability = totalReliability + reliabilityModule.GetCurrentReliability(flightData);
                    }
                }
                currentReliability = totalReliability * globalReliabilityModifier;
                lastPolling = currentMet;
            }
        }

        public virtual bool DoFailureCheck(double missionStartTime, double globalReliabilityModifier)
        {
            string scope;
            IFlightDataRecorder dataRecorder = null;
            float currentMet = (float)(Planetarium.GetUniversalTime() - missionStartTime);
            if ( currentMet > (lastFailureCheck + failureCheckFrequency) && activeFailure == null && currentMet > 10)
            {
                lastFailureCheck = currentMet;
                // Calculate reliability based on initial flight data, not current
                double totalReliability = 0.0;
                foreach (PartModule pm in this.part.Modules)
                {
                    IFlightDataRecorder fdr = pm as IFlightDataRecorder;
                    if (fdr != null)
                    {
                        dataRecorder = fdr;
                        break;
                    }
                }
                foreach (PartModule pm in this.part.Modules)
                {
                    ITestFlightReliability reliabilityModule = pm as ITestFlightReliability;
                    if (reliabilityModule != null)
                    {
                        scope = String.Format("{0}_{1}", dataRecorder.GetDataBody(), dataRecorder.GetDataSituation());
                        TestFlightData flightData;
                        if (initialFlightData == null)
                        {
                            flightData = new TestFlightData();
                            flightData.scope = scope;
                            flightData.flightData = 0.0f;
                        }
                        else
                            flightData = initialFlightData.Find(fd => fd.scope == scope);
                        totalReliability = totalReliability + reliabilityModule.GetCurrentReliability(flightData);
                    }
                }
                currentReliability = totalReliability * globalReliabilityModifier;
                // Roll for failure
                float roll = UnityEngine.Random.Range(0.0f,100.0f);
                LogFormatted_DebugOnly("TestFlightCore: " + this.part.name + "(" + this.part.flightID + ") Reliability " + currentReliability + ", Failure Roll " + roll);
                if (roll > currentReliability)
                {
                    // Failure occurs.  Determine which failure module to trigger
                    int totalWeight = 0;
                    int currentWeight = 0;
                    int chosenWeight = 0;
                    foreach(ITestFlightFailure fm in failureModules)
                    {
                        totalWeight += fm.GetFailureDetails().weight;
                    }
                    LogFormatted_DebugOnly("TestFlightCore: Total Weight " + totalWeight);
                    chosenWeight = UnityEngine.Random.Range(1,totalWeight);
                    LogFormatted_DebugOnly("TestFlightCore: Chosen Weight " + chosenWeight);
                    foreach(ITestFlightFailure fm in failureModules)
                    {
                        currentWeight += fm.GetFailureDetails().weight;
                        if (currentWeight >= chosenWeight)
                        {
                            // Trigger this module's failure
                            LogFormatted_DebugOnly("TestFlightCore: Triggering failure on " + fm);
                            activeFailure = fm;
                            failureAcknowledged = false;
                            fm.DoFailure();
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public override void OnUpdate()
        {
        }
    }
}

