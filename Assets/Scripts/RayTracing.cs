using System.Collections.Generic;
using UnityEngine;

public class RayTracing : MonoBehaviour
{
	[SerializeField] private ComputeShader _rayTracingShader;
	[SerializeField] private Texture _skyboxTexture;
	[SerializeField] private Light _directionalLight;

	[Header("Configs")]
	[SerializeField] private bool _shade = true;
	[SerializeField, Range(0, 5)] private int _reflectionBounce = 0;
	[SerializeField] private bool _useRandomSeed = false;
	[SerializeField] private int _randomSeed = 123;

	[Header("Spheres")]
	[SerializeField] private Vector2 _sphereRadius = new Vector2(3.0f, 8.0f);
	[SerializeField] private uint _spheresMax = 100;
	[SerializeField] private float _spherePlacementRadius = 100.0f;

	private Camera _camera;
	private float _lastFieldOfView;
	private bool _lastShadeEnabled;
	private int _lastReflectionBounce;
	private RenderTexture _target;
	private Material _addMaterial;
	private uint _currentSample = 0;
	private ComputeBuffer _sphereBuffer;
	private List<Transform> _transformsToWatch = new List<Transform>();

	struct Sphere
	{
		public Vector3 position;
		public float radius;
		public Vector3 albedo;
		public Vector3 specular;
	}

	private void Awake()
	{
		_camera = GetComponent<Camera>();

		_transformsToWatch.Add(transform);
		_transformsToWatch.Add(_directionalLight.transform);

		_lastShadeEnabled = _shade;
		_lastReflectionBounce = _reflectionBounce;

		if (!_useRandomSeed)
			_randomSeed = Random.Range(0, int.MaxValue);

		Random.InitState(_randomSeed);
	}

	private void OnEnable()
	{
		_currentSample = 0;
		SetUpScene();
	}

	private void OnDisable()
	{
		if (_sphereBuffer != null)
			_sphereBuffer.Release();
	}

	private void Update()
	{
		if (_camera.fieldOfView != _lastFieldOfView)
		{
			_currentSample = 0;
			_lastFieldOfView = _camera.fieldOfView;
		}

		if (_lastShadeEnabled != _shade)
		{
			_lastShadeEnabled = _shade;
			_currentSample = 0;
		}

		if (_lastReflectionBounce != _reflectionBounce)
		{
			_lastReflectionBounce = _reflectionBounce;
			_currentSample = 0;
		}

		foreach (Transform t in _transformsToWatch)
		{
			if (t.hasChanged)
			{
				_currentSample = 0;
				t.hasChanged = false;
			}
		}
	}

	private void SetUpScene()
	{
		List<Sphere> spheres = new List<Sphere>();

		// Add a number of random spheres
		for (int i = 0; i < _spheresMax; i++)
		{
			Sphere sphere = new Sphere();

			// Radius and radius
			sphere.radius = _sphereRadius.x + Random.value * (_sphereRadius.y - _sphereRadius.x);
			Vector2 randomPos = Random.insideUnitCircle * _spherePlacementRadius;
			sphere.position = new Vector3(randomPos.x, sphere.radius, randomPos.y);

			// Reject spheres that are intersecting others
			foreach (Sphere other in spheres)
			{
				float minDist = sphere.radius + other.radius;
				if (Vector3.SqrMagnitude(sphere.position - other.position) < minDist * minDist)
					goto SkipSphere;
			}

			// Albedo and specular color
			Color color = Random.ColorHSV();
			bool metal = Random.value < 0.5f;
			sphere.albedo = metal ? Vector4.zero : new Vector4(color.r, color.g, color.b);
			sphere.specular = metal ? new Vector4(color.r, color.g, color.b) : new Vector4(0.04f, 0.04f, 0.04f);

			// Add the sphere to the list
			spheres.Add(sphere);

		SkipSphere:
			continue;
		}

		// Assign to compute buffer
		if (_sphereBuffer != null)
			_sphereBuffer.Release();
		if (spheres.Count > 0)
		{
			_sphereBuffer = new ComputeBuffer(spheres.Count, 40);
			_sphereBuffer.SetData(spheres);
		}
	}

	private void SetShaderParameters()
	{
		_rayTracingShader.SetTexture(0, "_SkyboxTexture", _skyboxTexture);
		_rayTracingShader.SetMatrix("_CameraToWorld", _camera.cameraToWorldMatrix);
		_rayTracingShader.SetMatrix("_CameraInverseProjection", _camera.projectionMatrix.inverse);
		_rayTracingShader.SetVector("_PixelOffset", new Vector2(Random.value, Random.value));
		_rayTracingShader.SetBool("_IsShadeEnabled", _shade);
		_rayTracingShader.SetInt("_ReflectionBounce", _reflectionBounce);

		Vector3 l = _directionalLight.transform.forward;
		_rayTracingShader.SetVector("_DirectionalLight", new Vector4(l.x, l.y, l.z, _directionalLight.intensity));

		if (_sphereBuffer != null)
			_rayTracingShader.SetBuffer(0, "_Spheres", _sphereBuffer);
	}

	private void InitRenderTexture()
	{
		if (_target == null || _target.width != Screen.width || _target.height != Screen.height)
		{
			// Release render texture if we already have one
			if (_target != null)
				_target.Release();

			// Get a render target for Ray Tracing
			_target = new RenderTexture(Screen.width, Screen.height, 0,
				RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
			_target.enableRandomWrite = true;
			_target.Create();

			// Reset sampling
			_currentSample = 0;
		}
	}

	private void Render(RenderTexture destination)
	{
		// Make sure we have a current render target
		InitRenderTexture();

		// Set the target and dispatch the compute shader
		_rayTracingShader.SetTexture(0, "Result", _target);
		int threadGroupsX = Mathf.CeilToInt(Screen.width / 8.0f);
		int threadGroupsY = Mathf.CeilToInt(Screen.height / 8.0f);
		_rayTracingShader.Dispatch(0, threadGroupsX, threadGroupsY, 1);

		// Blit the result texture to the screen
		if (_addMaterial == null)
			_addMaterial = new Material(Shader.Find("Hidden/AddShader"));
		_addMaterial.SetFloat("_Sample", _currentSample);
		Graphics.Blit(_target, destination, _addMaterial);
		_currentSample++;
	}

	private void OnRenderImage(RenderTexture source, RenderTexture destination)
	{
		SetShaderParameters();
		Render(destination);
	}
}