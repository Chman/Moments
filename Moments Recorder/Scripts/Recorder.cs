/*
 * Copyright (c) 2015 Thomas Hourdel
 *
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 *
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 *    1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would be
 *    appreciated but is not required.
 * 
 *    2. Altered source versions must be plainly marked as such, and must not be
 *    misrepresented as being the original software.
 * 
 *    3. This notice may not be removed or altered from any source
 *    distribution.
 */

using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Moments.Encoder;
using ThreadPriority = System.Threading.ThreadPriority;

namespace Moments
{
	using UnityObject = UnityEngine.Object;

	public enum RecorderState
	{
		Recording,
		Paused,
		PreProcessing
	}

	[AddComponentMenu("Miscellaneous/Moments Recorder")]
	[RequireComponent(typeof(Camera)), DisallowMultipleComponent]
	public sealed class Recorder : MonoBehaviour
	{
		#region Exposed fields

		// These fields aren't public, the user shouldn't modify them directly as they can't break
		// everything if not used correctly. Use Setup() instead.

		[SerializeField, Min(8)]
		int m_Width = 320;

		[SerializeField, Min(8)]
		int m_Height = 200;

		[SerializeField]
		bool m_AutoAspect = true;

		[SerializeField, Range(1, 30)]
		int m_FramePerSecond = 15;

		[SerializeField, Min(-1)]
		int m_Repeat = 0;

		[SerializeField, Range(1, 100)]
		int m_Quality = 15;

		[SerializeField, Min(0.1f)]
		float m_BufferSize = 3f;

		#endregion

		#region Public fields

		/// <summary>
		/// Current state of the recorder.
		/// </summary>
		public RecorderState State { get; private set; }

		/// <summary>
		/// The folder to save the gif to. No trailing slash.
		/// </summary>
		public string SaveFolder { get; set; }

		/// <summary>
		/// Sets the worker threads priority. This will only affect newly created threads (on save).
		/// </summary>
		public ThreadPriority WorkerPriority = ThreadPriority.BelowNormal;

		/// <summary>
		/// Returns the estimated VRam used (in MB) for recording.
		/// </summary>
		public float EstimatedMemoryUse
		{
			get
			{
				float mem = m_FramePerSecond * m_BufferSize;
					  mem *= m_Width * m_Height * 4;
					  mem /= 1024 * 1024;
				return mem;
			}
		}

		#endregion

		#region Delegates

		/// <summary>
		/// Called when the pre-processing step has finished.
		/// </summary>
		public Action OnPreProcessingDone;

		/// <summary>
		/// Called by each worker thread every time a frame is processed during the save process.
		/// The first parameter holds the worker ID and the second one a value in range [0;1] for
		/// the actual progress. This callback is probably not thread-safe, use at your own risks.
		/// </summary>
		public Action<int, float> OnFileSaveProgress;

		/// <summary>
		/// Called once a gif file has been saved. The first parameter will hold the worker ID and
		/// the second one the absolute file path.
		/// </summary>
		public Action<int, string> OnFileSaved;

		#endregion

		#region Internal fields

		int m_MaxFrameCount;
		float m_Time;
		float m_TimePerFrame;
		Queue<RenderTexture> m_Frames;
		RenderTexture m_RecycledRenderTexture;
		ReflectionUtils<Recorder> m_ReflectionUtils;

		#endregion

		#region Public API

		/// <summary>
		/// Initializes the component. Use this if you need to change the recorder settings in a script.
		/// This will flush the previously saved frames as settings can't be changed while recording.
		/// </summary>
		/// <param name="autoAspect">Automatically compute height from the current aspect ratio</param>
		/// <param name="width">Width in pixels</param>
		/// <param name="height">Height in pixels</param>
		/// <param name="fps">Recording FPS</param>
		/// <param name="bufferSize">Maximum amount of seconds to record to memory</param>
		/// <param name="repeat">-1: no repeat, 0: infinite, >0: repeat count</param>
		/// <param name="quality">Quality of color quantization (conversion of images to the maximum
		/// 256 colors allowed by the GIF specification). Lower values (minimum = 1) produce better
		/// colors, but slow processing significantly. Higher values will speed up the quantization
		/// pass at the cost of lower image quality (maximum = 100).</param>
		public void Setup(bool autoAspect, int width, int height, int fps, float bufferSize, int repeat, int quality)
		{
			if (State == RecorderState.PreProcessing)
			{
				Debug.LogWarning("Attempting to setup the component during the pre-processing step.");
				return;
			}

			// Start fresh
			FlushMemory();

			// Set values and validate them
			m_AutoAspect = autoAspect;
			m_ReflectionUtils.ConstrainMin(x => x.m_Width, width);

			if (!autoAspect)
				m_ReflectionUtils.ConstrainMin(x => x.m_Height, height);

			m_ReflectionUtils.ConstrainRange(x => x.m_FramePerSecond, fps);
			m_ReflectionUtils.ConstrainMin(x => x.m_BufferSize, bufferSize);
			m_ReflectionUtils.ConstrainMin(x => x.m_Repeat, repeat);
			m_ReflectionUtils.ConstrainRange(x => x.m_Quality, quality);

			// Ready to go
			Init();
		}

		/// <summary>
		/// Pauses recording.
		/// </summary>
		public void Pause()
		{
			if (State == RecorderState.PreProcessing)
			{
				Debug.LogWarning("Attempting to pause recording during the pre-processing step. The recorder is automatically paused when pre-processing.");
				return;
			}

			State = RecorderState.Paused;
		}

		/// <summary>
		/// Starts or resumes recording. You can't resume while it's pre-processing data to be saved.
		/// </summary>
		public void Record()
		{
			if (State == RecorderState.PreProcessing)
			{
				Debug.LogWarning("Attempting to resume recording during the pre-processing step.");
				return;
			}

			State = RecorderState.Recording;
		}

		/// <summary>
		/// Clears all saved frames from memory and starts fresh.
		/// </summary>
		public void FlushMemory()
		{
			if (State == RecorderState.PreProcessing)
			{
				Debug.LogWarning("Attempting to flush memory during the pre-processing step.");
				return;
			}

			Init();

			if (m_RecycledRenderTexture != null)
				Flush(m_RecycledRenderTexture);

			if (m_Frames == null)
				return;

			foreach (RenderTexture rt in m_Frames)
				Flush(rt);

			m_Frames.Clear();
		}

		/// <summary>
		/// Saves the stored frames to a gif file. The filename will automatically be generated.
		/// Recording will be paused and won't resume automatically. You can use the 
		/// <code>OnPreProcessingDone</code> callback to be notified when the pre-processing
		/// step has finished.
		/// </summary>
		public void Save()
		{
			Save(GenerateFileName());
		}

		/// <summary>
		/// Saves the stored frames to a gif file. If the filename is null or empty, an unique one
		/// will be generated. You don't need to add the .gif extension to the name. Recording will
		/// be paused and won't resume automatically. You can use the <code>OnPreProcessingDone</code>
		/// callback to be notified when the pre-processing step has finished.
		/// </summary>
		/// <param name="filename">File name without extension</param>
		public void Save(string filename)
		{
			if (State == RecorderState.PreProcessing)
			{
				Debug.LogWarning("Attempting to save during the pre-processing step.");
				return;
			}

			if (m_Frames.Count == 0)
			{
				Debug.LogWarning("Nothing to save. Maybe you forgot to start the recorder ?");
				return;
			}

			State = RecorderState.PreProcessing;

			if (string.IsNullOrEmpty(filename))
				filename = GenerateFileName();

			StartCoroutine(PreProcess(filename));
		}

		#endregion

		#region Unity events

		void Awake()
		{
			m_ReflectionUtils = new ReflectionUtils<Recorder>(this);
			m_Frames = new Queue<RenderTexture>();
			Init();
		}

		void OnDestroy()
		{
			FlushMemory();
		}

		void OnRenderImage(RenderTexture source, RenderTexture destination)
		{
			if (State != RecorderState.Recording)
			{
				Graphics.Blit(source, destination);
				return;
			}

			m_Time += Time.unscaledDeltaTime;

			if (m_Time >= m_TimePerFrame)
			{
				// Limit the amount of frames stored in memory
				if (m_Frames.Count >= m_MaxFrameCount)
					m_RecycledRenderTexture = m_Frames.Dequeue();

				m_Time -= m_TimePerFrame;

				// Frame data
				RenderTexture rt = m_RecycledRenderTexture;
				m_RecycledRenderTexture = null;

				if (rt == null)
				{
					rt = new RenderTexture(m_Width, m_Height, 0, RenderTextureFormat.ARGB32);
					rt.wrapMode = TextureWrapMode.Clamp;
					rt.filterMode = FilterMode.Bilinear;
					rt.anisoLevel = 0;
				}

				Graphics.Blit(source, rt);
				m_Frames.Enqueue(rt);
			}

			Graphics.Blit(source, destination);
		}

		#endregion

		#region Methods

		// Used to reset internal values, called on Start(), Setup() and FlushMemory()
		void Init()
		{
			State = RecorderState.Paused;
			ComputeHeight();
			m_MaxFrameCount = Mathf.RoundToInt(m_BufferSize * m_FramePerSecond);
			m_TimePerFrame = 1f / m_FramePerSecond;
			m_Time = 0f;

			// Make sure the output folder is set or use the default one
			if (string.IsNullOrEmpty(SaveFolder))
			{
				#if UNITY_EDITOR
				SaveFolder = Application.dataPath; // Defaults to the asset folder in the editor for faster access to the gif file
				#else
				SaveFolder = Application.persistentDataPath;
				#endif
			}
		}

		// Automatically computes height from the current aspect ratio if auto aspect is set to true
		public void ComputeHeight()
		{
			if (!m_AutoAspect)
				return;

			m_Height = Mathf.RoundToInt(m_Width / GetComponent<Camera>().aspect);
		}

		void Flush(UnityObject obj)
		{
			#if UNITY_EDITOR
			if (Application.isPlaying)
				Destroy(obj);
			else
				DestroyImmediate(obj);
			#else
            UnityObject.Destroy(obj);
			#endif
		}

		// Gets a filename : GifCapture-yyyyMMddHHmmssffff
		string GenerateFileName()
		{
			string timestamp = DateTime.Now.ToString("yyyyMMddHHmmssffff");
			return "GifCapture-" + timestamp;
		}

		// Pre-processing coroutine to extract frame data and send everything to a separate worker thread
		IEnumerator PreProcess(string filename)
		{
			string filepath = SaveFolder + "/" + filename + ".gif";
			List<GifFrame> frames = new List<GifFrame>(m_Frames.Count);

			// Get a temporary texture to read RenderTexture data
			Texture2D temp = new Texture2D(m_Width, m_Height, TextureFormat.RGB24, false);
			temp.hideFlags = HideFlags.HideAndDontSave;
			temp.wrapMode = TextureWrapMode.Clamp;
			temp.filterMode = FilterMode.Bilinear;
			temp.anisoLevel = 0;

			// Process the frame queue
			while (m_Frames.Count > 0)
			{
				GifFrame frame = ToGifFrame(m_Frames.Dequeue(), temp);
				frames.Add(frame);
				yield return null;
			}

			// Dispose the temporary texture
			Flush(temp);

			// Switch the state to pause, let the user choose to keep recording or not
			State = RecorderState.Paused;

			// Callback
			if (OnPreProcessingDone != null)
				OnPreProcessingDone();

			// Setup a worker thread and let it do its magic
			GifEncoder encoder = new GifEncoder(m_Repeat, m_Quality);
			encoder.SetDelay(Mathf.RoundToInt(m_TimePerFrame * 1000f));
			Worker worker = new Worker(WorkerPriority)
			{
				m_Encoder = encoder,
				m_Frames = frames,
				m_FilePath = filepath,
				m_OnFileSaved = OnFileSaved,
				m_OnFileSaveProgress = OnFileSaveProgress
			};
			worker.Start();
		}

		// Converts a RenderTexture to a GifFrame
		// Should be fast enough for low-res textures but will tank the framerate at higher res
		GifFrame ToGifFrame(RenderTexture source, Texture2D target)
		{
			RenderTexture.active = source;
			target.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
			target.Apply();
			RenderTexture.active = null;

			return new GifFrame() { Width = target.width, Height = target.height, Data = target.GetPixels32() };
		}

		#endregion
	}
}
