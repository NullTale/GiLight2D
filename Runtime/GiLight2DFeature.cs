using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace GiLight2D
{
    public class GiLight2DFeature : ScriptableRendererFeature
    {
		private const string k_BlitShader  = "Hidden/GiLight2D/Blit";
		private const string k_AlphaShader = "Hidden/GiLight2D/Alpha";
		private const string k_UvShader    = "Hidden/GiLight2D/UV";
		private const string k_JfaShader   = "Hidden/GiLight2D/JumpFlood";
		private const string k_GiShader    = "Hidden/GiLight2D/Gi";
		private const string k_BlurShader  = "Hidden/GiLight2D/Blur";
		private const string k_DistShader  = "Hidden/GiLight2D/Distance";
		private const string k_NoiseShader = "Hidden/GiLight2D/Noise";
		
		private static readonly int s_MainTexId     = Shader.PropertyToID("_MainTex");
		private static readonly int s_NoiseOffsetId = Shader.PropertyToID("_NoiseOffset");
		private static readonly int s_NoiseTexId    = Shader.PropertyToID("_NoiseTex");
		private static readonly int s_UvScaleId     = Shader.PropertyToID("_UvScale");
		private static readonly int s_FalloffId     = Shader.PropertyToID("_Falloff");
		private static readonly int s_IntensityId   = Shader.PropertyToID("_Intensity");
		private static readonly int s_SamplesId     = Shader.PropertyToID("_Samples");
		private static readonly int s_OffsetId      = Shader.PropertyToID("_Offset");
		private static readonly int s_ColorTexId    = Shader.PropertyToID("_ColorTex");
		private static readonly int s_DistTexId     = Shader.PropertyToID("_DistTex");
		private static readonly int s_AspectId      = Shader.PropertyToID("_Aspect");
		private static readonly int s_StepSizeId    = Shader.PropertyToID("_StepSize");
		private static readonly int s_ScaleId	    = Shader.PropertyToID("_Scale");
		private static readonly int s_AlphaTexId	= Shader.PropertyToID("_AlphaTex");
		
		private static List<ShaderTagId> k_ShaderTags;
		private static Mesh              k_ScreenMesh;
		private static Texture2D         k_Noise;

		[SerializeField]
		private RenderPassEvent _event = RenderPassEvent.BeforeRenderingOpaques;
		[SerializeField]
		[Tooltip("Which objects should be rendered as Gi.")]
		private LayerMask			_mask = new LayerMask() { value = -1 };
		[SerializeField]
		[Tooltip("Enable depth stencil buffer for Gi objects rendering.")]
		private  bool				_depthStencil = true;
		
		[Tooltip("How much rays to emit from each point.")]
		[SerializeField]
		private  int				_samples  = 100;
		[SerializeField]
		[Tooltip("Distance map additional impact for raytracing.")]
		[Range(0.0f, 0.07f)]
		private  float				_distOffset = 0.007f;
		[SerializeField]
		private Optional<RangeFloat> _falloff   = new Optional<RangeFloat>(new RangeFloat(new Vector2(.01f, 1f), 1f), false);
		[SerializeField]
		private Optional<RangeFloat> _intensity = new Optional<RangeFloat>(new RangeFloat(new Vector2(.0f, 3f), 1f), false);
		[SerializeField]
		private NoiseOptions		 _noiseOptions;
		private Vector2Int           _noiseResolution;
		[SerializeField]
		[Tooltip("Orthographic additional camera space, to make objects visible outside the camera frame.")]
		private Optional<RangeFloat>    _border = new Optional<RangeFloat>(new RangeFloat(new Vector2(.0f, 3f), 0f), false);
		[SerializeField]
		private ScaleModeData		_scaleMode;
		
		[SerializeField]
		private Output				_output;
		[SerializeField]
		[Tooltip("Copy alpha channel from color texture to result, basically objects mask. May be useful for lighting combinations.")]
		private bool				_alpha;
		[SerializeField]
		[Tooltip("Override final output for debug purposes.")]
		private DebugOutput			_outputOverride = DebugOutput.None;
		
		[SerializeField]
		[Tooltip("Run render feature in scene view project window.")]
		private  bool				_runInSceneView;
		
		private Pass				_pass;
		private CameraToOutputPass  _cameraToOutputPass;
		
		private Material 			_uvMat;
		private Material 			_jfaMat;
		private Material 			_blitMat;
		private Material 			_giMat;
		private Material 			_distMat;
		private Material 			_alphaMat;
		
		private                 RenderTextureDescriptor _rtDesc = new RenderTextureDescriptor(0, 0, GraphicsFormat.None, 0, 0);
		private                 Vector2Int              _rtRes  = Vector2Int.zero;

		private bool ForceTextureOutput => _output._finalBlit == FinalBlit.Texture;
		private bool HasGiBorder => _border.Enabled && _border.Value.Value > 0f;
		
		public int		 Samples { get => _samples; set => _samples = value; }
		public float	 Falloff { get => _falloff.Value.Value; set => _falloff.Value.Value = value; }
		public float	 Intensity { get => _intensity.Value.Value; set => _intensity.Value.Value = value; }
		public float	 Scale { get => _scaleMode._ratio; set => _scaleMode._ratio = value; }
		public NoiseMode Noise
		{
			get => _noiseOptions._noiseMode;
			set
			{
				if (_noiseOptions._noiseMode == value)
					return;

				_setNoiseState(value);
			}
		}

		public float     Border
		{
			get => _border.Enabled ? _border.Value.Value : 0f;
			set
			{
				_border.Enabled = value > 0f;
				_border.Value.Value = value;
			}
		}

		public float     NoiseScale
		{
			get => _noiseOptions._noiseScale;
			set
			{
				_noiseOptions._noiseScale = value;
				
				_initNoise();
			}
		}

		// =======================================================================
        private class Pass : ScriptableRenderPass
        {
            public  GiLight2DFeature  _owner;
            
			private FilteringSettings _filtering;
			private RenderStateBlock  _override;
			private RanderTarget      _buffer;
			private RanderTarget      _dist;
			private RanderTarget      _tmp;
			private RenderTargetFlip  _jfa;
			private RanderTarget      _output;
			
			// =======================================================================
            public void Init()
            {
                renderPassEvent = _owner._event;
				
				_buffer = new RanderTarget().Allocate(nameof(_buffer));
				_dist   = new RanderTarget().Allocate(nameof(_dist));
				_tmp    = new RanderTarget().Allocate(nameof(_tmp));
				_jfa    = new RenderTargetFlip(new RanderTarget().Allocate($"{nameof(_jfa)}_a"), new RanderTarget().Allocate($"{nameof(_jfa)}_b"));
				if (_owner.ForceTextureOutput)
					_output = new RanderTarget().Allocate(_owner._output._outputGlobalTexture);
				
				_filtering   = new FilteringSettings(RenderQueueRange.transparent, _owner._mask);
				_override = new RenderStateBlock(RenderStateMask.Nothing);
			}

			public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
				// allocate resources
				ref var res          = ref _owner._rtRes;
				ref var desc         = ref _owner._rtDesc;
				var     cmd          = CommandBufferPool.Get(nameof(GiLight2DFeature));
#if UNITY_2021
				var     cameraOutput = RTHandles.Alloc(renderingData.cameraData.renderer.cameraColorTarget);
#else
				var     cameraOutput = renderingData.cameraData.renderer.cameraColorTargetHandle.nameID;
#endif
				// allocate render textures
				desc.colorFormat = RenderTextureFormat.ARGB32;
				if (_owner._depthStencil)
				{
					// set depth stencil format from rendering camera 
					desc.depthStencilFormat = renderingData.cameraData.cameraTargetDescriptor.depthStencilFormat;
					_buffer.Get(cmd, desc);
					desc.stencilFormat = GraphicsFormat.None;
				}
				else
				{
					_buffer.Get(cmd, desc);
				}

				if (_owner.ForceTextureOutput)
					_output.Get(cmd, desc);

				desc.colorFormat = RenderTextureFormat.RG16;
				_jfa.Get(cmd, desc);
				
				desc.colorFormat = RenderTextureFormat.R16;
				_dist.Get(cmd, desc);

				// render with layer mask
				var cullResults = renderingData.cullResults;
				if (_owner.HasGiBorder)
				{
					// expand camera ortho projection if has Gi border, drawback is that we need to do cull results second time
					// it is not necessary if we want to draw with separate camera
		            ref var cameraData = ref renderingData.cameraData;
		            var     camera     = cameraData.camera;
					var     aspect     = camera.aspect;
					var     ySize      = camera.orthographicSize + _owner._border.Value.Value;
					var     xSize      = ySize * aspect;
					
					var projectionMatrix = Matrix4x4.Ortho(-xSize, xSize, -ySize, ySize, camera.nearClipPlane, camera.farClipPlane);
					projectionMatrix = GL.GetGPUProjectionMatrix(projectionMatrix, cameraData.IsCameraProjectionMatrixFlipped());

                    var viewMatrix = cameraData.GetViewMatrix();
					RenderingUtils.SetViewAndProjectionMatrices(cmd, viewMatrix, projectionMatrix, false);
					
					// cull results
					camera.TryGetCullingParameters(out var cullingParameters);
					cullingParameters.cullingMatrix = projectionMatrix * viewMatrix;
					 
					var planes = GeometryUtility.CalculateFrustumPlanes(cullingParameters.cullingMatrix);
					for (var n = 0; n < 6; n ++) 
					    cullingParameters.SetCullingPlane(n, planes[n]);

					cullResults = context.Cull(ref cullingParameters);
				}
				
				cmd.SetRenderTarget(_buffer.Handle.nameID);
				cmd.ClearRenderTarget(_owner._depthStencil ? RTClearFlags.All : RTClearFlags.Color, Color.clear, 1f, 0);
				context.ExecuteCommandBuffer(cmd);
				cmd.Clear();
				var drawSettings = CreateDrawingSettings(k_ShaderTags, ref renderingData, SortingCriteria.CommonTransparent);
				context.DrawRenderers(cullResults, ref drawSettings, ref _filtering, ref _override);
				
				if (_owner.HasGiBorder)
				{
		            ref var cameraData = ref renderingData.cameraData;
					RenderingUtils.SetViewAndProjectionMatrices(cmd, cameraData.GetViewMatrix(), cameraData.GetGPUProjectionMatrix(), false);
				}
				
				// draw uv with alpha mask 
				_blit(_buffer.Handle, _jfa.From.Handle, _owner._uvMat);
				
				// fluid fill uv coords
		        var steps    = Mathf.CeilToInt(Mathf.Log(res.x * res.y) / 2f) + 1;
				var stepSize = new Vector2(res.x / (float)res.y, 1f);

		        for (var n = 0; n < steps; n++)
		        {
		            stepSize /= 2;
					
					cmd.SetGlobalVector(s_StepSizeId, stepSize);
					
					_blit(_jfa.From.Handle, _jfa.To.Handle, _owner._jfaMat);
					_jfa.Flip();
		        }

				// evaluate distance from uv coords
				_owner._distMat.SetFloat(s_OffsetId, _owner._distOffset);
				_blit(_jfa.From.Handle, _dist.Handle, _owner._distMat);
				_jfa.Flip();
				
				// apply raytracer, final blit
				cmd.SetGlobalTexture(s_ColorTexId, _buffer.Handle.nameID);
				cmd.SetGlobalTexture(s_DistTexId, _dist.Handle.nameID);
        		_owner._giMat.SetVector(s_AspectId, new Vector4(res.x / (float)res.y, 1f));
				_owner._giMat.SetFloat(s_SamplesId, _owner._samples);
				if (_owner._falloff.Enabled)
					_owner._giMat.SetFloat(s_FalloffId, _owner._falloff.Value.Value);
				if (_owner._intensity.Enabled)
					_owner._giMat.SetFloat(s_IntensityId, _owner._intensity.Value.Value);
				
				switch (_owner._noiseOptions._noiseMode)
				{
					case NoiseMode.Dynamic:
					{
						_owner._giMat.SetVector(s_NoiseOffsetId, new Vector4(Random.value, Random.value, 0, 0));
						_owner._giMat.SetTexture(s_NoiseTexId, k_Noise);
					} break;
					case NoiseMode.Static:
					{
						_owner._giMat.SetTexture(s_NoiseTexId, k_Noise);
					} break;
					case NoiseMode.Shader:
					{
						_owner._giMat.SetVector(s_NoiseOffsetId, new Vector4(Random.value, Random.value, 0, 0));
					} break;
					case NoiseMode.None:
					{
						_owner._giMat.SetTexture(s_NoiseTexId, Texture2D.blackTexture);
					} break;
					default:
						throw new ArgumentOutOfRangeException();
				}


				switch (_owner._output._finalBlit)
				{
					case FinalBlit.Texture:
					{
						if (_owner._alpha)
						{
							// draw in to the blit texture, then to the output with alpha combine
							desc.colorFormat = RenderTextureFormat.ARGB32;
							_tmp.Get(cmd, desc);

							cmd.SetRenderTarget(_tmp.Handle.nameID);
							cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, _owner._giMat, 0, 0);

							cmd.SetGlobalTexture(s_AlphaTexId, _buffer.Handle.nameID);
							_blit(_tmp.Handle, _output.Handle, _owner._alphaMat);
						}
						else
						{
							cmd.SetRenderTarget(_output.Handle.nameID);
							cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, _owner._giMat, 0, 0);
						}
					} break;
					
					case FinalBlit.Camera:
					{
						// alpha channel will be added in copy pass (if required)
						cmd.SetRenderTarget(cameraOutput);
						cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, _owner._giMat, 0, 0);
					} break;
					
					default:
						throw new ArgumentOutOfRangeException();
				}
				
				if (_owner._outputOverride != DebugOutput.None)
				{
					var dest = _owner._output._finalBlit switch
					{
						FinalBlit.Texture => _output.Handle,
						FinalBlit.Camera  => cameraOutput,
						_                 => throw new ArgumentOutOfRangeException()
					};
					
					switch (_owner._outputOverride)
					{
						case DebugOutput.Objects:
							_blit(_buffer.Handle, dest, _owner._blitMat);
							break;
						case DebugOutput.Flood:
							_blit(_jfa.To.Handle, dest, _owner._blitMat);
							break;
						case DebugOutput.Distance:
							_blit(_dist.Handle, dest, _owner._blitMat);
							break;
						case DebugOutput.None:
						default:
							throw new ArgumentOutOfRangeException();
					}
				}
				
				_execute();
				
				// -----------------------------------------------------------------------
				void _blit(RTHandle from, RTHandle to, Material mat)
				{
					GiLight2DFeature._blit(cmd, from, to, mat);
				}

				void _execute()
				{
					context.ExecuteCommandBuffer(cmd);
					CommandBufferPool.Release(cmd);
				}
			}

			public override void FrameCleanup(CommandBuffer cmd)
			{
				_buffer.Release(cmd);
				_dist.Release(cmd);
				_jfa.Release(cmd);
				
				if (_owner._alpha)
					_tmp.Release(cmd);
				
				if (_owner.ForceTextureOutput)
					_output.Release(cmd);
			}
        }

		private class CameraToOutputPass : ScriptableRenderPass
		{
            public  GiLight2DFeature _owner;
			private RanderTarget     _tmp;
			private RanderTarget     _buffer;
			
			// =======================================================================
			public void Init()
			{
				renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
				_tmp = new RanderTarget().Allocate(nameof(_tmp));
				_buffer = new RanderTarget().Allocate(nameof(_buffer));
			}
			
			public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
			{
				var     cmd  = CommandBufferPool.Get(nameof(GiLight2DFeature));
				ref var desc = ref _owner._rtDesc;
#if UNITY_2021
				var     cameraOutput = RTHandles.Alloc(renderingData.cameraData.renderer.cameraColorTarget);
#else
				var     cameraOutput = renderingData.cameraData.renderer.cameraColorTargetHandle.nameID;
#endif
				
				// add alpha channel, after potential post processing wich set it to one
				desc.colorFormat = renderingData.cameraData.cameraTargetDescriptor.colorFormat;
				_tmp.Get(cmd, desc);

				_blit(cmd, cameraOutput, _tmp.Handle, _owner._blitMat);
				cmd.SetGlobalTexture(s_AlphaTexId, _buffer.Handle.nameID);
				_blit(cmd, _tmp.Handle, cameraOutput, _owner._alphaMat);
				
				context.ExecuteCommandBuffer(cmd);
				CommandBufferPool.Release(cmd);
			}
			
			public override void FrameCleanup(CommandBuffer cmd)
			{
				_tmp.Release(cmd);
				_buffer.Release(cmd);
			}
		}
		
		public class RanderTarget
		{
			public RTHandle Handle;
			public int      Id;
			
			// =======================================================================
			public RanderTarget Allocate(RenderTexture rt, string name)
			{
				Handle = RTHandles.Alloc(rt, name);
				Id     = Shader.PropertyToID(name);
				
				return this;
			}
			
			public RanderTarget Allocate(string name)
			{
				Handle = _alloc(name);
				Id     = Shader.PropertyToID(name);
				
				return this;
			}
			
			public void Get(CommandBuffer cmd, RenderTextureDescriptor desc)
			{
				cmd.GetTemporaryRT(Id, desc);
			}
			
			public void Release(CommandBuffer cmd)
			{
				cmd.ReleaseTemporaryRT(Id);
			}
		}

		public class RenderTargetFlip
		{
			public RanderTarget	From => _isFlipped ? _a : _b;
			public RanderTarget	To => _isFlipped ? _b : _a;
			
			private bool         _isFlipped;
			private RanderTarget _a;
			private RanderTarget _b;
			
			// =======================================================================
			public RenderTargetFlip(RanderTarget a, RanderTarget b)
			{
				_a = a;
				_b = b;
			}
			
			public void Flip()
			{
				_isFlipped = !_isFlipped;
			}
			
			public void Release(CommandBuffer cmd)
			{
				_a.Release(cmd);
				_b.Release(cmd);
			}

			public void Get(CommandBuffer cmd, RenderTextureDescriptor desc)
			{
				_a.Get(cmd, desc);
				_b.Get(cmd, desc);
			}
		}

		[Serializable]
		public class ScaleModeData
		{
			public  ScaleMode   _scaleMode;
			public  float       _ratio               = 1f;
			public  int         _height              = 240;
		}

		[Serializable]
		public class Output
		{
			[Tooltip("Where to store Gi result. If the final result is a camera, then could be applied a post processing.")]
			public FinalBlit _finalBlit = FinalBlit.Camera;
			[Tooltip("Global name of output texture.")]
			public string _outputGlobalTexture = "_GiTex";
		}
		
		[Serializable]
		public class NoiseOptions
		{
			public NoiseMode _noiseMode = NoiseMode.Shader;
			[Range(0.01f, 1f)]
			public float _noiseScale = 1f;
		}
		
		public enum ScaleMode
		{
		    None,
			Scale,
			Fixed,
		}
		
		public enum NoiseMode
		{
			Dynamic = 0,
			Static  = 1,
			Shader  = 2,
			None    = 3
		}

		public enum DebugOutput
		{
			None,
			Objects,
			Flood,
			Distance
		}

		public enum FinalBlit
		{
			Texture,
			Camera
		}
		
        // =======================================================================
        public override void Create()
        {
            _pass = new Pass() { _owner = this };
            _pass.Init();
			
            _cameraToOutputPass = new CameraToOutputPass() { _owner = this };
            _cameraToOutputPass.Init();

			_uvMat    = new Material(Shader.Find(k_UvShader));
			_jfaMat   = new Material(Shader.Find(k_JfaShader));
			_blitMat  = new Material(Shader.Find(k_BlitShader));
			_alphaMat = new Material(Shader.Find(k_AlphaShader));
			_distMat  = new Material(Shader.Find(k_DistShader));
			_giMat    = new Material(Shader.Find(k_GiShader));
			if (_falloff.enabled)
				_giMat.EnableKeyword("FALLOFF_IMPACT");
			if (_intensity.enabled)
				_giMat.EnableKeyword("INTENSITY_IMPACT");
			_setNoiseState(_noiseOptions._noiseMode);
			
			if (k_ScreenMesh == null)
			{
				// init triangle
                k_ScreenMesh = new Mesh();
				_initScreenMesh(k_ScreenMesh, Matrix4x4.identity);
			}
			
			if (k_ShaderTags == null)
			{
				k_ShaderTags = new List<ShaderTagId>(new[]
				{
					new ShaderTagId("SRPDefaultUnlit"),
					new ShaderTagId("UniversalForward"),
					new ShaderTagId("UniversalForwardOnly")
				});
			}
			
			// init noise
			_initNoise();
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
			if (_runInSceneView)
			{
				// in game or scene view
				if (renderingData.cameraData.cameraType != CameraType.Game
					&& renderingData.cameraData.cameraType != CameraType.SceneView)
					return;
			}
			else
			{
				// in game view only
				if (renderingData.cameraData.cameraType != CameraType.Game)
					return;
			}
			
			_setupDesc(in renderingData);

			renderer.EnqueuePass(_pass);
			if (_alpha && _output._finalBlit == FinalBlit.Camera)
				renderer.EnqueuePass(_cameraToOutputPass);
        }

		protected override void Dispose(bool disposing)
		 {
			 CoreUtils.Destroy(_uvMat);
			 CoreUtils.Destroy(_jfaMat);
			 CoreUtils.Destroy(_blitMat); 
			 CoreUtils.Destroy(_alphaMat); 
			 CoreUtils.Destroy(_giMat);
			 CoreUtils.Destroy(_distMat);
		 }

		// =======================================================================
		private void _setupDesc(in RenderingData renderingData)
		{
			var camDesc = renderingData.cameraData.cameraTargetDescriptor;
			_rtRes = _scaleMode._scaleMode switch
			{
			    ScaleMode.None => new Vector2Int(camDesc.width, camDesc.height),
			     
				ScaleMode.Scale => new Vector2Int(
					Mathf.FloorToInt(camDesc.width * _scaleMode._ratio),
					Mathf.FloorToInt(camDesc.height * _scaleMode._ratio)
				),

				ScaleMode.Fixed => new Vector2Int(
					Mathf.FloorToInt((camDesc.width / (float)camDesc.height) * _scaleMode._height),
					_scaleMode._height
				),

				_ => throw new ArgumentOutOfRangeException()
			};
			
			_rtDesc.width  = _rtRes.x;
			_rtDesc.height = _rtRes.y;
			
			var ortho = renderingData.cameraData.camera.orthographicSize;
			var uvScale = _border.Enabled ? (ortho + _border.Value.Value) / ortho : 1f;
			
			_giMat.SetVector(s_ScaleId, new Vector4(uvScale, uvScale, 1f, 1f));
			_alphaMat.SetFloat(s_UvScaleId, 1f / uvScale);
		}
		
		private void _initNoise()
		{
			// try block to fix editor startup error
			try
			{
				var width  = Mathf.CeilToInt(Screen.width * _noiseOptions._noiseScale);
				var height = Mathf.CeilToInt(Screen.height * _noiseOptions._noiseScale);
				
				if (k_Noise != null && width == k_Noise.width && height == k_Noise.height)
					return;
				
				_noiseResolution.x = width;
				_noiseResolution.y = height;
				
				k_Noise            = new Texture2D(width, height, GraphicsFormat.R8_UNorm, 0);
				//k_Noise          = new Texture2D(width, height, GraphicsFormat.R8G8B8A8_UNorm);
				k_Noise.wrapMode   = TextureWrapMode.Repeat;
				k_Noise.filterMode = FilterMode.Bilinear;

				var pixels = width * height;
				var data   = new byte[pixels];
				for (var n = 0; n < pixels; n++)
					data[n] = (byte)(Random.Range(byte.MinValue, byte.MaxValue));

				k_Noise.SetPixelData(data, 0);
				k_Noise.Apply(false, true);
			}
			catch
			{
				k_Noise = null;
			}
		}
		
		private void _setNoiseState(NoiseMode value)
		{
			// hardcoded state machine
			switch (_noiseOptions._noiseMode)
			{
				case NoiseMode.Dynamic:
				case NoiseMode.Static:
					_giMat.DisableKeyword("TEXTURE_RANDOM");
					break;
				case NoiseMode.Shader:
					_giMat.DisableKeyword("FRAGMENT_RANDOM");
					break;
				case NoiseMode.None:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			_noiseOptions._noiseMode = value;

			switch (_noiseOptions._noiseMode)
			{
				case NoiseMode.Dynamic:
				case NoiseMode.Static:
					_giMat.EnableKeyword("TEXTURE_RANDOM");
					break;
				case NoiseMode.Shader:
					_giMat.EnableKeyword("FRAGMENT_RANDOM");
					break;
				case NoiseMode.None:
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}
		
		private static void _initScreenMesh(Mesh mesh, Matrix4x4 mat)
		{
			mesh.vertices  = _verts(0f);
			mesh.uv        = _texCoords();
			mesh.triangles = new int[3] { 0, 1, 2 };

			mesh.UploadMeshData(true);

			// -----------------------------------------------------------------------
			Vector3[] _verts(float z)
			{
				var r = new Vector3[3];
				for (var i = 0; i < 3; i++)
				{
					var uv = new Vector2((i << 1) & 2, i & 2);
					r[i] = mat.MultiplyPoint(new Vector3(uv.x * 2f - 1f, uv.y * 2f - 1f, z));
				}

				return r;
			}

			Vector2[] _texCoords()
			{
				var r = new Vector2[3];
				for (var i = 0; i < 3; i++)
				{
					if (SystemInfo.graphicsUVStartsAtTop)
						r[i] = new Vector2((i << 1) & 2, 1.0f - (i & 2));
					else
						r[i] = new Vector2((i << 1) & 2, i & 2);
				}

				return r;
			}
		}
		
		private static void _blit(CommandBuffer cmd, RTHandle from, RTHandle to, Material mat)
		{
			cmd.SetGlobalTexture(s_MainTexId, from.nameID);
			cmd.SetRenderTarget(to.nameID);
			cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, mat, 0, 0);
		}

		private static RTHandle _alloc(string id)
		{
			return RTHandles.Alloc(id, name: id);
		}
	}
}
