﻿using Unity.FPS.Game;
using UnityEngine;
using UnityEngine.Events;

namespace Unity.FPS.Gameplay
{
    [RequireComponent(typeof(CharacterController), typeof(PlayerInputHandler), typeof(AudioSource))]
    #region
    public class PlayerCharacterController : MonoBehaviour
    {
        [Header("References")] [Tooltip("Reference to the main camera used for the player")]
        public Camera PlayerCamera;

        [Tooltip("Audio source for footsteps, jump, etc...")]
        public AudioSource AudioSource;

        [Header("General")] [Tooltip("Force applied downward when in the air")]
        public float GravityDownForce = 20f;

        [Tooltip("Physic layers checked to consider the player grounded")]
        public LayerMask GroundCheckLayers = -1;

        [Tooltip("distance from the bottom of the character controller capsule to test for grounded")]
        public float GroundCheckDistance = 0.05f;

        [Header("Movement")] [Tooltip("Max movement speed when grounded (when not sprinting)")]
        public float MaxSpeedOnGround = 10f;

        [Tooltip(
            "Sharpness for the movement when grounded, a low value will make the player accelerate and decelerate slowly, a high value will do the opposite")]
        public float MovementSharpnessOnGround = 15;

        [Tooltip("Max movement speed when crouching")] [Range(0, 1)]
        public float MaxSpeedCrouchedRatio = 0.5f;

        [Tooltip("Max movement speed when not grounded")]
        public float MaxSpeedInAir = 10f;

        [Tooltip("Acceleration speed when in the air")]
        public float AccelerationSpeedInAir = 25f;

        [Tooltip("Acceleration when sliding")]
        public float SlideSpeed = 20f;

        [Tooltip("Multiplicator for the sprint speed (based on grounded speed)")]
        public float SprintSpeedModifier = 2f;

        [Tooltip("Drag value to slow down player while sliding")]
        private float dragFriction = 0.5f;

        [Tooltip("Height at which the player dies instantly when falling off the map")]
        public float KillHeight = -50f;

        [Header("Rotation")] [Tooltip("Rotation speed for moving the camera")]
        public float RotationSpeed = 200f;

        [Range(0.1f, 1f)] [Tooltip("Rotation speed multiplier when aiming")]
        public float AimingRotationMultiplier = 0.4f;

        [Header("Jump")] [Tooltip("Force applied upward when jumping")]
        public float JumpForce = 9f;

        [Header("Stance")] [Tooltip("Ratio (0-1) of the character height where the camera will be at")]
        public float CameraHeightRatio = 0.9f;

        [Tooltip("Height of character when standing")]
        public float CapsuleHeightStanding = 1.8f;

        [Tooltip("Height of character when crouching")]
        public float CapsuleHeightCrouching = 0.9f;

        [Tooltip("Speed of crouching transitions")]
        public float CrouchingSharpness = 10f;

        [Header("Audio")] [Tooltip("Amount of footstep sounds played when moving one meter")]
        public float FootstepSfxFrequency = 1f;

        [Tooltip("Amount of footstep sounds played when moving one meter while sprinting")]
        public float FootstepSfxFrequencyWhileSprinting = 1f;

        [Tooltip("Sound played for footsteps")]
        public AudioClip FootstepSfx;

        [Tooltip("Sound played when jumping")] public AudioClip JumpSfx;
        [Tooltip("Sound played when landing")] public AudioClip LandSfx;

        [Tooltip("Sound played when taking damage from fall")]
        public AudioClip FallDamageSfx;

        [Header("Fall Damage")]
        [Tooltip("Whether the player will recieve damage when hitting the ground at high speed")]
        public bool RecievesFallDamage;

        [Tooltip("Minimun fall speed for recieving fall damage")]
        public float MinSpeedForFallDamage = 10f;

        [Tooltip("Fall speed for recieving th emaximum amount of fall damage")]
        public float MaxSpeedForFallDamage = 30f;

        [Tooltip("Damage recieved when falling at the mimimum speed")]
        public float FallDamageAtMinSpeed = 10f;

        [Tooltip("Damage recieved when falling at the maximum speed")]
        public float FallDamageAtMaxSpeed = 50f;

        [Header("Funny test")]
        public GameObject gameObjectSphere;
        private GameObject collidingSphere;
        public GameObject playerMesh;
        private bool collided;

        [Header("Variables for grappling")]
        //Referencing
        public Transform cam;
        public Transform gunTip;
        public LineRenderer lr;
        public float grappleSpeed = 30f;

        //Grappling
        public float grappleDistance;
        private Vector3 targetPosition;
        public LayerMask whatIsGrappleable;

        //Cooldown
        public float grapplingCd;
        [SerializeField]
        private float grapplingCdTimer;
        private float k_HookGroundingPreventionTime = 0.5f;
        public float hookTimer;
        private bool grappling;

        [Header("Sliding Variables")]
        private bool isSliding = false;
        private bool m_justSlide = false;
        //Drag already exist
        public Vector3 currentPos;
        public Vector3 LastPos;
        private Vector3 slideDirection;
        public float speed;

        [Header("Animation")]
        public Animator animator;

        [Header("Intialized at Start")]
        public UnityAction<bool> OnStanceChanged;

        public Vector3 CharacterVelocity { get; set; }
        public bool IsGrounded { get; set; }
        public bool HasJumpedThisFrame { get; set; }
        public bool IsDead { get; private set; }
        public bool IsCrouching { get; private set; }
#endregion
        public float RotationMultiplier
        {
            get
            {
                if (m_WeaponsManager.IsAiming)
                {
                    return AimingRotationMultiplier;
                }

                return 1f;
            }
        }

        #region

        Health m_Health;
        public PlayerInputHandler m_InputHandler;
        public CharacterController m_Controller;
        PlayerWeaponsManager m_WeaponsManager;
        public Actor m_Actor;
        public Vector3 m_GroundNormal;
        Vector3 m_CharacterVelocity;
        Vector3 m_LatestImpactSpeed;
        public float m_LastTimeJumped = 0f;
        public bool m_LastTimeHooked = false;
        float m_CameraVerticalAngle = 0f;
        float m_FootstepDistanceCounter;
        public float m_TargetCharacterHeight;

        const float k_JumpGroundingPreventionTime = 0.2f;
        const float k_GroundCheckDistanceInAir = 0.07f;
        #endregion

        void Awake()
        {
            ActorsManager actorsManager = FindObjectOfType<ActorsManager>();
            if (actorsManager != null)
                actorsManager.SetPlayer(gameObject);
        }

        void Start()
        {
            // fetch components on the same gameObject
            m_Controller = GetComponent<CharacterController>();
            DebugUtility.HandleErrorIfNullGetComponent<CharacterController, PlayerCharacterController>(m_Controller,
                this, gameObject);

            m_InputHandler = GetComponent<PlayerInputHandler>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerInputHandler, PlayerCharacterController>(m_InputHandler,
                this, gameObject);

            m_WeaponsManager = GetComponent<PlayerWeaponsManager>();
            DebugUtility.HandleErrorIfNullGetComponent<PlayerWeaponsManager, PlayerCharacterController>(
                m_WeaponsManager, this, gameObject);

            m_Health = GetComponent<Health>();
            DebugUtility.HandleErrorIfNullGetComponent<Health, PlayerCharacterController>(m_Health, this, gameObject);

            m_Actor = GetComponent<Actor>();
            DebugUtility.HandleErrorIfNullGetComponent<Actor, PlayerCharacterController>(m_Actor, this, gameObject);

            m_Controller.enableOverlapRecovery = true;

            //animator = GetComponent<Animator>();
            Debug.Log(animator);

            m_Health.OnDie += OnDie;

            // force the crouch state to false when starting
            SetCrouchingState(false, true);
            UpdateCharacterHeight(true);
            lr.enabled = false;
        }

        void Update()
        {
            bool isSprinting = m_InputHandler.GetSprintInputHeld();

            playerMesh.transform.rotation = Quaternion.Euler(cam.forward.x, cam.forward.y, cam.forward.z);

            if (m_InputHandler.GetMoveInput().magnitude > 0.5f)
            {
                animator.SetBool("isWalking", true);
            }
            else
            {
                animator.SetBool("isWalking", false);
            }

            // check for Y kill
            if (!IsDead && transform.position.y < KillHeight)
            {
                m_Health.Kill();
            }

            HasJumpedThisFrame = false;

            bool wasGrounded = IsGrounded;

            GroundCheck();

            // landing
            if (IsGrounded && !wasGrounded)
            {
                // Fall damage
                float fallSpeed = -Mathf.Min(CharacterVelocity.y, m_LatestImpactSpeed.y);
                float fallSpeedRatio = (fallSpeed - MinSpeedForFallDamage) /
                                       (MaxSpeedForFallDamage - MinSpeedForFallDamage);
                if (RecievesFallDamage && fallSpeedRatio > 0f)
                {
                    float dmgFromFall = Mathf.Lerp(FallDamageAtMinSpeed, FallDamageAtMaxSpeed, fallSpeedRatio);
                    m_Health.TakeDamage(dmgFromFall, null);

                    // fall damage SFX
                    AudioSource.PlayOneShot(FallDamageSfx);
                }
                else
                {
                    // land SFX
                    AudioSource.PlayOneShot(LandSfx);
                }
            }

            if (m_InputHandler.GetSprintInputHeld() && !IsCrouching)
            {
                this.PlayerCamera.fieldOfView = 90f;
            }
            else if (m_InputHandler.GetSprintInputReleased())
            {
                this.PlayerCamera.fieldOfView = 80f;
                if (speed > 0)
                {
                    animator.SetBool("isWalking", true);
                }
                else animator.SetBool("isWalking", false);
            }

            Debug.Log(animator.GetBool("isWalking"));
            // crouching or sliding or hooking
            if (m_InputHandler.GetHookDown())
            {
                if (canHook() && !grappling)
                {
                    SetCrouchingState(false, false);

                    m_LastTimeHooked = true;
                    hookTimer = k_HookGroundingPreventionTime;
                    Hook();

                    //Starting countdown for cooldown
                    grapplingCdTimer = grapplingCd;

                    // Force grounding to false
                    IsGrounded = false;
                }
                else { grappling = false; }
                
            }
            else if (m_InputHandler.GetCrouchInputDown() && isSprinting && !IsCrouching && speed > 2f)
            {
                Sliding();
                animator.SetBool("isSliding", true);
            }
            else if (m_InputHandler.GetCrouchInputDown() && !isSprinting && !IsCrouching)
            {
                SetCrouchingState(!IsCrouching, false);
                animator.SetBool("isCrouching", true);
                UpdateCharacterHeight(false);

            }  else if(IsCrouching && m_InputHandler.GetCrouchInputDown())
            {
                SetCrouchingState(false, true);
                animator.SetBool("isCrouching", false);
                UpdateCharacterHeight(false);
            }

            if (hookTimer > 0) hookTimer -= Time.deltaTime;
            if (grapplingCdTimer > 0) grapplingCdTimer -= Time.deltaTime;


            UpdateCharacterHeight(false);
            //Debug.Log(CharacterVelocity);
            HandleCharacterMovement();
        }

        private void LateUpdate()
        {
            lr.SetPosition(0, gunTip.position);

            if (speed < 0.2f)
            {
                animator.SetBool("isWalking", false);
            }
            
        }
        private void FixedUpdate() 
        {
            var Direction = targetPosition - transform.position;

            if (grappling)
            {
                CharacterVelocity = new Vector3(0f, 0f, 0f);

                //Debug.Log(Direction.magnitude);

                CharacterVelocity += Direction.normalized * grappleSpeed;
            }

            currentPos = transform.position;
            speed = (currentPos - LastPos).magnitude / Time.deltaTime;
            //Debug.Log(speed);
            LastPos = currentPos;

            if (hookTimer <= 0 && Direction.magnitude < 0.5f)
            {
                grappling = false;
            }

            if (isSliding && speed > 2f)
            {
                CharacterVelocity -= cam.forward * dragFriction * Time.deltaTime;
                dragFriction += 2f;
                
            } 
            else if(m_justSlide && speed <= 2f)
            {
                isSliding = false;
                animator.SetBool("isSliding", false);
                animator.SetBool("isCrouching", true);
                m_justSlide = false;
                dragFriction = 0.5f;
                SetCrouchingState(true, true);
                UpdateCharacterHeight(true);
            }
            //else if (isSliding && speed < 2f) Debug.Log("Lost all speed"); isSliding = false; //dragFriction = 0.5f;

        }
        public void Sliding()
        {
            /*
             * Use crouch code for capsule height
             * Increase speed
             * Add gradually lower speed
             * when speed 0 set crouch to true
             */
            //Changes the character height

            m_justSlide = true;
            isSliding = true;
            slideDirection = cam.forward;

            CharacterVelocity = cam.forward * SlideSpeed;

            //CharacterVelocity = Vector3.back * dragFriction * Time.deltaTime;

            m_TargetCharacterHeight = CapsuleHeightCrouching;

            UpdateCharacterHeight(false);
        }
        private bool canHook()
        {
            if (grapplingCdTimer <= 0)
            {
                return true;
            }
            else return false;
        }
        private void DestroyLine()
        {
            lr.enabled = false;
        }
        public void Hook()
        {
            /*
             * Raycast to see that it hits the wall
            */
            if (grapplingCdTimer > 0) return;

            grappling = true;

            RaycastHit hit;

            if (Physics.Raycast(cam.position, cam.forward, out hit, grappleDistance, whatIsGrappleable))
            {
                targetPosition = hit.point;

                grapplingCdTimer = grapplingCd/2f;

                Invoke(nameof(DestroyLine),3f);
            }
            else
            {
                targetPosition = cam.position + cam.forward * grappleDistance;

                grappling = false;

                grapplingCdTimer = grapplingCd;

                Invoke(nameof(DestroyLine), 1f);

            }

            lr.enabled = true;
            lr.SetPosition(1, targetPosition);

        }
        public void OnDie()
        {
            IsDead = true;

            // Tell the weapons manager to switch to a non-existing weapon in order to lower the weapon
            m_WeaponsManager.SwitchToWeaponIndex(-1, true);

            EventManager.Broadcast(Events.PlayerDeathEvent);
        }

        public void GroundCheck()
        {
            
            // Make sure that the ground check distance while already in air is very small, to prevent suddenly snapping to ground
            float chosenGroundCheckDistance =
                IsGrounded ? (m_Controller.skinWidth + GroundCheckDistance) : k_GroundCheckDistanceInAir;

            // reset values before the ground check
            IsGrounded = false;
            m_GroundNormal = Vector3.up;

            // only try to detect ground if it's been a short amount of time since last jump; otherwise we may snap to the ground instantly after we try jumping
            if (hookTimer <= 0)
            {
                if (Time.time >= m_LastTimeJumped + k_JumpGroundingPreventionTime)
                {
                    // if we're grounded, collect info about the ground normal with a downward capsule cast representing our character capsule
                    if (Physics.CapsuleCast(GetCapsuleBottomHemisphere(), GetCapsuleTopHemisphere(m_Controller.height),
                        m_Controller.radius, Vector3.down, out RaycastHit hit, chosenGroundCheckDistance, GroundCheckLayers,
                        QueryTriggerInteraction.Ignore))
                    {
                        // storing the upward direction for the surface found
                        m_GroundNormal = hit.normal;

                        //Debug.Log(m_GroundNormal);
                        //Debug.Log(hit.normal);

                        // Only consider this a valid ground hit if the ground normal goes in the same direction as the character up
                        // and if the slope angle is lower than the character controller's limit
                        if (Vector3.Dot(hit.normal, transform.up) > 0f &&
                            IsNormalUnderSlopeLimit(m_GroundNormal))
                        {
                            IsGrounded = true;

                            // handle snapping to the ground
                            if (hit.distance > m_Controller.skinWidth)
                            {
                                m_Controller.Move(Vector3.down * hit.distance);

                            }
                        }
                    }
                }
            }
        }

        void HandleCharacterMovement()
        {
            // horizontal character rotation
            {
                // rotate the transform with the input speed around its local Y axis
                transform.Rotate(
                    new Vector3(0f, (m_InputHandler.GetLookInputsHorizontal() * RotationSpeed * RotationMultiplier),
                        0f), Space.Self);
            }

            // vertical camera rotation
            {
                // add vertical inputs to the camera's vertical angle
                m_CameraVerticalAngle += m_InputHandler.GetLookInputsVertical() * RotationSpeed * RotationMultiplier;

                // limit the camera's vertical angle to min/max
                m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -89f, 89f);

                // apply the vertical angle as a local rotation to the camera transform along its right axis (makes it pivot up and down)
                PlayerCamera.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);
            }

            // character movement handling
            bool isSprinting = m_InputHandler.GetSprintInputHeld();
            {
                if (isSprinting) isSprinting = SetCrouchingState(false, false);

                /*
                 if (isSprinting){
                    speedModifier = SprintSpeedModifier;
                 else speedModifier = 1f; 
                 thats what the bottom line is 
                 1*/

                float speedModifier = isSprinting ? SprintSpeedModifier : 1f;

                // converts move input to a worldspace vector based on our character's transform orientation
                Vector3 worldspaceMoveInput = transform.TransformVector(m_InputHandler.GetMoveInput());

                // handle grounded movement
                if (IsGrounded)
                {
                    // calculate the desired velocity from inputs, max speed, and current slope
                    Vector3 targetVelocity = worldspaceMoveInput * MaxSpeedOnGround * speedModifier;
                    // reduce speed if crouching by crouch speed ratio
                    if (IsCrouching)
                        targetVelocity *= MaxSpeedCrouchedRatio;
                    targetVelocity = GetDirectionReorientedOnSlope(targetVelocity.normalized, m_GroundNormal) *
                                     targetVelocity.magnitude;

                    // smoothly interpolate between our current velocity and the target velocity based on acceleration speed
                    CharacterVelocity = Vector3.Lerp(CharacterVelocity, targetVelocity,
                        MovementSharpnessOnGround * Time.deltaTime);

                    // jumping
                    if (IsGrounded && m_InputHandler.GetJumpInputDown())
                    {
                        isSliding = false;
                         // force the crouch state to false
                        if (SetCrouchingState(false, true))
                        {
                            // start by canceling out the vertical component of our velocity
                            CharacterVelocity = new Vector3(CharacterVelocity.x, 0f, CharacterVelocity.z);

                            // then, add the jumpSpeed value upwards
                            CharacterVelocity += Vector3.up * JumpForce;

                            // play sound
                            AudioSource.PlayOneShot(JumpSfx);

                            // remember last time we jumped because we need to prevent snapping to ground for a short time
                            m_LastTimeJumped = Time.time;
                            HasJumpedThisFrame = true;

                            // Force grounding to false
                            IsGrounded = false;
                            m_GroundNormal = Vector3.up;
                        }
                    }

                    // footsteps sound
                    float chosenFootstepSfxFrequency =
                        (isSprinting ? FootstepSfxFrequencyWhileSprinting : FootstepSfxFrequency);
                    if (m_FootstepDistanceCounter >= 1f / chosenFootstepSfxFrequency)
                    {
                        m_FootstepDistanceCounter = 0f;
                        AudioSource.PlayOneShot(FootstepSfx);
                    }

                    // keep track of distance traveled for footsteps sound
                    m_FootstepDistanceCounter += CharacterVelocity.magnitude * Time.deltaTime;
                }
                // handle air movement
                else if(!grappling)
                {
                    // add air acceleration
                    CharacterVelocity += worldspaceMoveInput * AccelerationSpeedInAir * Time.deltaTime;

                    // limit air speed to a maximum, but only horizontally
                    float verticalVelocity = CharacterVelocity.y;
                    Vector3 horizontalVelocity = Vector3.ProjectOnPlane(CharacterVelocity, Vector3.up);
                    horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, MaxSpeedInAir * speedModifier);
                    CharacterVelocity = horizontalVelocity + (Vector3.up * verticalVelocity);

                    // apply the gravity to the velocity
                    CharacterVelocity += Vector3.down * GravityDownForce * Time.deltaTime;
                }
            }

            // apply the final calculated velocity value as a character movement
            Vector3 capsuleBottomBeforeMove = GetCapsuleBottomHemisphere();
            Vector3 capsuleTopBeforeMove = GetCapsuleTopHemisphere(m_Controller.height);
            m_Controller.Move(CharacterVelocity * Time.deltaTime);

            // detect obstructions to adjust velocity accordingly
            m_LatestImpactSpeed = Vector3.zero;
            if (Physics.CapsuleCast(capsuleBottomBeforeMove, capsuleTopBeforeMove, m_Controller.radius,
                CharacterVelocity.normalized, out RaycastHit hit, CharacterVelocity.magnitude * Time.deltaTime, -1,
                QueryTriggerInteraction.Ignore))
            {
                // We remember the last impact speed because the fall damage logic might need it
                m_LatestImpactSpeed = CharacterVelocity;

                CharacterVelocity = Vector3.ProjectOnPlane(CharacterVelocity, hit.normal);
            }
        }

        // Returns true if the slope angle represented by the given normal is under the slope angle limit of the character controller
        public bool IsNormalUnderSlopeLimit(Vector3 normal)
        {
            return Vector3.Angle(transform.up, normal) <= m_Controller.slopeLimit;
        }

        

        // Gets the center point of the bottom hemisphere of the character controller capsule    
        public Vector3 GetCapsuleBottomHemisphere()
        {
            return transform.position + (transform.up * m_Controller.radius);
        }

        // Gets the center point of the top hemisphere of the character controller capsule    
        public Vector3 GetCapsuleTopHemisphere(float atHeight)
        {
            return transform.position + (transform.up * (atHeight - m_Controller.radius));
        }

        // Gets a reoriented direction that is tangent to a given slope
        public Vector3 GetDirectionReorientedOnSlope(Vector3 direction, Vector3 slopeNormal)
        {
            Vector3 directionRight = Vector3.Cross(direction, transform.up);
            return Vector3.Cross(slopeNormal, directionRight).normalized;
        }

        public void UpdateCharacterHeight(bool force)
        {
            // Update height instantly
            if (force)
            {
                m_Controller.height = m_TargetCharacterHeight;
                m_Controller.center = Vector3.up * m_Controller.height * 0.5f;
                PlayerCamera.transform.localPosition = Vector3.up * m_TargetCharacterHeight * CameraHeightRatio;
                m_Actor.AimPoint.transform.localPosition = m_Controller.center;
            }
            // Update smooth height
            else if (m_Controller.height != m_TargetCharacterHeight)
            {
                // resize the capsule and adjust camera position
                m_Controller.height = Mathf.Lerp(m_Controller.height, m_TargetCharacterHeight,
                    CrouchingSharpness * Time.deltaTime);
                m_Controller.center = Vector3.up * m_Controller.height * 0.5f;
                PlayerCamera.transform.localPosition = Vector3.Lerp(PlayerCamera.transform.localPosition,
                    Vector3.up * m_TargetCharacterHeight * CameraHeightRatio, CrouchingSharpness * Time.deltaTime);
                m_Actor.AimPoint.transform.localPosition = m_Controller.center;
            }
        }

        // returns false if there was an obstruction
        bool SetCrouchingState(bool crouched, bool ignoreObstructions)
        {
            // set appropriate heights
            if (crouched)
            {
                m_TargetCharacterHeight = CapsuleHeightCrouching;
            }
            else
            {
                // Detect obstructions
                if (!ignoreObstructions)
                {
                    Collider[] standingOverlaps = Physics.OverlapCapsule(
                        GetCapsuleBottomHemisphere(),
                        GetCapsuleTopHemisphere(CapsuleHeightStanding),
                        m_Controller.radius,
                        -1,
                        QueryTriggerInteraction.Ignore);
                    foreach (Collider c in standingOverlaps)
                    {
                        if (c != m_Controller)
                        {
                            return false;
                        }
                    }
                }

                m_TargetCharacterHeight = CapsuleHeightStanding;
            }

            if (OnStanceChanged != null)
            {
                OnStanceChanged.Invoke(crouched);
            }

            IsCrouching = crouched;
            return true;
        }

    }
}