using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Unity.FPS.Gameplay;
using Unity.FPS.Game;

public abstract class PlayerStateMachine
{
    float m_CameraVerticalAngle = 0f;
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
        return true;
    }
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
    public void BaseMovement(PlayerCharacterController Player)
    {
        // horizontal character rotation
        {
            // rotate the transform with the input speed around its local Y axis
            Player.transform.Rotate(
                new Vector3(0f, (m_InputHandler.GetLookInputsHorizontal() * Player.RotationSpeed * Player.RotationMultiplier),
                    0f), Space.Self);
        }

        // vertical camera rotation
        {
            // add vertical inputs to the camera's vertical angle
            m_CameraVerticalAngle += m_InputHandler.GetLookInputsVertical() * Player.RotationSpeed * Player.RotationMultiplier;

            // limit the camera's vertical angle to min/max
            m_CameraVerticalAngle = Mathf.Clamp(m_CameraVerticalAngle, -89f, 89f);

            // apply the vertical angle as a local rotation to the camera transform along its right axis (makes it pivot up and down)
            Player.PlayerCamera.transform.localEulerAngles = new Vector3(m_CameraVerticalAngle, 0, 0);
        }
    }
}
/// <summary>
/// This class needs to include all the basic movement scripts 
/// </summary>
public class PlayerWalkState : PlayerStateMachine
{
    public override void EnterState(PlayerCharacterController Player)
    {
        UpdateCharacterHeight(Player, true);
    }
    public override void UpdateState(PlayerCharacterController Player)
    {
        

        bool isSprinting = Player.m_InputHandler.GetSprintInputHeld();
        float speedModifier = isSprinting ? Player.SprintSpeedModifier : 1f;

        Vector3 worldspaceMoveInput = Player.transform.TransformVector(Player.m_InputHandler.GetMoveInput());

        if (Player.IsGrounded)
        {
            // calculate the desired velocity from inputs, max speed, and current slope
            Vector3 targetVelocity = worldspaceMoveInput * Player.MaxSpeedOnGround * speedModifier;

            // reduce speed if crouching by crouch speed ratio
            if (Player.IsCrouching)
            {
                targetVelocity *= Player.MaxSpeedCrouchedRatio;
                targetVelocity = Player.GetDirectionReorientedOnSlope(targetVelocity.normalized, Player.m_GroundNormal) * targetVelocity.magnitude;
            }

            // smoothly interpolate between our current velocity and the target velocity based on acceleration speed
            Player.CharacterVelocity = Vector3.Lerp(Player.CharacterVelocity, targetVelocity, Player.MovementSharpnessOnGround * Time.deltaTime);
        }

        if (Player.m_InputHandler.GetCrouchInputHeld() && !isSprinting)
        {
            Player.SwitchState(Player.Crouch_State);
        }
        else if (Player.IsGrounded && Player.m_InputHandler.GetJumpInputDown())
        {
            Player.SwitchState(Player.Jump_State);
        }else
            Player.SwitchState(Player.Slide_State);
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
        m_TargetCharacterHeight = Player.CapsuleHeightCrouching;

        UpdateCharacterHeight(Player, false);

        if (OnStanceChanged != null)
        {
            OnStanceChanged.Invoke(true);
        }
    }
    public override void UpdateState(PlayerCharacterController Player)
    {
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
    public float speedModifier;

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