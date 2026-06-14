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
        HandlePan();
        HandleZoom();
        HandleEdgePan();   
        HandleRotation(); 
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

    private void HandleZoom()
    {
        if (cinemachineFollow == null) return;


        float scrollY = Mouse.current.scroll.ReadValue().y;
        if(scrollY != 0)
        {
           float zoomSign = Mathf.Sign(scrollY);
           float zoomStep = 2f; 
           targetZoomDistance -= zoomSign * zoomStep;  
           targetZoomDistance = Mathf.Clamp(targetZoomDistance, minZoomDistance, maxZoomDistance); 
        }

        float currentDistance = cinemachineFollow.FollowOffset.y;
        float smoothedDistance = Mathf.SmoothDamp(currentDistance, targetZoomDistance, ref zoomVelocity, zoomSmoothTime);
        
        cinemachineFollow.FollowOffset = new Vector3(cinemachineFollow.FollowOffset.x, smoothedDistance, cinemachineFollow.FollowOffset.z);
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

        if (Keyboard.current.wKey.isPressed)
        {
            moveDirection += forward;
        }
        if (Keyboard.current.sKey.isPressed)
        {
            moveDirection -= forward;
        }
        if (Keyboard.current.aKey.isPressed)
        {
            moveDirection -= right;
        }
        if (Keyboard.current.dKey.isPressed)
        {
            moveDirection += right;
        }

        transform.Translate(moveDirection.normalized * panSpeed * Time.deltaTime, Space.World);
    }
}
