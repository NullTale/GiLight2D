using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

//  GiLight2D © NullTale - https://twitter.com/NullTale/
namespace GiLight2D
{
    public partial class GiLight2D
    {
        private class GiPass : ScriptableRenderPass
        {
            public GiLight2D _owner;
            
            private RenderTarget            _buffer;
            private RenderTarget            _dist;
            private RenderTargetFlip        _bounce;
            private RenderTarget            _bounceResult;
            private RenderTarget            _bounceTmp;
            private RenderTarget            _alpha;
            private RenderTargetFlip        _jfa;
            private RenderTargetPostProcess _pp;
            private RenderTarget            _output;
            private RTHandle                _cameraOutput;
            private RendererListParams      _rlp;
            private ProfilingSampler        _jfaProfiler;
            private ProfilingSampler        _objProfiler;
            private ProfilingSampler        _distProfiler;
            private ProfilingSampler        _traceProfiler;
            //private ProfilingSampler        _giProfiler;
			
            // =======================================================================
            public void Init()
            {
                renderPassEvent = _owner._event;
				
                _buffer       = new RenderTarget().Allocate(nameof(_buffer));
                _bounceResult = new RenderTarget().Allocate(nameof(_bounceResult));
                _dist         = new RenderTarget().Allocate(nameof(_dist));
                _bounce       = new RenderTargetFlip(new RenderTarget().Allocate($"{nameof(_bounce)}_a"), new RenderTarget().Allocate($"{nameof(_bounce)}_b"));
                _bounceTmp    = new RenderTarget().Allocate(nameof(_bounceTmp));
                _alpha        = new RenderTarget().Allocate(nameof(_alpha));
                _jfa          = new RenderTargetFlip(new RenderTarget().Allocate($"{nameof(_jfa)}_a"), new RenderTarget().Allocate($"{nameof(_jfa)}_b"));
                _pp           = new RenderTargetPostProcess(new RenderTarget().Allocate($"{nameof(_pp)}_a"), new RenderTarget().Allocate($"{nameof(_pp)}_b"));
                _output       = new RenderTarget().Allocate(_owner._output._globalTexture);
				
                _rlp       = new RendererListParams(new CullingResults(), new DrawingSettings(), new FilteringSettings(RenderQueueRange.all, _owner._mask));
                
                _jfaProfiler  = new ProfilingSampler("Screen Uv Fluid Fill");
                _objProfiler  = new ProfilingSampler("Objects");
                _distProfiler = new ProfilingSampler("Distance");
                _traceProfiler   = new ProfilingSampler("Trace");
                //_giProfiler   = new ProfilingSampler("Gi");
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // allocate resources
                ref var res  = ref _owner._rtRes;
                ref var bounceRes  = ref _owner._rtBounceRes;
                ref var desc = ref _owner._rtDesc;
                var     cmd  = CommandBufferPool.Get(nameof(GiLight2D));

                ref var cameraData = ref renderingData.cameraData;
                var     camera     = cameraData.camera;

                if (_owner.ForceTextureOutput)
                {
                    _output.Get(cmd, desc);
                    cmd.SetGlobalTexture(_output.Id, _output.Handle.nameID);
                }
                
#if !UNITY_2022_1_OR_NEWER
                _cameraOutput = RTHandles.Alloc(renderingData.cameraData.renderer.cameraColorTarget);
#else
                _cameraOutput = renderingData.cameraData.renderer.cameraColorTargetHandle;
#endif
                
                var aspectRatioX = (res.x / (float)res.y);
                var aspectRatioY = 1f;
				if (_owner._aspect < 0f)
					aspectRatioX *= 1f - _owner._aspect;
				if (_owner._aspect > 0f)
					aspectRatioY *= 1f + _owner._aspect; 

                var piercing    = Mathf.LerpUnclamped(0f, 7f,_owner._traceOptions._piercing) / (800f / Mathf.Max(bounceRes.x, bounceRes.y));
                _owner._giMat.SetVector(s_AspectId, new Vector4(aspectRatioX, aspectRatioY, piercing * aspectRatioX / bounceRes.x, piercing / bounceRes.y));
                _owner._giMat.SetFloat(s_SamplesId, _owner._rays);
                _owner._giMat.SetFloat(s_IntensityId, _owner._intensity.Value);
                _owner._giMat.SetFloat(s_PowerId, _owner._distance / (camera.orthographicSize * 2f));
                
                _owner._distMat.SetVector(s_AspectId, new Vector4(aspectRatioX, aspectRatioY, 0, 0));
                _owner._jfaMat.SetVector(s_AspectId, new Vector4(aspectRatioX, aspectRatioY, 0, 0));
                
                // allocate render textures
                desc.colorFormat = RenderTextureFormat.ARGB32;
                if (_owner._depthStencil)
                {
                    // set depth stencil format from rendering camera 
                    desc.depthStencilFormat = renderingData.cameraData.cameraTargetDescriptor.depthStencilFormat;
                    _buffer.Get(cmd, desc);
                    desc.depthStencilFormat = GraphicsFormat.None;
                }
                else
                {
                    _buffer.Get(cmd, desc);
                }

                desc.colorFormat = RenderTextureFormat.RG16;
                _jfa.Get(cmd, desc);
				
                desc.colorFormat = RenderTextureFormat.R16;
                _dist.Get(cmd, desc);
                

                _objProfiler.Begin(cmd);
                // render with layer mask
                var cullResults = renderingData.cullResults;
                if (_owner.HasGiBorder)
                {
                    // expand camera ortho projection if has Gi border, drawback is that we need to do cull results second time
                    // it is not necessary if we want to draw with separate camera
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

                _rlp.cullingResults = cullResults;
                _rlp.drawSettings   = CreateDrawingSettings(k_ShaderTags, ref renderingData, SortingCriteria.CommonTransparent);
                
                var rl = context.CreateRendererList(ref _rlp);
                cmd.DrawRendererList(rl);
				
                if (_owner.HasGiBorder)
                {
                    RenderingUtils.SetViewAndProjectionMatrices(cmd, cameraData.GetViewMatrix(), cameraData.GetGPUProjectionMatrix(), false);
                }
				
                _objProfiler.End(cmd);
                
                _jfaProfiler.Begin(cmd);
                // draw uv with alpha mask 
                _blit(_buffer.Handle, _jfa.From.Handle, _owner._blitMat, 3);
				
                // fluid fill uv coords
                var max      = Mathf.Max(res.x, res.y);
                var steps    = Mathf.CeilToInt(Mathf.Log(max));
                var stepSize = new Vector2(1, 1);

                for (var n = 0; n < steps; n++)
                {
                    stepSize /= 2f;
                    cmd.SetGlobalVector(s_StepSizeId, stepSize);
					
                    _blit(_jfa.From.Handle, _jfa.To.Handle, _owner._jfaMat);
                    _jfa.Flip();
                }
                _jfaProfiler.End(cmd);

                _distProfiler.Begin(cmd);
                // evaluate distance from uv coords
                _blit(_jfa.From.Handle, _dist.Handle, _owner._distMat);
                _distProfiler.End(cmd);
                
                _jfa.Flip();
				
                // apply raytracer, final blit
                cmd.SetGlobalTexture(s_ColorTexId, _buffer.Handle.nameID);
                cmd.SetGlobalTexture(s_DistTexId, _dist.Handle.nameID);
				
                switch (_owner._noiseOptions._noise)
                {
                    case NoiseSource.None:
                    {
                        _owner._giMat.SetTexture(s_NoiseTexId, Texture2D.blackTexture);
                    } break;
                    case NoiseSource.Texture:
                    {
                        var xOffset = 0f;
                        if (_owner._noiseOptions._velocity.x != 0)
                        {
                            var xPeriod = (1f / _owner._noiseOptions._velocity.x);
                            xOffset = -(Time.unscaledTime % xPeriod / xPeriod);
                        } 
                        var yOffset = 0f;
                        if (_owner._noiseOptions._velocity.y != 0)
                        {
                            var yPeriod = (1f / _owner._noiseOptions._velocity.y);
                            yOffset = -(Time.unscaledTime % yPeriod / yPeriod);
                        }
                        
                        var xTiling = _owner._noiseTiling.x;
                        var yTiling = _owner._noiseTiling.y;
                        if (_owner._noiseOptions._orthoRelative.Enabled)
                        {
                            var     aspect     = camera.aspect;
                            var     camHeight  = camera.orthographicSize * 2f;
                            var     camWidth   = camera.aspect * camHeight;
                            
                            var scaleX = camWidth / (_owner._noiseOptions._orthoRelative.Value * 2f * aspect);
                            var scaleY = camHeight / (_owner._noiseOptions._orthoRelative.Value * 2f);
                            
                            xOffset += ((camera.transform.position.x % camWidth) / camWidth) * scaleX - (scaleX - 1f) * camWidth * .5f / camWidth;
                            yOffset += ((camera.transform.position.y % camHeight) / camHeight) * scaleY - (scaleY - 1f) * camHeight * .5f / camHeight;
                            
                            xTiling *= scaleX;
                            yTiling *= scaleY;
                        }
                        
                        _owner._giMat.SetVector(s_NoiseTilingOffsetId, new Vector4(xTiling, yTiling, xOffset, yOffset));
                        _owner._giMat.SetTexture(s_NoiseTexId, k_Noise);
                    } break;
                    case NoiseSource.Shader:
                    {
                        _owner._giMat.SetVector(s_NoiseTilingOffsetId, new Vector4(Random.value, Random.value, 0, 0));
                    } break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }

                _traceProfiler.Begin(cmd);
                // add ray bounces to the initial color texture
                if (_owner._traceOptions._enable)
                {
                    desc.width = bounceRes.x;
                    desc.height = bounceRes.y;
                    
                    // blit object mask (possibly can use stencil buffer instead)
                    desc.colorFormat = RenderTextureFormat.R8;
                    _alpha.Get(cmd, in desc);
                    
                    _blit(_buffer.Handle, _alpha.Handle, _owner._blitMat, 1);
                    cmd.SetGlobalTexture(s_AlphaTexId, _alpha.Handle.nameID);
                    
                    desc.colorFormat = RenderTextureFormat.ARGB32;
                    _bounceTmp.Get(cmd, in desc);
                    _bounce.Get(cmd, in desc);
                    _bounceResult.Get(cmd, in desc);
                        
                    desc.width  = res.x;
                    desc.height = res.y;
                    
                    cmd.SetGlobalTexture(s_ATexId, _bounceTmp.Handle.nameID);
                    cmd.SetGlobalFloat(s_IntensityBounceId, _owner._traceOptions._intencity * _owner._traceOptions._scales[0]);

                    // execute gi, only for outline pixels, then add them to the main color texture, repeat with ray bounce texture
                    cmd.SetRenderTarget(_bounce.To.Handle.nameID);
                    cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1f, 0);
                    cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, _owner._giMat, 0, 1);
                    
                    cmd.SetRenderTarget(_bounceResult.Handle.nameID);
                    cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1f, 0);
                    cmd.SetGlobalTexture(s_MainTexId, _bounce.To.Handle.nameID);
                    cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, _owner._blitMat, 0, 0);
                    
                    for (var n = 1; n < _owner._traceOptions._bounces; n++)
                    {
                        _bounce.Flip();
                        
                        cmd.SetGlobalFloat(s_IntensityBounceId, _owner._traceOptions._intencity * _owner._traceOptions._scales[n]);
                        
                        cmd.SetRenderTarget(_bounce.To.Handle.nameID);
                        cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1f, 0);
                        
                        cmd.SetGlobalTexture(s_ColorTexId, _bounce.From.Handle.nameID);
                        cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, _owner._giMat, 0, 1);
                        
                        _blit(_bounceResult.Handle, _bounceTmp.Handle, _owner._blitMat);
                        
                        cmd.SetGlobalTexture(s_BTexId, _bounce.To.Handle.nameID);
                        cmd.SetRenderTarget(_bounceResult.Handle.nameID);
                        cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, _owner._blitMat, 0, 2);   
                    }
                    
                    cmd.SetGlobalTexture(s_ColorTexId, _buffer.Handle.nameID);
                    cmd.SetGlobalTexture(s_BounceTexId, _bounceResult.Handle.nameID);
                }

                _traceProfiler.End(cmd);
                
                // draw gi & apply post process
                desc.colorFormat = RenderTextureFormat.ARGB32;
                var passes = _postProcessCount();
                var output = _owner._output._output switch
                {
                    FinalBlit.Texture => _output.Handle,
                    FinalBlit.Camera  => _cameraOutput,
                    _                 => throw new ArgumentOutOfRangeException()
                };
                
                _pp.Setup(cmd, in desc, output, passes, _owner._giMat);
                
                if (_hasBlurImpact())
                {
                    var step = new Vector4(_owner._blurOptions._step * (desc.width / desc.height), _owner._blurOptions._step, 0, 0);
                    
                    _owner._blurMat.SetVector(s_StepId, step);
                    _pp.Apply(cmd, _owner._blurMat);
                }

                // output debug override
                if (_owner._outputOverride != DebugOutput.None)
                {
                    switch (_owner._outputOverride)
                    {
                        case DebugOutput.Objects:
                        {
                            _blit(_buffer.Handle, _cameraOutput, _owner._blitMat);
                        } break;
                        case DebugOutput.Flood:
                        {
                            _blit(_jfa.To.Handle, _cameraOutput, _owner._blitMat);
                        } break;
                        case DebugOutput.Distance:
                        {
                            _blit(_dist.Handle, _cameraOutput, _owner._blitMat);
                        } break;
                        case DebugOutput.Bounce:
                        {
                            cmd.SetRenderTarget(_cameraOutput);
                            cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1f, 0);
                            
                            if (_owner._traceOptions._enable)
                                _blit(_bounceResult.Handle, _cameraOutput, _owner._blitMat);
                        } break;
                        case DebugOutput.None:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
				
                _execute();
				
                // -----------------------------------------------------------------------
                void _blit(RTHandle from, RTHandle to, Material mat, int pass = 0)
                {
                    GiLight2D._blit(cmd, from, to, mat, pass);
                }

                int _postProcessCount()
                {
                    var count = 0;
                    
                    if (_hasBlurImpact())
                        count ++;

                    return count;
                }
                
                bool _hasBlurImpact()
                {
                    return _owner._blurOptions._enable && _owner._blurOptions._step > 0f;
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
                _bounceResult.Release(cmd);
                _dist.Release(cmd);
                _jfa.Release(cmd);
                _pp.Release(cmd);
                _output.Release(cmd);
                _alpha.Release(cmd);
                
#if !UNITY_2022_1_OR_NEWER
                RTHandles.Release(_cameraOutput);
#endif
            }
        }
    }
}