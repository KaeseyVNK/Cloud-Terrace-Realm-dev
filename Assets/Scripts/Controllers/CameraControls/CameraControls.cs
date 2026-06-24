using System;
using Unity.Cinemachine;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraControls : MonoBehaviour
{
    [Header("Camera Movement Settings")]
    [SerializeField] private float panSpeed = 20f;
    //[SerializeField] private float zoomSpeed = 10f;    
    [SerializeField] private float rotationSpeed = 10f;  
    [SerializeField] private float edgePanThreshold = 10f;
    [SerializeField] private float edgePanSpeed = 20f;  
    [SerializeField] private bool enableEdgePan = true;

    [Header("Zoom Settings")]
    [SerializeField] private float minZoomDistance = 5f;
    [SerializeField] private float maxZoomDistance = 40f;   
    [Tooltip("Thời gian nội suy Zoom, số càng lờn thì càng trễ/mượt (vd: 0.15)")]
    [SerializeField] private float zoomSmoothTime = 0.15f; 

    [Header("Mobile Touch Settings")]
    [SerializeField] private float touchPanSensitivity = 0.05f;
    [SerializeField] private float touchZoomSensitivity = 0.05f;
    [SerializeField] private float touchRotationSensitivity = 0.1f;

    [Header("References")]
    [Tooltip("Kéo Cinemachine Camera từ Scene vào đây")]
    [SerializeField] private CinemachineCamera vcam; 
    
    private CinemachineFollow cinemachineFollow;
    private float targetZoomDistance;
    private float zoomVelocity = 0f; 
    private bool hasStoredView;
    private Vector3 storedPosition;
    private Quaternion storedRotation;
    private float storedZoomDistance;

    private float screenWidth = Screen.width;
    private float screenHeight = Screen.height;

    void Awake()
    {
        if (vcam != null)
        {
            cinemachineFollow = vcam.GetComponent<CinemachineFollow>();  

            targetZoomDistance = cinemachineFollow.FollowOffset.magnitude; 
        }
    }

    void Update()
    {
        bool isTouchSupported = false;
        #if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR
        isTouchSupported = (Input.touchCount > 0);
        #endif

        if (isTouchSupported)
        {
            HandleTouchControls();
        }
        else
        {
            HandlePan();
            HandleEdgePan();
            HandleRotation();
            HandleZoomInput();
        }

        ApplySmoothZoom();
    }

    public void FocusOnPosition(Vector3 position, bool preserveExistingStoredView = true)
    {
        if (!hasStoredView || !preserveExistingStoredView)
        {
            StoreCurrentView();
        }

        transform.position = position;
    }

    public bool ReturnToStoredView()
    {
        if (!hasStoredView)
        {
            return false;
        }

        transform.SetPositionAndRotation(storedPosition, storedRotation);
        targetZoomDistance = storedZoomDistance;
        zoomVelocity = 0f;
        hasStoredView = false;
        return true;
    }

    public bool HasStoredView => hasStoredView;

    private void StoreCurrentView()
    {
        storedPosition = transform.position;
        storedRotation = transform.rotation;
        storedZoomDistance = targetZoomDistance;
        hasStoredView = true;
    }

    private void HandleRotation()
    {
        if (Mouse.current.rightButton.isPressed)
        {
            float mouseX = Mouse.current.delta.ReadValue().x;
            
            transform.Rotate(Vector3.up, mouseX * rotationSpeed * Time.deltaTime, Space.World);
        }

        // Hoặc có thể sử dụng phím Q và E để xoay
        float rotateDir = 0f;
        if (Keyboard.current.qKey.isPressed) rotateDir = 1f;
        if (Keyboard.current.eKey.isPressed) rotateDir = -1f;

        if (rotateDir != 0f)
        {
            transform.Rotate(Vector3.up, rotateDir * rotationSpeed * Time.deltaTime, Space.World);
        }
    }

    private void HandleEdgePan()
    {
        if(!enableEdgePan || vcam == null) return;

        Vector3 moveDirection = Vector3.zero;
        Vector3 forward = vcam.transform.forward;
        forward.y = 0;
        forward.Normalize();
        Vector3 right = vcam.transform.right;
        right.y = 0;        
        right.Normalize();  
        Vector2 mousePosition = Mouse.current.position.ReadValue();

        if (mousePosition.y >= screenHeight - edgePanThreshold)
        {
            moveDirection += forward;
        }
        else if (mousePosition.y <= edgePanThreshold)
        {
            moveDirection -= forward;
        }

        if (mousePosition.x >= screenWidth - edgePanThreshold)
        {
            moveDirection += right;
        }
        else if (mousePosition.x <= edgePanThreshold)
        {
            moveDirection -= right;
        }

        transform.Translate(moveDirection.normalized * edgePanSpeed * Time.deltaTime, Space.World);
    }

    private void HandleZoomInput()
    {
        if (cinemachineFollow == null) return;

        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (scrollY != 0)
        {
            float zoomSign = Mathf.Sign(scrollY);
            float zoomStep = 2f; 
            targetZoomDistance -= zoomSign * zoomStep;  
            targetZoomDistance = Mathf.Clamp(targetZoomDistance, minZoomDistance, maxZoomDistance); 
        }
    }

    private void ApplySmoothZoom()
    {
        if (cinemachineFollow == null) return;

        float currentDistance = cinemachineFollow.FollowOffset.y;
        float smoothedDistance = Mathf.SmoothDamp(currentDistance, targetZoomDistance, ref zoomVelocity, zoomSmoothTime);
        
        cinemachineFollow.FollowOffset = new Vector3(cinemachineFollow.FollowOffset.x, smoothedDistance, cinemachineFollow.FollowOffset.z);
    }

    private void HandleTouchControls()
    {
        if (vcam == null) return;

        if (Input.touchCount == 1)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == UnityEngine.TouchPhase.Moved && !UnitSelectionManager.IsBoxSelectMode)
            {
                Vector3 forward = vcam.transform.forward;
                forward.y = 0f;
                forward.Normalize();

                Vector3 right = vcam.transform.right;
                right.y = 0f;
                right.Normalize();

                // Di chuyển camera ngược hướng vuốt ngón tay
                float zoomFactor = targetZoomDistance / maxZoomDistance;
                float sensitivity = touchPanSensitivity * zoomFactor;
                Vector3 moveDirection = -(right * touch.deltaPosition.x + forward * touch.deltaPosition.y) * sensitivity;

                transform.Translate(moveDirection, Space.World);
            }
        }
        else if (Input.touchCount == 2)
        {
            Touch touch0 = Input.GetTouch(0);
            Touch touch1 = Input.GetTouch(1);

            // Bỏ qua khung hình đầu tiên khi chạm ngón tay mới để tránh bị giật hình
            if (touch0.phase == UnityEngine.TouchPhase.Began || touch1.phase == UnityEngine.TouchPhase.Began)
            {
                return;
            }

            // 1. Pinch to Zoom
            float currentDistance = Vector2.Distance(touch0.position, touch1.position);
            Vector2 prevPos0 = touch0.position - touch0.deltaPosition;
            Vector2 prevPos1 = touch1.position - touch1.deltaPosition;
            float prevDistance = Vector2.Distance(prevPos0, prevPos1);

            if (prevDistance > 0f)
            {
                float zoomDelta = (currentDistance - prevDistance) * touchZoomSensitivity;
                targetZoomDistance -= zoomDelta;
                targetZoomDistance = Mathf.Clamp(targetZoomDistance, minZoomDistance, maxZoomDistance);
            }

            // 2. Rotate Camera
            Vector2 currentVector = touch1.position - touch0.position;
            float currentAngle = Mathf.Atan2(currentVector.y, currentVector.x) * Mathf.Rad2Deg;

            Vector2 prevVector = prevPos1 - prevPos0;
            float prevAngle = Mathf.Atan2(prevVector.y, prevVector.x) * Mathf.Rad2Deg;

            float angleDelta = Mathf.DeltaAngle(prevAngle, currentAngle);
            transform.Rotate(Vector3.up, -angleDelta * touchRotationSensitivity, Space.World);
        }
    }

    private void HandlePan()
    {
        if (vcam == null) return;

        Vector3 moveDirection = Vector3.zero;

        Vector3 forward = vcam.transform.forward;
        forward.y = 0;
        forward.Normalize();

        Vector3 right = vcam.transform.right;
        right.y = 0;
        right.Normalize();

        if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
        {
            moveDirection += forward;
        }
        if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
        {
            moveDirection -= forward;
        }

        if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
        {
            moveDirection -= right;
        }

        if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
        {
            moveDirection += right;
        }

        transform.Translate(moveDirection.normalized * panSpeed * Time.deltaTime, Space.World);
    }
}
