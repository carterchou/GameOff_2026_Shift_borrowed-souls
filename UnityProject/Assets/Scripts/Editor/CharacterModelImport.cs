using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace BorrowedSouls.EditorTools
{
    /// <summary>
    /// Import rules for the character FBX files:
    /// Generic rig, clip names without Blender's "Armature|" prefix, Idle and Run* loop, *_normal textures as normal maps.
    ///
    /// </summary>
    public sealed class CharacterModelImport : AssetPostprocessor
    {
        private const string Folder = "Assets/Art/Characters/";
        private const string PropFolder = "Assets/Art/Props/";
        private const string BlenderTakePrefix = "Armature|";

        private bool IsCharacterAsset => assetPath.StartsWith(Folder);
        private bool IsPropAsset => assetPath.StartsWith(PropFolder);

        /// <summary>Raise this when the rules change, so Unity re-imports the models.</summary>
        public override uint GetVersion() => 7;

        private void OnPreprocessModel()
        {
            if (IsPropAsset)
            {
                var prop = (ModelImporter)assetImporter;
                prop.animationType = ModelImporterAnimationType.None;
                prop.importAnimation = false;
                prop.importCameras = false;
                prop.importLights = false;
                prop.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                return;
            }
            if (!IsCharacterAsset) return;
            var importer = (ModelImporter)assetImporter;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; // Generic defaults to none: no Animator
            importer.motionNodeName = "";
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importBlendShapes = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
        }

        private void OnPreprocessAnimation()
        {
            if (!IsCharacterAsset) return;
            var importer = (ModelImporter)assetImporter;
            var clips = new List<ModelImporterClipAnimation>();
            foreach (ModelImporterClipAnimation clip in importer.defaultClipAnimations)
            {
                clip.name = clip.takeName.StartsWith(BlenderTakePrefix) ? clip.takeName.Substring(BlenderTakePrefix.Length) : clip.takeName;
                bool loops = clip.name == "Idle" || clip.name.StartsWith("Run") || clip.name == "Walk" || clip.name == "SeatedIdle" || clip.name == "Slumped";
                clip.loopTime = loops;
                clip.loopPose = loops;
                clips.Add(clip);
            }
            importer.clipAnimations = clips.ToArray();
        }

        /// <summary>Blender's FBX carries an opacity the URP importer reads as fully transparent, which hides the body.</summary>
        private void OnPostprocessMaterial(Material material)
        {
            if (!IsCharacterAsset && !IsPropAsset) return;
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_ZWrite", 1f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
            material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.SetOverrideTag("RenderType", "Opaque");
            material.renderQueue = -1;
            Color colour = material.GetColor("_BaseColor");
            colour.a = 1f;
            material.SetColor("_BaseColor", colour);
        }

        private void OnPreprocessTexture()
        {
            if (!IsCharacterAsset) return;
            if (assetPath.EndsWith("_normal.png")) ((TextureImporter)assetImporter).textureType = TextureImporterType.NormalMap;
        }
    }
}
