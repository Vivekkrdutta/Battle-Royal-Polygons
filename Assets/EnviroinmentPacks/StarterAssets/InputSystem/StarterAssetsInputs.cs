
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace StarterAssets
{
	public class StarterAssetsInputs : MonoBehaviour
	{
		[Header("Character Input Values")]
		public Vector2 move;
		public Vector2 look;
		public bool jump;
		public bool sprint;
		public bool aim;
		public bool fire;
		public bool interact;
		public bool reload;
		public bool swapgun;

		[Header("Movement Settings")]
		public bool analogMovement;

		[Header("Mouse Cursor Settings")]
		public bool cursorLocked = true;
		public bool cursorInputForLook = true;

		private bool _ignoreCursor = false;
		public bool IgnoreCursor
		{
			get => _ignoreCursor;
			set
			{
				_ignoreCursor = value;
				Cursor.lockState = CursorLockMode.None;
				Cursor.visible = value;
			}
		}

#if ENABLE_INPUT_SYSTEM
		public void OnMove(InputValue value)
		{
			MoveInput(value.Get<Vector2>());
		}

		public void OnLook(InputValue value)
		{
			if(cursorInputForLook)
			{
				LookInput(value.Get<Vector2>());
			}
		}

		public void OnJump(InputValue value)
		{
			JumpInput(value.isPressed);
		}

		public void OnSprint(InputValue value)
		{
			SprintInput(value.isPressed);
		}

		public void OnAim(InputValue value)
		{
			AimInput(value.isPressed);
		}

		public void OnFire(InputValue value)
		{
			FireInput(value.isPressed);
		}
		public void OnInteract(InputValue value)
		{
			InteractInput(value.isPressed);
		}
		public void OnReload(InputValue value)
		{
			ReloadInput(value.isPressed);
		}
		public void OnSwapGun(InputValue value)
		{
			SwapGunInput(value.isPressed);
		}
#endif

        private void AimInput(bool isPressed)
        {
            aim = isPressed;
        }

        public void MoveInput(Vector2 newMoveDirection)
		{
			move = newMoveDirection;
		} 

		public void LookInput(Vector2 newLookDirection)
		{
			look = newLookDirection;
		}

		public void JumpInput(bool newJumpState)
		{
			jump = newJumpState;
		}

		public void SprintInput(bool newSprintState)
		{
			sprint = newSprintState;
		}
		public void InteractInput(bool newInteractState)
		{
			interact = newInteractState;
		}
		public void ReloadInput(bool newReloadState)
		{
			reload = newReloadState;
		}
		public void SwapGunInput(bool newSwapGunState)
		{
			swapgun = newSwapGunState;
		}


        private void OnApplicationFocus(bool hasFocus)
		{
			if (IgnoreCursor) return;

			SetCursorState(cursorLocked);
		}

		private void SetCursorState(bool newState)
		{
			Cursor.lockState = newState ? CursorLockMode.Locked : CursorLockMode.None;
		}

		private void FireInput(bool newFireState)
		{
			fire = newFireState;
		}
	}
	
}