using Unity.Cinemachine;
using Unity.Cinemachine.TargetTracking;
using UnityEngine;

namespace ProjectRidge.CameraRig
{
    /// <summary>
    /// Aplica el ángulo fijo 3/4 a una CinemachineCamera con CinemachineFollow en World Space.
    /// Se reaplica cada frame para poder ajustar IsoCameraConfig en play mode.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(CinemachineCamera), typeof(CinemachineFollow))]
    public class IsoCameraRig : MonoBehaviour
    {
        [SerializeField] private IsoCameraConfig config;

        private CinemachineCamera cinemachineCamera;
        private CinemachineFollow follow;

        private void OnEnable()
        {
            cinemachineCamera = GetComponent<CinemachineCamera>();
            follow = GetComponent<CinemachineFollow>();
            Apply();
        }

        private void LateUpdate() => Apply();

        private void Apply()
        {
            if (config == null) return;

            Quaternion rotation = Quaternion.Euler(config.pitch, config.yaw, 0f);
            transform.rotation = rotation;
            follow.FollowOffset = Vector3.up * config.targetHeight + rotation * new Vector3(0f, 0f, -config.distance) + Quaternion.Euler(0f, config.yaw, 0f) * Vector3.forward * config.framingOffset;

            var tracker = follow.TrackerSettings;
            tracker.BindingMode = BindingMode.WorldSpace;
            tracker.PositionDamping = Vector3.one * config.positionDamping;
            follow.TrackerSettings = tracker;

            var lens = cinemachineCamera.Lens;
            lens.FieldOfView = config.fieldOfView;
            cinemachineCamera.Lens = lens;
        }
    }
}
