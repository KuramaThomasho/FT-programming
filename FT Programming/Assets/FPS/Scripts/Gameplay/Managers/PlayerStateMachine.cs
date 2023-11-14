using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.FPS.Gameplay;
using Unity.FPS.Game;

public abstract class PlayerStateMachine
{
    float m_CameraVerticalAngle = 0f;
    public float speedModifier = 1f;
    public bool wasGrounded = false;
    /// <summary>
    /// This function acts as the monobehaviour start function in the classes
    /// </summary>
    /// <param name="Player"></param>
    public abstract void EnterState(PlayerCharacterController Player);
    /// <summary>
    /// This one acts as the monobehaviour update funtion in the classes
    /// </summary>
    /// <param name="Player"></param>
    public abstract void UpdateState(PlayerCharacterController Player);
    public abstract void OnCollisionEnter(PlayerCharacterController Player);
    public bool DetectObstruction(PlayerCharacterController Player)
    {
        Collider[] standingOverlaps = Physics.OverlapCapsule(
                        Player.GetCapsuleBottomHemisphere(),
                        Player.GetCapsuleTopHemisphere(Player.CapsuleHeightStanding),
                        Player.m_Controller.radius,
                        -1,
                        QueryTriggerInteraction.Ignore);
        foreach (Collider c in standingOverlaps)
        {
            if (c != Player.m_Controller)
            {
                return false;
            }
        }
        return true;
    }
    /*
    public void UpdateCharacterHeight(PlayerCharacterController Player, bool force)
    {

        // Update height instantly
        if (force)
        {
            Player.m_Controller.height = Player.m_TargetCharacterHeight;
            Player.m_Controller.center = Vector3.up * Player.m_Controller.height * 0.5f;
            Player.PlayerCamera.transform.localPosition = Vector3.up * Player.m_TargetCharacterHeight * Player.CameraHeightRatio;
            Player.m_Actor.AimPoint.transform.localPosition = Player.m_Controller.center;
        }
        // Update smooth height
        else if (Player.m_Controller.height != Player.m_TargetCharacterHeight)
        {
            // resize the capsule and adjust camera position
            Player.m_Controller.height = Mathf.Lerp(Player.m_Controller.height, Player.m_TargetCharacterHeight,
                Player.CrouchingSharpness * Time.deltaTime);
            Player.m_Controller.center = Vector3.up * Player.m_Controller.height * 0.5f;
            Player.PlayerCamera.transform.localPosition = Vector3.Lerp(Player.PlayerCamera.transform.localPosition,
                Vector3.up * Player.m_TargetCharacterHeight * Player.CameraHeightRatio, Player.CrouchingSharpness * Time.deltaTime);
            Player.m_Actor.AimPoint.transform.localPosition = Player.m_Controller.center;
        }
    }
    */
    public void BaseCameraMovement(PlayerCharacterController Player)
    {
        // horizontal character rotation
        {
            // rotate the transform with the input speed around its local Y axis
            Player.transform.Rotate(
                new Vector3(0f, (Player.m_InputHandler.GetLookInputsHorizontal() * Player.RotationSpeed * Player.RotationMultiplier),
                    0f), Space.Self);
        }

        // vertical camera rotation
        {
            // add vertical inputs to the camera's vertical angle
            m_CameraVerticalAngle += Player.m_InputHandler.GetLookInputsVertical() * Player.RotationSpeed * Player.RotationMultiplier;

            // limit the camera's vertical angle to min/max
            m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -89f, 89f);

            // apply the vertical angle as a local rotation to the camera transform along its right axis (makes it pivot up and down)
            Player.PlayerCamera.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);
        }
    }
    public void CalculateVelocity(PlayerCharacterController Player)
    {
        // apply the final calculated velocity value as a character movement
        Vector3 capsuleBottomBeforeMove = Player.GetCapsuleBottomHemisphere();
        Vector3 capsuleTopBeforeMove = Player.GetCapsuleTopHemisphere(Player.m_Controller.height);
        Player.m_Controller.Move(Player.CharacterVelocity * Time.deltaTime);

        // detect obstructions to adjust velocity accordingly
        Player.m_LatestImpactSpeed = Vector3.zero;
        if (Physics.CapsuleCast(capsuleBottomBeforeMove, capsuleTopBeforeMove, Player.m_Controller.radius,
            Player.CharacterVelocity.normalized, out RaycastHit hit, Player.CharacterVelocity.magnitude * Time.deltaTime, -1,
            QueryTriggerInteraction.Ignore))
        {
            // We remember the last impact speed because the fall damage logic might need it
            Player.m_LatestImpactSpeed = Player.CharacterVelocity;

            Player.CharacterVelocity = Vector3.ProjectOnPlane(Player.CharacterVelocity, hit.normal);
        }
    }
    public void OnGroundMovement(PlayerCharacterController Player, float speed)
    {
        //Converts move input to a worldspace vector based on our character's transform orientation
        Vector3 worldspaceMoveInput = Player.transform.TransformVector(Player.m_InputHandler.GetMoveInput());

        // calculate the desired velocity from inputs, max speed, and current slope
        Vector3 targetVelocity = worldspaceMoveInput * Player.MaxSpeedOnGround * speed;

        // smoothly interpolate between our current velocity and the target velocity based on acceleration speed
        Player.CharacterVelocity = Vector3.Lerp(Player.CharacterVelocity, targetVelocity, Player.MovementSharpnessOnGround * Time.deltaTime);
    }
}
/// <summary>
/// This class needs to include all the basic movement scripts 
/// </summary>
public class PlayerWalkState : PlayerStateMachine
{
    public override void EnterState(PlayerCharacterController Player)
    {
        Player.UpdateCharacterHeight(true);
    }
    public override void UpdateState(PlayerCharacterController Player)
    {
        Debug.Log("UPDATING"); 
        Player.HasJumpedThisFrame = false;
        bool wasGrounded = Player.IsGrounded;
        Player.GroundCheck();

        BaseCameraMovement(Player);
        CalculateVelocity(Player);

        bool isSprinting = Player.m_InputHandler.GetSprintInputHeld();
        float speedModifier = isSprinting ? Player.SprintSpeedModifier : 1f;

        // footsteps sound
        float chosenFootstepSfxFrequency = (isSprinting ? Player.FootstepSfxFrequencyWhileSprinting : Player.FootstepSfxFrequency);
        if (Player.m_FootstepDistanceCounter >= 1f / chosenFootstepSfxFrequency)
        {
            Player.m_FootstepDistanceCounter = 0f;
            Player.AudioSource.PlayOneShot(Player.FootstepSfx);
        }

        // keep track of distance traveled for footsteps sound
        Player.m_FootstepDistanceCounter += Player.CharacterVelocity.magnitude * Time.deltaTime;

        //Checking for jump input
        if (Player.IsGrounded && Player.m_InputHandler.GetJumpInputDown())
        {
            wasGrounded = true;
            Player.SwitchState(Player.Jump_State);
        }

        //Checking for crouch input
        if (Player.m_InputHandler.GetCrouchInputHeld() && !isSprinting)
        {
            Player.SwitchState(Player.Crouch_State);
        }else
            Player.SwitchState(Player.Slide_State);

        OnGroundMovement(Player, speedModifier);
    }
    public override void OnCollisionEnter(PlayerCharacterController Player)
    {

    }
}
public class PlayerCrouchState : PlayerStateMachine
{
    float m_TargetCharacterHeight;
    public UnityAction<bool> OnStanceChanged;

    public override void EnterState(PlayerCharacterController Player)
    {
        //Converts move input to a worldspace vector based on our character's transform orientation
        Vector3 worldspaceMoveInput = Player.transform.TransformVector(Player.m_InputHandler.GetMoveInput());

        // calculate the desired velocity from inputs, max speed, and current slope
        Vector3 targetVelocity = worldspaceMoveInput * Player.MaxSpeedOnGround * speedModifier;

        m_TargetCharacterHeight = Player.CapsuleHeightCrouching;

        Player.UpdateCharacterHeight(false);

        if (OnStanceChanged != null)
        {
            OnStanceChanged.Invoke(true);
        }
        ///Reduces Speed calculation for crouched movement
        targetVelocity *= Player.MaxSpeedCrouchedRatio;
        targetVelocity = Player.GetDirectionReorientedOnSlope(targetVelocity.normalized, Player.m_GroundNormal) * targetVelocity.magnitude;

        // smoothly interpolate between our current velocity and the target velocity based on acceleration speed
        Player.CharacterVelocity = Vector3.Lerp(Player.CharacterVelocity, targetVelocity, Player.MovementSharpnessOnGround * Time.deltaTime);
    }
    public override void UpdateState(PlayerCharacterController Player)
    {
        Player.HasJumpedThisFrame = false;

        if (Player.m_InputHandler.GetCrouchInputReleased())
        {
            Player.SwitchState(Player.Walk_State);
        }
    }
    public void LeaveState(PlayerCharacterController Player)
    {
        m_TargetCharacterHeight = Player.CapsuleHeightCrouching;
    }
    public override void OnCollisionEnter(PlayerCharacterController Player)
    {
        
    }

}
public class PlayerJumpState : PlayerStateMachine
{
    public override void EnterState(PlayerCharacterController Player)
    {
       speedModifier = 1f;

        // start by canceling out the vertical component of our velocity
        Player.CharacterVelocity = new Vector3(Player.CharacterVelocity.x, 0f, Player.CharacterVelocity.z);

        // then, add the jumpSpeed value upwards
        Player.CharacterVelocity += Vector3.up * Player.JumpForce;

        // play sound
        Player.AudioSource.PlayOneShot(Player.JumpSfx);

        // remember last time we jumped because we need to prevent snapping to ground for a short time
        Player.m_LastTimeJumped = Time.time;
        Player.HasJumpedThisFrame = true;

        // Force grounding to false
        Player.IsGrounded = false;
        Player.m_GroundNormal = Vector3.up;
    }
    public override void UpdateState(PlayerCharacterController Player)
    {
        Vector3 worldspaceMoveInput = Player.transform.TransformVector(Player.m_InputHandler.GetMoveInput());

        // add air acceleration
        Player.CharacterVelocity += worldspaceMoveInput * Player.AccelerationSpeedInAir * Time.deltaTime;

        // limit air speed to a maximum, but only horizontally
        float verticalVelocity = Player.CharacterVelocity.y;
        Vector3 horizontalVelocity = Vector3.ProjectOnPlane(Player.CharacterVelocity, Vector3.up);
        horizontalVelocity = Vector3.ClampMagnitude(horizontalVelocity, Player.MaxSpeedInAir * speedModifier);
        Player.CharacterVelocity = horizontalVelocity + (Vector3.up * verticalVelocity);

        // apply the gravity to the velocity
        Player.CharacterVelocity += Vector3.down * Player.GravityDownForce * Time.deltaTime;

        // landing
        if (Player.IsGrounded && !wasGrounded)
        {
            // Fall damage
            float fallSpeed = -Mathf.Min(Player.CharacterVelocity.y, Player.m_LatestImpactSpeed.y);
            float fallSpeedRatio = (fallSpeed - Player.MinSpeedForFallDamage) /
                                   (Player.MaxSpeedForFallDamage - Player.MinSpeedForFallDamage);
            if (Player.RecievesFallDamage && fallSpeedRatio > 0f)
            {
                float dmgFromFall = Mathf.Lerp(Player.FallDamageAtMinSpeed, Player.FallDamageAtMaxSpeed, fallSpeedRatio);
                Player.m_Health.TakeDamage(dmgFromFall, null);

                // fall damage SFX
                Player.AudioSource.PlayOneShot(Player.FallDamageSfx);
            }
            else
            {
                // land SFX
                Player.AudioSource.PlayOneShot(Player.LandSfx);
            }
        }

        CalculateVelocity(Player);
    }
    public override void OnCollisionEnter(PlayerCharacterController Player)
    {

    }
}
public class PlayerSlideState : PlayerStateMachine
{
    public override void EnterState(PlayerCharacterController Player)
    {

    }
    public override void UpdateState(PlayerCharacterController Player)
    {

    }
    public override void OnCollisionEnter(PlayerCharacterController Player)
    {

    }
}
public class PlayerHookState : PlayerStateMachine
{
    public override void EnterState(PlayerCharacterController Player)
    {

    }
    public override void UpdateState(PlayerCharacterController Player)
    {

    }
    public override void OnCollisionEnter(PlayerCharacterController Player)
    {

    }
}