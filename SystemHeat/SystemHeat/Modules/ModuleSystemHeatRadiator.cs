using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KSP.Localization;

namespace SystemHeat
{
  /// <summary>
  /// The connection between a stock ModuleActiveRadiator and the SystemHeat system
  /// </summary>
  public class ModuleSystemHeatRadiator : ModuleActiveRadiator
  {
    /// <summary>
    /// Unique identifier
    /// </summary>
    [KSPField(isPersistant = false)]
    public string moduleID;

    /// <summary>
    /// This should correspond to the related ModuleSystemHeat. If not specified, the first module will be found
    /// </summary>
    [KSPField(isPersistant = false)]
    public string systemHeatModuleID;

    /// <summary>
    /// This should map system temperature to heat radiated
    /// </summary>
    [KSPField(isPersistant = false)]
    public FloatCurve temperatureCurve = new FloatCurve();


    /// <summary>
    /// Convective area
    /// </summary>
    [KSPField(isPersistant = false)]
    public float convectiveArea = 1f;

    /// <summary>
    /// UI: Current status
    /// </summary>
    [KSPField(isPersistant = false, guiActive = true, guiActiveEditor = true, guiName = "#LOC_SystemHeat_ModuleSystemHeatRadiator_RadiatorStatus_Title", groupName = "sysheatradiator", groupDisplayName = "#LOC_SystemHeat_ModuleSystemHeatRadiator_GroupName")]
    public string RadiatorStatus = "Offline";

    /// <summary>
    /// UI: Current status
    /// </summary>
    [KSPField(isPersistant = false, guiActive = true, guiActiveEditor = true, guiName = "#LOC_SystemHeat_ModuleSystemHeatRadiator_ConvectionStatus_Title", groupName = "sysheatradiator", groupDisplayName = "#LOC_SystemHeat_ModuleSystemHeatRadiator_GroupName")]
    public string ConvectionStatus = "Offline";

    /// <summary>
    /// UI: Current radiator of efficiency
    /// </summary>
    [KSPField(isPersistant = false, guiActive = true, guiActiveEditor = true, guiName = "#LOC_SystemHeat_ModuleSystemHeatRadiator_RadiatorEfficiency_Title", groupName = "sysheatradiator", groupDisplayName = "#LOC_SystemHeat_ModuleSystemHeatRadiator_GroupName")]
    public string RadiatorEfficiency = "-1%";


    /// <summary>
    /// ID of the linked scalar module for animating heat
    /// </summary>
    [KSPField(isPersistant = false)]
    public string scalarModuleID;

    /// <summary>
    /// Temperature value at which glow starts to be visible
    /// </summary>
    [KSPField(isPersistant = false)]
    public float draperPoint = 650;

    /// <summary>
    /// Temperature value at which glow is maxed
    /// </summary>
    [KSPField(isPersistant = false)]
    public float maxTempAnimation = -1f;

    /// <summary>
    /// Rate at which heat animates
    /// </summary>
    [KSPField(isPersistant = false)]
    public float heatAnimationRate = 0.1f;

    /// <summary>
    /// Rate at which heat animates
    /// </summary>
    [KSPField(isPersistant = true)]
    public bool sunTracking = true;

    [KSPEvent(guiActive = false, guiName = "", guiActiveEditor = true, active = true, groupName = "sysheatradiator", groupDisplayName = "#LOC_SystemHeat_ModuleSystemHeatRadiator_GroupName")]
    public void ToggleEditorSunTracking()
    {
      sunTracking = !sunTracking;
    }
    protected ModuleSystemHeat heatModule;
    protected ModuleSystemHeatColorAnimator scalarModule;

    public override void Start()
    {
      base.Start();
      heatModule = ModuleUtils.FindHeatModule(this.part, systemHeatModuleID);

      if (scalarModuleID != "")
        scalarModule = part.GetComponents<ModuleSystemHeatColorAnimator>().ToList().Find(x => x.moduleID == scalarModuleID);

      if (maxTempAnimation == -1f)
        maxTempAnimation = (float)part.maxTemp;

      maxTempAnimation -= draperPoint;

      Utils.Log("[ModuleSystemHeatRadiator] Setup completed", LogType.Modules);

      if (this.GetComponent<ModuleDeployableRadiator>() != null)
      {
        if (HighLogic.LoadedSceneIsFlight)
        {

          if (sunTracking)
          {
            base._depRad.trackingMode = ModuleDeployablePart.TrackingMode.SUN;
            base._depRad.isTracking = true;
          }
          else
          {
            base._depRad.trackingMode = ModuleDeployablePart.TrackingMode.NONE;
            base._depRad.trackingSpeed = 0f;
            base._depRad.isTracking = false;
          }
        }
        else
        {
          this.Events["ToggleEditorSunTracking"].guiActiveEditor = true;
        }

      }
      else
      {
        if (HighLogic.LoadedSceneIsEditor)
        {
          this.Events["ToggleEditorSunTracking"].guiActiveEditor = false;
        }
      }

    }

    public override string GetModuleDisplayName()
    {
      return Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatRadiator_DisplayName");
    }
    public override string GetInfo()
    {

      string message = Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatRadiator_PartInfo",
        temperatureCurve.Curve.keys[0].time.ToString("F0"),
        temperatureCurve.Evaluate(temperatureCurve.Curve.keys[0].time).ToString("F0"),
        temperatureCurve.Evaluate(temperatureCurve.Curve.keys[temperatureCurve.Curve.keys.Length - 1].time).ToString("F0"),
        temperatureCurve.Curve.keys[temperatureCurve.Curve.keys.Length - 1].time.ToString("F0")
        );
      message += base.GetInfo();
      return message;
    }

    public override void FixedUpdate()
    {
      base.FixedUpdate();

      if (heatModule != null)
      {

        if (HighLogic.LoadedSceneIsFlight)
        {
          if (base.IsCooling)
          {
            float radiativeFlux = -temperatureCurve.Evaluate(heatModule.LoopTemperature);
            float convectiveFlux = 0f;

            if (vessel.atmDensity > 0d)
            {
              Fields["ConvectionStatus"].guiActive = true;
              HeatLoop lp = heatModule.Loop;
              if (lp != null)
              {
                float tDelta = lp.ConvectionTemperature - Mathf.Clamp(heatModule.LoopTemperature,
              (float)PhysicsGlobals.SpaceTemperature, temperatureCurve.Curve.keys[temperatureCurve.Curve.keys.Length - 1].time);

                convectiveFlux = Mathf.Clamp(
                   tDelta * heatModule.Loop.ConvectionFlux * (float)part.heatConvectiveConstant * convectiveArea * 0.5f,
                  float.MinValue, 0f);

              }

              ConvectionStatus = Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatRadiator_ConvectionStatus_Running", convectiveFlux.ToString("F1"));
            }
            else
            {
              Fields["ConvectionStatus"].guiActive = false;
            }

            heatModule.AddFlux(moduleID, 0f, radiativeFlux + convectiveFlux, false);


            RadiatorEfficiency = Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatRadiator_RadiatorEfficiency_Running",
              (-radiativeFlux / temperatureCurve.Evaluate(temperatureCurve.Curve.keys[temperatureCurve.Curve.keys.Length - 1].time) * 100f).ToString("F0"));
            RadiatorStatus = Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatRadiator_RadiatorStatus_Running", radiativeFlux.ToString("F1"));

            if (scalarModule != null)
            {
              scalarModule.SetScalar(Mathf.MoveTowards(scalarModule.GetScalar, Mathf.Clamp01((heatModule.currentLoopTemperature - draperPoint) / maxTempAnimation), TimeWarp.fixedDeltaTime * heatAnimationRate));
            }
          }
          else
          {
            Fields["RadiatorStatus"].guiActive = false;
            Fields["ConvectionStatus"].guiActive = false;
            heatModule.AddFlux(moduleID, 0f, 0f, false);
            RadiatorEfficiency = Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatRadiator_RadiatorEfficiency_Offline");
            if (scalarModule != null)
              scalarModule.SetScalar(Mathf.MoveTowards(scalarModule.GetScalar, 0f, TimeWarp.fixedDeltaTime * heatAnimationRate));
          }


        }

        if (HighLogic.LoadedSceneIsEditor)
        {
          if (sunTracking)
          {
            this.Events["ToggleEditorSunTracking"].guiName = "Disable Sun Tracking";
          }
          else
          {
            this.Events["ToggleEditorSunTracking"].guiName = "Enable Sun Tracking";
          }

          float radiativeFlux = -temperatureCurve.Evaluate(heatModule.LoopTemperature);
          float convectiveFlux = 0f;

          HeatLoop lp = heatModule.Loop;
          if (lp != null)
          {
            float tDelta = lp.ConvectionTemperature - Mathf.Clamp(heatModule.LoopTemperature,
              (float)PhysicsGlobals.SpaceTemperature, temperatureCurve.Curve.keys[temperatureCurve.Curve.keys.Length - 1].time);

            convectiveFlux = Mathf.Clamp(
               tDelta * heatModule.Loop.ConvectionFlux * (float)part.heatConvectiveConstant * convectiveArea * 0.5f,
              float.MinValue, 0f);

            //Utils.Log($"tD {tDelta}, tC, {lp.ConvectionTemperature} tL {heatModule.LoopTemperature}, tMax {temperatureCurve.Curve.keys[temperatureCurve.Curve.keys.Length - 1].time}, convA {convectiveArea}");
          }

          
          heatModule.AddFlux(moduleID, 0f, radiativeFlux + convectiveFlux, false);

          ConvectionStatus = Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatRadiator_ConvectionStatus_Running", convectiveFlux.ToString("F1"));
          RadiatorEfficiency = Localizer.Format("#LOC_SystemHeat_ModuleSystemHeatRadiator_RadiatorEfficiency_Running",
            ((temperatureCurve.Evaluate(heatModule.LoopTemperature) / temperatureCurve.Evaluate(temperatureCurve.Curve.keys[temperatureCurve.Curve.keys.Length - 1].time)) * 100f).ToString("F0"));
        }
      }
    }


  }
}
