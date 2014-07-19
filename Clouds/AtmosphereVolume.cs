﻿using EVEManager;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Utils;

namespace Atmosphere
{
    public class AtmosphereVolumeMaterial : MaterialManager
    {
        [Persistent]
        Color _Color = new Color(1, 1, 1, 1);
        [Persistent]
        float _Visibility = .0001f;
    }

    class AtmosphereVolume
    {

        GameObject AtmosphereMesh;
        Material AtmosphereMaterial;

        [Persistent]
        AtmosphereVolumeMaterial atmosphereMaterial;

        CelestialBody celestialBody = null;
        Transform scaledCelestialTransform = null;
        float radius;

        public bool Scaled
        {
            get { return AtmosphereMesh.layer == EVEManagerClass.SCALED_LAYER; }
            set
            {
                if (value)
                {
                    float scale = (float)(1000f / celestialBody.Radius);
                    Reassign(EVEManagerClass.SCALED_LAYER, scaledCelestialTransform, scale);
                }
                else
                {
                    Reassign(EVEManagerClass.MACRO_LAYER, celestialBody.transform, 1);
                }
            }
        }

        private static Shader atmosphereShader = null;
        private static Shader AtmosphereShader
        {
            get
            {
                if (atmosphereShader == null)
                {
                    Assembly assembly = Assembly.GetExecutingAssembly();
                    atmosphereShader = EVEManagerClass.GetShader(assembly, "Atmosphere.Shaders.Compiled-SphereAtmosphere.shader");
                } return atmosphereShader;
            }
        }

        internal void Apply(CelestialBody celestialBody, Transform scaledCelestialTransform, float radius)
        {
            Remove();
            this.celestialBody = celestialBody;
            this.scaledCelestialTransform = scaledCelestialTransform;
            AtmosphereMaterial = new Material(AtmosphereShader);
            HalfSphere hp = new HalfSphere(radius, AtmosphereMaterial);
            AtmosphereMesh = hp.GameObject;
            Scaled = true;
            this.radius = radius;
            atmosphereMaterial.ApplyMaterialProperties(AtmosphereMaterial);

            GameEvents.onGameSceneLoadRequested.Add(GameSceneLoaded);
        }

        private void GameSceneLoaded(GameScenes scene)
        {
            if (scene == GameScenes.SPACECENTER || scene == GameScenes.FLIGHT)
            {
                foreach (GameObject go in GameObject.FindObjectsOfType<GameObject>())
                {
                    MeshRenderer mr = go.GetComponent<MeshRenderer>();
                    if(mr != null)
                    {
                        List<Material> materials = mr.materials.ToList() ;
                        materials.AddRange(mr.sharedMaterials);
                        foreach (Material mat in materials)
                        {
                            String name = mat.shader.name;
                            AtmosphereManager.Log("Shader " + name);
                            
                            switch (name)
                            {
                                case "KSP/Diffuse":
                                    mat.shader = Shader.Find("Diffuse");
                                    break;
                                case "KSP/Unlit":
                                    mat.shader = Shader.Find("Unlit/Texture");
                                    break;
                                case "KSP/Specular":
                                    mat.shader = Shader.Find("Specular");
                                    break;
                                case "KSP/Bumped":
                                    mat.shader = Shader.Find("Bumped Diffuse");
                                    break;
                                case "KSP/Bumped Specular":
                                    mat.shader = Shader.Find("Bumped Specular");
                                    break;
                                case "KSP/Emissive/Bumped Specular":
                                    Shader shader = Shader.Find("Self-Illumin/Bumped Specular");
                                    mat.shader = shader;
                                    break;
                            }
                            if (mat.shader == null)
                            {
                                AtmosphereManager.Log("Shader is null!");
                            }
                            String rtype = mat.GetTag("RenderType", false);
                            if(rtype == null || rtype == "")
                            {
                                AtmosphereManager.Log("Shader has no Render Type!");
                                mat.shader = Shader.Find("Diffuse");
                            }
                        }
                    }
                }
            }
        }

        public void Reassign(int layer, Transform parent, float scale)
        {
            AtmosphereMesh.transform.parent = parent;
            AtmosphereMesh.transform.localPosition = Vector3.zero;
            AtmosphereMesh.transform.localScale = scale * Vector3.one;
            AtmosphereMesh.layer = layer;
        }

        public void Remove()
        {
            if (AtmosphereMesh != null)
            {
                AtmosphereMesh.transform.parent = null;
                GameObject.DestroyImmediate(AtmosphereMesh);
                AtmosphereMesh = null;
            }
        }

        internal void UpdateRotation(Quaternion rotation)
        {
            if (rotation != null)
            {
                AtmosphereMesh.transform.localRotation = rotation;
            }
        }


    }
}