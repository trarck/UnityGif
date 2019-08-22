using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using Gif.Decode;
using ThreadPriority = System.Threading.ThreadPriority;
using System.IO;

namespace Gif
{
	internal sealed class DecodeWorker
	{
        static int workerId = 1;

		Thread m_Thread;
		int m_Id;

		internal GifDecoder m_Decoder;
        internal string m_FilePath;
        internal GifImage m_Image;
        internal Action<GifImage> OnDecodeFinish;
        
		internal DecodeWorker(ThreadPriority priority)
		{
			m_Id = workerId++;
			m_Thread = new Thread(Run);
			m_Thread.Priority = priority;
        }

        internal void Start()
		{
            m_Thread.Start();
		}

		void Run()
		{
            using (FileStream fs = File.OpenRead(m_FilePath))
            {
                m_Decoder.DecodeImage(fs,ref m_Image);

                if (OnDecodeFinish != null)
                {
                    OnDecodeFinish(m_Image);
                }
            }
        }
	}
}
