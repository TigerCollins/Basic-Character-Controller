using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem; //Relates to the new input system and not the default unity system (as of Unity 2021.4)

[RequireComponent(typeof(CharacterController))]
public class BasicMovementScript : MonoBehaviour
{
    /// <summary>
    /// Visible to inspector
    /// </summary>
    [Header("BASIC MOVEMENT SCRIPT")]

    [Space(30)]

    [SerializeField]
    private CharacterController _characterController;
    private InputActionAsset _inputAction;


    [Header("Control Details")]
    [SerializeField]
    private GameObject _playerAvatarObject;
    [SerializeField]
    private ControlTypeEnum _controlType;
    public bool isPlayer = true;
    [SerializeField]
    private MovementDetails _movementControl;
    [SerializeField]
    private FirstPersonDetails _firstPersonControl;
    [SerializeField]
    private JumpDetails jumpDetails;
        [SerializeField]
    private SprintDetails sprintDetails;


    [Header("World Interaction")]
    [ReadOnly]
    [SerializeField]
    private Vector3 _currentMovement; //debug purposes only
    [SerializeField]
    private GravityOptions _gravitySettings;
    [SerializeField]
    private RaycastDetails _raycast;
   


    /// <summary>
    /// Invisble to Inspector
    /// </summary>
    private Vector3 desiredMovementDirection;
    private float _horizontalAxis;
    private float _verticalAxis;

    private float _horizontalLookAxis;
    private float _verticalLookAxis;

    Vector3 velocity;
    float gravity;


    float baseFOV;
    float newFOV;
    float baseSprintMultipler = 1;
    float actualSprintMultipler = 1;
    bool isSprinting;
    bool hasJumped;

    //cam tilt
    float currentTilt = 0f;


    // Start is called before the first frame update
    void Awake()
    {
        CheckForNullReference();
        AwakeGravity();
        Cursor.lockState = CursorLockMode.Locked;
        SprintSetup();
    }

    private void SprintSetup()
    {
        if(isPlayer)
        baseFOV = _firstPersonControl.firstPersonCamera.fieldOfView;
        newFOV = baseFOV;
    }

    public enum ControlTypeEnum
    {
        SideScroller,
        Topdown,
        FirstPerson
    }

    public void CheckForNullReference()
    {
        //If the user hasn't assigned the CharacterController script...
        if (_characterController == null)
        {
            //A more effecient version of GetComponent<>
            if (TryGetComponent(out CharacterController newCharacterController))
            {
                _characterController = newCharacterController;
            }
        }

        //If the user hasn't assigned the InputActionAsset...
        if (_inputAction == null)
        {
            PlayerInput tempPlayerInput = FindObjectOfType<PlayerInput>();
            //If no PlayerInput script is in scene
            if (tempPlayerInput == null)
            {
                Debug.LogWarning("Could not find PlayerInput script for " + gameObject.name + ". Please add component to scene or assign InputAction manually");
            }

            //If Player input hasn't been assign a InputActionAsset...
            else if (tempPlayerInput.actions == null)
            {
                Debug.LogWarning("The Actions variable for " + tempPlayerInput.gameObject.name + " is null. Please assign variable on " + tempPlayerInput.gameObject.name + " or assign component on BasicMovementScript");
            }

            //If everything is assigned on other objects...
            else
            {
                _inputAction = tempPlayerInput.actions;

            }

        }

        //If the Raycast starting point hasn't been assigned...
        if (_raycast.raycastPoint == null && _raycast.useRaycast)
        {
            Debug.LogWarning("Could not find Raycast Point transform. Raycast may not be in the desired position as a result, add a reference if it is inaccurate");
            _raycast.raycastPoint = transform;
        }
    }

    // Update is called once per frame
    void Update()
    {
        CheckGravity();
        ApplyGravity();
        Movement(_movementControl.moveAxis);
        CheckGrounded();
        FOVLerp();

        if (_raycast.useRaycast)
        {
            Debug.DrawRay(_raycast.raycastPoint.position, _raycast.raycastPoint.forward * _raycast.raycastDistance, _raycast.raycastColour, Time.deltaTime);
            //Raycast();
        }
    }


    void FixedUpdate()
    {
        CameraTilter();

    }
    /// <summary>
    /// Gravity specific functions
    /// </summary>

    void AwakeGravity()
    {
        if (_gravitySettings.useCustomGravity && _gravitySettings.customGravityDetails != _gravitySettings.previousGravityStrength)
        {
            gravity = _gravitySettings.customGravityDetails.y;
            _gravitySettings.previousGravityStrength = _gravitySettings.customGravityDetails;
        }

        else if (Physics.gravity != _gravitySettings.previousGravityStrength)
        {
            _gravitySettings.previousGravityStrength = Physics.gravity;
            gravity = Physics.gravity.y;
        }
    }

    void ApplyGravity()
    {
        //Reset the MoveVector
        //  Debug.Log(gameObject.name + ": Character controller is currently " + _characterController.isGrounded);
        if (_gravitySettings.useCustomGravity )
        {
            _currentMovement = Vector3.zero;
            velocity += _gravitySettings.customGravityDetails * Time.deltaTime;
            _currentMovement += _gravitySettings.customGravityDetails;
        }

        else if (!_gravitySettings.useCustomGravity)
        {
            _currentMovement = Vector3.zero;
            velocity.y += Physics.gravity.y * Time.deltaTime; 
            _currentMovement += Physics.gravity;
        }
        _characterController.Move(velocity * Time.deltaTime);

    }

    void CheckGravity()
    {
        if (_gravitySettings.useCustomGravity && _gravitySettings.customGravityDetails != _gravitySettings.previousGravityStrength)
        {
            _gravitySettings.previousGravityStrength = _gravitySettings.customGravityDetails;
            gravity = _gravitySettings.customGravityDetails.y;
            _gravitySettings.onCustomGravityStrengthChange.Invoke();
        }

        else if (Physics.gravity != _gravitySettings.previousGravityStrength)
        {
            _gravitySettings.previousGravityStrength = Physics.gravity;
            gravity = Physics.gravity.y;

            _gravitySettings.onCustomGravityStrengthChange.Invoke();
        }
    }

    /// <summary>
    /// Movement related functions.
    /// <param name="context"></param> relates to the use of the PlayerInputModule
    /// </summary>

    public void AdjustMovementVector(InputAction.CallbackContext context)
    {
        _movementControl.moveAxis = context.ReadValue<Vector2>();  //Mainly for debugging. You can place Context.ReadValue directly into the Movement argument if desired.
    }

    public void AdjustLookVector(InputAction.CallbackContext context)
    {
        if (_controlType == ControlTypeEnum.FirstPerson)
        {
            _firstPersonControl.lookAxis = context.ReadValue<Vector2>();  //Mainly for debugging. You can place Context.ReadValue directly into the Movement argument if desired.
            _horizontalLookAxis += (_firstPersonControl.xSensitivity / 10) * _firstPersonControl.lookAxis.x;
            
            float newVerticalLookAxis = _verticalLookAxis + ((_firstPersonControl.ySensitivity / 10) * _firstPersonControl.lookAxis.y);
            _verticalLookAxis = Mathf.Clamp(newVerticalLookAxis, _firstPersonControl.yAxis.bottomClamp, _firstPersonControl.yAxis.topClamp);
            // CameraTilter();
            if (_firstPersonControl.yAxis.invertY)
            {
                _firstPersonControl.firstPersonCamera.transform.localEulerAngles = new Vector3(_verticalLookAxis, 0, 0);
                _firstPersonControl.firstPersonCamera.transform.parent.transform.localEulerAngles = new Vector3(0, _horizontalLookAxis, 0);
                
            }

            else
            {
                _firstPersonControl.firstPersonCamera.transform.localEulerAngles = new Vector3(-_verticalLookAxis, 0, 0);
                _firstPersonControl.firstPersonCamera.transform.parent.transform.localEulerAngles = new Vector3(0, _horizontalLookAxis, 0);
          

            }
           // transform.Rotate(Vector3.up * _firstPersonControl.lookAxis.x);
        }
    }

    

    public void JumpFunction(InputAction.CallbackContext callbackContext)
    {
        if(jumpDetails.canJump && callbackContext.performed && jumpDetails.isGrounded && !hasJumped)
        {
            hasJumped = true;
            if(_gravitySettings.useCustomGravity)
            {
                velocity.y = Mathf.Sqrt(jumpDetails.jumpHeight * -2f * gravity);

            }

            else
            {
                velocity.y = Mathf.Sqrt(jumpDetails.jumpHeight * -2f * gravity);

            }
            
        }

    }

    public void CheckGrounded()
    {
        if(jumpDetails.groudCheck != null)
        {
            jumpDetails.isGrounded = Physics.CheckSphere(jumpDetails.groudCheck.position, jumpDetails.groundDistance, jumpDetails.groundLayerMask);
            if(jumpDetails.isGrounded == false)
            {
                hasJumped = false;
            }
        }
    }

    public void CameraTilter()
    {
        if (_controlType == ControlTypeEnum.FirstPerson)
        {
          //  localTilt = 0;

            // if (-_firstPersonControl.lookAxis.x > -1f && _firstPersonControl.lookAxis.x > 1f)
            // {
            float localTilt = -_firstPersonControl.lookAxis.x * 6 * _firstPersonControl.tiltStrengthMultiplier;
          //  }


            currentTilt = Mathf.Lerp(currentTilt, -_firstPersonControl.lookAxis.x, _firstPersonControl.camLerpTime * Time.fixedDeltaTime);
          

            float camTilt = Mathf.Lerp(currentTilt, localTilt, _firstPersonControl.camLerpTime * Time.fixedDeltaTime);
           // currentTilt = camTilt;
            Debug.Log(camTilt);
            Vector3 newTiltAngle = new Vector3(0, 0, camTilt);

            _firstPersonControl.firstPersonCamera.transform.parent.parent.transform.localRotation = Quaternion.Euler(newTiltAngle);
          //  _firstPersonControl.firstPersonCamera.transform.localRotation = Quaternion.Euler((Vector3.left * _verticalLookAxis) + (Vector3.forward * _horizontalLookAxis * _firstPersonControl.cameraTilt) + (Vector3.forward * currentTilt));
        }
    }

    //This allows for events on Get and Set functions for MaxMovementClamp.
    public float MaxMovementClamp
    {
        get
        {
            return _movementControl.maxMovementClamp;
        }

        set
        {
            //to save performance, this lives in a if statement
            if (value != _movementControl.maxMovementClamp)
            {
                _movementControl.maxMovementClamp = value;
            }
        }
    }

    //This allows for events on Get and Set functions for MovementMultiplier.
    public float MovementMultiplier
    {
        get
        {
            return _movementControl.movementMultipler;
        }

        set
        {
            //to save performance, this lives in a if statement
            if (value != _movementControl.movementMultipler)
            {
                _movementControl.movementMultipler = value;
            }
        }
    }

    public void Raycast(InputAction.CallbackContext context)
    {
        RaycastHit hit;
        if (_raycast.useRaycast && Physics.Raycast(_raycast.raycastPoint.position, _raycast.raycastPoint.forward, out hit, _raycast.raycastDistance) && context.performed)
        {
            //Below is the if statement to find objects. Can be used from Unity 2017 onwards, otherwise use GetComponent instead of TryGetComponent()
            /*
             if (hit.collider.TryGetComponent(out QuipScript newQuipScript))
             {
                 newQuipScript.UpdateText();
             }
             */
        }
    }

    void Movement(Vector2 _inputTranslation)
    {
        //Sets up movement
        _horizontalAxis = _inputTranslation.x;
        _verticalAxis = _inputTranslation.y;

        //Sets up Rotation
        Vector3 _rotationDirection;
        float rotationX = -_inputTranslation.x;
        float rotationY = _inputTranslation.y;

        //Sets movement and then calls rotation function
        switch (_controlType)
        {
            case ControlTypeEnum.SideScroller:
                desiredMovementDirection = Vector3.ClampMagnitude(new Vector3(_horizontalAxis, 0, 0), _movementControl.maxMovementClamp);
                _rotationDirection = Vector3.ClampMagnitude(new Vector3(0, 0, rotationX), _movementControl.maxMovementClamp);
                _characterController.Move(desiredMovementDirection * Time.deltaTime * MovementMultiplier * actualSprintMultipler);
                MovementRotation(_rotationDirection);
                break;
            case ControlTypeEnum.Topdown:
                desiredMovementDirection = Vector3.ClampMagnitude(new Vector3(_horizontalAxis, 0, _verticalAxis), _movementControl.maxMovementClamp);
                _rotationDirection = Vector3.ClampMagnitude(new Vector3(rotationY, 0, rotationX), _movementControl.maxMovementClamp);
                _characterController.Move(desiredMovementDirection * Time.deltaTime * MovementMultiplier * actualSprintMultipler);
                MovementRotation(_rotationDirection);
                break;
            case ControlTypeEnum.FirstPerson:
                if (_firstPersonControl.firstPersonCamera != null)
                {
                    // Vector3 camera
                    desiredMovementDirection = _firstPersonControl.firstPersonCamera.transform.right * _horizontalAxis + _firstPersonControl.firstPersonCamera.transform.parent.transform.forward * _verticalAxis;
                    _characterController.Move(desiredMovementDirection * Time.deltaTime * MovementMultiplier * actualSprintMultipler);
                }

                else
                {
                    Debug.LogError("Couldn't find first person camera, assign the missing variable.");
                }

                break;
            default:
                break;
        }
    }

    void MovementRotation(Vector3 _desiredMoveDirection)
    {
        if (_desiredMoveDirection != Vector3.zero && _playerAvatarObject != null)
        {
            _playerAvatarObject.transform.rotation = Quaternion.Slerp(_playerAvatarObject.transform.rotation, Quaternion.LookRotation(_desiredMoveDirection), _movementControl.rotationSpeed / 10);
        }

        else if (_desiredMoveDirection != Vector3.zero)
        {
            Debug.LogError("Could not find Player Avatar Object as part of the BasicMovementScript " + gameObject.name + ". Resolve null reference to get player rotation");
        }
    }

    public void SprintMovement(InputAction.CallbackContext callbackContext)
    {
        if(callbackContext.performed || callbackContext.started )
        {
            if(_controlType == ControlTypeEnum.FirstPerson && sprintDetails.canSprint)
            {
                if (_movementControl.moveAxis.y > 0)
                {
                    isSprinting = true;

                    newFOV = baseFOV * sprintDetails.fovMultiplier;
                    actualSprintMultipler = sprintDetails.sprintMultiplier;
                }
            }

        }

        else if(callbackContext.canceled && sprintDetails.canSprint)
        {
            if (_controlType == ControlTypeEnum.FirstPerson)
            {
                isSprinting = false;
                newFOV = baseFOV;
                actualSprintMultipler = baseSprintMultipler;
            }
        }
    }

    public void IsSprintingCheck()
    {
        if(_movementControl.moveAxis.y < 0 || _movementControl.moveAxis.x != 0)
        {
            isSprinting = false;
        }
    }

    public void FOVLerp()
    {
        if(_controlType == ControlTypeEnum.FirstPerson)
        {
            if(_movementControl.moveAxis.y > 0 && isSprinting)
            {
                _firstPersonControl.firstPersonCamera.fieldOfView = Mathf.Lerp(_firstPersonControl.firstPersonCamera.fieldOfView, newFOV, sprintDetails.fovLerpTime * Time.deltaTime);

            }

            else if(!isSprinting)
            {
                _firstPersonControl.firstPersonCamera.fieldOfView = Mathf.Lerp(_firstPersonControl.firstPersonCamera.fieldOfView, baseFOV, sprintDetails.fovLerpTime * Time.deltaTime);

            }
        }
    }
}

[System.Serializable]
public class GravityOptions
{
    public bool useCustomGravity;
    public Vector3 customGravityDetails = new Vector3(0, .2f, 0);
    [HideInInspector]
    public Vector3 previousGravityStrength;

    [Space(10)]

    public UnityEvent onCustomGravityStrengthChange;
}

[System.Serializable]
public class RaycastDetails
{
    public bool useRaycast;
    [Tooltip("This decides where the raycast comes from Leave this variable blank for it to default to this gameobject.")]
    public Transform raycastPoint;
    public float raycastDistance;
    public Color raycastColour;
}

[System.Serializable]
public class MovementDetails
{
    public float movementMultipler = 2;
    [Tooltip("If set correctly, the idea is that it stops diagonal movement from simply combining Horizontal and Vertical speeds.")]
    public float maxMovementClamp = 1;
    [ReadOnly]
    public Vector2 moveAxis; //debug purposes only

    [Space(10)]

    [Range(0, 5)]
    public float rotationSpeed = .75f;
}

[System.Serializable]
public class FirstPersonDetails
{ 
    public Vector2 lookAxis;
    public Camera firstPersonCamera;

    [Space(5)]

    [Range(0, 10)]
    public float ySensitivity = 3;
    [Range(0, 10)]
    public float xSensitivity = 3;
    public YAxisDetails yAxis;

    [Space(5)]


    public float tiltStrengthMultiplier = 5f;
    public float camLerpTime = 5f;
    public float cameraTilt = 0.2f;

}

[System.Serializable]
public class YAxisDetails
{
    public bool invertY;

    [Space(5)]

    public float bottomClamp;
    public float topClamp;
}


[System.Serializable]
public class JumpDetails
{
    public bool canJump;
    public float jumpHeight;
    public bool isGrounded;

    [Space(10)]

    public Transform groudCheck;
    public float groundDistance = .4f;
    public LayerMask groundLayerMask;
}

[System.Serializable]
public class SprintDetails
{
    public bool canSprint;
    public float sprintMultiplier;
    public float fovMultiplier;
    public float fovLerpTime;
}