using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[RequireComponent(typeof(Camera))]
public class CameraControl : MonoBehaviour
{
	[SerializeField] private GameObject focusObject;
	[SerializeField] private float _zoomScale = 10f;
	[SerializeField] private float _zoomScaleCtrl = 3f;
	[SerializeField] private float _rotationSpeed = 500.0f;
	
	private float _maxFieldOfView = 160f;
	private float _minFieldOfView = 0f;
	private float _defaultFieldOfView = 60f;

	private Vector3 _mouseWorldPosStart;
	private Camera _mainCamera;

	private void Awake()
	{
		_mainCamera = Camera.main;
	}

	private void Update()
	{
		if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.Mouse2))
			CamOrbit();

		if (Input.GetKey(KeyCode.LeftShift) && Input.GetKey(KeyCode.F))
			Focus();

		if (Input.GetMouseButtonDown(2) && !Input.GetKey(KeyCode.LeftShift))
			_mouseWorldPosStart = GetPerspectivePos();

		if (Input.GetMouseButton(2) && !Input.GetKey(KeyCode.LeftShift))
			Pan();

		Zoom(Input.GetAxis("Mouse ScrollWheel"));
	}

	private Bounds GetBound(GameObject go)
	{
		Bounds bound = new Bounds(go.transform.position, Vector3.zero);
		var renderers = go.GetComponentsInChildren<Renderer>();
		foreach (var renderer in renderers)
			bound.Encapsulate(renderer.bounds);

		return bound;
	}

	public void Focus()
	{
		_mainCamera.fieldOfView = _defaultFieldOfView;
		Bounds bound = GetBound(focusObject);
		Vector3 boundSize = bound.size;
		float boundDiagonal = Mathf.Sqrt((boundSize.x * boundSize.x) + (boundSize.y * boundSize.y) + (boundSize.z * boundSize.z));
		float camDistanceToBoundCentre = boundDiagonal / 2f / (Mathf.Tan(_mainCamera.fieldOfView / 2f * Mathf.Deg2Rad));
		float camDistanceToBoundWithOffset = camDistanceToBoundCentre + boundDiagonal / 2f;
		transform.position = bound.center + (-transform.forward * camDistanceToBoundWithOffset);
	}

	private void CamOrbit()
	{
		if (Input.GetAxis("Mouse Y") != 0 || Input.GetAxis("Mouse X") != 0)
		{
			float verticalInput = Input.GetAxis("Mouse Y") * _rotationSpeed * Time.deltaTime;
			float horizontalInput = Input.GetAxis("Mouse X") * _rotationSpeed * Time.deltaTime;
			// transform.Rotate(Vector3.right, -verticalInput);
			// transform.Rotate(Vector3.up, horizontalInput);
			if (Mathf.Abs(horizontalInput) >= Mathf.Abs(verticalInput))
				transform.eulerAngles = transform.eulerAngles + Vector3.up * horizontalInput;
			else
				transform.eulerAngles = transform.eulerAngles + Vector3.right * -verticalInput;
		}
	}

	private void Pan()
	{
		if (Input.GetAxis("Mouse Y") != 0 || Input.GetAxis("Mouse X") != 0)
		{
			Vector3 mouseWorldPosDiff = _mouseWorldPosStart - GetPerspectivePos();
			transform.position += mouseWorldPosDiff;
		}
	}

	private void Zoom(float zoomDiff)
	{
		if (zoomDiff != 0f)
		{
			float zoomScaleKeyModifier = Input.GetKey(KeyCode.LeftControl) ? _zoomScaleCtrl : 1;

			_mouseWorldPosStart = GetPerspectivePos();
			// _mainCamera.fieldOfView = Mathf.Clamp(_mainCamera.fieldOfView - zoomDiff * _zoomScale * zoomScaleKeyModifier, _minFieldOfView, _maxFieldOfView);
			// Vector3 mouseWorldPosDiff = _mouseWorldPosStart - GetPerspectivePos();
			// transform.position += mouseWorldPosDiff * 3;

			Vector3 zoomAmount = (-transform.position + _mouseWorldPosStart).normalized * zoomDiff * _zoomScale * zoomScaleKeyModifier;
			transform.position += zoomAmount;
		}
	}

	private Vector3 GetPerspectivePos()
	{
		Ray ray = _mainCamera.ScreenPointToRay(Input.mousePosition);
		Vector3 planeNormal = transform.forward;
		Vector3 planePosition = transform.position + transform.forward * 10f;
		Plane plane = new Plane();
		plane.SetNormalAndPosition(planeNormal, planePosition);
		DrawPlane(planePosition, planeNormal);
		float dist;
		plane.Raycast(ray, out dist);
		return ray.GetPoint(dist);
	}

	private void DrawPlane(Vector3 position, Vector3 normal)
	{
		Vector3 v3;

		if (normal.normalized != Vector3.forward)
			v3 = Vector3.Cross(normal, Vector3.forward).normalized * normal.magnitude;
		else
			v3 = Vector3.Cross(normal, Vector3.up).normalized * normal.magnitude;

		var corner0 = position + v3;
		var corner2 = position - v3;

		var q = Quaternion.AngleAxis(90.0f, normal);
		v3 = q * v3;
		var corner1 = position + v3;
		var corner3 = position - v3;

		Debug.DrawLine(corner0, corner2, Color.green);
		Debug.DrawLine(corner1, corner3, Color.green);
		Debug.DrawLine(corner0, corner1, Color.green);
		Debug.DrawLine(corner1, corner2, Color.green);
		Debug.DrawLine(corner2, corner3, Color.green);
		Debug.DrawLine(corner3, corner0, Color.green);
		Debug.DrawRay(position, normal, Color.red);
	}
}