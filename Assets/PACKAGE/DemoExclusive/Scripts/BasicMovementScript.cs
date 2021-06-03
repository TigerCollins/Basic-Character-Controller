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
    public float movementMultipler = 2;
    [Tooltip("If set correctly, the idea is that it stops diagonal movement from simply combining Horizontal and Vertical speeds.")]
    public float maxMovementClamp = 1;
    [ReadOnly]
    [SerializeField]
    private Vector2 _moveAxis; //debug purposes only

    [Space(10)]

    [Range(0, 5)]
    [SerializeField]
    private float _rotationSpeed = .75f;

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

    // Start is called before the first frame update
    void Awake()
    {
        CheckForNullReference();
        AwakeGravity();
    }

    private void Start()
    {

    }

    public enum ControlTypeEnum
    {
        SideScroller,
        Topdown
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
        Movement(_moveAxis);
        if (_raycast.useRaycast)
        {
            Debug.DrawRay(_raycast.raycastPoint.position, _raycast.raycastPoint.forward * _raycast.raycastDistance, _raycast.raycastColour, Time.deltaTime);
            //Raycast();
        }
    }

    /// <summary>
    /// Gravity specific functions
    /// </summary>

    void AwakeGravity()
    {
        if (_gravitySettings.useCustomGravity && _gravitySettings.customGravityDetails != _gravitySettings.previousGravityStrength)
        {
            _gravitySettings.previousGravityStrength = _gravitySettings.customGravityDetails;
        }

        else if (Physics.gravity != _gravitySettings.previousGravityStrength)
        {
            _gravitySettings.previousGravityStrength = Physics.gravity;
        }
    }

    void ApplyGravity()
    {
        //Reset the MoveVector
        //  Debug.Log(gameObject.name + ": Character controller is currently " + _characterController.isGrounded);
        if (_gravitySettings.useCustomGravity && !_characterController.isGrounded)
        {
            _currentMovement = Vector3.zero;
            _characterController.Move(_gravitySettings.customGravityDetails * Time.deltaTime);
            _currentMovement += _gravitySettings.customGravityDetails;
        }

        else if (!_gravitySettings.useCustomGravity && !_characterController.isGrounded)
        {
            _currentMovement = Vector3.zero;
            _characterController.Move(Physics.gravity * Time.deltaTime); ;
            _currentMovement += Physics.gravity;
        }
    }

    void CheckGravity()
    {
        if (_gravitySettings.useCustomGravity && _gravitySettings.customGravityDetails != _gravitySettings.previousGravityStrength)
        {
            _gravitySettings.previousGravityStrength = _gravitySettings.customGravityDetails;
            _gravitySettings.onCustomGravityStrengthChange.Invoke();
        }

        else if (Physics.gravity != _gravitySettings.previousGravityStrength)
        {
            _gravitySettings.previousGravityStrength = Physics.gravity;
            _gravitySettings.onCustomGravityStrengthChange.Invoke();
        }
    }

    /// <summary>
    /// Movement related functions.
    /// <param name="context"></param> relates to the use of the PlayerInputModule
    /// </summary>

    public void adjustMovementVector(InputAction.CallbackContext context)
    {
        _moveAxis = context.ReadValue<Vector2>();  //Mainly for debugging. You can place Context.ReadValue directly into the Movement argument if desired.
    }


    //This allows for events on Get and Set functions for MaxMovementClamp.
    public float MaxMovementClamp
    {
        get
        {
            return maxMovementClamp;
        }

        set
        {
            //to save performance, this lives in a if statement
            if (value != maxMovementClamp)
            {
                maxMovementClamp = value;
            }
        }
    }

    //This allows for events on Get and Set functions for MovementMultiplier.
    public float MovementMultiplier
    {
        get
        {
            return movementMultipler;
        }

        set
        {
            //to save performance, this lives in a if statement
            if (value != movementMultipler)
            {
                movementMultipler = value;
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
                desiredMovementDirection = Vector3.ClampMagnitude(new Vector3(_horizontalAxis, 0, 0), maxMovementClamp);
                _rotationDirection = Vector3.ClampMagnitude(new Vector3(0, 0, rotationX), maxMovementClamp);
                _characterController.Move(desiredMovementDirection * Time.deltaTime * MovementMultiplier);
                MovementRotation(_rotationDirection);
                break;
            case ControlTypeEnum.Topdown:
                desiredMovementDirection = Vector3.ClampMagnitude(new Vector3(_horizontalAxis, 0, _verticalAxis), maxMovementClamp);
                _rotationDirection = Vector3.ClampMagnitude(new Vector3(rotationY, 0, rotationX), maxMovementClamp);
                _characterController.Move(desiredMovementDirection * Time.deltaTime * MovementMultiplier);
                MovementRotation(_rotationDirection);
                break;
            default:
                break;
        }
    }

    void MovementRotation(Vector3 _desiredMoveDirection)
    {
        if (_desiredMoveDirection != Vector3.zero && _playerAvatarObject != null)
        {
            _playerAvatarObject.transform.rotation = Quaternion.Slerp(_playerAvatarObject.transform.rotation, Quaternion.LookRotation(_desiredMoveDirection), _rotationSpeed / 10);
        }

        else if (_desiredMoveDirection != Vector3.zero)
        {
            Debug.LogError("Could not find Player Avatar Object as part of the BasicMovementScript " + gameObject.name + ". Resolve null reference to get player rotation");
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
