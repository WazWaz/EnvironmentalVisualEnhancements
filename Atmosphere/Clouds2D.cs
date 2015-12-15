﻿using EVEManager;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using ShaderLoader;
using UnityEngine;
using Utils;

namespace Atmosphere
{
    public class Clouds2DMaterial : MaterialManager
    {
#pragma warning disable 0169
#pragma warning disable 0414
        [Persistent]
        float _FalloffPow = 2f;
        [Persistent]
        float _FalloffScale = 3f;
        [Persistent]
        float _MinLight = .5f;
        [Persistent, InverseScaled]
        float _RimDist = 0.0001f;
        [Persistent, InverseScaled]
        float _RimDistSub = 1.01f;
        [Persistent, InverseScaled]
        float _InvFade = .008f;
        [Scaled]
        float _Radius = 1000f;

        public float Radius { set { _Radius = value; } }
    }

    public class CloudShadowMaterial : MaterialManager
    {
        [Persistent]
        float _ShadowFactor = .75f;
    }

    class Clouds2D
    {
        GameObject CloudMesh;
        Material CloudMaterial;
        Projector ShadowProjector = null;
        GameObject ShadowProjectorGO = null;
        CloudsMaterial cloudsMat = null;
        
        [Persistent]
        Clouds2DMaterial macroCloudMaterial = null;
        [Persistent, Optional]
        CloudShadowMaterial shadowMaterial = null;

        Tools.Layer scaledLayer = Tools.Layer.Scaled;
        Light Sunlight;
        bool isScaled = false;
        public bool Scaled
        {
            get { return isScaled; }
            set
            {
                CloudsManager.Log("Clouds2D is now " + (value ? "SCALED" : "MACRO"));
                if (isScaled != value)
                {
                    if (value)
                    {
                        macroCloudMaterial.ApplyMaterialProperties(CloudMaterial, ScaledSpace.ScaleFactor);
                        cloudsMat.ApplyMaterialProperties(CloudMaterial, ScaledSpace.ScaleFactor);

                        if (ShadowProjector != null)
                        {
                            macroCloudMaterial.ApplyMaterialProperties(ShadowProjector.material, ScaledSpace.ScaleFactor);
                            cloudsMat.ApplyMaterialProperties(ShadowProjector.material, ScaledSpace.ScaleFactor);
                        }
                        float scale = (float)(1000f / celestialBody.Radius);
                        if (HighLogic.LoadedScene == GameScenes.MAINMENU)
                        {
                            scale *= 1.008f;
                        }
                        Reassign(scaledLayer, scaledCelestialTransform, scale);
                    }
                    else
                    {
                        macroCloudMaterial.ApplyMaterialProperties(CloudMaterial);
                        cloudsMat.ApplyMaterialProperties(CloudMaterial);

                        if (ShadowProjector != null)
                        {
                            macroCloudMaterial.ApplyMaterialProperties(ShadowProjector.material);
                            cloudsMat.ApplyMaterialProperties(ShadowProjector.material);
                        }
                                                
                        Reassign(Tools.Layer.Local, celestialBody.transform, 1);
                    }
                    isScaled = value;
                }
            }
        }
        CelestialBody celestialBody = null;
        Transform scaledCelestialTransform = null;
        float radius;     
        float radiusScale;
        
        private static Shader cloudShader = null;

        internal Clouds2D CloneForMainMenu(GameObject mainMenuBody)
        {
            Clouds2D mainMenu = new Clouds2D();
            mainMenu.macroCloudMaterial = this.macroCloudMaterial;
            mainMenu.shadowMaterial = this.shadowMaterial;
            mainMenu.Apply(this.celestialBody, mainMenuBody.transform, this.cloudsMat, this.radius, (Tools.Layer)mainMenuBody.layer);
            return mainMenu;
        }

        private static Shader CloudShader
        {
            get
            {
                if (cloudShader == null)
                {
                    cloudShader = ShaderLoaderClass.FindShader("EVE/Cloud");
                } return cloudShader;
            }
        }

        private static Shader cloudShadowShader = null;
        private static Shader CloudShadowShader
        {
            get
            {
                if (cloudShadowShader == null)
                {
                    cloudShadowShader = ShaderLoaderClass.FindShader("EVE/CloudShadow");
                } return cloudShadowShader;
            }
        }

        private bool _enabled = true;
        public bool enabled { get {return _enabled; }
            set
            {
                _enabled = value;
                if (CloudMesh != null)
                {
                    CloudMesh.SetActive(value);
                }
                if (ShadowProjector != null)
                {
                    ShadowProjector.enabled = value;
                }
            } }

        internal void Apply(CelestialBody celestialBody, Transform scaledCelestialTransform, CloudsMaterial cloudsMaterial, float radius, Tools.Layer layer = Tools.Layer.Scaled)
        {
            CloudsManager.Log("Applying 2D clouds...");
            Remove();
            this.celestialBody = celestialBody;
            this.scaledCelestialTransform = scaledCelestialTransform;
            HalfSphere hp = new HalfSphere(radius, ref CloudMaterial, CloudShader);
            CloudMesh = hp.GameObject;
            this.radius = radius;
            macroCloudMaterial.Radius = radius;
            this.cloudsMat = cloudsMaterial;
            this.scaledLayer = layer;
            
            if (shadowMaterial != null)
            {
                ShadowProjectorGO = new GameObject();
                ShadowProjector = ShadowProjectorGO.AddComponent<Projector>();
                ShadowProjector.nearClipPlane = 10;
                ShadowProjector.fieldOfView = 60;
                ShadowProjector.aspectRatio = 1;
                ShadowProjector.orthographic = true;
                ShadowProjector.transform.parent = celestialBody.transform;
                ShadowProjector.material = new Material(CloudShadowShader);
                shadowMaterial.ApplyMaterialProperties(ShadowProjector.material);
            }


            
            Scaled = true;
        }

        public void Reassign(Tools.Layer layer, Transform parent, float scale)
        {
            CloudMesh.transform.parent = parent;
            CloudMesh.transform.localPosition = Vector3.zero;
            CloudMesh.transform.localScale = scale * Vector3.one;
            CloudMesh.layer = (int)layer;

            radiusScale = radius * scale;
            float worldRadiusScale = Vector3.Distance(parent.transform.TransformPoint(Vector3.up * radiusScale), parent.transform.TransformPoint(Vector3.zero));

            if (layer == Tools.Layer.Local)
            {
                Sunlight = Sun.Instance.light;
                CloudMaterial.SetFloat("_OceanRadius", (float)celestialBody.Radius * scale);
                CloudMaterial.EnableKeyword("WORLD_SPACE_ON");
                CloudMaterial.EnableKeyword("SOFT_DEPTH_ON");
            }
            else
            {
                //hack to get protected variable
                FieldInfo field = typeof(Sun).GetFields(BindingFlags.Instance | BindingFlags.NonPublic).First(
                    f => f.Name == "scaledSunLight" );
                Sunlight = (Light)field.GetValue(Sun.Instance);
                CloudMaterial.DisableKeyword("WORLD_SPACE_ON");
                CloudMaterial.DisableKeyword("SOFT_DEPTH_ON");
            }

            if(HighLogic.LoadedScene == GameScenes.MAINMENU)
            {
                Sunlight = GameObject.FindObjectsOfType<Light>().Last(l => l.isActiveAndEnabled);
            }

            if (ShadowProjector != null)
            {

                float dist = (float)(2 * worldRadiusScale);
                ShadowProjector.farClipPlane = dist;
                ShadowProjector.orthographicSize = worldRadiusScale;

                ShadowProjector.material.SetFloat("_Radius", (float)radiusScale);
                ShadowProjector.material.SetFloat("_PlanetRadius", (float)celestialBody.Radius*scale);
                ShadowProjector.transform.parent = parent;

                ShadowProjectorGO.layer = (int)layer;
                if (layer == Tools.Layer.Local)
                {
                    ShadowProjector.ignoreLayers = ~(Tools.Layer.Default.Mask() |
                                                     Tools.Layer.TransparentFX.Mask() |
                                                     Tools.Layer.Water.Mask() |
                                                     Tools.Layer.Local.Mask() |
                                                     Tools.Layer.Kerbals.Mask() |
                                                     Tools.Layer.Parts.Mask());
                    ShadowProjector.material.EnableKeyword("WORLD_SPACE_ON");
                }
                else
                {
                    ShadowProjector.ignoreLayers = ~layer.Mask();
                    ShadowProjector.material.DisableKeyword("WORLD_SPACE_ON");
                }
                
            }
        }

        public void Remove()
        {
            if (CloudMesh != null)
            {
                CloudsManager.Log("Removing 2D clouds...");
                CloudMesh.transform.parent = null;
                GameObject.DestroyImmediate(CloudMesh);
                CloudMesh = null;
            }
            if(ShadowProjectorGO != null)
            {
                ShadowProjectorGO.transform.parent = null;
                ShadowProjector.transform.parent = null;
                GameObject.DestroyImmediate(ShadowProjector);
                GameObject.DestroyImmediate(ShadowProjectorGO);
                ShadowProjector = null;
                ShadowProjectorGO = null;
            }
        }

        internal void UpdateRotation(QuaternionD rotation, Matrix4x4 World2Planet, Matrix4x4 mainRotationMatrix, Matrix4x4 detailRotationMatrix)
        {
            if (rotation != null)
            {
                CloudMesh.transform.localRotation = rotation;
                if (ShadowProjector != null && Sunlight != null)
                {
                    Vector3 worldSunDir;
                    Vector3 sunDirection;

                    worldSunDir = Vector3.Normalize(Sunlight.transform.forward);
                    sunDirection = Vector3.Normalize(ShadowProjector.transform.parent.InverseTransformDirection(worldSunDir));

                    ShadowProjector.transform.localPosition = radiusScale * -sunDirection;
                    ShadowProjector.transform.forward = worldSunDir;

                    if (Scaled)
                    {
                        ShadowProjector.material.SetVector(ShaderProperties.SUNDIR_PROPERTY, sunDirection); 
                    }
                    else
                    {
                        ShadowProjector.material.SetVector(ShaderProperties.SUNDIR_PROPERTY, worldSunDir);
                    }

                }
            }
            CloudMaterial.SetVector(ShaderProperties.PLANET_ORIGIN_PROPERTY, CloudMesh.transform.position);
            SetRotations(World2Planet, mainRotationMatrix, detailRotationMatrix);
        }

        private void SetRotations(Matrix4x4 World2Planet, Matrix4x4 mainRotation, Matrix4x4 detailRotation)
        {
            Matrix4x4 rotation = (mainRotation*World2Planet) * CloudMesh.transform.localToWorldMatrix;
            CloudMaterial.SetMatrix(ShaderProperties.MAIN_ROTATION_PROPERTY, rotation);
            CloudMaterial.SetMatrix(ShaderProperties.DETAIL_ROTATION_PROPERTY, detailRotation);

            if (ShadowProjector != null)
            {
                if(Scaled)
                {
                    ShadowProjector.material.SetMatrix(ShaderProperties.MAIN_ROTATION_PROPERTY, mainRotation);
                }
                else
                {
                    ShadowProjector.material.SetMatrix(ShaderProperties.MAIN_ROTATION_PROPERTY, mainRotation * ShadowProjector.transform.parent.worldToLocalMatrix);
                    ShadowProjector.material.SetVector(ShaderProperties.PLANET_ORIGIN_PROPERTY, ShadowProjector.transform.parent.position);
                }

                ShadowProjector.material.SetMatrix(ShaderProperties.DETAIL_ROTATION_PROPERTY, detailRotation);
            }
        }

    }
}