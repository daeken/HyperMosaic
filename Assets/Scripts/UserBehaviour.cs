using System;
using UnityEngine;

public class UserBehaviour : MonoBehaviour {
	[Tooltip("Maximum slope the character can jump on")]
	[Range(5f, 60f)]
	public float slopeLimit = 45f;
	[Tooltip("Move speed in meters/second")]
	public float moveSpeed = 2f;
	[Tooltip("Yaw speed")]
	public float yawSpeed = 100f;
	[Tooltip("Pitch speed")]
	public float pitchSpeed = 100f;
	[Tooltip("Whether the character can jump")]
	public bool allowJump;
	[Tooltip("Upward speed to apply when jumping in meters/second")]
	public float jumpSpeed = 4f;

	bool GravityOn;

	new Rigidbody rigidbody;
	CapsuleCollider capsuleCollider;
	Transform cameraTransform;

	bool IsGrounded;
	float ForwardInput;
	float RightInput;
	float YawInput;
	float PitchInput;
	bool JumpInput;

	float Pitch;

	void Awake() {
		rigidbody = GetComponent<Rigidbody>();
		capsuleCollider = GetComponent<CapsuleCollider>();
		cameraTransform = transform.GetChild(0);
	}
	void Update() {
		Cursor.lockState = CursorLockMode.Locked;
		if(GravityOn) return;
		if(!Physics.Raycast(transform.position, -Vector3.up, 10)) return;

		GravityOn = true;
		var rb = GetComponent<Rigidbody>();
		rb.useGravity = true;
	}

	void FixedUpdate() {
		var vertical = Input.GetAxis("Vertical");
		var horizontal = Input.GetAxis("Horizontal");
		var jump = Input.GetKey(KeyCode.Space);

		ForwardInput = vertical;
		RightInput = horizontal;
		YawInput = Input.GetAxis("Mouse X");
		PitchInput = Input.GetAxis("Mouse Y");
		JumpInput = jump;

		CheckGrounded();
		ProcessActions();
	}

	void CheckGrounded() {
		IsGrounded = false;
		var capsuleHeight = Mathf.Max(capsuleCollider.radius * 2f, capsuleCollider.height);
		var capsuleBottom = transform.TransformPoint(capsuleCollider.center - Vector3.up * capsuleHeight / 2f);
		var radius = transform.TransformVector(capsuleCollider.radius, 0f, 0f).magnitude;

		var ray = new Ray(capsuleBottom + transform.up * .01f, -transform.up);
		if(!Physics.Raycast(ray, out var hit, radius * 5f)) return;
		var normalAngle = Vector3.Angle(hit.normal, transform.up);
		if(!(normalAngle < slopeLimit)) return;
		var maxDist = radius / Mathf.Cos(Mathf.Deg2Rad * normalAngle) - radius + .02f;
		if(hit.distance < maxDist)
			IsGrounded = true;
	}

	void ProcessActions() {
		// Turning
		if(YawInput != 0f) {
			var angle = Mathf.Clamp(YawInput, -1f, 1f) * yawSpeed;
			transform.Rotate(Vector3.up, Time.fixedDeltaTime * angle);
		}
		if(PitchInput != 0f) {
			Pitch += Time.fixedDeltaTime * PitchInput * pitchSpeed;
			Pitch = Mathf.Clamp(Pitch, -90, 90);
			cameraTransform.localRotation = Quaternion.Euler(-Pitch, 0, 0);
		}

		// Movement
		var move = transform.forward * Mathf.Clamp(ForwardInput, -1f, 1f) * moveSpeed * Time.fixedDeltaTime;
		move += transform.right * Mathf.Clamp(RightInput, -1f, 1f) * moveSpeed * Time.fixedDeltaTime;
		rigidbody.MovePosition(transform.position + move);

		// Jump
		if(JumpInput && allowJump && IsGrounded)
			rigidbody.AddForce(transform.up * jumpSpeed, ForceMode.VelocityChange);
	}
}