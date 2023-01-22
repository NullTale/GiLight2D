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
            private RanderTarget            _buffer;
            private RanderTarget            _dist;
            private RenderTargetFlip        _jfa;
            private RenderTargetPostProcess _pp;
            private RanderTarget            _output;
            private RTHandle                _cameraOutput;
			
            // =======================================================================
            public void Init()
            {
                renderPassEvent = _owner._event;
				
                _buffer = new RanderTarget().Allocate(nameof(_buffer));
                _dist   = new RanderTarget().Allocate(nameof(_dist));
                _jfa    = new RenderTargetFlip(new RanderTarget().Allocate($"{nameof(_jfa)}_a"), new RanderTarget().Allocate($"{nameof(_jfa)}_b"));
                _pp     = new RenderTargetPostProcess(new RanderTarget().Allocate($"{nameof(_pp)}_a"), new RanderTarget().Allocate($"{nameof(_pp)}_b"));
                if (_owner.ForceTextureOutput)
                    _output = new RanderTarget().Allocate(_owner._output._outputGlobalTexture);
				
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

                desc.colorFormat = RenderTextureFormat.ARGB32;
                var passes = _postProcessCount(); 
                switch (_owner._output._finalBlit)
                {
                    case FinalBlit.Texture:
                    {
                        _pp.Setup(cmd, in desc, _output.Handle, passes, _owner._giMat);
                        if (_owner._blurOptions._enable)
                        {
                            _pp.Apply(cmd, _owner._blurMat);
                        }
                        
                        if (_owner._alpha)
                        {
                            cmd.SetGlobalTexture(s_AlphaTexId, _buffer.Handle.nameID);
                            _pp.Apply(cmd, _owner._alphaMat);
                        }
                    } break;
					
                    case FinalBlit.Camera:
                    {
                        // alpha channel will be added in copy pass (if required)
                        _pp.Setup(cmd, in desc, _cameraOutput, passes - (_owner._alpha ? 1 : 0), _owner._giMat);
                        if (_owner._blurOptions._enable)
                        {
                            _pp.Apply(cmd, _owner._blurMat);
                        }
                        
                    } break;
					
                    default:
                        throw new ArgumentOutOfRangeException();
                }
				
                if (_owner._outputOverride != DebugOutput.None)
                {
                    var dest = _owner._output._finalBlit switch
                    {
                        FinalBlit.Texture => _output.Handle,
                        FinalBlit.Camera  => _cameraOutput,
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

                int _postProcessCount()
                {
                    var count = 0;
                    if (_owner._blurOptions._enable)
                        count ++;
                    
                    if (_owner._alpha)
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
                _dist.Release(cmd);
                _jfa.Release(cmd);
                _pp.Release(cmd);

                if (_owner.ForceTextureOutput)
                    _output.Release(cmd);
                
#if !UNITY_2022_1_OR_NEWER
                RTHandles.Release(_cameraOutput);
#endif
            }
        }
    }
}