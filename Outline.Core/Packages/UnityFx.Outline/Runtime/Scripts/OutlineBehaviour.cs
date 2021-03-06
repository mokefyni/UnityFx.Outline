﻿// Copyright (C) 2019-2020 Alexander Bogarsukov. All rights reserved.
// See the LICENSE.md file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityFx.Outline
{
	/// <summary>
	/// Attach this script to a <see cref="GameObject"/> to add outline effect. It can be configured in edit-time or in runtime via scripts.
	/// </summary>
	/// <seealso cref="OutlineEffect"/>
	[ExecuteInEditMode]
	[DisallowMultipleComponent]
	public sealed class OutlineBehaviour : MonoBehaviour, IOutlineSettingsEx
	{
		#region data

#pragma warning disable 0649

		[SerializeField, Tooltip("Sets outline resources to use. Do not change the defaults unless you know what you're doing.")]
		private OutlineResources _outlineResources;
		[SerializeField, HideInInspector]
		private OutlineSettingsInstance _outlineSettings;
		[SerializeField, Tooltip("If set, list of object renderers is updated on each frame. Enable if the object has child renderers which are enabled/disabled frequently.")]
		private bool _updateRenderers;

#pragma warning restore 0649

		private OutlineRendererCollection _renderers;
		private CommandBuffer _commandBuffer;

		private Dictionary<Camera, CommandBuffer> _cameraMap = new Dictionary<Camera, CommandBuffer>();
		private float _cameraMapUpdateTimer;

		#endregion

		#region interface

		/// <summary>
		/// Gets or sets resources used by the effect implementation.
		/// </summary>
		/// <exception cref="ArgumentNullException">Thrown if setter argument is <see langword="null"/>.</exception>
		public OutlineResources OutlineResources
		{
			get
			{
				return _outlineResources;
			}
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("OutlineResources");
				}

				if (_outlineResources != value)
				{
					CreateSettingsIfNeeded();

					_outlineResources = value;
					_outlineSettings.OutlineResources = _outlineResources;
				}
			}
		}

		/// <summary>
		/// Gets outline renderers. By default all child <see cref="Renderer"/> components are used for outlining.
		/// </summary>
		public ICollection<Renderer> OutlineRenderers
		{
			get
			{
				CreateRenderersIfNeeded();
				return _renderers;
			}
		}

		/// <summary>
		/// Gets all cameras outline data is rendered to.
		/// </summary>
		public ICollection<Camera> Cameras
		{
			get
			{
				return _cameraMap.Keys;
			}
		}

		#endregion

		#region MonoBehaviour

		private void Awake()
		{
			CreateRenderersIfNeeded();
			CreateSettingsIfNeeded();

			_outlineSettings.OutlineResources = _outlineResources;
		}

		private void OnDestroy()
		{
			if (_outlineSettings != null)
			{
				_outlineSettings.OutlineResources = null;
			}
		}

		private void OnEnable()
		{
			CreateCommandBufferIfNeeded();
			Camera.onPreRender += OnCameraPreRender;
		}

		private void OnDisable()
		{
			Camera.onPreRender -= OnCameraPreRender;

			foreach (var kvp in _cameraMap)
			{
				if (kvp.Key)
				{
					kvp.Key.RemoveCommandBuffer(OutlineRenderer.RenderEvent, kvp.Value);
				}
			}

			_cameraMap.Clear();

			if (_commandBuffer != null)
			{
				_commandBuffer.Dispose();
				_commandBuffer = null;
			}
		}

		private void Update()
		{
			_cameraMapUpdateTimer += Time.deltaTime;

			if (_cameraMapUpdateTimer > 16)
			{
				RemoveDestroyedCameras();
				_cameraMapUpdateTimer = 0;
			}

			if (_outlineResources != null && _renderers != null)
			{
				if (_updateRenderers)
				{
					_renderers.Reset(false);
				}

				_commandBuffer.Clear();

				using (var renderer = new OutlineRenderer(_commandBuffer, BuiltinRenderTextureType.CameraTarget))
				{
					renderer.Render(_renderers.GetList(), _outlineSettings.OutlineResources, _outlineSettings);
				}
			}
		}

#if UNITY_EDITOR

		private void OnValidate()
		{
			CreateRenderersIfNeeded();
			CreateCommandBufferIfNeeded();
			CreateSettingsIfNeeded();

			_outlineSettings.OutlineResources = _outlineResources;
		}

		private void Reset()
		{
			if (_outlineSettings != null)
			{
				_outlineSettings.OutlineResources = _outlineResources;
			}

			if (_renderers != null)
			{
				_renderers.Reset(true);
			}
		}

#endif

		#endregion

		#region IOutlineSettingsEx

		/// <summary>
		/// Gets or sets outline settings. Set this to non-<see langword="null"/> value to share settings with other components.
		/// </summary>
		public OutlineSettings OutlineSettings
		{
			get
			{
				if (_outlineSettings == null)
				{
					_outlineSettings = new OutlineSettingsInstance();
				}

				return _outlineSettings.OutlineSettings;
			}
			set
			{
				if (_outlineSettings == null)
				{
					_outlineSettings = new OutlineSettingsInstance();
				}

				_outlineSettings.OutlineSettings = value;
			}
		}

		#endregion

		#region IOutlineSettings

		/// <inheritdoc/>
		public Color OutlineColor
		{
			get
			{
				CreateSettingsIfNeeded();
				return _outlineSettings.OutlineColor;
			}
			set
			{
				CreateSettingsIfNeeded();
				_outlineSettings.OutlineColor = value;
			}
		}

		/// <inheritdoc/>
		public int OutlineWidth
		{
			get
			{
				CreateSettingsIfNeeded();
				return _outlineSettings.OutlineWidth;
			}
			set
			{
				CreateSettingsIfNeeded();
				_outlineSettings.OutlineWidth = value;
			}
		}

		/// <inheritdoc/>
		public float OutlineIntensity
		{
			get
			{
				CreateSettingsIfNeeded();
				return _outlineSettings.OutlineIntensity;
			}
			set
			{
				CreateSettingsIfNeeded();
				_outlineSettings.OutlineIntensity = value;
			}
		}

		/// <inheritdoc/>
		public OutlineRenderFlags OutlineRenderMode
		{
			get
			{
				CreateSettingsIfNeeded();
				return _outlineSettings.OutlineRenderMode;
			}
			set
			{
				CreateSettingsIfNeeded();
				_outlineSettings.OutlineRenderMode = value;
			}
		}

		#endregion

		#region IEquatable

		/// <inheritdoc/>
		public bool Equals(IOutlineSettings other)
		{
			return OutlineSettings.Equals(_outlineSettings, other);
		}

		#endregion

		#region implementation

		private void OnCameraPreRender(Camera camera)
		{
			if (camera)
			{
				if (_outlineSettings.RequiresCameraDepth)
				{
					camera.depthTextureMode |= DepthTextureMode.Depth;
				}

				if (!_cameraMap.ContainsKey(camera))
				{
					camera.AddCommandBuffer(OutlineRenderer.RenderEvent, _commandBuffer);
					_cameraMap.Add(camera, _commandBuffer);
				}
			}
		}

		private void RemoveDestroyedCameras()
		{
			List<Camera> camerasToRemove = null;

			foreach (var camera in _cameraMap.Keys)
			{
				if (camera == null)
				{
					if (camerasToRemove != null)
					{
						camerasToRemove.Add(camera);
					}
					else
					{
						camerasToRemove = new List<Camera>() { camera };
					}
				}
			}

			if (camerasToRemove != null)
			{
				foreach (var camera in camerasToRemove)
				{
					_cameraMap.Remove(camera);
				}
			}
		}

		private void CreateCommandBufferIfNeeded()
		{
			if (_commandBuffer == null)
			{
				_commandBuffer = new CommandBuffer();
				_commandBuffer.name = string.Format("{0} - {1}", GetType().Name, name);
			}
		}

		private void CreateSettingsIfNeeded()
		{
			if (_outlineSettings == null)
			{
				_outlineSettings = new OutlineSettingsInstance(_outlineResources);
			}
		}

		private void CreateRenderersIfNeeded()
		{
			if (_renderers == null)
			{
				_renderers = new OutlineRendererCollection(gameObject);
				_renderers.Reset(true);
			}
		}

		#endregion
	}
}
