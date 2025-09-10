using StarterAssets;
using UnityEngine;

public class MinimapCameraFollow : MonoBehaviour
{
    [SerializeField] private float cameraMoveLerpSpeed = 10f;
    [SerializeField] private Vector2 sizeRange = new Vector2(5f,15f);
    [SerializeField] private float speedForMaxRange = 1.0f;
    [SerializeField] private float cameraSizeChagneLerpSpeed = 0.5f;
    // just move with the player
    private Transform targetToFollow;
    public static MinimapCameraFollow Instance { get; private set; }

    private Camera cam;
    private ThirdPersonController controller;

    private void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
    }
    public void SetTarget(Transform targetToFollow)
    {
        this.targetToFollow = targetToFollow;
        controller = targetToFollow.GetComponent<ThirdPersonController>();
    }
    private void Update()
    {
        if (!targetToFollow) return;

        var targetPosition = new Vector3(targetToFollow.position.x, transform.position.y, targetToFollow.position.z); // no change in height
        var targetRotation = Quaternion.Euler(90, targetToFollow.eulerAngles.y, 0f); // only in y axis

        transform.position = Vector3.Lerp(transform.position,targetPosition,cameraMoveLerpSpeed * Time.deltaTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, cameraMoveLerpSpeed * Time.deltaTime);

        float val = Mathf.InverseLerp(0, speedForMaxRange, controller.GetSpeed());

        var targetSize = Mathf.Lerp(sizeRange.x, sizeRange.y, val);

        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, cameraSizeChagneLerpSpeed * Time.deltaTime);
    }
}
