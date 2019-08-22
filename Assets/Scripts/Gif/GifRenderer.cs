using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Gif.Decode;

namespace Gif
{
    public abstract class GifRenderer : MonoBehaviour
    {
        protected enum State
        {
            Idle,
            Decoding,
            Decoded,
            GenTexture,
            Show
        }

        [SerializeField]
        string m_GifFilePath = "";

        [SerializeField]
        System.Threading.ThreadPriority m_WorkerPriority = System.Threading.ThreadPriority.BelowNormal;

        float m_Delay = 0.1f;
        float m_Elapsed=0;
        int m_Index = 0;

        GifImage m_Image;
        [SerializeField]
        List<Texture> m_Textures;

        protected State m_State = State.Idle;

        private void Start()
        {
            if (!string.IsNullOrEmpty(m_GifFilePath))
            {
                ShowGif(m_GifFilePath);
            }
            else
            {
                m_State = State.Idle;
            }
        }

        private void Update()
        {
            switch (m_State)
            {
                case State.Decoded:
                    StartCoroutine(CreateTextures(m_Image));
                    break;
                case State.Show:
                    {
                        m_Elapsed += Time.deltaTime;
                        if (m_Elapsed > m_Delay)
                        {
                            int next = Mathf.FloorToInt(m_Elapsed / m_Delay);

                            m_Index += next;

                            while (m_Index >= m_Textures.Count)
                            {
                                m_Index -= m_Textures.Count;
                            }
                            
                            m_Elapsed -= next * m_Delay;

                            Render(m_Textures[m_Index]);
                        }
                    }
                    break;
            }
        }

        protected virtual void Render(Texture texture)
        {

        }

        public void ShowGif(string filePath)
        {
            if (!System.IO.Path.IsPathRooted(filePath))
            {
                filePath = System.IO.Path.Combine(Application.dataPath, filePath);
            }

            GifDecoder decoder = new GifDecoder();
            m_Image = new GifImage();

            DecodeWorker worker = new DecodeWorker(m_WorkerPriority)
            {
                m_Decoder = decoder,
                m_FilePath = filePath,
                m_Image=m_Image,
                OnDecodeFinish = OnDecodeFinish
            };
            m_State = State.Decoding;
            worker.Start();
        }

        void OnDecodeFinish(GifImage image)
        {
            m_State = State.Decoded;
            m_Delay = image.timePerFrame/100.0f;
        }

        IEnumerator CreateTextures(GifImage image)
        {
            m_State = State.GenTexture;
            if (m_Textures == null)
            {
                m_Textures = new List<Texture>();
            }
            else
            {
                m_Textures.Clear();
            }

            for(int i = 0; i < image.frames.Count; ++i)
            {
                GifFrame frame = image.frames[i];
                Texture2D tex = new Texture2D(frame.width, frame.height, TextureFormat.ARGB32, false);
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.filterMode = FilterMode.Bilinear;
                tex.anisoLevel = 0;
                tex.SetPixels32(frame.pixels);
                tex.Apply();
                m_Textures.Add(tex);
                yield return null;
            }

            AfterCreateTextures();
        }

        protected virtual void AfterCreateTextures()
        {
            m_State = State.Show;
            m_Elapsed = 0;
            m_Index = 0;
        }
    }
}