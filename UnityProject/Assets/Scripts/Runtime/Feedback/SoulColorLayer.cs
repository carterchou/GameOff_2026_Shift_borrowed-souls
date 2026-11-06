using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// The layer the URP SoulColorPass draws again after post-processing, so whatever is on it stays in colour during
    /// soul vision. Only for objects without colliders.
    /// </summary>
    public static class SoulColorLayer
    {
        private const string LayerName = "SoulColor";

        /// <summary>The layer index, or -1 when the project has not set the layer up.</summary>
        public static int Id => LayerMask.NameToLayer(LayerName);

        public static void Apply(GameObject target)
        {
            int layer = Id;
            if (layer < 0) return;
            foreach (Transform part in target.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = layer;
        }
    }
}
