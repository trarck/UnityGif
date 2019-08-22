using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gif.Encode;

namespace Gif
{
    using UnityObject = UnityEngine.Object;
    using ThreadPriority = System.Threading.ThreadPriority;

    public enum RecorderState
    {
        Recording,
        Paused,
        PreProcessing
    }

    public class Recorder : MonoBehaviour
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

        [SerializeField]
        Vector2 m_Offset = Vector2.zero;

        [SerializeField]
        Vector2 m_Anchor = Vector2.zero;

        [SerializeField]
        //RenderTexture quality
        float m_DownsampleFactor = 1;

        [SerializeField]
        float m_ParseDuration = 0.04f;

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
        Stack<RenderTexture> m_FreeFrames;

        Camera m_Camera;
        int m_RtWidth;
        int m_RtHeight;
        int m_FactorWidth;
        int m_FactorHeight;
        Vector2 m_FactorOffset;
        Texture2D m_Texture;
        Texture2D m_FactorTexture;

        Rect m_PixelsRect=Rect.zero;
        EncodeWorker m_Worker;

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
        public void Setup(bool autoAspect, int width, int height, int fps, float bufferSize, int repeat, int quality,float downsampleFactor, Camera camera=null)
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
            m_Width=width;

            if (!autoAspect)
            {
                m_Height = height;
            }

            m_FramePerSecond = fps;
            m_BufferSize = bufferSize;
            m_Repeat = repeat;
            m_Quality = quality;
            m_DownsampleFactor = downsampleFactor;

            m_Camera = camera;


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
        public void Record(string filename=null)
        {
            if (State == RecorderState.PreProcessing)
            {
                Debug.LogWarning("Attempting to resume recording during the pre-processing step.");
                return;
            }

            State = RecorderState.Recording;

            ComputeDownsample();

            if (string.IsNullOrEmpty(filename))
            {
                filename = GenerateFileName();
            }

            SetupWorker(filename);
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

            if (m_Texture)
            {
                Flush(m_Texture);
            }

            if (m_FactorTexture)
            {
                Flush(m_FactorTexture);
            }

            if (m_Frames == null)
                return;

            foreach (RenderTexture rt in m_Frames)
                Flush(rt);

            foreach (RenderTexture rt in m_FreeFrames)
                Flush(rt);

            m_Frames.Clear();

            m_FreeFrames.Clear();

            if (m_Worker!=null)
            {
                m_Worker.Clear();
            }
        }

        /// <summary>
        /// Saves the stored frames to a gif file. If the filename is null or empty, an unique one
        /// will be generated. You don't need to add the .gif extension to the name. Recording will
        /// be paused and won't resume automatically. You can use the <code>OnPreProcessingDone</code>
        /// callback to be notified when the pre-processing step has finished.
        /// </summary>
        public void Save()
        {
            if (State == RecorderState.PreProcessing)
            {
                Debug.LogWarning("Attempting to save during the pre-processing step.");
                return;
            }

            State = RecorderState.PreProcessing;

            m_Worker.Stop();

            State = RecorderState.Paused;
        }

        #endregion

        #region Unity events

        void Awake()
        {

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
                m_Time -= m_TimePerFrame;

                // Frame data
                RenderTexture rt = null;
                if (m_FreeFrames.Count > 0)
                {
                    rt = m_FreeFrames.Pop();
                }
                if (rt == null)
                {
                    //rt = new RenderTexture(m_Width, m_Height, 0, RenderTextureFormat.ARGB32);
                    rt = new RenderTexture(m_RtWidth, m_RtHeight, 0, RenderTextureFormat.ARGB32);
                    rt.wrapMode = TextureWrapMode.Clamp;
                    rt.filterMode = FilterMode.Bilinear;
                    rt.anisoLevel = 0;
                }

                Graphics.Blit(source, rt);
                m_Frames.Enqueue(rt);
            }

            Graphics.Blit(source, destination);
        }

        private void Update()
        {
            // Process the frame queue
            float startTime = Time.realtimeSinceStartup;

            while (m_Frames.Count > 0 && (startTime-Time.realtimeSinceStartup)< m_ParseDuration)
            {
                RenderTexture rt = m_Frames.Dequeue();
                GifFrame frame = ToGifFrame(rt, m_Texture);
                m_Worker.AddFrame(frame);
                m_FreeFrames.Push(rt);
            }
        }

        #endregion

        #region Methods

        // Used to reset internal values, called on Start(), Setup() and FlushMemory()
        void Init()
        {
            if (!m_Camera)
            {
                m_Camera = GetComponent<Camera>();
                if (!m_Camera)
                {
                    m_Camera = Camera.main;
                }
            }

            m_Frames = new Queue<RenderTexture>();
            m_FreeFrames = new Stack<RenderTexture>();

            State = RecorderState.Paused;

            ComputeHeight();

            ComputeDownsample();

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

            m_Texture = new Texture2D(m_FactorWidth, m_FactorHeight, TextureFormat.RGB24, false);
            m_Texture.hideFlags = HideFlags.HideAndDontSave;
            m_Texture.wrapMode = TextureWrapMode.Clamp;
            m_Texture.filterMode = FilterMode.Bilinear;
            m_Texture.anisoLevel = 0;

            m_FactorTexture =new Texture2D(m_FactorWidth,m_FactorHeight, TextureFormat.RGB24, false);
            m_FactorTexture.hideFlags = HideFlags.HideAndDontSave;
            m_FactorTexture.wrapMode = TextureWrapMode.Clamp;
            m_FactorTexture.filterMode = FilterMode.Bilinear;
            m_FactorTexture.anisoLevel = 0;
        }

        // Automatically computes height from the current aspect ratio if auto aspect is set to true
        public void ComputeHeight()
        {
            if (!m_AutoAspect)
                return;

            m_Height = Mathf.RoundToInt(m_Width /m_Camera.aspect);
        }

        void ComputeDownsample()
        {
            m_RtWidth = (int)(m_Camera.pixelWidth / m_DownsampleFactor);
            m_RtHeight = (int)(m_Camera.pixelHeight / m_DownsampleFactor);
            m_FactorWidth = (int)(m_Width / m_DownsampleFactor);
            m_FactorHeight = (int)(m_Height / m_DownsampleFactor);
            m_FactorOffset = m_Offset / m_DownsampleFactor;

            m_PixelsRect.width = m_FactorWidth;
            m_PixelsRect.height = m_FactorHeight;
            m_PixelsRect.x = m_RtWidth * m_Anchor.x + m_FactorOffset.x;
            m_PixelsRect.y = m_RtWidth * m_Anchor.y + m_FactorOffset.y;
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

        void SetupWorker(string filename)
        {
            string filepath = SaveFolder + "/" + filename + ".gif";

            if (m_Texture)
            {
                m_Texture.Resize(m_FactorWidth, m_FactorHeight);
            }

            if (m_FactorTexture)
            {
                m_FactorTexture.Resize(m_FactorWidth, m_FactorHeight);
            }

            // Setup a worker thread and let it do its magic
            GifEncoder encoder = new GifEncoder(m_Repeat, m_Quality);
            encoder.SetDelay(Mathf.RoundToInt(m_TimePerFrame * 1000f));

            if (m_Worker != null)
            {
                m_Worker.Clear();
            }

            m_Worker = new EncodeWorker(WorkerPriority)
            {
                m_Encoder = encoder,
                m_FilePath = filepath,
                m_OnFileSaved = OnFileSaved,
                m_OnFileSaveProgress = OnFileSaveProgress
            };

            m_Worker.Start();
        }

        // Pre-processing coroutine to extract frame data and send everything to a separate worker thread
        IEnumerator PreProcess(string filename)
        {
            string filepath = SaveFolder + "/" + filename + ".gif";
            List<GifFrame> frames = new List<GifFrame>(m_Frames.Count);

            // Get a temporary texture to read RenderTexture data
            Texture2D temp = new Texture2D(m_FactorWidth, m_FactorHeight, TextureFormat.RGB24, false);
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
            EncodeWorker worker = new EncodeWorker(WorkerPriority)
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
            target.ReadPixels(m_PixelsRect, 0, 0);
            target.Apply();           
            RenderTexture.active = null;

            //scale
            if (m_Width != m_FactorWidth || m_Height != m_FactorHeight)
            {
                m_FactorTexture.Resize(m_FactorWidth, m_FactorHeight);
                m_FactorTexture.SetPixels(target.GetPixels());
                TextureScale.Bilinear(m_FactorTexture, m_Width, m_Height);
                target = m_FactorTexture;
            }
            
            return new GifFrame() { width = target.width, height = target.height, pixels = target.GetPixels32() };
        }

        #endregion
    }
}

