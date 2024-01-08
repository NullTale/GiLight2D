using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

//  GiLight2D © NullTale - https://twitter.com/NullTale/
namespace GiLight2D
{
    public partial class GiLight2D : ScriptableRendererFeature
    {
        private const string k_BlitShader  = "Hidden/GiLight2D/Blit";
        private const string k_JfaShader   = "Hidden/GiLight2D/JumpFlood";
        private const string k_GiShader    = "Hidden/GiLight2D/Gi";
        private const string k_BlurShader  = "Hidden/GiLight2D/Blur";
        private const string k_DistShader  = "Hidden/GiLight2D/Distance";
		
        private static readonly int s_MainTexId           = Shader.PropertyToID("_MainTex");
        private static readonly int s_NoiseTilingOffsetId = Shader.PropertyToID("_NoiseTilingOffset");
        private static readonly int s_NoiseTexId          = Shader.PropertyToID("_NoiseTex");
        private static readonly int s_UvScaleId           = Shader.PropertyToID("_UvScale");
        private static readonly int s_IntensityId         = Shader.PropertyToID("_Intensity");
        private static readonly int s_IntensityBounceId   = Shader.PropertyToID("_IntensityBounce");
        private static readonly int s_SamplesId           = Shader.PropertyToID("_Samples");
        private static readonly int s_OffsetId            = Shader.PropertyToID("_Offset");
        private static readonly int s_ColorTexId          = Shader.PropertyToID("_ColorTex");
        private static readonly int s_BounceTexId         = Shader.PropertyToID("_BounceTex");
        private static readonly int s_DistTexId           = Shader.PropertyToID("_DistTex");
        private static readonly int s_AspectId            = Shader.PropertyToID("_Aspect");
        private static readonly int s_PowerId             = Shader.PropertyToID("_Power");
        private static readonly int s_StepSizeId          = Shader.PropertyToID("_StepSize");
        private static readonly int s_ScaleId             = Shader.PropertyToID("_Scale");
        private static readonly int s_StepId              = Shader.PropertyToID("_Step");
        private static readonly int s_AlphaTexId          = Shader.PropertyToID("_AlphaTex");
        private static readonly int s_ATexId              = Shader.PropertyToID("_ATex");
        private static readonly int s_BTexId              = Shader.PropertyToID("_BTex");
		
        private static List<ShaderTagId> k_ShaderTags;
        private static Mesh              k_ScreenMesh;
        private static Texture2D         k_Noise;
        
        public static Mesh ScreenMesh => k_ScreenMesh;

        [SerializeField]
        private RenderPassEvent      _event = RenderPassEvent.BeforeRenderingOpaques;
        [SerializeField]
        [Tooltip("Which objects should be rendered as Gi")]
        private LayerMask            _mask = new LayerMask() { value = -1 };
        
        [SerializeField]
        [Tooltip("Which volume settings to use")]
        private Optional<LayerMask>  _volume = new Optional<LayerMask>(false);
        [SerializeField]
        private OutputOptions        _output = new OutputOptions();
        [SerializeField]
        [Tooltip("Attach depth stencil buffer for Gi rendering. Allows stencil mask interaction and z culling")]
        private bool                 _depthStencil = true;
		
        [SerializeField]
        [Tooltip("How many rays to emit from each pixel")]
        private int                  _rays = 100;
        [SerializeField]
        public TraceOptions          _traceOptions = new TraceOptions();
        [SerializeField]
        [Tooltip("Final light intensity, basically color multiplier")]
        private RangeFloat 		     _intensity = new RangeFloat(new Vector2(.0f, 3f), 1f);
        [SerializeField]
        [Tooltip("Max ray distance in world space (relative to full alpha)")]
        private float                _distance = 7;
        [SerializeField]
        [Tooltip("Rays aspect")]
        private float 			     _aspect = 0;
        [SerializeField]
        [Tooltip("Maximum number of ray steps")]
        private RaySteps             _steps = RaySteps.N16;
        [SerializeField]
        [Tooltip("Distance map additional offset for each ray step")]
        private Optional<RangeFloat> _distOffset = new Optional<RangeFloat>(new RangeFloat(new Vector2(-.01f, .1f), .0f), false);
        [SerializeField]
        private NoiseOptions         _noiseOptions = new NoiseOptions();
        [SerializeField]
        [Tooltip("Additional orthographic camera space, to make objects visible outside of the camera frame")]
        private Optional<RangeFloat> _border = new Optional<RangeFloat>(new RangeFloat(new Vector2(.0f, 3f), 0f), false);
        [SerializeField]
        private ScaleModeOptions     _scaleMode = new ScaleModeOptions();
		
        [SerializeField]
        private BlurOptions          _blurOptions = new BlurOptions();
        [Header("Debug")]
        [SerializeField]
        [Tooltip("Override final output for debug purposes")]
        private DebugOutput          _outputOverride = DebugOutput.None;
		
        [SerializeField]
        [Tooltip("Run render feature in scene view project window")]
        private bool                 _runInSceneView;
		
		
        [SerializeField]
        private ShaderCollection     _shaders = new ShaderCollection();
		
        private GiPass               _giPass;
		
        private Material _giMat;
        private Material _blitMat;
        private Material _jfaMat;
        private Material _distMat;
        private Material _blurMat;
        
        private  VolumeStack _stack;
		
        private RenderTextureDescriptor _rtDesc      = new RenderTextureDescriptor(0, 0, GraphicsFormat.None, 0, 0);
        private Vector2Int              _rtRes       = Vector2Int.zero;
        private Vector2Int              _noiseRes    = Vector2Int.zero;
        private Vector2                 _noiseTiling = Vector2.one;
        private Vector2Int              _rtBounceRes = Vector2Int.zero;
        private Fps                     _fps         = new Fps();

        private bool ForceTextureOutput => _output._output == FinalBlit.Texture;
        private bool HasGiBorder        => _border.Enabled && _border.Value.Value > 0f;
		
        public int        Rays       { get => _rays;                  set => _rays = value; }
        public float      Intensity  { get => _intensity.Value;       set => _intensity.Value = value; }
        public float      Aspect     { get => _aspect;                set => _aspect = value; }
        public float      Distance   { get => _distance;              set => _distance = value; }
        public float      GiScale    { get => _scaleMode._ratio;      set => _scaleMode._ratio = value; }
        public bool       Blur       { get => _blurOptions._enable;   set => _blurOptions._enable = value; }
        public float      BlurStep   { get => _blurOptions._step;   set => _blurOptions._step = value; }
        public Vector2Int GiTexSize => _rtRes;
        public Vector2Int NoiseTexSize => _noiseRes;

        
        public float BounceIntensity
        {
            get => _traceOptions._intencity;
            set => _traceOptions._intencity = value;
        }

        public float BouncePiercing
        {
            get => _traceOptions._piercing;
            set => _traceOptions._piercing = value;
        }

        public int BounceCount
        {
            set
            {
                var isEnabled = value > 0;
                if (isEnabled != _traceOptions._enable)
                {
                    _traceOptions._enable = isEnabled;
                    
                    if (isEnabled)
                    {
                        _giMat.EnableKeyword("RAY_BOUNCES");
                    }
                    else
                    {
                        _giMat.DisableKeyword("RAY_BOUNCES");
                    }
                }
                
                _traceOptions._bounces = Mathf.Clamp(value, 1, 3);
            }
            get => _traceOptions._bounces;
        }
        
        public NoiseSource Noise
        {
            get => _noiseOptions._noise;
            set
            {
                if (_noiseOptions._noise == value)
                    return;

                _setNoiseState(value);
            }
        }
        
        public NoiseTexture NoisePattern
        {
            get => _noiseOptions._pattern;
            set
            {
                _noiseOptions._pattern = value;
                _initNoise();
            }
        }

        public bool NoiseFilter
        {
            get => _noiseOptions._bilinear;
            set
            {
                _noiseOptions._bilinear = value;
                _initNoise();
            }
        }

        public Vector2 NoiseVelocity
        {
            get => _noiseOptions._velocity;
            set => _noiseOptions._velocity = value;
        }

        public RaySteps Steps
        {
            get => _steps;
            set => _setSteps(value);
        }
        
        public float DistOffset
        {
            get => _distOffset.Enabled ? _distOffset.Value.Value : 0f;
            set
            {
                _distOffset.Value.Value = value;
                _distMat.SetFloat(s_OffsetId, _distOffset.Value.Value);
            }
        }

        public float Border
        {
            get => _border.Enabled ? _border.Value.Value : 0f;
            set
            {
                _border.Enabled     = value > 0f;
                _border.Value.Value = value;
            }
        }

        public float NoiseScale
        {
            get => _noiseOptions._scale;
            set
            {
                _noiseOptions._scale = value;
				
                _initNoise();
            }
        }

        public FinalBlit Output
        {
            get => _output._output;
            set => _output._output = value;
        }
        
        public DebugOutput OutputOverride
        {
            get => _outputOverride;
            set => _outputOverride = value;
        }

        private NoiseTexture _noisePattern;
        private bool         _noiseFilter;

        // =======================================================================
        public class RenderTarget
        {
            public RTHandle Handle;
            public int      Id;
			
            private bool    _allocated;
            
            // =======================================================================
            public RenderTarget Allocate(RenderTexture rt, string name)
            {
                Handle = RTHandles.Alloc(rt, name);
                Id     = Shader.PropertyToID(name);
				
                return this;
            }
			
            public RenderTarget Allocate(string name)
            {
                Handle = _alloc(name);
                Id     = Shader.PropertyToID(name);
				
                return this;
            }
			
            public void Get(CommandBuffer cmd, in RenderTextureDescriptor desc)
            {
                _allocated = true;
                cmd.GetTemporaryRT(Id, desc);
            }
			
            public void Release(CommandBuffer cmd)
            {
                if (_allocated == false)
                    return;
                
                _allocated = false;
                cmd.ReleaseTemporaryRT(Id);
            }
        }

        public class RenderTargetFlip
        {
            public RenderTarget From => _isFlipped ? _a : _b;
            public RenderTarget To   => _isFlipped ? _b : _a;
			
            private bool         _isFlipped;
            private RenderTarget _a;
            private RenderTarget _b;
			
            // =======================================================================
            public RenderTargetFlip(RenderTarget a, RenderTarget b)
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

            public void Get(CommandBuffer cmd, in RenderTextureDescriptor desc)
            {
                _a.Get(cmd, desc);
                _b.Get(cmd, desc);
            }
        }
        
        public class RenderTargetPostProcess
        {
            private RenderTargetFlip _flip;
            private RTHandle         _output;
            private Material         _giMat;
            private int              _passes;
            private int              _passesLeft;
            
            public  RTHandle         GiTexture => _passesLeft == 0 ? _output : _flip.To.Handle;
			
            // =======================================================================
            public RenderTargetPostProcess(RenderTarget a, RenderTarget b)
            {
                _flip = new RenderTargetFlip(a, b);
            }

            public void Setup(CommandBuffer cmd, in RenderTextureDescriptor desc, RTHandle output, int passes, Material giMat)
            {
                _passes = passes;
                _passesLeft = passes;
                _output = output;
                _giMat = giMat;
                
                if (passes > 0)
                {
                    // draw gi to the tmp flip texture
                    _flip.Get(cmd, in desc);
                    
                    cmd.SetRenderTarget(_flip.To.Handle.nameID);
                    cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, _giMat, 0, 0);
                }
                else
                {
                    // no post process added, draw gi to the output
                    cmd.SetRenderTarget(_output.nameID);
                    cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, _giMat, 0, 0);
                }
            }

            public void Apply(CommandBuffer cmd, Material mat, int pass = 0)
            {
                // draw in output or tmp render target
                _flip.Flip();
                _passesLeft --;
                
                cmd.SetGlobalTexture(s_MainTexId, _flip.From.Handle.nameID);
                cmd.SetRenderTarget(_passesLeft > 0 ? _flip.To.Handle.nameID : _output.nameID);
                cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, mat, 0, pass);
            }
            
            public void Release(CommandBuffer cmd)
            {
                if (_passes > 0)
                {
                    _flip.Release(cmd);
                }
            }
        }

        [Serializable]
        internal class BlurOptions
        {
            public bool     _enable = true;
            [Tooltip("Blur type")]
            public BlurMode _mode = BlurMode.Box;
            [Tooltip("Blur distance in uv coords, if disabled step will be set to one pixel per sample")]
            [Range(0, 0.003f)]
            public float _step;
        }
        
        [Serializable]
        public class ScaleModeOptions
        {
            [Tooltip("Resolution scale of gi texture.")]
            public ScaleMode _scaleMode;
            [Tooltip("Scale ratio relative to main resolution")]
            [Range(0.1f, 2f)]
            public float     _ratio  = 1f;
            [Tooltip("Fixed height of gi texture, width will be set relative to the aspect")]
            public int       _height = 240;
        }

        [Serializable]
        internal class OutputOptions
        {
            [Tooltip("Where to store final result")]
            public FinalBlit _output = FinalBlit.Camera;
            [Tooltip("Global name of output texture")]
            public string _globalTexture = "_giTex";
            [Tooltip("What information to store in the alpha channel")]
            public Alpha _alpha = Alpha.One;
        }
		
        [Serializable]
        internal class NoiseOptions
        {
            public NoiseSource   _noise = NoiseSource.Shader;
            [Range(0f, 1f)]
            public float         _scale    = 1f;
            public Vector2      _velocity = new Vector2(0f, 0f);
            public NoiseTexture _pattern  = NoiseTexture.Random;
            public bool         _bilinear = true;
            public Texture2D    _texture;
            [Tooltip("Noise would be scaled and translated relative to the camera world position and ortho size")]
            public Optional<float> _orthoRelative;
        }
        
        [Serializable]
        public class TraceOptions
        {
            public bool  _enable = false;
            [Tooltip("Bounce texture scale")]
            [Range(0.001f, 1)]
            public float _scale = 1f;
            [Tooltip("Bounce ray piecing")]
            [Range(0, 1)]
            public float _piercing = .5f;
            [Tooltip("Number of bounces")]
            [Range(1, 3)]
            public int   _bounces = 1;
            [Tooltip("Scale of bounce light")]
            public float _intencity = 1;
            [Tooltip("Bounce scales")]
            [Range(0, 1)]
            public float[] _scales = new float[3] { 1f, 1f, 1f };
        }

        [Serializable]
        public class ShaderCollection
        {
            public Shader _gi;
            public Shader _jfa;
            public Shader _dist;
            public Shader _blit;
            public Shader _blur;
        }

        [Serializable]
        public class Fps
        {
            [Range(0.1f, 60.0f)]
            public float _observation = 1.0f;
            
            private float        _fps;
            private float        _deltaSum;
            private Queue<float> _framesDelta = new Queue<float>();

            private float _lastFrame;

            public float Current => _fps;

            // =======================================================================
            public void Update()
            {
                var currentDelta = Time.time - _lastFrame;
                while (_deltaSum + currentDelta > _observation && _framesDelta.Count > 0)
                    _deltaSum -= _framesDelta.Dequeue();

                _fps = _framesDelta.Count > 0 ? _framesDelta.Count / (_deltaSum + currentDelta) : 0;
            }
            
            public void Frame()
            {
                // do not update twice in one frame if we use renderer multiple times
                if (_lastFrame == Time.time)
                    return;
                
                var deltaTime = Time.time - _lastFrame; 
                _lastFrame = Time.time;
                
                _framesDelta.Enqueue(deltaTime);
                _deltaSum += deltaTime;
            }
        }
        
        public enum ScaleMode
        {
            None,
            
            [Tooltip("Scale relative camera resolution")]
            Scale,
            [Tooltip("Scale always has fixed frame height, but aspect still will be taken from the camera")]
            Fixed,
        }
		
        public enum NoiseSource
        {
            None    = 0,
            Texture = 1,
            Shader  = 2,
        }
        
        public enum NoiseTexture
        {
            Random,
            LinesH,
            LinesV,
            Checker,
            Texture,
        }

        public enum DebugOutput
        {
            None     = 0,
            Objects  = 1,
            Flood    = 2,
            Distance = 3,
            Bounce   = 4
        }

        public enum FinalBlit
        {
            Texture,
            Camera
        }

        public enum BlurMode
        {
            Horizontal,
            Vertial,
            Cross,
            Box
        }

        public enum RaySteps
        {
            N4,
            N6,
            N8,
            N12,
            N16,
        }

        public enum Alpha
        {
            [Tooltip("Always one")]
            One,
            [Tooltip("Object mask")]
            Mask,
            [Tooltip("Texture will be used for alpha blending")]
            Blend
        }
        
        // =======================================================================
        public override void Create()
        {
            _giPass = new GiPass() { _owner = this };
            _giPass.Init();
            
            _fps = new Fps();

            _validateShaders();
			
            _initMaterials();

            _setNoiseState(_noiseOptions._noise);
            
            _stack = _volume.Enabled ? VolumeManager.instance.CreateStack() : VolumeManager.instance.stack;
			
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
        private static          float _timeLimiter;
        private static readonly int   s_SrcMode = Shader.PropertyToID("SrcMode");
        private static readonly int   s_DstMode = Shader.PropertyToID("DstMode");

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
#if UNITY_EDITOR
            // fix resources lost error after build
            if (_giMat == null)
            {
                Create();
            }
#endif

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
            
            _applyFromPostProcess();
            
            _setupDesc(in renderingData);
            
            if (_outputOverride != DebugOutput.None)
            {
                _giPass.renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
            }
            else
            {
                _giPass.renderPassEvent = _event;
            }
            
            renderer.EnqueuePass(_giPass);
        }

        private void _applyFromPostProcess()
        {
            if (_volume.Enabled)
                VolumeManager.instance.Update(_stack, null, _volume.Value);
            
            var settings = _stack.GetComponent<GiLightVol>();
            if (settings.active == false)
                return;
            
            if (settings.m_Rays.overrideState)
                Rays = settings.m_Rays.value;
            
            if (settings.m_Intensity.overrideState)
                Intensity = settings.m_Intensity.value;
            
            if (settings.m_Distance.overrideState)
                Distance = settings.m_Distance.value;
            
            if (settings.m_Aspect.overrideState)
                Aspect = settings.m_Aspect.value;
        
            if (settings.m_Steps.overrideState)
                Steps = settings.m_Steps.value switch
                {
                    1 => RaySteps.N4,
                    2 => RaySteps.N6,
                    3 => RaySteps.N8,
                    4 => RaySteps.N12,
                    5 => RaySteps.N16,
                    _ => throw new ArgumentOutOfRangeException()
                };
            
            if (settings.m_Scale.overrideState)
                GiScale = settings.m_Scale.value;
            
            if (settings.m_NoiseTex.overrideState)
            {
                if (settings.m_NoiseTex.value == GiLightVol.NoiseTexture.None)
                {
                    Noise = NoiseSource.None;
                }
                else
                {
                    Noise = NoiseSource.Texture;
                    NoisePattern = settings.m_NoiseTex.value switch
                    {
                        GiLightVol.NoiseTexture.Random  => NoiseTexture.Random,
                        GiLightVol.NoiseTexture.LinesH  => NoiseTexture.LinesH,
                        GiLightVol.NoiseTexture.LinesV  => NoiseTexture.LinesV,
                        GiLightVol.NoiseTexture.Checker => NoiseTexture.Checker,
                        _                                    => throw new ArgumentOutOfRangeException()
                    };
                }
            }
        
            if (settings.m_NoiseVel.overrideState)
                NoiseVelocity = settings.m_NoiseVel.value;
        
            if (settings.m_NoiseScale.overrideState)
                NoiseScale = settings.m_NoiseScale.value;
            
            if (settings.m_NoiseFilter.overrideState)
                NoiseFilter = settings.m_NoiseFilter.value;
            
            if (settings.m_Blur.overrideState)
                BlurStep = Mathf.LerpUnclamped(0.0f, 0.003f, settings.m_Blur.value);
        }

        // =======================================================================
        private void _initMaterials()
        {
            _jfaMat   = new Material(_shaders._jfa);
            _blitMat  = new Material(_shaders._blit);
            
            _blurMat = new Material(_shaders._blur);
            switch (_blurOptions._mode)
            {
                case BlurMode.Horizontal:
                    _blurMat.EnableKeyword("HORIZONTAL");
                    break;
                case BlurMode.Vertial:
                    _blurMat.EnableKeyword("VERTICAL");
                    break;
                case BlurMode.Cross:
                    _blurMat.EnableKeyword("CROSS");
                    break;
                case BlurMode.Box:
                    _blurMat.EnableKeyword("BOX");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            _distMat  = new Material(_shaders._dist);
            if (_distOffset.Enabled)
            {
                _distMat.EnableKeyword("ENABLE_OFFSET");
                _distMat.SetFloat(s_OffsetId, _distOffset.Value.Value);
            }
                
            _giMat    = new Material(_shaders._gi);
            if (_traceOptions._enable)
                _giMat.EnableKeyword("RAY_BOUNCES");
            _setSteps(_steps);
            _setAlpha(_output._alpha);
        }
        
        private void _validateShaders()
        {
#if UNITY_EDITOR
            _validate(ref _shaders._gi,		k_GiShader);
            _validate(ref _shaders._blit,	k_BlitShader);
            _validate(ref _shaders._jfa,	k_JfaShader);
            _validate(ref _shaders._dist,	k_DistShader);
            _validate(ref _shaders._blur,	k_BlurShader);
			
            UnityEditor.EditorUtility.SetDirty(this);
            // -----------------------------------------------------------------------
            void _validate(ref Shader shader, string path)
            {
                if (shader != null)
                    return;
				
                shader = Shader.Find(path);
            }
#endif
        }
		
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
                    Mathf.FloorToInt((camDesc.width / (float)camDesc.height) * _scaleMode._height * _scaleMode._ratio),
                    Mathf.FloorToInt((_scaleMode._height * _scaleMode._ratio))
                ),

                _ => throw new ArgumentOutOfRangeException()
            };
            
            if (_rtRes.x < 1)
                _rtRes.x = 1;
            if (_rtRes.y < 1)
                _rtRes.y = 1;
            
            var ortho   = renderingData.cameraData.camera.orthographicSize;
            var uvScale = _border.Enabled ? (ortho + _border.Value.Value) / ortho : 1f;
            
            // increase resolution for border padding
            if (_border.Enabled)
            {
                var scaleInc = uvScale - 1f;
                _rtRes.x += Mathf.FloorToInt(_rtRes.x * scaleInc);
                _rtRes.y += Mathf.FloorToInt(_rtRes.x * scaleInc);;
            }
			
            _giMat.SetVector(s_ScaleId, new Vector4(uvScale, uvScale, 1f, 1f));
            
            _rtDesc.width  = _rtRes.x;
            _rtDesc.height = _rtRes.y;
            
            _rtBounceRes.x = Mathf.CeilToInt(_rtRes.x * _traceOptions._scale);
            _rtBounceRes.y = Mathf.CeilToInt(_rtRes.y * _traceOptions._scale);
        }
		
        private void _initNoise()
        {
            // try block to fix editor startup error
            try
            {
                var width  = Mathf.CeilToInt(Screen.width * _noiseOptions._scale);
                var height = Mathf.CeilToInt(Screen.height * _noiseOptions._scale);
                
                if (width < 2)
                    width = 2;
                if (height < 2)
                    height = 2;
                
                if (width > 4)
                    width  -= width % 4;
                if (height > 4)
                    height -= height % 4;

                // rebuild only if params was changed
                if (k_Noise != null 
                    && (width == k_Noise.width && height == k_Noise.height) 
                    && (_noiseOptions._pattern == _noisePattern) 
                    && (_noiseOptions._bilinear == _noiseFilter))
                    return;
                
                _noisePattern = _noiseOptions._pattern; 
                _noiseFilter  = _noiseOptions._bilinear;
                
                if (_noiseOptions._pattern == NoiseTexture.Texture)
                {
                    k_Noise = _noiseOptions._texture;
                    _noiseRes.x = k_Noise.width;
                    _noiseRes.y = k_Noise.height;
                    
                    _noiseTiling.x = width > k_Noise.width ? width / (float)k_Noise.width : k_Noise.width / (float)width;
                    _noiseTiling.y = height > k_Noise.height ? height / (float)k_Noise.height : k_Noise.height / (float)height;
                    return;
                }
				
                _noiseTiling.x = 1;
                _noiseTiling.y = 1;
                
                _noiseRes.x = width;
                _noiseRes.y = height;
				
                k_Noise            = new Texture2D(width, height, GraphicsFormat.R8_UNorm, 0);
                k_Noise.name       = nameof(k_Noise);
                k_Noise.wrapMode   = TextureWrapMode.Repeat;
                k_Noise.filterMode = _noiseOptions._bilinear ? FilterMode.Bilinear : FilterMode.Point;
                
                var pixels = width * height;
                var data   = new byte[pixels];
                switch (_noiseOptions._pattern)
                {
                    case NoiseTexture.Random:
                    {
                        for (var n = 0; n < pixels; n++)
                            data[n] = (byte)(Random.Range(byte.MinValue, byte.MaxValue));
                    } break;
                    case NoiseTexture.LinesH:
                    {
                        for (var n = 0; n < pixels; n++)
                            data[n] = n / width % 2 == 1 ? byte.MaxValue : byte.MinValue;
                    } break;
                    case NoiseTexture.LinesV:
                    {
                        for (var n = 0; n < pixels; n++)
                            data[n] = n % width % 2 == 1 ? byte.MaxValue : byte.MinValue;
                    } break;
                    case NoiseTexture.Checker:
                    {
                        for (var n = 0; n < pixels; n++)
                        {
                            var x = n / width;
                            var y = n % width;

                            data[n] = (x % 2 == 0 && y % 2 == 0) || (x % 2 == 1 && y % 2 == 1) ? byte.MaxValue : byte.MinValue;
                        }
                    } break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                k_Noise.SetPixelData(data, 0);
                
                if (width % 4 == 0 && height % 4 == 0)
                    k_Noise.Compress(true);
                k_Noise.Apply(false, true);
            }
            catch
            {
                k_Noise = null;
            }
        }
		
        private void _setNoiseState(NoiseSource value)
        {
            // hardcoded state machine
            switch (_noiseOptions._noise)
            {
                case NoiseSource.None:
                case NoiseSource.Texture:
                    _giMat.DisableKeyword("TEXTURE_RANDOM");
                    break;
                case NoiseSource.Shader:
                    _giMat.DisableKeyword("FRAGMENT_RANDOM");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            _noiseOptions._noise = value;

            switch (_noiseOptions._noise)
            {
                case NoiseSource.None:
                case NoiseSource.Texture:
                    _giMat.EnableKeyword("TEXTURE_RANDOM");
                    break;
                case NoiseSource.Shader:
                    _giMat.EnableKeyword("FRAGMENT_RANDOM");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
		
        private void _setSteps(RaySteps steps)
        {
            switch (_steps)
            {
                case RaySteps.N4:
                    _giMat.DisableKeyword("STEPS_4");
                    break;
                case RaySteps.N6:
                    _giMat.DisableKeyword("STEPS_6");
                    break;
                case RaySteps.N8:
                    _giMat.DisableKeyword("STEPS_8");
                    break;
                case RaySteps.N12:
                    _giMat.DisableKeyword("STEPS_12");
                    break;
                case RaySteps.N16:
                    _giMat.DisableKeyword("STEPS_16");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            _steps = steps;
            
            switch (_steps)
            {
                case RaySteps.N4:
                    _giMat.EnableKeyword("STEPS_4");
                    break;
                case RaySteps.N6:
                    _giMat.EnableKeyword("STEPS_6");
                    break;
                case RaySteps.N8:
                    _giMat.EnableKeyword("STEPS_8");
                    break;
                case RaySteps.N12:
                    _giMat.EnableKeyword("STEPS_12");
                    break;
                case RaySteps.N16:
                    _giMat.EnableKeyword("STEPS_16");
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private void _setAlpha(Alpha alpha)
        {
            switch (alpha)
            {
                case Alpha.One:
                    _giMat.DisableKeyword("ONE_ALPHA");
                    break;
                case Alpha.Mask:
                    _giMat.DisableKeyword("OBJECTS_MASK_ALPHA");
                    break;
                case Alpha.Blend:
                    _giMat.DisableKeyword("NORMALIZED_ALPHA");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(alpha), alpha, null);
            }
            
            _output._alpha = alpha;
            
            switch (alpha)
            {
                case Alpha.One:
                    _giMat.EnableKeyword("ONE_ALPHA");
                    break;
                case Alpha.Mask:
                    _giMat.EnableKeyword("OBJECTS_MASK_ALPHA");
                    break;
                case Alpha.Blend:
                    _giMat.EnableKeyword("NORMALIZED_ALPHA");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(alpha), alpha, null);
            }
            
            // draw with alpha blending if camera overlay
            if (_output._output == FinalBlit.Camera)
            {
                _giMat.SetInt (s_SrcMode, (int)BlendMode.SrcAlpha);
                _giMat.SetInt (s_DstMode, (int)BlendMode.OneMinusSrcAlpha);
            }
            else
            {
                _giMat.SetInt(s_SrcMode, (int)BlendMode.One);
                _giMat.SetInt(s_DstMode, (int)BlendMode.Zero);
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
		
        private static void _blit(CommandBuffer cmd, RTHandle from, RTHandle to, Material mat, int pass = 0)
        {
            cmd.SetGlobalTexture(s_MainTexId, from.nameID);
            cmd.SetRenderTarget(to.nameID);
            cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, mat, 0, pass);
        }

        private static RTHandle _alloc(string id)
        {
            return RTHandles.Alloc(id, name: id);
        }
    }
}