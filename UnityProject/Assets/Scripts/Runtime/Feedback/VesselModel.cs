using BorrowedSouls.Vessels;
using UnityEngine;

namespace BorrowedSouls.Feedback
{
    /// <summary>
    /// Art for a vessel. Picks one model variant when the body wakes and hands its animator to
    /// <see cref="VesselAnimationDriver"/>. The models are not in the public repo, so a missing
    /// reference is normal there: the vessel just keeps its grey-box visuals.
    /// </summary>
    public sealed class VesselModel : MonoBehaviour
    {
        [SerializeField] private VesselRuntime _vessel;
        [SerializeField] private VesselAnimationDriver _driver;
        [Tooltip("The first variant is the one the player starts in; enemies pick at random.")]
        [SerializeField] private GameObject[] _variants = new GameObject[0];
        [Tooltip("Same order as Variants.")]
        [SerializeField] private RuntimeAnimatorController[] _controllers = new RuntimeAnimatorController[0];

        /// <summary>The main skinned mesh of the spawned model, or null if the vessel kept its grey-box visuals.</summary>
        public Renderer Body { get; private set; }

        /// <summary>The spawned model's root.</summary>
        public Transform Root { get; private set; }

        /// <summary>Spawns a variant under the parent. Returns false if no variant is available.</summary>
        public bool Spawn(Transform parent)
        {
            int index = PickVariant();
            if (index < 0) return false;

            GameObject model = Instantiate(_variants[index], parent);
            model.name = "Model";
            Root = model.transform;
            Root.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            Body = model.GetComponentInChildren<SkinnedMeshRenderer>();

            var animator = model.GetComponent<Animator>();
            animator.runtimeAnimatorController = _controllers[index];
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate; // a corpse still has to finish falling off screen
            _driver.Bind(animator);
            return true;
        }

        private int PickVariant()
        {
            int usable = 0;
            for (int i = 0; i < _variants.Length; i++)
            {
                if (IsUsable(i)) usable++;
            }
            if (usable == 0) return -1;
            if (_vessel.StartsAsPlayer && IsUsable(0)) return 0;

            int pick = Random.Range(0, usable);
            for (int i = 0; i < _variants.Length; i++)
            {
                if (IsUsable(i) && pick-- == 0) return i;
            }
            return -1;
        }

        private bool IsUsable(int index) =>
            _variants[index] != null && index < _controllers.Length && _controllers[index] != null;
    }
}
