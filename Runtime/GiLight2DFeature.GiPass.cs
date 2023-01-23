using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = UnityEngine.Random;

namespace GiLight2D
{
    public partial class GiLight2DFeature
    {
        private class GiPass : ScriptableRenderPass
        {
            public GiLight2DFeature _owner;
            
            private FilteringSettings       _filtering;
            private RenderStateBlock        _override;
            private RenderTarget            _buffer;
            private RenderTarget            _dist;
            private RenderTargetFlip        _bounce;
            private RenderTarget            _tmp;
            private RenderTarget            _objects;
            private RenderTarget            _alpha;
            private RenderTargetFlip        _jfa;
            private RenderTargetPostProcess _pp;
            private RenderTarget            _output;
            private RTHandle                _cameraOutput;
			
            // =======================================================================
            public void Init()
            {
                renderPassEvent = _owner._event;
				
                _buffer = new RenderTarget().Allocate(nameof(_buffer));
                _objects = new RenderTarget().Allocate(nameof(_objects));
                _dist   = new RenderTarget().Allocate(nameof(_dist));
                _bounce = new RenderTargetFlip(new RenderTarget().Allocate($"{nameof(_bounce)}_a"), new RenderTarget().Allocate($"{nameof(_bounce)}_b"));
                _tmp    = new RenderTarget().Allocate(nameof(_tmp));
                _alpha  = new RenderTarget().Allocate(nameof(_alpha));
                _jfa    = new RenderTargetFlip(new RenderTarget().Allocate($"{nameof(_jfa)}_a"), new RenderTarget().Allocate($"{nameof(_jfa)}_b"));
                _pp     = new RenderTargetPostProcess(new RenderTarget().Allocate($"{nameof(_pp)}_a"), new RenderTarget().Allocate($"{nameof(_pp)}_b"));
                _output = new RenderTarget().Allocate(_owner._output._outputGlobalTexture);
				
                _filtering = new FilteringSettings(RenderQueueRange.transparent, _owner._mask);
                _override  = new RenderStateBlock(RenderStateMask.Nothing);
            }

            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                // allocate resources
                ref var res  = ref _owner._rtRes;
                ref var desc = ref _owner._rtDesc;
                var     cmd  = CommandBufferPool.Get(nameof(GiLight2DFeature));
#if UNITY_2021
                _cameraOutput = RTHandles.Alloc(renderingData.cameraData.renderer.cameraColorTarget);
#else
				_cameraOutput = renderingData.cameraData.renderer.cameraColorTargetHandle;
#endif
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
				
                if (_owner.CleanEdges)
                {
                    desc.colorFormat = RenderTextureFormat.ARGB32;
                    _objects.Get(cmd, in desc);
                    _blit(_buffer.Handle, _objects.Handle, _owner._giMat, 3);
                }
                
                // draw uv with alpha mask 
                _blit(_buffer.Handle, _jfa.From.Handle, _owner._blitMat, 3);
				
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
                _blit(_jfa.From.Handle, _dist.Handle, _owner._distMat);
                _jfa.Flip();
				
                // apply raytracer, final blit
                cmd.SetGlobalTexture(s_ColorTexId, _buffer.Handle.nameID);
                cmd.SetGlobalTexture(s_DistTexId, _dist.Handle.nameID);
                _owner._giMat.SetVector(s_AspectId, new Vector4(res.x / (float)res.y, 1f));
                _owner._giMat.SetFloat(s_SamplesId, _owner._rays);
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

                // add ray bounces to the initial color texture
                if (_owner._traceOptions._enable && _owner._traceOptions._bounces > 0)
                {
                    desc.colorFormat = RenderTextureFormat.R8;
                    _alpha.Get(cmd, in desc);
                    
                    _blit(_buffer.Handle, _alpha.Handle, _owner._blitMat, 1);
                    cmd.SetGlobalTexture(s_AlphaTexId, _alpha.Handle.nameID);
                    
                    desc.colorFormat = RenderTextureFormat.ARGB32;
                    _tmp.Get(cmd, in desc);
                    _bounce.Get(cmd, in desc);
                    
                    cmd.SetGlobalTexture(s_ATexId, _tmp.Handle.nameID);
                    cmd.SetGlobalTexture(s_BTexId, _bounce.To.Handle.nameID);
                    _owner._giMat.SetFloat(s_IntensityBounceId, _owner._traceOptions._intencity);

                    // execute gi, only for outline pixels, then add them to the main color texture, repeat with ray bounce texture
                    cmd.SetRenderTarget(_bounce.To.Handle.nameID);
                    cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1f, 0);
                    cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, _owner._giMat, 0, 1);
                    
                    _blit(_buffer.Handle, _tmp.Handle, _owner._blitMat);
                    
                    cmd.SetRenderTarget(_buffer.Handle.nameID);
                    cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, _owner._blitMat, 0, 2);
                    
                    for (var n = 1; n < _owner._traceOptions._bounces; n++)
                    {
                        _bounce.Flip();
                        
                        cmd.SetRenderTarget(_bounce.To.Handle.nameID);
                        cmd.ClearRenderTarget(RTClearFlags.Color, Color.clear, 1f, 0);
                        
                        cmd.SetGlobalTexture(s_ColorTexId, _bounce.From.Handle.nameID);
                        cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, _owner._giMat, 0, 1);
                        
                        _blit(_buffer.Handle, _tmp.Handle, _owner._blitMat);
                        
                        cmd.SetGlobalTexture(s_BTexId, _bounce.To.Handle.nameID);
                        cmd.SetRenderTarget(_buffer.Handle.nameID);
                        cmd.DrawMesh(k_ScreenMesh, Matrix4x4.identity, _owner._blitMat, 0, 2);   
                    }
                    
                    cmd.SetGlobalTexture(s_ColorTexId, _buffer.Handle.nameID);
                }
                
                // apply post process
                desc.colorFormat = RenderTextureFormat.ARGB32;
                var passes = _postProcessCount();
                var output = _owner._output._finalBlit switch
                {
                    FinalBlit.Texture => _output.Handle,
                    FinalBlit.Camera  => _cameraOutput,
                    _                 => throw new ArgumentOutOfRangeException()
                };
                
                _pp.Setup(cmd, in desc, output, passes, _owner._giMat);
                
                if (_owner.CleanEdges)
                {
                    cmd.SetGlobalTexture("_OverlayTex", _objects.Handle.nameID);
                    _pp.Apply(cmd, _owner._giMat, 2);
                }
                
                if (_owner._blurOptions._enable)
                {
                    var step = _owner._blurOptions._step.Enabled ?
                        new Vector4(_owner._blurOptions._step.Value.Value, _owner._blurOptions._step.Value.Value, 0, 0) :
                        new Vector4(1f / desc.width, 1f / desc.height, 0f, 0f);
                    
                    _owner._blurMat.SetVector(s_StepId, step);
                    _pp.Apply(cmd, _owner._blurMat);
                }

                // output debug override
                if (_owner._outputOverride != DebugOutput.None)
                {
                    switch (_owner._outputOverride)
                    {
                        case DebugOutput.Objects:
                            _blit(_buffer.Handle, output, _owner._blitMat);
                            break;
                        case DebugOutput.Flood:
                            _blit(_jfa.To.Handle, output, _owner._blitMat);
                            break;
                        case DebugOutput.Distance:
                            _blit(_dist.Handle, output, _owner._blitMat);
                            break;
                        case DebugOutput.None:
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
				
                _execute();
				
                // -----------------------------------------------------------------------
                void _blit(RTHandle from, RTHandle to, Material mat, int pass = 0)
                {
                    GiLight2DFeature._blit(cmd, from, to, mat, pass);
                }

                int _postProcessCount()
                {
                    var count = 0;
                    if (_owner.CleanEdges)
                        count ++;
                    
                    if (_owner._blurOptions._enable)
                        count ++;

                    return count;
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
                _objects.Release(cmd);
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