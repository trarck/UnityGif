using System.Collections.Generic;
using UnityEngine;

namespace Gif
{
	public class GifImage
	{
        bool m_Loop = false;


        public int width { get; set; }
        public int height { get; set; }

        public float timePerFrame { get; set; }

        public bool loop
        {
            get
            {
                return m_Loop;
            }
            set
            {
                m_Loop = value;
            }
        }

        List<GifFrame> m_Frames;

        public GifImage()
        {
            m_Frames = new List<GifFrame>();
        }

        public GifImage(int width,int height)
        {
            this.width = width;
            this.height = height;
            m_Frames = new List<GifFrame>();
        }

        public void AddFrame(GifFrame frame)
        {
            m_Frames.Add(frame);
        }

        public List<GifFrame> frames
        {
            get
            {
                return m_Frames;
            }
            set
            {
                m_Frames = value;
            }
        }
	}
}
