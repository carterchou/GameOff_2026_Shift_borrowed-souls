using UnityEngine;

namespace BorrowedSouls.Data
{
    /// <summary>Hit feedback strength for one attack class.
    [CreateAssetMenu(menuName = "Borrowed Souls/Feedback Profile", fileName = "FB_")]
    public sealed class FeedbackProfile : ScriptableObject
    {
        [SerializeField, Min(0f)] private float _hitStop = 0.04f;
        [SerializeField, Min(0f)] private float _impulseForce = 0.25f;
        [SerializeField, Min(0f)] private float _flashDuration = 0.06f;
        [SerializeField, Min(0f)] private float _punchScale = 0.12f;
        [SerializeField, Min(0f)] private float _sparkScale = 1f;

        public float HitStop => _hitStop;
        public float ImpulseForce => _impulseForce;
        public float FlashDuration => _flashDuration;
        public float PunchScale => _punchScale;
        public float SparkScale => _sparkScale;
    }
}
